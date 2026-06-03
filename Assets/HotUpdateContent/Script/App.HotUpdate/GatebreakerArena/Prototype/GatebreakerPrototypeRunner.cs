using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Paddle;
using App.HotUpdate.GatebreakerArena.UI;
using App.Shared.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.HotUpdate.GatebreakerArena.Prototype
{
    [DefaultExecutionOrder(10000)]
    public sealed class GatebreakerPrototypeRunner : MonoBehaviour
    {
        private const int DefaultLocalPlayerId = 1;
        private const float GuardDepth = 0.55f;
        private const float CameraHeight = 20f;
        private const float CameraMargin = 0.9f;
        private const string ArenaRootName = "ArenaRoot";
        private const string ObjPoolRootName = "ObjPool";
        private const string DebugCollisionOverlayName = "DebugCollisionOverlay";
        private const string SceneInstanceName = "Scene3v3";
        private const float Scene3v3HalfWidth = 2.81f;
        private const float Scene3v3HalfHeight = 2.456f;
        private const float Scene3v3PaddlePrefabNormalScale = 0.14f;
        private const float SceneVisualBoundsPadding = 0.04f;
        private const float DebugOverlayPrefabDepth = -0.08f;
        private const float DebugOverlayFallbackHeight = 0.08f;
        private const int DebugOverlaySortingOrder = 1200;
        private const string SceneDebugLayerName = "SceneDebug";
        private const int SceneDebugLayerFallback = 6;
        private const float LocalStartCountdownSeconds = 5f;
        private const float LocalStartReadyTextSeconds = 0.75f;
        private const float LanDiagnosticsPanelMargin = 8f;
        private const float LanDiagnosticsTopOffset = 46f;
        private const int LanDiagnosticsTitleFontSize = 22;
        private const int LanDiagnosticsBodyFontSize = 16;
        private const int LanDiagnosticsSmallFontSize = 14;

        private readonly Dictionary<int, Transform> _ballViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, int> _ballViewSlots = new Dictionary<int, int>();
        private readonly Dictionary<int, Transform> _paddleViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Renderer> _paddleRenderers = new Dictionary<int, Renderer>();
        private readonly Dictionary<int, Renderer> _guardRenderers = new Dictionary<int, Renderer>();
        private readonly List<LineRenderer> _debugCollisionLines = new List<LineRenderer>();
        private readonly HashSet<int> _liveBallIds = new HashSet<int>();
        private readonly Dictionary<int, Stack<GameObject>> _ballViewPools = new Dictionary<int, Stack<GameObject>>();

        private GatebreakerMatchRuntime _runtime;
        private GatebreakerInputService _inputService;
        private GatebreakerArenaHudPresenter _hudPresenter;
        private GatebreakerArenaSceneBindingService _sceneBindingService;
        private LanRoomService _lanRoomService;
        private LanDiagnosticsService _lanDiagnosticsService;
        private ILanTransport _lanTransport;
        private GatebreakerVisualAssetService _visualAssetService;
        private GatebreakerVisualAssetSet _visualAssets;
        private Transform _visualRoot;
        private Transform _poolRoot;
        private Transform _debugCollisionOverlayRoot;
        private GameObject _sceneInstance;
        private Material _arenaMaterial;
        private Material _wallMaterial;
        private Material _localMaterial;
        private Material _dangerMaterial;
        private Material _paddleMaterial;
        private Material _debugOverlayMaterial;
        private Material[] _playerMaterials;
        private Camera _prototypeCamera;
        private Bounds _sceneVisualBounds;
        private ServeBlockReason _lastServeBlockReason = ServeBlockReason.None;
        private int _localPlayerId = DefaultLocalPlayerId;
        private bool _initialized;
        private bool _guiServePressed;
        private float _guiMoveAxis;
        private ulong _lanClientInstanceId;
        private string _lanPlayerName = "Player";
        private string _lanRoomCodeInput = string.Empty;
        private string _cachedLocalLanAddress = "-";
        private string _lastLanDiagnosticsSummary = string.Empty;
        private string _lastLanDiagnosticsExportPath = string.Empty;
        private float _lanInputAccumulator;
        private float _nextLocalLanAddressRefreshTime;
        private Vector2 _lanDiagnosticsScroll;
        private bool _showLanDiagnostics;
        private bool _usePrefabVisuals;
        private bool _ownsVisualRoot;
        private bool _hasSceneVisualBounds;
        private bool _missingInputServiceWarningLogged;
        private bool _lanEntryUiHiddenForPlaying;
        private float _paddlePrefabLength = 1f;
        private StartupUiState _startupUiState = StartupUiState.ModeSelect;
        private float _localStartCountdownElapsed;
        private string _lastStartCountdownText;

        private enum StartupUiState
        {
            ModeSelect,
            LocalCountdown,
            LocalPlaying,
            OnlineMenu,
            OnlineRoom,
        }

        private float ArenaHalfWidth => _runtime?.Arena != null ? _runtime.Arena.HalfWidth : 8f;
        private float ArenaHalfHeight => _runtime?.Arena != null ? _runtime.Arena.HalfHeight : 5f;

        public void Initialize(GatebreakerArenaApplicationContext context)
        {
            InitializeAsync(context).GetAwaiter().GetResult();
        }

        public async Task InitializeAsync(GatebreakerArenaApplicationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _visualAssetService = context.VisualAssetService;
            _lanRoomService = context.LanRoomService;
            _lanDiagnosticsService = context.LanDiagnosticsService;
            _lanTransport = context.Services?.Get<ILanTransport>();
            _sceneBindingService = context.SceneBindingService;
            await InitializeAsync(context.MatchRuntime, context.InputService, context.HudPresenter, DefaultLocalPlayerId);
            _sceneBindingService?.Bind(
                ResolveSceneUiBinding(context.Services),
                BuildSceneUiCallbacks(),
                context.Logger);
            RefreshBoundHud();
            EnsureLanIdentity();
        }

        public void Initialize(
            GatebreakerMatchRuntime runtime,
            GatebreakerInputService inputService,
            GatebreakerArenaHudPresenter hudPresenter,
            int localPlayerId = DefaultLocalPlayerId)
        {
            InitializeAsync(runtime, inputService, hudPresenter, localPlayerId).GetAwaiter().GetResult();
        }

        public async Task InitializeAsync(
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

            await EnsureSceneAsync();
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

            if (!EnsureLocalRuntimeReady())
            {
                return;
            }

            HandleLanDiagnosticsShortcuts();

            if (HandleStartupUiState())
            {
                return;
            }

#if UNITY_EDITOR
            if (HandleEditorDebugShortcuts())
            {
                return;
            }
#endif

            if (!IsLanPlaying() && Input.GetKeyDown(KeyCode.R))
            {
                RestartLocalPrototype();
            }

            if (IsLanPlaying())
            {
                SyncLanLocalPlayer();
            }
            else
            {
                HandleLocalPlayerSelection();
            }

            float screenMoveAxis = ReadMoveAxis();
            _sceneBindingService?.PreviewMoveAxis(screenMoveAxis);
            float moveAxis = screenMoveAxis * GetLocalMoveAxisSign();
            bool servePressed = Input.GetKeyDown(KeyCode.Space) || _guiServePressed;
            _guiServePressed = false;
            var frame = new PlayerInputFrame(_localPlayerId, moveAxis, servePressed, BuildServeAimDirection(moveAxis));
            if (IsLanPlaying())
            {
                SubmitLanInputAtFixedRate(frame);
                SyncPlayerViews();
                SyncBallViews();
                RefreshBoundHud();
                return;
            }

            _lanInputAccumulator = 0f;
            SetLocalInputFrame(frame);
            _runtime.ApplyInputFrame(frame);
            _runtime.TickLocalPrototype(Time.deltaTime);
            if (servePressed)
            {
                PlayerRuntimeState localPlayer = _runtime.FindPlayer(_localPlayerId);
                _lastServeBlockReason = localPlayer?.ServeResource?.LastBlockReason ?? ServeBlockReason.PlayerDisabled;
            }

            SyncPlayerViews();
            SyncBallViews();
            RefreshBoundHud();
        }

        private bool EnsureLocalRuntimeReady()
        {
            if (_runtime != null)
            {
                return true;
            }

            Debug.LogError("GatebreakerPrototypeRunner: runtime dependency is missing; disabling prototype runner.");
            _initialized = false;
            enabled = false;
            return false;
        }

#if UNITY_EDITOR
        private bool HandleEditorDebugShortcuts()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return false;
            }

            if (IsLanPlaying())
            {
                Debug.LogWarning("GatebreakerPrototypeRunner: editor ESC result shortcut is ignored during LAN play.");
                return false;
            }

            if (!_runtime.ForceFinishWithCurrentLeader())
            {
                return false;
            }

            _guiServePressed = false;
            _lanInputAccumulator = 0f;
            SyncPlayerViews();
            SyncBallViews();
            RefreshBoundHud();
            Debug.Log("GatebreakerPrototypeRunner: editor ESC forced local prototype result.");
            return true;
        }
