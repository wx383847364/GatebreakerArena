using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
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

        private static readonly Color[] PlayerPalette =
        {
            new Color(0.20f, 0.68f, 1.00f),
            new Color(1.00f, 0.55f, 0.18f),
            new Color(0.45f, 0.88f, 0.38f),
            new Color(0.84f, 0.42f, 1.00f),
        };

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
        private ulong _lanClientInstanceId;
        private string _lanPlayerName = "Player";
        private string _lanRoomCodeInput = string.Empty;
        private string _cachedLocalLanAddress = "-";
        private float _lanInputAccumulator;
        private float _nextLocalLanAddressRefreshTime;
        private bool _usePrefabVisuals;
        private bool _ownsVisualRoot;
        private bool _hasSceneVisualBounds;

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

            if (Input.GetKeyDown(KeyCode.R))
            {
                _runtime.StartLocalPrototype();
                _runtime.SetLocalPlayer(_localPlayerId);
                _lastServeBlockReason = ServeBlockReason.None;
                _guiServePressed = false;
                RebuildDebugCollisionOverlay();
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
            RefreshBoundHud();
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
            UpdateSceneVisualBounds();
        }

        private void UpdateSceneVisualBounds()
        {
            _hasSceneVisualBounds = false;
            if (_sceneInstance == null)
            {
                return;
            }

            Renderer[] renderers = _sceneInstance.GetComponentsInChildren<Renderer>(false);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Transform sceneTransform = _sceneInstance.transform;
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
                AccumulateSceneBoundsCorner(sceneTransform, center, extents, -1f, -1f, -1f, ref min, ref max);
                AccumulateSceneBoundsCorner(sceneTransform, center, extents, -1f, -1f, 1f, ref min, ref max);
                AccumulateSceneBoundsCorner(sceneTransform, center, extents, -1f, 1f, -1f, ref min, ref max);
                AccumulateSceneBoundsCorner(sceneTransform, center, extents, -1f, 1f, 1f, ref min, ref max);
                AccumulateSceneBoundsCorner(sceneTransform, center, extents, 1f, -1f, -1f, ref min, ref max);
                AccumulateSceneBoundsCorner(sceneTransform, center, extents, 1f, -1f, 1f, ref min, ref max);
                AccumulateSceneBoundsCorner(sceneTransform, center, extents, 1f, 1f, -1f, ref min, ref max);
                AccumulateSceneBoundsCorner(sceneTransform, center, extents, 1f, 1f, 1f, ref min, ref max);
                hasBounds = true;
            }

            if (!hasBounds)
            {
                return;
            }

            _sceneVisualBounds = new Bounds((min + max) * 0.5f, max - min);
            _hasSceneVisualBounds = true;
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

        private void RebuildDebugCollisionOverlay()
        {
            ClearDebugCollisionOverlay();
            if (_visualRoot == null || _runtime?.Arena == null)
            {
                return;
            }

            EnsureDebugCollisionOverlayRoot();
            IReadOnlyList<GatebreakerCollisionOverlayLine> lines =
                GatebreakerCollisionOverlayGeometry.BuildLines(_runtime.Arena, _runtime.Players.Count);
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
                CreateMaterial(PlayerPalette[0]),
                CreateMaterial(PlayerPalette[1]),
                CreateMaterial(PlayerPalette[2]),
                CreateMaterial(PlayerPalette[3]),
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
                PlayerRuntimeState player = _runtime?.FindPlayer(playerId);
                if (player?.Paddle == null || player.IsDisabled)
                {
                    continue;
                }

                Transform paddle = EnsurePaddleView(playerId);
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
                return new Vector3(paddle.Length * tangentScale, visualNormalSize * normalScale, 1f);
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

            int playerSlot = ResolveBallPlayerSlot(ball);
            GameObject ballObject = AcquireBallViewObject(playerSlot);
            ballObject.name = $"Ball {ballId}";
            ballObject.transform.SetParent(_visualRoot, false);
            ballObject.SetActive(true);
            ballObject.transform.localScale = GetCompensatedVisualScale(0.45f);

            _ballViewSlots[ballId] = playerSlot;
            _ballViews[ballId] = ballObject.transform;
            return ballObject.transform;
        }

        private int ResolveBallPlayerSlot(BallRuntimeState ball)
        {
            if (ball == null || _runtime == null)
            {
                return 1;
            }

            int slot = 0;
            for (int i = 0; i < _runtime.Players.Count; i++)
            {
                PlayerRuntimeState player = _runtime.Players[i];
                if (player == null || player.IsDisabled || player.Paddle == null)
                {
                    continue;
                }

                slot += 1;
                if (player.PlayerId == ball.OwnerPlayerId)
                {
                    return Mathf.Clamp(slot, 1, 3);
                }
            }

            return 1;
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

        private GameObject AcquireBallViewObject(int playerSlot)
        {
            int safeSlot = Mathf.Clamp(playerSlot, 1, 3);
            Stack<GameObject> pool = GetBallViewPool(safeSlot);
            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            GatebreakerLoadedPrefab ballPrefab = _visualAssets?.GetBallForPlayerSlot(safeSlot);
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
            int playerSlot = _ballViewSlots.TryGetValue(ballId, out int slot) ? slot : 1;
            GetBallViewPool(playerSlot).Push(ballObject);
        }

        private Stack<GameObject> GetBallViewPool(int playerSlot)
        {
            int safeSlot = Mathf.Clamp(playerSlot, 1, 3);
            if (!_ballViewPools.TryGetValue(safeSlot, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                _ballViewPools[safeSlot] = pool;
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

        private GatebreakerArenaSceneUiCallbacks BuildSceneUiCallbacks()
        {
            return new GatebreakerArenaSceneUiCallbacks
            {
                ServeRequested = RequestGuiServe,
                CreateLanHostRequested = CreateLanHost,
                StartLanDiscoveryRequested = StartLanDiscovery,
                JoinLanRoomRequested = JoinLanRoom,
                ToggleLanReadyRequested = ToggleLanReady,
                StartLanLoadingRequested = StartLanLoading,
                LeaveLanRoomRequested = LeaveLanRoom,
                AcknowledgeLanStartRequested = AcknowledgeLanStart,
                LanPlayerNameChanged = SetLanPlayerName,
                LanRoomCodeChanged = SetLanRoomCode,
                HitOffsetInfluenceChanged = value => _runtime?.BounceTuning?.SetHitOffsetInfluenceValue(value),
                PaddleVelocityInfluenceChanged = value => _runtime?.BounceTuning?.SetPaddleVelocityInfluenceValue(value),
                MinimumOutwardShareChanged = value => _runtime?.BounceTuning?.SetMinimumOutwardShareValue(value),
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
                return GetLocalLanAddress();
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
            return !string.IsNullOrEmpty(endpoint.Address) ? endpoint.Address : "-";
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
            _lanTransport?.StartDiscovery();
            _lanTransport?.StartTcpHost();
            int tcpPort = _lanTransport?.TcpListenEndpoint.Port ?? 0;
            RoomSnapshot snapshot = _lanRoomService.CreateHost(_lanPlayerName, _lanClientInstanceId, tcpPort: tcpPort);
            _lanRoomCodeInput = snapshot.RoomCode;
        }

        private void StartLanDiscovery()
        {
            if (_lanRoomService == null)
            {
                return;
            }

            EnsureLanIdentity();
            _lanTransport?.StartDiscovery();
            _lanRoomService.StartDiscovery(_lanClientInstanceId, _lanPlayerName);
        }

        private void JoinLanRoom()
        {
            if (_lanRoomService == null || string.IsNullOrWhiteSpace(_lanRoomCodeInput))
            {
                return;
            }

            _lanRoomService.JoinDiscoveredRoom(_lanRoomCodeInput);
        }

        private void ToggleLanReady()
        {
            if (_lanRoomService == null)
            {
                return;
            }

            ToggleLanReady(_lanRoomService.CurrentSnapshot);
        }

        private void StartLanLoading()
        {
            _lanRoomService?.StartLoading();
        }

        private void LeaveLanRoom()
        {
            _lanRoomService?.Leave("ui");
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

        private void RefreshBoundHud()
        {
            if (_sceneBindingService == null || _hudPresenter == null)
            {
                return;
            }

            GatebreakerHudSnapshot snapshot = _hudPresenter.BuildSnapshot(_localPlayerId);
            _sceneBindingService.UpdateHud(snapshot, _lastServeBlockReason);
            _sceneBindingService.UpdateResult(snapshot);
            _sceneBindingService.UpdateBounceTuning(_runtime?.BounceTuning, snapshot?.Phase ?? MatchPhase.Waiting);
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
            int index = Mathf.Abs(playerId - 1) % PlayerPalette.Length;
            return PlayerPalette[index];
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
