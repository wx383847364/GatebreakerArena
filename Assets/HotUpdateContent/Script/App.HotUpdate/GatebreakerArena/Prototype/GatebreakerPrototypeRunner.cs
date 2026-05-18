using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Paddle;
using App.HotUpdate.GatebreakerArena.UI;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Prototype
{
    public sealed class GatebreakerPrototypeRunner : MonoBehaviour
    {
        private const int DefaultLocalPlayerId = 1;
        private const float GuardDepth = 0.55f;
        private const float CameraHeight = 20f;
        private const float CameraMargin = 0.9f;
        private const float ScreenPadding = 16f;
        private const float ControlGap = 10f;
        private const float ServeButtonHeight = 50f;
        private const float TuningPanelPreferredHeight = 180f;
        private const float TuningPanelMinimumHeight = 118f;

        private readonly Dictionary<int, Transform> _ballViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Renderer> _ballRenderers = new Dictionary<int, Renderer>();
        private readonly Dictionary<int, Transform> _paddleViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Renderer> _paddleRenderers = new Dictionary<int, Renderer>();
        private readonly Dictionary<int, Renderer> _guardRenderers = new Dictionary<int, Renderer>();
        private readonly HashSet<int> _liveBallIds = new HashSet<int>();

        private GatebreakerMatchRuntime _runtime;
        private GatebreakerInputService _inputService;
        private GatebreakerArenaHudPresenter _hudPresenter;
        private LanRoomService _lanRoomService;
        private ILanTransport _lanTransport;
        private Transform _visualRoot;
        private Material _arenaMaterial;
        private Material _wallMaterial;
        private Material _localMaterial;
        private Material _dangerMaterial;
        private Material _paddleMaterial;
        private Material[] _playerMaterials;
        private Camera _prototypeCamera;
        private GUIStyle _hudStyle;
        private GUIStyle _dangerStyle;
        private GUIStyle _resultTitleStyle;
        private GUIStyle _resultBodyStyle;
        private GUIStyle _serveButtonStyle;
        private GUIStyle _tuningLabelStyle;
        private ServeBlockReason _lastServeBlockReason = ServeBlockReason.None;
        private int _localPlayerId = DefaultLocalPlayerId;
        private bool _initialized;
        private bool _guiServePressed;
        private Vector2 _tuningScrollPosition;
        private ulong _lanClientInstanceId;
        private string _lanPlayerName = "Player";
        private string _lanRoomCodeInput = string.Empty;
        private float _lanInputAccumulator;

        private float ArenaHalfWidth => _runtime?.Arena != null ? _runtime.Arena.HalfWidth : 8f;
        private float ArenaHalfHeight => _runtime?.Arena != null ? _runtime.Arena.HalfHeight : 5f;

        public void Initialize(GatebreakerArenaApplicationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Initialize(context.MatchRuntime, context.InputService, context.HudPresenter, DefaultLocalPlayerId);
            _lanRoomService = context.LanRoomService;
            _lanTransport = context.Services?.Get<ILanTransport>();
            EnsureLanIdentity();
            context.SceneBindingService?.MarkBound();
        }

        public void Initialize(
            GatebreakerMatchRuntime runtime,
            GatebreakerInputService inputService,
            GatebreakerArenaHudPresenter hudPresenter,
            int localPlayerId = DefaultLocalPlayerId)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
            _hudPresenter = hudPresenter ?? throw new ArgumentNullException(nameof(hudPresenter));
            _localPlayerId = localPlayerId;
            _runtime.SetLocalPlayer(_localPlayerId);

            EnsureScene();
            SyncPlayerViews();
            SyncBallViews();
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _runtime.StartLocalPrototype();
                _runtime.SetLocalPlayer(_localPlayerId);
                _lastServeBlockReason = ServeBlockReason.None;
                _guiServePressed = false;
            }

            if (IsLanPlaying())
            {
                SyncLanLocalPlayer();
            }
            else
            {
                HandleLocalPlayerSelection();
            }

            float moveAxis = ReadMoveAxis() * GetLocalMoveAxisSign();
            bool servePressed = Input.GetKeyDown(KeyCode.Space) || _guiServePressed;
            _guiServePressed = false;
            var frame = new PlayerInputFrame(_localPlayerId, moveAxis, servePressed, GetLocalViewUp());
            if (IsLanPlaying())
            {
                SubmitLanInputAtFixedRate(frame);
                SyncPlayerViews();
                SyncBallViews();
                return;
            }

            _lanInputAccumulator = 0f;
            _inputService.SetFrame(frame);
            _runtime.ApplyInputFrame(frame);
            _runtime.TickLocalPrototype(Time.deltaTime);
            if (servePressed)
            {
                PlayerRuntimeState localPlayer = _runtime.FindPlayer(_localPlayerId);
                _lastServeBlockReason = localPlayer?.ServeResource?.LastBlockReason ?? ServeBlockReason.PlayerDisabled;
            }

            SyncPlayerViews();
            SyncBallViews();
        }

        private bool IsLanPlaying()
        {
            return _lanRoomService != null &&
                   _lanRoomService.CurrentSnapshot.State == LanRoomState.Playing;
        }

        private void SyncLanLocalPlayer()
        {
            RoomSnapshot snapshot = _lanRoomService?.CurrentSnapshot;
            if (snapshot?.Players == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Players.Length; i++)
            {
                RoomPlayerSnapshot player = snapshot.Players[i];
                if (player.SlotIndex != snapshot.LocalSlotIndex || player.PlayerId <= 0)
                {
                    continue;
                }

                if (player.PlayerId != _localPlayerId && _runtime.SetLocalPlayer(player.PlayerId))
                {
                    _localPlayerId = player.PlayerId;
                    _lastServeBlockReason = ServeBlockReason.None;
                    ConfigurePrototypeCamera();
                }

                return;
            }
        }

        private void SubmitLanInputAtFixedRate(PlayerInputFrame frame)
        {
            float frameDelta = 1f / Mathf.Max(1, LockstepSession.SimulationFps);
            _lanInputAccumulator += Mathf.Max(0f, Time.deltaTime);
            int submitCount = Mathf.FloorToInt(_lanInputAccumulator / frameDelta);
            if (submitCount <= 0)
            {
                return;
            }

            submitCount = Mathf.Min(submitCount, 4);
            _lanInputAccumulator -= submitCount * frameDelta;
            short moveAxisQ = GatebreakerLockstepInputConverter.QuantizeSignedUnit(frame.MoveAxis);
            short aimXQ = GatebreakerLockstepInputConverter.QuantizeSignedUnit(frame.AimDirection.x);
            short aimYQ = GatebreakerLockstepInputConverter.QuantizeSignedUnit(frame.AimDirection.y);
            ushort buttons = frame.ServePressed ? GatebreakerLockstepInputConverter.ServeButton : (ushort)0;
            for (int i = 0; i < submitCount; i++)
            {
                _lanRoomService.Lockstep.SubmitLocalInput(moveAxisQ, aimXQ, aimYQ, buttons);
                buttons = 0;
            }
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            ConfigurePrototypeCamera();
        }

        private void OnGUI()
        {
            if (!_initialized)
            {
                return;
            }

            EnsureHudStyles();
            GatebreakerHudSnapshot snapshot = _hudPresenter.BuildSnapshot(_localPlayerId);
            Rect panel = new Rect(16f, 16f, 390f, snapshot.HasDanger ? 344f : 316f);
            GUI.Box(panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 20f));
            GUILayout.Label("Gatebreaker Arena 原型", _hudStyle);
            GUILayout.Label("A/D 或方向键：移动    空格：发球    R：重开", _hudStyle);
            GUILayout.Label("1-4：切换本地玩家视角/控制", _hudStyle);
            GUILayout.Space(4f);
            GUILayout.Label($"阶段：{FormatPhase(snapshot.Phase)}    时间：{FormatTime(snapshot.RemainingTime)}", _hudStyle);
            GUILayout.Label("比分：", _hudStyle);
            DrawScoreRows(snapshot);
            GUILayout.Label(
                $"弹药：{snapshot.CurrentServeAmmo}/{snapshot.MaxServeAmmo}    冷却：{snapshot.ServeCooldownRemaining:0.0}秒",
                _hudStyle);
            GUILayout.Label(
                $"己方在场球：{snapshot.OwnedBallsInField}/{snapshot.MaxOwnedBallsInField}",
                _hudStyle);
            GUILayout.Label(
                $"发球限制：{FormatServeBlockReason(snapshot.ServeBlockReason)}    上次空格：{FormatServeBlockReason(_lastServeBlockReason)}",
                _hudStyle);
            if (snapshot.HasDanger)
            {
                GUILayout.Space(4f);
                GUILayout.Label("危险：场上有敌方球", _dangerStyle);
            }

            GUILayout.EndArea();
            DrawServeButton(snapshot);
            DrawBounceTuningPanel(snapshot);
            DrawLanRoomPanel();
            DrawResultPanel(snapshot);
        }

        private void OnDestroy()
        {
            DestroyMaterial(_arenaMaterial);
            DestroyMaterial(_wallMaterial);
            DestroyMaterial(_localMaterial);
            DestroyMaterial(_dangerMaterial);
            DestroyMaterial(_paddleMaterial);
            if (_playerMaterials == null)
            {
                return;
            }

            for (int i = 0; i < _playerMaterials.Length; i++)
            {
                DestroyMaterial(_playerMaterials[i]);
            }
        }

        private void EnsureScene()
        {
            if (_visualRoot != null)
            {
                return;
            }

            _visualRoot = new GameObject("Gatebreaker Prototype Visuals").transform;
            _visualRoot.SetParent(transform, false);
            CreateMaterials();
            ConfigurePrototypeCamera();
            CreateLightIfNeeded();
            CreateArena();
            CreateWalls();
            CreateGuardZones();
            CreatePaddles();
            ConfigurePrototypeCamera();
        }

        private void CreateMaterials()
        {
            _arenaMaterial = CreateMaterial(new Color(0.10f, 0.12f, 0.13f));
            _wallMaterial = CreateMaterial(new Color(0.75f, 0.78f, 0.82f));
            _localMaterial = CreateMaterial(new Color(0.20f, 0.68f, 1.00f));
            _dangerMaterial = CreateMaterial(new Color(1.00f, 0.22f, 0.16f));
            _paddleMaterial = CreateMaterial(new Color(0.01f, 0.01f, 0.01f));
            _playerMaterials = new[]
            {
                CreateMaterial(new Color(0.20f, 0.68f, 1.00f)),
                CreateMaterial(new Color(1.00f, 0.55f, 0.18f)),
                CreateMaterial(new Color(0.45f, 0.88f, 0.38f)),
                CreateMaterial(new Color(0.84f, 0.42f, 1.00f)),
            };
        }

        private void CreateArena()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Arena Floor";
            floor.transform.SetParent(_visualRoot, false);
            floor.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            floor.transform.localScale = new Vector3(ArenaHalfWidth * 2f, 0.12f, ArenaHalfHeight * 2f);
            SetMaterial(floor, _arenaMaterial);
        }

        private void CreateWalls()
        {
            CreateBlock("North Wall", new Vector3(0f, 0.35f, ArenaHalfHeight + 0.15f), new Vector3(ArenaHalfWidth * 2f, 0.7f, 0.3f), _wallMaterial);
            CreateBlock("South Wall", new Vector3(0f, 0.35f, -ArenaHalfHeight - 0.15f), new Vector3(ArenaHalfWidth * 2f, 0.7f, 0.3f), _wallMaterial);
            CreateBlock("East Wall", new Vector3(ArenaHalfWidth + 0.15f, 0.35f, 0f), new Vector3(0.3f, 0.7f, ArenaHalfHeight * 2f), _wallMaterial);
            CreateBlock("West Wall", new Vector3(-ArenaHalfWidth - 0.15f, 0.35f, 0f), new Vector3(0.3f, 0.7f, ArenaHalfHeight * 2f), _wallMaterial);
        }

        private void CreateGuardZones()
        {
            CreateGuardZone(1, "South Guard Zone", new Vector3(0f, 0.02f, -ArenaHalfHeight + GuardDepth * 0.5f), new Vector3(ArenaHalfWidth * 1.15f, 0.04f, GuardDepth));
            CreateGuardZone(2, "North Guard Zone", new Vector3(0f, 0.02f, ArenaHalfHeight - GuardDepth * 0.5f), new Vector3(ArenaHalfWidth * 1.15f, 0.04f, GuardDepth));
            CreateGuardZone(3, "East Guard Zone", new Vector3(ArenaHalfWidth - GuardDepth * 0.5f, 0.02f, 0f), new Vector3(GuardDepth, 0.04f, ArenaHalfHeight * 1.15f));
            CreateGuardZone(4, "West Guard Zone", new Vector3(-ArenaHalfWidth + GuardDepth * 0.5f, 0.02f, 0f), new Vector3(GuardDepth, 0.04f, ArenaHalfHeight * 1.15f));
        }

        private void CreatePaddles()
        {
            for (int playerId = 1; playerId <= 4; playerId++)
            {
                Transform paddle = EnsurePaddleView(playerId);
                PlayerRuntimeState player = _runtime?.FindPlayer(playerId);
                paddle.localPosition = GetPaddlePosition(player);
                paddle.localRotation = GetPaddleRotation(player?.Paddle);
            }
        }

        private void CreateGuardZone(int playerId, string name, Vector3 position, Vector3 scale)
        {
            GameObject guard = CreateBlock(name, position, scale, GetPlayerMaterial(playerId));
            Renderer guardRenderer = guard.GetComponent<Renderer>();
            guardRenderer.material.color = WithAlpha(GetPlayerColor(playerId), 0.32f);
            _guardRenderers[playerId] = guardRenderer;
        }

        private GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(_visualRoot, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            SetMaterial(block, material);
            return block;
        }

        private void SyncPlayerViews()
        {
            if (_runtime == null)
            {
                return;
            }

            IReadOnlyList<PlayerRuntimeState> players = _runtime.Players;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerRuntimeState player = players[i];
                Transform paddle = EnsurePaddleView(player.PlayerId);
                paddle.localPosition = GetPaddlePosition(player);
                paddle.localRotation = GetPaddleRotation(player.Paddle);
                if (player.Paddle != null)
                {
                    bool horizontal = Mathf.Abs(player.Paddle.Normal.y) > 0.5f;
                    paddle.localScale = horizontal
                        ? new Vector3(player.Paddle.Length, 0.35f, player.Paddle.Thickness)
                        : new Vector3(player.Paddle.Thickness, 0.35f, player.Paddle.Length);
                }

                if (_paddleRenderers.TryGetValue(player.PlayerId, out Renderer renderer))
                {
                    renderer.material.color = _paddleMaterial.color;
                }

                if (_guardRenderers.TryGetValue(player.PlayerId, out Renderer guard))
                {
                    Color guardColor = player.Zone != null && player.Zone.IsDanger
                        ? _dangerMaterial.color
                        : player.IsDisabled ? Color.gray : GetPlayerColor(player.PlayerId);
                    guard.material.color = WithAlpha(guardColor, 0.32f);
                }
            }
        }

        private Transform EnsurePaddleView(int playerId)
        {
            if (_paddleViews.TryGetValue(playerId, out Transform paddle))
            {
                return paddle;
            }

            GameObject paddleObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paddleObject.name = $"Player {playerId} Paddle";
            paddleObject.transform.SetParent(_visualRoot, false);
            paddleObject.transform.localScale = playerId <= 2
                ? new Vector3(2.2f, 0.35f, 0.35f)
                : new Vector3(0.35f, 0.35f, 2.2f);
            SetMaterial(paddleObject, _paddleMaterial);
            _paddleViews[playerId] = paddleObject.transform;
            _paddleRenderers[playerId] = paddleObject.GetComponent<Renderer>();
            return paddleObject.transform;
        }

        private Vector3 GetPaddlePosition(PlayerRuntimeState player)
        {
            if (player?.Paddle != null)
            {
                return new Vector3(player.Paddle.Position.x, 0.35f, player.Paddle.Position.y);
            }

            switch (player != null ? player.PlayerId : 1)
            {
                case 1:
                    return new Vector3(0f, 0.35f, -ArenaHalfHeight + 0.55f);
                case 2:
                    return new Vector3(0f, 0.35f, ArenaHalfHeight - 0.55f);
                case 3:
                    return new Vector3(ArenaHalfWidth - 0.55f, 0.35f, 0f);
                default:
                    return new Vector3(-ArenaHalfWidth + 0.55f, 0.35f, 0f);
            }
        }

        private static Quaternion GetPaddleRotation(PaddleRuntimeState paddle)
        {
            return Quaternion.identity;
        }

        private void SyncBallViews()
        {
            _liveBallIds.Clear();
            IReadOnlyList<BallRuntimeState> balls = _runtime.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                BallRuntimeState ball = balls[i];
                if (ball == null || ball.BallState == BallState.Destroyed || ball.BallState == BallState.ScoredOut)
                {
                    continue;
                }

                _liveBallIds.Add(ball.BallId);
                Transform ballView = EnsureBallView(ball.BallId);
                ballView.localPosition = new Vector3(ball.Position.x, 0.35f, ball.Position.y);
                ballView.localScale = GetCompensatedVisualScale(0.45f);
                if (_ballRenderers.TryGetValue(ball.BallId, out Renderer renderer))
                {
                    renderer.material.color = ball.OwnerPlayerId == _localPlayerId
                        ? _localMaterial.color
                        : _dangerMaterial.color;
                }
            }

            RemoveStaleBallViews();
        }

        private Transform EnsureBallView(int ballId)
        {
            if (_ballViews.TryGetValue(ballId, out Transform ballView))
            {
                return ballView;
            }

            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = $"Ball {ballId}";
            ballObject.transform.SetParent(_visualRoot, false);
            ballObject.transform.localScale = GetCompensatedVisualScale(0.45f);
            SetMaterial(ballObject, _dangerMaterial);
            _ballViews[ballId] = ballObject.transform;
            _ballRenderers[ballId] = ballObject.GetComponent<Renderer>();
            return ballObject.transform;
        }

        private void RemoveStaleBallViews()
        {
            var staleIds = new List<int>();
            foreach (int ballId in _ballViews.Keys)
            {
                if (!_liveBallIds.Contains(ballId))
                {
                    staleIds.Add(ballId);
                }
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                int ballId = staleIds[i];
                if (_ballViews.TryGetValue(ballId, out Transform ballView))
                {
                    Destroy(ballView.gameObject);
                }

                _ballViews.Remove(ballId);
                _ballRenderers.Remove(ballId);
            }
        }

        private static float ReadMoveAxis()
        {
            float moveAxis = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                moveAxis -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                moveAxis += 1f;
            }

            return Mathf.Clamp(moveAxis, -1f, 1f);
        }

        private void HandleLocalPlayerSelection()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SelectLocalPlayer(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SelectLocalPlayer(2);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SelectLocalPlayer(3);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SelectLocalPlayer(4);
            }
        }

        private void SelectLocalPlayer(int playerId)
        {
            if (_runtime.SetLocalPlayer(playerId))
            {
                _localPlayerId = playerId;
                _lastServeBlockReason = ServeBlockReason.None;
                ConfigurePrototypeCamera();
            }
        }

        private Vector2 GetLocalViewUp()
        {
            PlayerRuntimeState localPlayer = _runtime?.FindPlayer(_localPlayerId);
            Vector2 normal = localPlayer?.Paddle != null ? localPlayer.Paddle.Normal : Vector2.up;
            return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up;
        }

        private Vector2 GetLocalViewRight()
        {
            Vector2 viewUp = GetLocalViewUp();
            return new Vector2(viewUp.y, -viewUp.x).normalized;
        }

        private float GetLocalMoveAxisSign()
        {
            PlayerRuntimeState localPlayer = _runtime?.FindPlayer(_localPlayerId);
            if (localPlayer?.Paddle == null)
            {
                return 1f;
            }

            float alignment = Vector2.Dot(localPlayer.Paddle.Tangent.normalized, GetLocalViewRight());
            return alignment >= 0f ? 1f : -1f;
        }

        private static string FormatTime(float remainingTime)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static string FormatPhase(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Waiting:
                    return "等待";
                case MatchPhase.Countdown:
                    return "倒计时";
                case MatchPhase.Playing:
                    return "进行中";
                case MatchPhase.GoalPause:
                    return "进球暂停";
                case MatchPhase.Overtime:
                    return "加时";
                case MatchPhase.Result:
                    return "结算";
                default:
                    return phase.ToString();
            }
        }

        private static string FormatServeBlockReason(ServeBlockReason reason)
        {
            switch (reason)
            {
                case ServeBlockReason.None:
                    return "无";
                case ServeBlockReason.PlayerDisabled:
                    return "玩家已出局";
                case ServeBlockReason.CoolingDown:
                    return "冷却中";
                case ServeBlockReason.NoAmmo:
                    return "弹药不足";
                case ServeBlockReason.OwnedBallLimit:
                    return "己方球已达上限";
                case ServeBlockReason.MatchBallLimit:
                    return "全场球已达上限";
                default:
                    return reason.ToString();
            }
        }

        private void DrawScoreRows(GatebreakerHudSnapshot snapshot)
        {
            if (snapshot.PlayerScores == null || snapshot.PlayerScores.Count == 0)
            {
                GUILayout.Label("  无玩家", _hudStyle);
                return;
            }

            for (int i = 0; i < snapshot.PlayerScores.Count; i++)
            {
                PlayerScoreSnapshot score = snapshot.PlayerScores[i];
                string label = score.PlayerId == snapshot.LocalPlayerId
                    ? $"玩家{score.PlayerId}*：{score.Score}"
                    : $"玩家{score.PlayerId}：{score.Score}";
                GUILayout.Label($"  {label}", _hudStyle);
            }
        }

        private void DrawResultPanel(GatebreakerHudSnapshot snapshot)
        {
            if (snapshot.Phase != MatchPhase.Result)
            {
                return;
            }

            Rect panel = new Rect((Screen.width - 360f) * 0.5f, (Screen.height - 210f) * 0.5f, 360f, 210f);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 22f, panel.y + 18f, panel.width - 44f, panel.height - 36f));
            GUILayout.Label("比赛结束", _resultTitleStyle);
            GUILayout.Space(8f);
            GUILayout.Label(BuildWinnerText(snapshot), _resultBodyStyle);
            GUILayout.Space(8f);
            DrawScoreRows(snapshot);
            GUILayout.FlexibleSpace();
            GUILayout.Label("按 R 重新开始", _resultBodyStyle);
            GUILayout.EndArea();
        }

        private void DrawLanRoomPanel()
        {
            if (_lanRoomService == null)
            {
                return;
            }

            RoomSnapshot snapshot = _lanRoomService.CurrentSnapshot;
            float panelX = Mathf.Max(16f, Screen.width - 326f);
            float panelY = Screen.width < 740f ? 376f : 16f;
            Rect panel = new Rect(panelX, panelY, 310f, 240f);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f));
            GUILayout.Label("LAN 房间", _hudStyle);
            GUILayout.Label($"状态：{FormatLanRoomState(snapshot.State)}", _hudStyle);
            GUILayout.Label($"房间号：{(string.IsNullOrEmpty(snapshot.RoomCode) ? "-" : snapshot.RoomCode)}", _hudStyle);
            GUILayout.Label($"玩家：{snapshot.Players.Length}/{Mathf.Max(1, snapshot.MaxPlayers)}", _hudStyle);
            _lanPlayerName = GUILayout.TextField(_lanPlayerName, 18);
            _lanRoomCodeInput = GUILayout.TextField(_lanRoomCodeInput, 12);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建"))
            {
                CreateLanHost();
            }

            if (GUILayout.Button("发现"))
            {
                StartLanDiscovery();
            }

            if (GUILayout.Button("加入"))
            {
                JoinLanRoom();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("准备"))
            {
                ToggleLanReady(snapshot);
            }

            GUI.enabled = snapshot.CanStart;
            if (GUILayout.Button("开始"))
            {
                _lanRoomService.StartLoading();
            }

            GUI.enabled = true;
            if (GUILayout.Button("离开"))
            {
                _lanRoomService.Leave("ui");
            }

            GUILayout.EndHorizontal();

            if (snapshot.State == LanRoomState.Loading && !snapshot.IsHost)
            {
                if (GUILayout.Button("确认加载完成"))
                {
                    _lanRoomService.AcknowledgeStart();
                }
            }

            if (!string.IsNullOrEmpty(snapshot.Error))
            {
                GUILayout.Label(TruncateLanStatus(snapshot.Error), _dangerStyle);
            }

            GUILayout.EndArea();
        }

        private void EnsureLanIdentity()
        {
            if (_lanClientInstanceId != 0UL)
            {
                return;
            }

            unchecked
            {
                _lanClientInstanceId = (ulong)System.DateTime.UtcNow.Ticks ^ (ulong)UnityEngine.Random.Range(1, int.MaxValue);
            }
        }

        private void CreateLanHost()
        {
            EnsureLanIdentity();
            _lanTransport?.StartDiscovery();
            _lanTransport?.StartTcpHost();
            int tcpPort = _lanTransport?.TcpListenEndpoint.Port ?? 0;
            RoomSnapshot snapshot = _lanRoomService.CreateHost(_lanPlayerName, _lanClientInstanceId, tcpPort: tcpPort);
            _lanRoomCodeInput = snapshot.RoomCode;
        }

        private void StartLanDiscovery()
        {
            EnsureLanIdentity();
            _lanTransport?.StartDiscovery();
            _lanRoomService.StartDiscovery(_lanClientInstanceId, _lanPlayerName);
        }

        private void JoinLanRoom()
        {
            if (string.IsNullOrWhiteSpace(_lanRoomCodeInput))
            {
                return;
            }

            _lanRoomService.JoinDiscoveredRoom(_lanRoomCodeInput);
        }

        private void ToggleLanReady(RoomSnapshot snapshot)
        {
            RoomPlayerSnapshot local = null;
            for (int i = 0; i < snapshot.Players.Length; i++)
            {
                if (snapshot.Players[i].IsLocal)
                {
                    local = snapshot.Players[i];
                    break;
                }
            }

            bool nextReady = local == null || !local.IsReady;
            _lanRoomService.SetReady(nextReady);
        }

        private static string TruncateLanStatus(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 44)
            {
                return value;
            }

            return value.Substring(0, 41) + "...";
        }

        private static string FormatLanRoomState(LanRoomState state)
        {
            switch (state)
            {
                case LanRoomState.Discovering:
                    return "发现中";
                case LanRoomState.Lobby:
                    return "大厅";
                case LanRoomState.Joining:
                    return "加入中";
                case LanRoomState.Loading:
                    return "加载";
                case LanRoomState.Playing:
                    return "对战";
                case LanRoomState.Left:
                    return "已离开";
                case LanRoomState.Aborted:
                    return "已中止";
                case LanRoomState.Idle:
                default:
                    return "空闲";
            }
        }

        private static string BuildWinnerText(GatebreakerHudSnapshot snapshot)
        {
            if (!snapshot.HasWinner || snapshot.WinnerPlayerId <= 0)
            {
                return "本局没有胜者";
            }

            int score = FindPlayerScore(snapshot, snapshot.WinnerPlayerId);
            return $"玩家{snapshot.WinnerPlayerId} 获胜！最终分数：{score}";
        }

        private static int FindPlayerScore(GatebreakerHudSnapshot snapshot, int playerId)
        {
            if (snapshot.PlayerScores == null)
            {
                return 0;
            }

            for (int i = 0; i < snapshot.PlayerScores.Count; i++)
            {
                PlayerScoreSnapshot score = snapshot.PlayerScores[i];
                if (score.PlayerId == playerId)
                {
                    return score.Score;
                }
            }

            return 0;
        }

        private void DrawServeButton(GatebreakerHudSnapshot snapshot)
        {
            if (snapshot.Phase == MatchPhase.Result)
            {
                return;
            }

            Rect arenaRect = CalculateArenaGuiRect();
            Rect buttonRect = CalculateServeButtonRect(arenaRect);
            if (GUI.Button(buttonRect, BuildServeButtonText(snapshot), _serveButtonStyle))
            {
                _guiServePressed = true;
            }
        }

        private void DrawBounceTuningPanel(GatebreakerHudSnapshot snapshot)
        {
            if (snapshot.Phase == MatchPhase.Result || _runtime?.BounceTuning == null)
            {
                return;
            }

            Rect serveButtonRect = CalculateServeButtonRect(CalculateArenaGuiRect());
            Rect panel = CalculateTuningPanelRect(serveButtonRect);
            GUI.Box(panel, GUIContent.none);
            Rect contentRect = new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 20f);
            GUILayout.BeginArea(contentRect);
            _tuningScrollPosition = GUILayout.BeginScrollView(_tuningScrollPosition, false, true);
            GUILayout.Label("反弹调参", _tuningLabelStyle);
            DrawTuningSlider(
                "命中位置影响",
                _runtime.BounceTuning.HitOffsetInfluenceValue,
                PaddleBounceTuning.HitOffsetInfluenceMin,
                PaddleBounceTuning.HitOffsetInfluenceMax,
                _runtime.BounceTuning.HitOffsetInfluence,
                _runtime.BounceTuning.SetHitOffsetInfluenceValue,
                _hudStyle);
            DrawTuningSlider(
                "板速影响",
                _runtime.BounceTuning.PaddleVelocityInfluenceValue,
                PaddleBounceTuning.PaddleVelocityInfluenceMin,
                PaddleBounceTuning.PaddleVelocityInfluenceMax,
                _runtime.BounceTuning.PaddleVelocityInfluence,
                _runtime.BounceTuning.SetPaddleVelocityInfluenceValue,
                _hudStyle);
            DrawTuningSlider(
                "最小离板分量",
                _runtime.BounceTuning.MinimumOutwardShareValue,
                PaddleBounceTuning.MinimumOutwardShareMin,
                PaddleBounceTuning.MinimumOutwardShareMax,
                _runtime.BounceTuning.MinimumOutwardShare,
                _runtime.BounceTuning.SetMinimumOutwardShareValue,
                _hudStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void DrawTuningSlider(
            string label,
            int value,
            int min,
            int max,
            float actualValue,
            Action<int> setter,
            GUIStyle labelStyle)
        {
            GUILayout.Label($"{label}：{value}（实际：{actualValue:0.00}）", labelStyle);
            int nextValue = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
            if (nextValue != value)
            {
                setter(nextValue);
            }
        }

        private Rect CalculateServeButtonRect(Rect arenaRect)
        {
            float availableWidth = Mathf.Max(1f, Screen.width - ScreenPadding * 2f);
            float minWidth = Mathf.Min(220f, availableWidth);
            float maxWidth = Mathf.Min(360f, availableWidth);
            float width = Mathf.Clamp(arenaRect.width * 0.62f, minWidth, maxWidth);
            float x = Mathf.Clamp(
                arenaRect.x + (arenaRect.width - width) * 0.5f,
                ScreenPadding,
                Mathf.Max(ScreenPadding, Screen.width - ScreenPadding - width));
            float y = CalculateControlStartY(arenaRect, CalculateTuningPanelHeight(arenaRect));
            return new Rect(x, y, width, ServeButtonHeight);
        }

        private Rect CalculateTuningPanelRect(Rect serveButtonRect)
        {
            Rect arenaRect = CalculateArenaGuiRect();
            float height = CalculateTuningPanelHeight(arenaRect);
            return new Rect(
                serveButtonRect.x,
                serveButtonRect.yMax + ControlGap,
                serveButtonRect.width,
                height);
        }

        private float CalculateTuningPanelHeight(Rect arenaRect)
        {
            float preferredStartY = arenaRect.yMax + 12f;
            float availableBelow = Screen.height - preferredStartY - ScreenPadding - ServeButtonHeight - ControlGap;
            if (availableBelow >= TuningPanelMinimumHeight)
            {
                return Mathf.Min(TuningPanelPreferredHeight, availableBelow);
            }

            float compactHeight = Mathf.Max(TuningPanelMinimumHeight, Screen.height * 0.22f);
            return Mathf.Min(TuningPanelPreferredHeight, compactHeight);
        }

        private float CalculateControlStartY(Rect arenaRect, float tuningPanelHeight)
        {
            float totalControlHeight = ServeButtonHeight + ControlGap + tuningPanelHeight;
            float preferredY = arenaRect.yMax + 12f;
            float maxY = Screen.height - totalControlHeight - ScreenPadding;
            if (maxY >= preferredY)
            {
                return preferredY;
            }

            return Mathf.Clamp(maxY, ScreenPadding, Mathf.Max(ScreenPadding, Screen.height - ServeButtonHeight - ScreenPadding));
        }

        private Rect CalculateArenaGuiRect()
        {
            if (_prototypeCamera == null)
            {
                return new Rect(Screen.width * 0.2f, Screen.height * 0.45f, Screen.width * 0.6f, Screen.height * 0.32f);
            }

            Vector3[] corners =
            {
                new Vector3(-ArenaHalfWidth, 0f, -ArenaHalfHeight),
                new Vector3(-ArenaHalfWidth, 0f, ArenaHalfHeight),
                new Vector3(ArenaHalfWidth, 0f, -ArenaHalfHeight),
                new Vector3(ArenaHalfWidth, 0f, ArenaHalfHeight),
            };

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 world = _visualRoot != null ? _visualRoot.TransformPoint(corners[i]) : corners[i];
                Vector3 screen = _prototypeCamera.WorldToScreenPoint(world);
                float guiY = Screen.height - screen.y;
                minX = Mathf.Min(minX, screen.x);
                minY = Mathf.Min(minY, guiY);
                maxX = Mathf.Max(maxX, screen.x);
                maxY = Mathf.Max(maxY, guiY);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private static string BuildServeButtonText(GatebreakerHudSnapshot snapshot)
        {
            return $"发射子弹：{snapshot.CurrentServeAmmo}/{snapshot.MaxServeAmmo}";
        }

        private Color GetPlayerColor(int playerId)
        {
            return GetPlayerMaterial(playerId).color;
        }

        private Material GetPlayerMaterial(int playerId)
        {
            if (_playerMaterials == null || _playerMaterials.Length == 0)
            {
                return _localMaterial;
            }

            int index = Mathf.Abs(playerId - 1) % _playerMaterials.Length;
            return _playerMaterials[index];
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Sprites/Default") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Diffuse");
            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static void SetMaterial(GameObject gameObject, Material material)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void EnsureHudStyles()
        {
            if (_hudStyle != null)
            {
                return;
            }

            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
            };
            _hudStyle.normal.textColor = Color.white;
            _dangerStyle = new GUIStyle(_hudStyle)
            {
                fontStyle = FontStyle.Bold,
            };
            _dangerStyle.normal.textColor = new Color(1f, 0.35f, 0.25f);
            _resultTitleStyle = new GUIStyle(_hudStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
            };
            _resultTitleStyle.normal.textColor = Color.white;
            _resultBodyStyle = new GUIStyle(_hudStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
            };
            _resultBodyStyle.normal.textColor = Color.white;
            _serveButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };
            _tuningLabelStyle = new GUIStyle(_hudStyle)
            {
                fontStyle = FontStyle.Bold,
            };
        }

        private void ConfigurePrototypeCamera()
        {
            if (_prototypeCamera == null)
            {
                _prototypeCamera = Camera.main;
                if (_prototypeCamera == null)
                {
                    GameObject cameraObject = new GameObject("Gatebreaker Prototype Camera");
                    _prototypeCamera = cameraObject.AddComponent<Camera>();
                    cameraObject.tag = "MainCamera";
                }
            }

            _prototypeCamera.rect = CalculateSquareCameraViewport();
            float aspect = Mathf.Max(0.1f, _prototypeCamera.aspect);
            if (_visualRoot != null)
            {
                _visualRoot.localScale = GetPrototypeVisualScale();
            }

            Vector2 viewUp2D = GetLocalViewUp();
            Vector2 viewRight2D = GetLocalViewRight();
            Vector3 screenUp = GetVisualDirection(viewUp2D);
            _prototypeCamera.transform.position = new Vector3(0f, CameraHeight, 0f);
            _prototypeCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, screenUp);
            _prototypeCamera.orthographic = true;
            CalculateViewExtents(viewUp2D, viewRight2D, out float viewHalfHeight, out float viewHalfWidth);
            _prototypeCamera.orthographicSize = Mathf.Max(
                viewHalfHeight + CameraMargin,
                (viewHalfWidth + CameraMargin) / aspect);
            _prototypeCamera.nearClipPlane = 0.1f;
            _prototypeCamera.farClipPlane = CameraHeight + 10f;
            _prototypeCamera.clearFlags = CameraClearFlags.SolidColor;
            _prototypeCamera.backgroundColor = new Color(0.03f, 0.04f, 0.05f);
        }

        private static Rect CalculateSquareCameraViewport()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            float side = Mathf.Min(Screen.width, Screen.height);
            float x = (Screen.width - side) * 0.5f / Screen.width;
            float y = (Screen.height - side) * 0.5f / Screen.height;
            return new Rect(x, y, side / Screen.width, side / Screen.height);
        }

        private Vector3 GetPrototypeVisualScale()
        {
            float depthScale = ArenaHalfHeight > 0.001f ? ArenaHalfWidth / ArenaHalfHeight : 1f;
            return new Vector3(1f, 1f, depthScale);
        }

        private Vector3 GetCompensatedVisualScale(float worldSize)
        {
            Vector3 scale = GetPrototypeVisualScale();
            return new Vector3(
                worldSize / Mathf.Max(0.001f, scale.x),
                worldSize / Mathf.Max(0.001f, scale.y),
                worldSize / Mathf.Max(0.001f, scale.z));
        }

        private Vector3 GetVisualDirection(Vector2 direction)
        {
            Vector3 scale = GetPrototypeVisualScale();
            Vector3 visualDirection = new Vector3(direction.x * scale.x, 0f, direction.y * scale.z);
            return visualDirection.sqrMagnitude > 0.0001f ? visualDirection.normalized : Vector3.forward;
        }

        private void CalculateViewExtents(Vector2 viewUp, Vector2 viewRight, out float halfHeight, out float halfWidth)
        {
            halfHeight = 0f;
            halfWidth = 0f;
            Vector3 scale = GetPrototypeVisualScale();
            Vector2[] corners =
            {
                new Vector2(-ArenaHalfWidth * scale.x, -ArenaHalfHeight * scale.z),
                new Vector2(-ArenaHalfWidth * scale.x, ArenaHalfHeight * scale.z),
                new Vector2(ArenaHalfWidth * scale.x, -ArenaHalfHeight * scale.z),
                new Vector2(ArenaHalfWidth * scale.x, ArenaHalfHeight * scale.z),
            };
            Vector2 visualUp = new Vector2(viewUp.x * scale.x, viewUp.y * scale.z).normalized;
            Vector2 visualRight = new Vector2(viewRight.x * scale.x, viewRight.y * scale.z).normalized;

            for (int i = 0; i < corners.Length; i++)
            {
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector2.Dot(corners[i], visualUp)));
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector2.Dot(corners[i], visualRight)));
            }
        }

        private void CreateLightIfNeeded()
        {
            if (FindObjectOfType<Light>() != null)
            {
                return;
            }

            GameObject lightObject = new GameObject("Gatebreaker Prototype Light");
            lightObject.transform.SetParent(_visualRoot, false);
            lightObject.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
        }
    }
}