#endif

        private void SetLocalInputFrame(PlayerInputFrame frame)
        {
            if (_inputService != null)
            {
                _inputService.SetFrame(frame);
                return;
            }

            if (!_missingInputServiceWarningLogged)
            {
                Debug.LogWarning("GatebreakerPrototypeRunner: input service is missing; local prototype continues with direct runtime input.");
                _missingInputServiceWarningLogged = true;
            }
        }

        private bool IsLanPlaying()
        {
            return _lanRoomService != null &&
                   _lanRoomService.CurrentSnapshot.State == LanRoomState.Playing;
        }

        private void HandleLanDiagnosticsShortcuts()
        {
            if (_lanDiagnosticsService == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                _showLanDiagnostics = !_showLanDiagnostics;
                if (_showLanDiagnostics)
                {
                    _lastLanDiagnosticsSummary = _lanDiagnosticsService.CreateSummaryText(_lanRoomService?.CurrentSnapshot);
                    LogLanDiagnosticsSummary("F8");
                }
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                _lanDiagnosticsService.Flush();
                _lastLanDiagnosticsSummary = _lanDiagnosticsService.CreateSummaryText(_lanRoomService?.CurrentSnapshot);
                LogLanDiagnosticsSummary("F9");
            }
        }

        private bool HandleStartupUiState()
        {
            if (HandleLanRoomTerminalNavigation())
            {
                return true;
            }

            if (IsLanPlaying())
            {
                _startupUiState = StartupUiState.OnlineRoom;
                if (!_lanEntryUiHiddenForPlaying)
                {
                    _sceneBindingService?.HideEntryUi();
                    _lanEntryUiHiddenForPlaying = true;
                    RefreshBoundHud();
                }

                return false;
            }

            _lanEntryUiHiddenForPlaying = false;
            if (_startupUiState == StartupUiState.LocalCountdown)
            {
                UpdateLocalStartCountdown();
                return true;
            }

            return _startupUiState != StartupUiState.LocalPlaying;
        }

        private bool HandleLanRoomTerminalNavigation()
        {
            RoomSnapshot snapshot = _lanRoomService?.CurrentSnapshot;
            if (!IsLanRoomTerminal(snapshot) || _startupUiState == StartupUiState.ModeSelect)
            {
                return false;
            }

            ReturnToModeSelectFromResult();
            return true;
        }

        private void UpdateLocalStartCountdown()
        {
            _localStartCountdownElapsed += Mathf.Max(0f, Time.deltaTime);
            float remaining = LocalStartCountdownSeconds - _localStartCountdownElapsed;
            if (remaining > 0f)
            {
                ShowStartCountdown(Mathf.CeilToInt(remaining).ToString());
                return;
            }

            if (_localStartCountdownElapsed < LocalStartCountdownSeconds + LocalStartReadyTextSeconds)
            {
                ShowStartCountdown("开始游戏");
                return;
            }

            _startupUiState = StartupUiState.LocalPlaying;
            _sceneBindingService?.HideEntryUi();
            _lastStartCountdownText = null;
            RefreshBoundHud();
        }

        private void ShowStartCountdown(string text)
        {
            if (text == _lastStartCountdownText)
            {
                return;
            }

            _lastStartCountdownText = text;
            _sceneBindingService?.ShowStartCountdown(text);
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

                if (player.PlayerId != _localPlayerId && _runtime.SetLocalPlayer(player.PlayerId, true))
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
            if (_lanDiagnosticsService == null)
            {
                return;
            }

            GUIStyle diagButtonStyle = CreateLanDiagnosticsButtonStyle();
            if (GUI.Button(new Rect(8f, Screen.height - 42f, 74f, 34f), "DIAG", diagButtonStyle))
            {
                _showLanDiagnostics = !_showLanDiagnostics;
                if (_showLanDiagnostics)
                {
                    _lastLanDiagnosticsSummary = _lanDiagnosticsService.CreateSummaryText(_lanRoomService?.CurrentSnapshot);
                    LogLanDiagnosticsSummary("DIAG");
                }
            }

            if (!_showLanDiagnostics)
            {
                return;
            }

            DrawLanDiagnosticsOverlay();
        }

        private void DrawLanDiagnosticsOverlay()
        {
            RoomSnapshot room = _lanRoomService?.CurrentSnapshot;
            LanDiagnosticsSnapshot diagnostics = _lanDiagnosticsService.CreateSnapshot();
            float width = Mathf.Max(320f, Screen.width - LanDiagnosticsPanelMargin * 2f);
            float height = Mathf.Max(260f, Screen.height - LanDiagnosticsTopOffset - LanDiagnosticsPanelMargin);
            Rect panel = new Rect(LanDiagnosticsPanelMargin, LanDiagnosticsTopOffset, width, height);
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.98f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = previousColor;

            GUIStyle titleStyle = CreateLanDiagnosticsLabelStyle(LanDiagnosticsTitleFontSize, FontStyle.Bold);
            GUIStyle bodyStyle = CreateLanDiagnosticsLabelStyle(LanDiagnosticsBodyFontSize, FontStyle.Normal);
            GUIStyle eventStyle = CreateLanDiagnosticsLabelStyle(LanDiagnosticsSmallFontSize, FontStyle.Normal);
            GUIStyle textAreaStyle = CreateLanDiagnosticsTextAreaStyle();
            GUIStyle buttonStyle = CreateLanDiagnosticsButtonStyle();

            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 20f));
            GUILayout.Label("LAN Diagnostics", titleStyle);
            GUILayout.Label("state=" + (room != null ? room.State.ToString() : "-") +
                            "  role=" + (room != null ? (room.IsHost ? "Host" : "Client") : "-") +
                            "  room=" + (room != null ? room.RoomCode : "-"), bodyStyle);
            GUILayout.Label("session=" + (room != null ? room.SessionId.ToString() : "-") +
                            "  slot=" + (room != null ? room.LocalSlotIndex.ToString() : "-") +
                            "  log=" + ShortenPath(diagnostics.CurrentLogPath), bodyStyle);
            if (room?.Lockstep != null)
            {
                GUILayout.Label("frame confirmed=" + room.Lockstep.LatestConfirmedFrame +
                                " target=" + room.Lockstep.LocalTargetFrame +
                                " waiting=" + JoinInts(room.Lockstep.WaitingSlotIndexes), bodyStyle);
            }

            if (!string.IsNullOrEmpty(diagnostics.LastWriteError))
            {
                GUILayout.Label("writeError=" + diagnostics.LastWriteError, bodyStyle);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", buttonStyle, GUILayout.Height(36f)))
            {
                _lastLanDiagnosticsSummary = _lanDiagnosticsService.CreateSummaryText(room);
                LogLanDiagnosticsSummary("Refresh");
            }

            if (GUILayout.Button("Flush", buttonStyle, GUILayout.Height(36f)))
            {
                _lanDiagnosticsService.Flush();
                _lastLanDiagnosticsSummary = _lanDiagnosticsService.CreateSummaryText(room);
                LogLanDiagnosticsSummary("Flush");
            }

            if (GUILayout.Button("Export Package", buttonStyle, GUILayout.Height(36f)))
            {
                _lastLanDiagnosticsExportPath = _lanDiagnosticsService.ExportDiagnosticsPackage(room);
                _lastLanDiagnosticsSummary = _lanDiagnosticsService.CreateSummaryText(room);
                LogLanDiagnosticsSummary("Export");
            }

            if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(36f)))
            {
                _showLanDiagnostics = false;
            }

            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_lastLanDiagnosticsExportPath))
            {
                GUILayout.Label("export=" + ShortenPath(_lastLanDiagnosticsExportPath), bodyStyle);
            }

            _lanDiagnosticsScroll = GUILayout.BeginScrollView(_lanDiagnosticsScroll);
            GUILayout.Label("Recent events:", bodyStyle);
            LanDiagnosticEvent[] events = diagnostics.RecentEvents ?? Array.Empty<LanDiagnosticEvent>();
            int start = Mathf.Max(0, events.Length - 30);
            for (int i = start; i < events.Length; i++)
            {
                LanDiagnosticEvent item = events[i];
                GUILayout.Label(item.MonotonicMs + " " + item.EventName + " f=" + item.FrameIndex + " " + item.Result + " " + item.Detail, eventStyle);
            }

            if (!string.IsNullOrEmpty(_lastLanDiagnosticsSummary))
            {
                GUILayout.Space(10f);
                GUILayout.Label("Summary:", bodyStyle);
                GUILayout.TextArea(_lastLanDiagnosticsSummary, textAreaStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void LogLanDiagnosticsSummary(string reason)
        {
            if (_lanDiagnosticsService == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_lastLanDiagnosticsSummary))
            {
                _lastLanDiagnosticsSummary = _lanDiagnosticsService.CreateSummaryText(_lanRoomService?.CurrentSnapshot);
            }

            Debug.Log("[Gatebreaker LAN Diagnostics][" + reason + "]\n" + _lastLanDiagnosticsSummary);
        }

        private static GUIStyle CreateLanDiagnosticsLabelStyle(int fontSize, FontStyle fontStyle)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                wordWrap = true,
                richText = false,
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle CreateLanDiagnosticsTextAreaStyle()
        {
            var style = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = LanDiagnosticsSmallFontSize,
                wordWrap = true,
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle CreateLanDiagnosticsButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = LanDiagnosticsBodyFontSize,
                fontStyle = FontStyle.Bold,
            };
            return style;
        }

        private void OnDestroy()
        {
            _visualAssets?.Dispose();
            DestroyBallViewCache();
            ClearDebugCollisionOverlay();
            if (_debugCollisionOverlayRoot != null)
            {
                Destroy(_debugCollisionOverlayRoot.gameObject);
                _debugCollisionOverlayRoot = null;
            }

            if (_ownsVisualRoot && _visualRoot != null)
            {
                Destroy(_visualRoot.gameObject);
                _visualRoot = null;
                _ownsVisualRoot = false;
            }

            DestroyMaterial(_arenaMaterial);
            DestroyMaterial(_wallMaterial);
            DestroyMaterial(_localMaterial);
            DestroyMaterial(_dangerMaterial);
            DestroyMaterial(_paddleMaterial);
            DestroyMaterial(_debugOverlayMaterial);
            _sceneBindingService?.Clear();
            if (_playerMaterials == null)
            {
                return;
            }

            for (int i = 0; i < _playerMaterials.Length; i++)
            {
                DestroyMaterial(_playerMaterials[i]);
            }
        }

        private async Task EnsureSceneAsync()
        {
            if (_visualRoot != null)
            {
                return;
            }

            ResolveArenaRoot();
            _visualAssets = _visualAssetService != null
                ? await _visualAssetService.LoadAsync(_runtime?.EffectiveRule, _runtime?.BallRule)
                : null;

            if (_visualAssets != null && _visualAssets.IsComplete)
            {
                _usePrefabVisuals = true;
                CreatePrefabScene();
                ApplyPrefabPaddleLengthCalibration();
                RebuildDebugCollisionOverlay();
                ConfigurePrototypeCamera();
                return;
            }

            _visualAssets?.Dispose();
            _visualAssets = null;
            _usePrefabVisuals = false;
            CreateMaterials();
            ConfigurePrototypeCamera();
            CreateLightIfNeeded();
            CreateArena();
            CreateWalls();
            CreateGuardZones();
            CreatePaddles();
            RebuildDebugCollisionOverlay();
            ConfigurePrototypeCamera();
        }

        private void CreatePrefabScene()
        {
            _sceneInstance = Instantiate(_visualAssets.Scene.Prefab, _visualRoot, false);
            _sceneInstance.name = SceneInstanceName;
            _sceneInstance.transform.localPosition = Vector3.zero;
            _sceneInstance.transform.localRotation = Quaternion.identity;
            _sceneInstance.transform.localScale = Vector3.one;
            ApplyScenePlayerSideColors();
            UpdateSceneVisualBounds();
        }

        private void ApplyScenePlayerSideColors()
        {
            IReadOnlyList<MapPlayerSideBindingDefinition> bindings = _runtime?.EffectiveRule?.Map?.PlayerSideBindings;
            if (_sceneInstance == null || bindings == null)
            {
                return;
            }

            Transform[] transforms = _sceneInstance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bindings.Count; i++)
            {
                MapPlayerSideBindingDefinition binding = bindings[i];
                if (binding == null || string.IsNullOrEmpty(binding.ScenePosition))
                {
                    continue;
                }

                Transform position = FindSceneTransform(transforms, binding.ScenePosition);
                if (position == null)
                {
                    continue;
                }

                ApplySceneNetColor(position, GetPlayerColor(binding.PlayerId));
            }
        }

        private static Transform FindSceneTransform(Transform[] transforms, string name)
        {
            if (transforms == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null && string.Equals(transform.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return transform;
                }
            }

            return null;
        }

        private static void ApplySceneNetColor(Transform position, Color ownerColor)
        {
            Transform[] children = position.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null ||
                    !child.gameObject.activeSelf ||
                    !string.Equals(child.name, "net", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SpriteRenderer[] spriteRenderers = child.GetComponentsInChildren<SpriteRenderer>(true);
                for (int spriteIndex = 0; spriteIndex < spriteRenderers.Length; spriteIndex++)
                {
                    SpriteRenderer spriteRenderer = spriteRenderers[spriteIndex];
                    spriteRenderer.color = new Color(ownerColor.r, ownerColor.g, ownerColor.b, spriteRenderer.color.a);
                }

                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer is SpriteRenderer)
                    {
                        continue;
                    }

                    Material material = UnityEngine.Application.isPlaying ? renderer.material : renderer.sharedMaterial;
                    if (material == null)
                    {
                        continue;
                    }

                    Color color = new Color(ownerColor.r, ownerColor.g, ownerColor.b, material.color.a);
                    material.color = color;
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", color);
                    }
                }
            }

        }

        private void UpdateSceneVisualBounds()
        {
            _hasSceneVisualBounds = false;
            if (_sceneInstance == null)
            {
                return;
            }

            if (TryCalculateLocalRendererBounds(_sceneInstance, out Bounds bounds))
            {
                _sceneVisualBounds = bounds;
                _hasSceneVisualBounds = true;
            }
        }

        private void ApplyPrefabPaddleLengthCalibration()
        {
            _paddlePrefabLength = 1f;
            if (_runtime?.Arena == null || !_runtime.Arena.HasCustomBoundary)
            {
                return;
            }

            if (!TryCalculatePrefabRendererLength(_visualAssets?.Paddle?.Prefab, out float prefabLength))
            {
                return;
            }

            _paddlePrefabLength = prefabLength;
            float visualMatchedLength = _runtime.Arena.PaddleLength * prefabLength;
            _runtime.SetArenaPaddleLength(visualMatchedLength);
        }

        private bool TryCalculatePrefabRendererLength(GameObject prefab, out float length)
        {
            length = 0f;
            if (prefab == null)
            {
                return false;
            }

            GameObject probe = Instantiate(prefab);
            probe.name = prefab.name + " Bounds Probe";
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;

            bool hasBounds = TryCalculateLocalRendererBounds(probe, out Bounds bounds);
            Destroy(probe);
            if (!hasBounds || bounds.size.x <= 0.001f)
            {
                return false;
            }

            length = bounds.size.x;
            return true;
        }

        private static bool TryCalculateLocalRendererBounds(GameObject root, out Bounds localBounds)
        {
            localBounds = default;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Transform rootTransform = root.transform;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!ShouldIncludeSceneVisualBounds(renderer))
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 extents = bounds.extents;
                if (extents.x <= 0.0001f && extents.y <= 0.0001f)
                {
                    continue;
                }

                Vector3 center = bounds.center;
                AccumulateSceneBoundsCorner(rootTransform, center, extents, -1f, -1f, -1f, ref min, ref max);
                AccumulateSceneBoundsCorner(rootTransform, center, extents, -1f, -1f, 1f, ref min, ref max);
                AccumulateSceneBoundsCorner(rootTransform, center, extents, -1f, 1f, -1f, ref min, ref max);
                AccumulateSceneBoundsCorner(rootTransform, center, extents, -1f, 1f, 1f, ref min, ref max);
                AccumulateSceneBoundsCorner(rootTransform, center, extents, 1f, -1f, -1f, ref min, ref max);
                AccumulateSceneBoundsCorner(rootTransform, center, extents, 1f, -1f, 1f, ref min, ref max);
                AccumulateSceneBoundsCorner(rootTransform, center, extents, 1f, 1f, -1f, ref min, ref max);
                AccumulateSceneBoundsCorner(rootTransform, center, extents, 1f, 1f, 1f, ref min, ref max);
                hasBounds = true;
            }

            if (!hasBounds)
            {
                return false;
            }

            localBounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private static bool ShouldIncludeSceneVisualBounds(Renderer renderer)
        {
            return renderer != null &&
                   renderer.enabled &&
                   !(renderer is ParticleSystemRenderer);
        }

        private static void AccumulateSceneBoundsCorner(
            Transform sceneTransform,
            Vector3 center,
            Vector3 extents,
            float xSign,
            float ySign,
            float zSign,
            ref Vector3 min,
            ref Vector3 max)
        {
            Vector3 worldCorner = new Vector3(
                center.x + extents.x * xSign,
                center.y + extents.y * ySign,
                center.z + extents.z * zSign);
            Vector3 localCorner = sceneTransform.InverseTransformPoint(worldCorner);
            min = Vector3.Min(min, localCorner);
            max = Vector3.Max(max, localCorner);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RebuildDebugCollisionOverlay()
        {
            ClearDebugCollisionOverlay();
            if (_visualRoot == null || _runtime?.Arena == null)
            {
                return;
            }

            EnsureDebugCollisionOverlayRoot();
            IReadOnlyList<GatebreakerCollisionOverlayLine> lines =
                GatebreakerCollisionOverlayGeometry.BuildLines(_runtime.Arena, _runtime.Players);
            for (int i = 0; i < lines.Count; i++)
            {
                GatebreakerCollisionOverlayLine line = lines[i];
                AddDebugCollisionLine(
                    ToDebugOverlayPosition(line.Start),
                    ToDebugOverlayPosition(line.End),
                    line.Kind);
            }

            AddPrefabBoxColliderOverlayLines();
        }

        private void EnsureDebugCollisionOverlayRoot()
        {
            if (_debugCollisionOverlayRoot != null)
            {
                return;
            }

            var overlayObject = new GameObject(DebugCollisionOverlayName);
            overlayObject.layer = GetSceneDebugLayer();
            _debugCollisionOverlayRoot = overlayObject.transform;
            _debugCollisionOverlayRoot.SetParent(_visualRoot, false);
            _debugCollisionOverlayRoot.localPosition = Vector3.zero;
            _debugCollisionOverlayRoot.localRotation = Quaternion.identity;
            _debugCollisionOverlayRoot.localScale = Vector3.one;
        }

        private void ClearDebugCollisionOverlay()
        {
            for (int i = 0; i < _debugCollisionLines.Count; i++)
            {
                LineRenderer line = _debugCollisionLines[i];
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }

            _debugCollisionLines.Clear();
        }

        private void AddPrefabBoxColliderOverlayLines()
        {
            if (!_usePrefabVisuals || _sceneInstance == null || _debugCollisionOverlayRoot == null)
            {
                return;
            }

            BoxCollider2D[] colliders = _sceneInstance.GetComponentsInChildren<BoxCollider2D>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider2D collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Vector2 halfSize = collider.size * 0.5f;
                Vector2 offset = collider.offset;
                Vector2[] colliderCorners =
                {
                    offset + new Vector2(-halfSize.x, -halfSize.y),
                    offset + new Vector2(-halfSize.x, halfSize.y),
                    offset + new Vector2(halfSize.x, halfSize.y),
                    offset + new Vector2(halfSize.x, -halfSize.y),
                    offset + new Vector2(-halfSize.x, -halfSize.y),
                };
                var overlayCorners = new Vector3[colliderCorners.Length];
                for (int cornerIndex = 0; cornerIndex < colliderCorners.Length; cornerIndex++)
                {
                    Vector3 world = collider.transform.TransformPoint(colliderCorners[cornerIndex]);
                    Vector3 overlayLocal = _debugCollisionOverlayRoot.InverseTransformPoint(world);
                    overlayLocal.z = DebugOverlayPrefabDepth * 0.5f;
                    overlayCorners[cornerIndex] = overlayLocal;
                }

                AddDebugCollisionLine(overlayCorners, new Color(1f, 1f, 1f, 0.42f), 0.012f);
            }
        }

        private void AddDebugCollisionLine(
            Vector3 start,
            Vector3 end,
            GatebreakerCollisionOverlayLineKind kind)
        {
            GetDebugOverlayStyle(kind, out Color color, out float width);
            AddDebugCollisionLine(new[] { start, end }, color, width);
        }

        private void AddDebugCollisionLine(Vector3[] positions, Color color, float width)
        {
            if (positions == null || positions.Length < 2)
            {
                return;
            }

            var lineObject = new GameObject("Debug Collision Line");
            lineObject.layer = GetSceneDebugLayer();
            Transform lineTransform = lineObject.transform;
            lineTransform.SetParent(_debugCollisionOverlayRoot, false);
            lineTransform.localPosition = Vector3.zero;
            lineTransform.localRotation = Quaternion.identity;
            lineTransform.localScale = Vector3.one;

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = positions.Length;
            line.SetPositions(positions);
            line.material = GetDebugOverlayMaterial();
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sortingOrder = DebugOverlaySortingOrder;
            _debugCollisionLines.Add(line);
        }

        private Vector3 ToDebugOverlayPosition(Vector2 position)
        {
            Vector3 visual = ToVisualPosition(position, DebugOverlayFallbackHeight);
            if (_usePrefabVisuals)
            {
                visual.z = DebugOverlayPrefabDepth;
            }
            else
            {
                visual.y = DebugOverlayFallbackHeight;
            }

            return visual;
        }

        private Material GetDebugOverlayMaterial()
        {
            if (_debugOverlayMaterial != null)
            {
                return _debugOverlayMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default") ??
                            Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Diffuse");
            _debugOverlayMaterial = new Material(shader);
            _debugOverlayMaterial.color = Color.white;
            return _debugOverlayMaterial;
        }

        private static void GetDebugOverlayStyle(
            GatebreakerCollisionOverlayLineKind kind,
            out Color color,
            out float width)
        {
            switch (kind)
            {
                case GatebreakerCollisionOverlayLineKind.GoalTrigger:
                    color = new Color(0.10f, 1.00f, 0.95f, 1f);
                    width = 0.026f;
                    break;
                case GatebreakerCollisionOverlayLineKind.GoalBand:
                    color = new Color(1.00f, 0.92f, 0.10f, 0.78f);
                    width = 0.018f;
                    break;
                case GatebreakerCollisionOverlayLineKind.PaddleContact:
                    color = new Color(1.00f, 0.10f, 1.00f, 1f);
                    width = 0.020f;
                    break;
                case GatebreakerCollisionOverlayLineKind.Wall:
                default:
                    color = new Color(1.00f, 0.08f, 0.04f, 1f);
                    width = 0.034f;
                    break;
            }
        }

        private void ResolveArenaRoot()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                GameObject[] roots = activeScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i] != null && roots[i].name == ArenaRootName)
                    {
                        _visualRoot = roots[i].transform;
                        ResolveObjPoolRoot();
                        _ownsVisualRoot = false;
                        return;
                    }
                }
            }

            GameObject arenaRootObject = new GameObject(ArenaRootName);
            _visualRoot = arenaRootObject.transform;
            ResolveObjPoolRoot();
            _ownsVisualRoot = true;
        }

        private void ResolveObjPoolRoot()
        {
            if (_visualRoot == null)
            {
                _poolRoot = null;
                return;
            }

            Transform existing = _visualRoot.Find(ObjPoolRootName);
            if (existing != null)
            {
                _poolRoot = existing;
                return;
            }

            var poolObject = new GameObject(ObjPoolRootName);
            _poolRoot = poolObject.transform;
            _poolRoot.SetParent(_visualRoot, false);
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
                CreateMaterial(GetPlayerColor(1)),
                CreateMaterial(GetPlayerColor(2)),
                CreateMaterial(GetPlayerColor(3)),
                CreateMaterial(GetPlayerColor(4)),
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
            IReadOnlyList<PlayerRuntimeState> players = _runtime?.Players;
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                PlayerRuntimeState player = players[i];
                if (player?.Zone == null || player.IsDisabled)
                {
                    continue;
                }

                CreateGuardZone(player);
            }
        }

        private void CreatePaddles()
        {
            IReadOnlyList<PlayerRuntimeState> players = _runtime?.Players;
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                PlayerRuntimeState player = players[i];
                if (player?.Paddle == null || player.IsDisabled)
                {
                    continue;
                }

                Transform paddle = EnsurePaddleView(player.PlayerId);
                paddle.localPosition = GetPaddlePosition(player);
                paddle.localRotation = GetPaddleRotation(player?.Paddle);
            }
        }

        private void CreateGuardZone(PlayerRuntimeState player)
        {
            int playerId = player.PlayerId;
            Vector3 position = ToVisualPosition(player.Zone.Center, 0.02f);
            Vector3 scale = Mathf.Abs(player.Zone.Normal.y) > 0.5f
                ? new Vector3(ArenaHalfWidth * 1.15f, 0.04f, GuardDepth)
                : new Vector3(GuardDepth, 0.04f, ArenaHalfHeight * 1.15f);
            GameObject guard = CreateBlock($"Player {playerId} Guard Zone", position, scale, GetPlayerMaterial(playerId));
            Renderer guardRenderer = guard.GetComponent<Renderer>();
            GatebreakerPlayerVisualColor.ApplyZoneColor(guardRenderer, GetPlayerColor(playerId), 0.32f);
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
                if (player == null || player.Paddle == null || player.IsDisabled)
                {
                    RemovePaddleView(player != null ? player.PlayerId : 0);
                    continue;
                }

                Transform paddle = EnsurePaddleView(player.PlayerId);
                paddle.localPosition = GetPaddlePosition(player);
                paddle.localRotation = GetPaddleRotation(player.Paddle);
                paddle.localScale = GetPaddleVisualScale(player.Paddle);

                if (!_usePrefabVisuals && _paddleRenderers.TryGetValue(player.PlayerId, out Renderer renderer))
                {
                    renderer.material.color = _paddleMaterial.color;
                }

                if (_guardRenderers.TryGetValue(player.PlayerId, out Renderer guard))
                {
                    Color guardColor = player.Zone != null && player.Zone.IsDanger
                        ? _dangerMaterial.color
                        : player.IsDisabled ? Color.gray : GetPlayerColor(player.PlayerId);
                    GatebreakerPlayerVisualColor.ApplyZoneColor(guard, guardColor, 0.32f);
                }
            }

            RebuildDebugCollisionOverlay();
        }

        private Transform EnsurePaddleView(int playerId)
        {
            if (_paddleViews.TryGetValue(playerId, out Transform paddle))
            {
                return paddle;
            }

            GameObject paddleObject = _usePrefabVisuals && _visualAssets?.Paddle?.Prefab != null
                ? Instantiate(_visualAssets.Paddle.Prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            paddleObject.name = $"Player {playerId} Paddle";
            paddleObject.transform.SetParent(_visualRoot, false);
            paddleObject.transform.localScale = Vector3.one;
            if (!_usePrefabVisuals)
            {
                SetMaterial(paddleObject, _paddleMaterial);
                _paddleRenderers[playerId] = paddleObject.GetComponent<Renderer>();
            }
            else
            {
                GatebreakerPlayerVisualColor.ApplyPaddleColor(paddleObject, GetPlayerColor(playerId));
            }

            _paddleViews[playerId] = paddleObject.transform;
            return paddleObject.transform;
        }

        private void RemovePaddleView(int playerId)
        {
            if (playerId <= 0)
            {
                return;
            }

            if (_paddleViews.TryGetValue(playerId, out Transform paddle) && paddle != null)
            {
                Destroy(paddle.gameObject);
            }

            _paddleViews.Remove(playerId);
            _paddleRenderers.Remove(playerId);
            if (_guardRenderers.TryGetValue(playerId, out Renderer guard) && guard != null)
            {
                Destroy(guard.gameObject);
            }

            _guardRenderers.Remove(playerId);
        }

        private Vector3 GetPaddlePosition(PlayerRuntimeState player)
        {
            if (player?.Paddle != null)
            {
                return ToVisualPosition(player.Paddle.Position, 0.35f);
            }

            switch (player != null ? player.PlayerId : 1)
            {
                case 1:
                    return ToVisualPosition(new Vector2(0f, -ArenaHalfHeight + 0.55f), 0.35f);
                case 2:
                    return ToVisualPosition(new Vector2(0f, ArenaHalfHeight - 0.55f), 0.35f);
                case 3:
                    return ToVisualPosition(new Vector2(ArenaHalfWidth - 0.55f, 0f), 0.35f);
                default:
                    return ToVisualPosition(new Vector2(-ArenaHalfWidth + 0.55f, 0f), 0.35f);
            }
        }

        private Quaternion GetPaddleRotation(PaddleRuntimeState paddle)
        {
            if (paddle == null)
            {
                return Quaternion.identity;
            }

            if (_usePrefabVisuals)
            {
                Vector3 visualTangent = GetVisualDirection(paddle.Tangent);
                float angle = Mathf.Atan2(visualTangent.y, visualTangent.x) * Mathf.Rad2Deg;
                return Quaternion.Euler(0f, 0f, angle);
            }

            return Quaternion.identity;
        }

        private Vector3 GetPaddleVisualScale(PaddleRuntimeState paddle)
        {
            if (_usePrefabVisuals)
            {
                float tangentScale = GetPrefabAxisScale(paddle.Tangent);
                float normalScale = GetPrefabAxisScale(paddle.Normal);
                float visualNormalSize = _runtime?.Arena != null && _runtime.Arena.HasCustomBoundary
                    ? Mathf.Max(paddle.Thickness, Scene3v3PaddlePrefabNormalScale)
                    : paddle.Thickness;
                float prefabLength = Mathf.Max(0.001f, _paddlePrefabLength);
                return new Vector3(
                    paddle.Length * tangentScale / prefabLength,
                    visualNormalSize * normalScale,
                    1f);
            }

            bool horizontal = Mathf.Abs(paddle.Normal.y) > 0.5f;
            return horizontal
                ? new Vector3(paddle.Length, 0.35f, paddle.Thickness)
                : new Vector3(paddle.Thickness, 0.35f, paddle.Length);
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
                Transform ballView = EnsureBallView(ball);
                ballView.localPosition = ToVisualPosition(ball.Position, 0.35f);
                ballView.localScale = GetCompensatedVisualScale(0.45f);
            }

            RemoveStaleBallViews();
        }

        private Transform EnsureBallView(BallRuntimeState ball)
        {
            int ballId = ball != null ? ball.BallId : 0;
            if (_ballViews.TryGetValue(ballId, out Transform ballView))
            {
                return ballView;
            }

            int ownerPlayerId = ball != null ? ball.OwnerPlayerId : 1;
            GameObject ballObject = AcquireBallViewObject(ownerPlayerId);
            ballObject.name = $"Ball {ballId}";
            ballObject.transform.SetParent(_visualRoot, false);
            ballObject.SetActive(true);
            ballObject.transform.localScale = GetCompensatedVisualScale(0.45f);

            _ballViewSlots[ballId] = ownerPlayerId;
            _ballViews[ballId] = ballObject.transform;
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
                    ReleaseBallViewObject(ballId, ballView.gameObject);
                }

                _ballViews.Remove(ballId);
                _ballViewSlots.Remove(ballId);
            }
        }

        private GameObject AcquireBallViewObject(int ownerPlayerId)
        {
            int safePlayerId = Mathf.Clamp(ownerPlayerId, 1, 4);
            Stack<GameObject> pool = GetBallViewPool(safePlayerId);
            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            GatebreakerLoadedPrefab ballPrefab = _visualAssets?.GetBallForPlayerId(safePlayerId);
            return _usePrefabVisuals && ballPrefab?.Prefab != null
                ? Instantiate(ballPrefab.Prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        }

        private void ReleaseBallViewObject(int ballId, GameObject ballObject)
        {
            if (ballObject == null)
            {
                return;
            }

            ballObject.SetActive(false);
            ballObject.transform.SetParent(_poolRoot != null ? _poolRoot : _visualRoot, false);
            int ownerPlayerId = _ballViewSlots.TryGetValue(ballId, out int playerId) ? playerId : 1;
            GetBallViewPool(ownerPlayerId).Push(ballObject);
        }

        private Stack<GameObject> GetBallViewPool(int ownerPlayerId)
        {
            int safePlayerId = Mathf.Clamp(ownerPlayerId, 1, 4);
            if (!_ballViewPools.TryGetValue(safePlayerId, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                _ballViewPools[safePlayerId] = pool;
            }

            return pool;
        }

        private void DestroyBallViewCache()
        {
            foreach (Transform ballView in _ballViews.Values)
            {
                if (ballView != null)
                {
                    Destroy(ballView.gameObject);
                }
            }

            _ballViews.Clear();
            _ballViewSlots.Clear();
            foreach (Stack<GameObject> pool in _ballViewPools.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject pooled = pool.Pop();
                    if (pooled != null)
                    {
                        Destroy(pooled);
                    }
                }
            }

            _ballViewPools.Clear();
        }

        private float ReadMoveAxis()
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

            moveAxis += _guiMoveAxis;
            return Mathf.Clamp(moveAxis, -1f, 1f);
        }

        private Vector2 BuildServeAimDirection(float moveAxis)
        {
            Vector2 viewUp = GetLocalViewUp();
            if (Mathf.Abs(moveAxis) < 0.01f)
            {
                return viewUp;
            }

            Vector2 viewRight = GetLocalViewRight();
            return (viewUp + viewRight * Mathf.Sign(moveAxis)).normalized;
        }

        private void RequestGuiServe()
        {
            _guiServePressed = true;
        }

        private void RestartLocalPrototype()
        {
            if (IsLanPlaying())
            {
                return;
            }

            ResetPrototypeMatchForEntryUi();
        }

        private void ResetPrototypeMatchForEntryUi()
        {
            _runtime?.StartLocalPrototype();
            _runtime?.SetLocalPlayer(_localPlayerId);
            _lastServeBlockReason = ServeBlockReason.None;
            _guiServePressed = false;
            _guiMoveAxis = 0f;
            RebuildDebugCollisionOverlay();
            RefreshBoundHud();
        }

        private void SetGuiMoveAxis(float moveAxis)
        {
            _guiMoveAxis = Mathf.Clamp(moveAxis, -1f, 1f);
        }

        private GatebreakerArenaSceneUiCallbacks BuildSceneUiCallbacks()
        {
            return new GatebreakerArenaSceneUiCallbacks
            {
                ServeRequested = RequestGuiServe,
                LocalBattleRequested = StartLocalBattleCountdown,
                OnlineBattleRequested = ShowOnlineBattleMenu,
                CreateLanHostRequested = CreateLanHost,
                StartLanDiscoveryRequested = StartLanDiscovery,
                JoinLanRoomRequested = JoinLanRoom,
                ToggleLanReadyRequested = ToggleLanReady,
                StartLanLoadingRequested = StartLanLoading,
                LeaveLanRoomRequested = LeaveLanRoom,
                AcknowledgeLanStartRequested = AcknowledgeLanStart,
                LanPlayerNameChanged = SetLanPlayerName,
                LanRoomCodeChanged = SetLanRoomCode,
                MoveAxisChanged = SetGuiMoveAxis,
                HitOffsetInfluenceChanged = value => _runtime?.BounceTuning?.SetHitOffsetInfluenceValue(value),
                PaddleVelocityInfluenceChanged = value => _runtime?.BounceTuning?.SetPaddleVelocityInfluenceValue(value),
                MinimumOutwardShareChanged = value => _runtime?.BounceTuning?.SetMinimumOutwardShareValue(value),
                RestartMatchRequested = RequestResultRestart,
                ResultBackRequested = RequestResultBack,
                InitialLanPlayerName = _lanPlayerName,
                InitialLanRoomCode = _lanRoomCodeInput,
            };
        }

        private static IGatebreakerArenaSceneUiBinding ResolveSceneUiBinding(IServiceContainer services)
        {
            return services?.Get<IGatebreakerArenaSceneUiBinding>() ??
                   GatebreakerArenaSceneUiBindingRegistry.Current;
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

        private string GetRoomLanAddress(RoomSnapshot snapshot)
        {
            if (snapshot != null && snapshot.IsHost)
            {
                string localAddress = GetLocalLanAddress();
                int tcpPort = _lanTransport?.TcpListenEndpoint.Port ?? 0;
                return tcpPort > 0 && !string.IsNullOrWhiteSpace(localAddress)
                    ? localAddress + ":" + tcpPort.ToString()
                    : localAddress;
            }

            string roomCode = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.RoomCode)
                ? snapshot.RoomCode
                : _lanRoomCodeInput;
            DiscoveredRoom room = FindDiscoveredRoom(roomCode);
            if (room == null && _lanRoomService != null && _lanRoomService.DiscoveredRooms.Count == 1)
            {
                room = _lanRoomService.DiscoveredRooms[0];
            }

            LanEndpoint endpoint = ExtractLanEndpoint(room?.ReliableEndpoint ?? room?.DiscoveryEndpoint);
            return endpoint.IsValid ? endpoint.ToString() : "-";
        }

        private DiscoveredRoom FindDiscoveredRoom(string roomCode)
        {
            if (_lanRoomService == null || string.IsNullOrWhiteSpace(roomCode))
            {
                return null;
            }

            IReadOnlyList<DiscoveredRoom> rooms = _lanRoomService.DiscoveredRooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                DiscoveredRoom room = rooms[i];
                if (string.Equals(room.Advertise.RoomCode, roomCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return room;
                }
            }

            return null;
        }

        private static LanEndpoint ExtractLanEndpoint(object endpoint)
        {
            return endpoint is LanEndpoint lanEndpoint ? lanEndpoint : default(LanEndpoint);
        }

        private static string ShortenPath(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 58)
            {
                return string.IsNullOrEmpty(value) ? "-" : value;
            }

            return "..." + value.Substring(value.Length - 55);
        }

        private static string JoinInts(int[] values)
        {
            if (values == null || values.Length <= 0)
            {
                return "-";
            }

            return string.Join(",", Array.ConvertAll(values, item => item.ToString()));
        }

        private string GetLocalLanAddress()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextLocalLanAddressRefreshTime)
            {
                return _cachedLocalLanAddress;
            }

            _cachedLocalLanAddress = ResolveLocalLanAddress();
            _nextLocalLanAddressRefreshTime = now + 2f;
            return _cachedLocalLanAddress;
        }

        private static string ResolveLocalLanAddress()
        {
            try
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress fallback = null;
                for (int i = 0; i < host.AddressList.Length; i++)
                {
                    IPAddress address = host.AddressList[i];
                    if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                    {
                        continue;
                    }

                    byte[] bytes = address.GetAddressBytes();
                    if (bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254)
                    {
                        fallback = fallback ?? address;
                        continue;
                    }

                    return address.ToString();
                }

                return fallback != null ? fallback.ToString() : "-";
            }
            catch (Exception)
            {
                return "-";
            }
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
            if (_lanRoomService == null)
            {
                return;
            }

            EnsureLanIdentity();
            ResetLocalLanSessionTransport();
            _lanTransport?.StartDiscovery();
            bool tcpStarted = _lanTransport != null && _lanTransport.StartTcpHost();
            int tcpPort = _lanTransport?.TcpListenEndpoint.Port ?? 0;
            if (!tcpStarted || tcpPort <= 0)
            {
                _lanRoomService.HandleHostTransportStartFailed("tcpPort=" + tcpPort.ToString());
                _startupUiState = StartupUiState.OnlineMenu;
                _lanEntryUiHiddenForPlaying = false;
                _sceneBindingService?.ShowOnlineMenu();
                RefreshBoundHud();
                return;
            }

            RoomSnapshot snapshot = _lanRoomService.CreateHost(_lanPlayerName, _lanClientInstanceId, tcpPort: tcpPort);
            _lanRoomCodeInput = snapshot.RoomCode;
            _startupUiState = StartupUiState.OnlineRoom;
            _lanEntryUiHiddenForPlaying = false;
            _sceneBindingService?.ShowLanRoomStatus();
            RefreshBoundHud();
        }

        private void StartLanDiscovery()
        {
            if (_lanRoomService == null)
            {
                return;
            }

            EnsureLanIdentity();
            ResetLocalLanSessionTransport();
            _lanTransport?.StartDiscovery();
            _lanRoomService.StartDiscovery(_lanClientInstanceId, _lanPlayerName);
            _startupUiState = StartupUiState.OnlineMenu;
            _lanEntryUiHiddenForPlaying = false;
            _sceneBindingService?.ShowOnlineMenu();
            RefreshBoundHud();
        }

        private void JoinLanRoom()
        {
            if (_lanRoomService == null || string.IsNullOrWhiteSpace(_lanRoomCodeInput))
            {
                _lanRoomService?.RecordUiAction("JoinClicked", "ignored;roomCode=" + (_lanRoomCodeInput ?? string.Empty));
                return;
            }

            _lanRoomService.RecordUiAction("JoinClicked", "roomCode=" + _lanRoomCodeInput);
            if (_lanRoomService.JoinDiscoveredRoom(_lanRoomCodeInput))
            {
                _startupUiState = StartupUiState.OnlineRoom;
                _lanEntryUiHiddenForPlaying = false;
                _sceneBindingService?.ShowLanRoomStatus();
                RefreshBoundHud();
            }
        }

        private void ToggleLanReady()
        {
            if (_lanRoomService == null)
            {
                return;
            }

            _lanRoomService.RecordUiAction("ReadyClicked", BuildLanUiSnapshotDetail(_lanRoomService.CurrentSnapshot));
            ToggleLanReady(_lanRoomService.CurrentSnapshot);
        }

        private void StartLanLoading()
        {
            _lanRoomService?.RecordUiAction("StartClicked", BuildLanUiSnapshotDetail(_lanRoomService.CurrentSnapshot));
            _lanRoomService?.StartLoading();
        }

        private void LeaveLanRoom()
        {
            _lanRoomService?.Leave("ui");
            ResetLocalLanSessionAfterLeave("ui");
            _startupUiState = StartupUiState.ModeSelect;
            _lanEntryUiHiddenForPlaying = false;
            _sceneBindingService?.ShowModeSelect();
            RefreshBoundHud();
        }

        private void RequestResultRestart()
        {
            RoomSnapshot snapshot = _lanRoomService?.CurrentSnapshot;
            if (!IsLanResultRoom(snapshot))
            {
                RestartLocalPrototype();
                return;
            }

            if (IsLanRoomTerminal(snapshot))
            {
                ReturnToModeSelectFromResult();
                return;
            }

            bool readyAfterReturn = snapshot.IsHost;
            if (!_lanRoomService.ReturnToLobbyFromResult(readyAfterReturn))
            {
                ReturnToModeSelectFromResult();
                return;
            }

            _startupUiState = StartupUiState.OnlineRoom;
            _lanEntryUiHiddenForPlaying = false;
            ResetPrototypeMatchForEntryUi();
            _sceneBindingService?.ShowLanRoomStatus();
            RefreshBoundHud();
        }

        private void RequestResultBack()
        {
            RoomSnapshot snapshot = _lanRoomService?.CurrentSnapshot;
            if (!IsLanResultRoom(snapshot) || IsLanRoomTerminal(snapshot))
            {
                ReturnToModeSelectFromResult();
                return;
            }

            _lanRoomService.Leave(snapshot.IsHost ? "resultBackHost" : "resultBack");
            ResetLocalLanSessionAfterLeave(snapshot.IsHost ? "resultBackHost" : "resultBack");
            ReturnToModeSelectFromResult();
        }

        private void ReturnToModeSelectFromResult()
        {
            ResetPrototypeMatchForEntryUi();
            _startupUiState = StartupUiState.ModeSelect;
            _lanEntryUiHiddenForPlaying = false;
            _sceneBindingService?.ShowModeSelect();
            RefreshBoundHud();
        }

        private void AcknowledgeLanStart()
        {
            _lanRoomService?.AcknowledgeStart();
        }

        private void SetLanPlayerName(string value)
        {
            _lanPlayerName = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        }

        private void SetLanRoomCode(string value)
        {
            _lanRoomCodeInput = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void StartLocalBattleCountdown()
        {
            RestartLocalPrototype();
            _startupUiState = StartupUiState.LocalCountdown;
            _localStartCountdownElapsed = 0f;
            _lastStartCountdownText = null;
            ShowStartCountdown(Mathf.CeilToInt(LocalStartCountdownSeconds).ToString());
        }

        private void ShowOnlineBattleMenu()
        {
            ResetTerminalLocalLanSessionForOnlineEntry();
            _startupUiState = StartupUiState.OnlineMenu;
            _sceneBindingService?.ShowOnlineMenu();
            RefreshBoundHud();
        }

        private void ResetLocalLanSessionAfterLeave(string reason)
        {
            ResetLocalLanSessionTransport();
            _lanRoomService?.ResetAfterLocalLeave(reason);
            _lanRoomCodeInput = string.Empty;
        }

        private void ResetTerminalLocalLanSessionForOnlineEntry()
        {
            RoomSnapshot snapshot = _lanRoomService?.CurrentSnapshot;
            if (snapshot == null ||
                (snapshot.State != LanRoomState.Left &&
                 snapshot.State != LanRoomState.Aborted))
            {
                return;
            }

            ResetLocalLanSessionAfterLeave("onlineEntry");
        }

        private void ResetLocalLanSessionTransport()
        {
            _lanTransport?.StopTcpHost();
            _lanTransport?.StopDiscovery();
        }

        private void ToggleLanReady(RoomSnapshot snapshot)
        {
            if (_lanRoomService == null || snapshot?.Players == null)
            {
                return;
            }

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

        private static string BuildLanUiSnapshotDetail(RoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "snapshot=null";
            }

            int active = 0;
            int human = 0;
            int ai = 0;
            RoomPlayerSnapshot[] players = snapshot.Players ?? Array.Empty<RoomPlayerSnapshot>();
            for (int i = 0; i < players.Length; i++)
            {
                RoomPlayerSnapshot player = players[i];
                if (player == null || !player.IsActive)
                {
                    continue;
                }

                active++;
                if (player.IsAi)
                {
                    ai++;
                }
                else
                {
                    human++;
                }
            }

            return "state=" + snapshot.State +
                   ";roomCode=" + (snapshot.RoomCode ?? string.Empty) +
                   ";canStart=" + snapshot.CanStart +
                   ";localSlot=" + snapshot.LocalSlotIndex +
                   ";active=" + active +
                   ";human=" + human +
                   ";ai=" + ai +
                   ";total=" + players.Length;
        }

        private static bool IsLanResultRoom(RoomSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.SessionId != 0UL &&
                   snapshot.State != LanRoomState.Idle &&
                   snapshot.State != LanRoomState.Discovering &&
                   snapshot.State != LanRoomState.Joining;
        }

        private static bool IsLanRoomTerminal(RoomSnapshot snapshot)
        {
            return snapshot != null &&
                   (snapshot.State == LanRoomState.Left ||
                    (snapshot.State == LanRoomState.Aborted && snapshot.AbortReason == MatchAbortReason.HostLeft));
        }

        private void RefreshBoundHud()
        {
            if (_sceneBindingService == null || _hudPresenter == null)
            {
                return;
            }

            GatebreakerHudSnapshot snapshot = _hudPresenter.BuildSnapshot(_localPlayerId);
            _sceneBindingService.UpdateHud(snapshot, _lastServeBlockReason);
            _sceneBindingService.UpdateResult(snapshot);
            bool canShowTuning = _startupUiState == StartupUiState.LocalPlaying || IsLanPlaying();
            _sceneBindingService.UpdateBounceTuning(
                canShowTuning ? _runtime?.BounceTuning : null,
                snapshot?.Phase ?? MatchPhase.Waiting);
            if (_lanRoomService != null)
            {
                RoomSnapshot roomSnapshot = _lanRoomService.CurrentSnapshot;
                _sceneBindingService.UpdateLanRoom(
                    roomSnapshot,
                    GetLocalLanAddress(),
                    GetRoomLanAddress(roomSnapshot));
            }
        }

        private Color GetPlayerColor(int playerId)
        {
            return GatebreakerPlayerVisualColor.ToUnityColor(_runtime.ModeCatalog.GetPlayerColor(playerId));
        }

        private Vector3 ToVisualPosition(Vector2 position, float height)
        {
            return _usePrefabVisuals
                ? new Vector3(position.x * GetPrefabVisualXScale(), position.y * GetPrefabVisualYScale(), 0f)
                : new Vector3(position.x, height, position.y);
        }

        private float GetPrefabVisualXScale()
        {
            return Scene3v3HalfWidth / Mathf.Max(0.001f, ArenaHalfWidth);
        }

        private float GetPrefabVisualYScale()
        {
            return Scene3v3HalfHeight / Mathf.Max(0.001f, ArenaHalfHeight);
        }

        private float GetPrefabVisualUniformScale()
        {
            return Mathf.Min(GetPrefabVisualXScale(), GetPrefabVisualYScale());
        }

        private float GetPrefabAxisScale(Vector2 axis)
        {
            if (axis.sqrMagnitude <= 0.0001f)
            {
                return GetPrefabVisualUniformScale();
            }

            Vector2 normalized = axis.normalized;
            return new Vector2(
                normalized.x * GetPrefabVisualXScale(),
                normalized.y * GetPrefabVisualYScale()).magnitude;
        }

        private Material GetPlayerMaterial(int playerId)
        {
            if (_playerMaterials == null || _playerMaterials.Length == 0)
            {
                return _localMaterial;
            }

            int index = Mathf.Clamp(playerId, 1, _playerMaterials.Length) - 1;
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

            _prototypeCamera.rect = _usePrefabVisuals
                ? new Rect(0f, 0f, 1f, 1f)
                : CalculateSquareCameraViewport();
            float aspect = Mathf.Max(0.1f, _prototypeCamera.aspect);

            Vector2 viewUp2D = GetLocalViewUp();
            Vector2 viewRight2D = GetLocalViewRight();
            Vector3 screenUp = GetVisualDirection(viewUp2D);
            _prototypeCamera.transform.position = _usePrefabVisuals
                ? new Vector3(0f, 0f, -CameraHeight)
                : new Vector3(0f, CameraHeight, 0f);
            _prototypeCamera.transform.rotation = _usePrefabVisuals
                ? Quaternion.LookRotation(Vector3.forward, screenUp)
                : Quaternion.LookRotation(Vector3.down, screenUp);
            _prototypeCamera.orthographic = true;
            CalculateViewExtents(viewUp2D, viewRight2D, out float viewHalfHeight, out float viewHalfWidth);
            if (_visualRoot != null)
            {
                _visualRoot.localScale = GetPrototypeVisualScale(viewHalfHeight, viewHalfWidth, aspect);
            }

            _prototypeCamera.orthographicSize = CalculateOrthographicSize(viewHalfHeight, viewHalfWidth, aspect);
            _prototypeCamera.nearClipPlane = 0.1f;
            _prototypeCamera.farClipPlane = CameraHeight + 10f;
            _prototypeCamera.cullingMask &= ~(1 << GetSceneDebugLayer());
            _prototypeCamera.clearFlags = CameraClearFlags.SolidColor;
            _prototypeCamera.backgroundColor = new Color(0.03f, 0.04f, 0.05f);
        }

        private float CalculateOrthographicSize(float viewHalfHeight, float viewHalfWidth, float aspect)
        {
            float safeAspect = Mathf.Max(0.1f, aspect);
            if (_usePrefabVisuals)
            {
                return Mathf.Max(0.001f, viewHalfWidth / safeAspect);
            }

            return Mathf.Max(
                viewHalfHeight + CameraMargin,
                (viewHalfWidth + CameraMargin) / safeAspect);
        }

        private float CalculatePrefabViewportScale(float viewHalfHeight, float viewHalfWidth, float aspect)
        {
            float safeHalfHeight = Mathf.Max(0.001f, viewHalfHeight);
            float horizontalFillOrthographicSize = Mathf.Max(0.001f, viewHalfWidth / Mathf.Max(0.1f, aspect));
            return Mathf.Min(1f, horizontalFillOrthographicSize / safeHalfHeight);
        }

        private static int GetSceneDebugLayer()
        {
            int layer = LayerMask.NameToLayer(SceneDebugLayerName);
            return layer >= 0 ? layer : SceneDebugLayerFallback;
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
            if (_usePrefabVisuals)
            {
                return Vector3.one;
            }

            float depthScale = ArenaHalfHeight > 0.001f ? ArenaHalfWidth / ArenaHalfHeight : 1f;
            return new Vector3(1f, 1f, depthScale);
        }

        private Vector3 GetPrototypeVisualScale(float viewHalfHeight, float viewHalfWidth, float aspect)
        {
            if (!_usePrefabVisuals)
            {
                return GetPrototypeVisualScale();
            }

            float prefabScale = CalculatePrefabViewportScale(viewHalfHeight, viewHalfWidth, aspect);
            return new Vector3(prefabScale, prefabScale, prefabScale);
        }

        private Vector3 GetCompensatedVisualScale(float worldSize)
        {
            if (_usePrefabVisuals)
            {
                float prefabScale = GetPrefabVisualUniformScale();
                return new Vector3(worldSize * prefabScale, worldSize * prefabScale, 1f);
            }

            Vector3 scale = GetPrototypeVisualScale();
            return new Vector3(
                worldSize / Mathf.Max(0.001f, scale.x),
                worldSize / Mathf.Max(0.001f, scale.y),
                worldSize / Mathf.Max(0.001f, scale.z));
        }

        private Vector3 GetVisualDirection(Vector2 direction)
        {
            if (_usePrefabVisuals)
            {
                Vector3 xyDirection = new Vector3(direction.x * GetPrefabVisualXScale(), direction.y * GetPrefabVisualYScale(), 0f);
                return xyDirection.sqrMagnitude > 0.0001f ? xyDirection.normalized : Vector3.up;
            }

            Vector3 scale = GetPrototypeVisualScale();
            Vector3 visualDirection = new Vector3(direction.x * scale.x, 0f, direction.y * scale.z);
            return visualDirection.sqrMagnitude > 0.0001f ? visualDirection.normalized : Vector3.forward;
        }

        private void CalculateViewExtents(Vector2 viewUp, Vector2 viewRight, out float halfHeight, out float halfWidth)
        {
            halfHeight = 0f;
            halfWidth = 0f;
            if (_usePrefabVisuals)
            {
                if (_hasSceneVisualBounds)
                {
                    CalculateBoundsViewExtents(_sceneVisualBounds, viewUp, viewRight, out halfHeight, out halfWidth);
                    halfHeight += SceneVisualBoundsPadding;
                    halfWidth += SceneVisualBoundsPadding;
                    return;
                }

                Vector2[] prefabCorners =
                {
                    new Vector2(-Scene3v3HalfWidth, -Scene3v3HalfHeight),
                    new Vector2(-Scene3v3HalfWidth, Scene3v3HalfHeight),
                    new Vector2(Scene3v3HalfWidth, -Scene3v3HalfHeight),
                    new Vector2(Scene3v3HalfWidth, Scene3v3HalfHeight),
                };
                Vector2 prefabUp = viewUp.sqrMagnitude > 0.0001f ? viewUp.normalized : Vector2.up;
                Vector2 prefabRight = viewRight.sqrMagnitude > 0.0001f ? viewRight.normalized : Vector2.right;
                for (int i = 0; i < prefabCorners.Length; i++)
                {
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector2.Dot(prefabCorners[i], prefabUp)));
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector2.Dot(prefabCorners[i], prefabRight)));
                }

                return;
            }

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

        private static void CalculateBoundsViewExtents(
            Bounds bounds,
            Vector2 viewUp,
            Vector2 viewRight,
            out float halfHeight,
            out float halfWidth)
        {
            halfHeight = 0f;
            halfWidth = 0f;
            Vector2 prefabUp = viewUp.sqrMagnitude > 0.0001f ? viewUp.normalized : Vector2.up;
            Vector2 prefabRight = viewRight.sqrMagnitude > 0.0001f ? viewRight.normalized : Vector2.right;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            AccumulateBoundsViewCorner(new Vector2(min.x, min.y), prefabUp, prefabRight, ref halfHeight, ref halfWidth);
            AccumulateBoundsViewCorner(new Vector2(min.x, max.y), prefabUp, prefabRight, ref halfHeight, ref halfWidth);
            AccumulateBoundsViewCorner(new Vector2(max.x, min.y), prefabUp, prefabRight, ref halfHeight, ref halfWidth);
            AccumulateBoundsViewCorner(new Vector2(max.x, max.y), prefabUp, prefabRight, ref halfHeight, ref halfWidth);
        }

        private static void AccumulateBoundsViewCorner(
            Vector2 corner,
            Vector2 prefabUp,
            Vector2 prefabRight,
            ref float halfHeight,
            ref float halfWidth)
        {
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector2.Dot(corner, prefabUp)));
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector2.Dot(corner, prefabRight)));
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
