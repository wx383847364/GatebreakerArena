using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.BrickDuel;
using App.HotUpdate.GatebreakerArena.Chip;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Paddle;
using App.HotUpdate.GatebreakerArena.Phase;
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
        private const float MainCameraOrthographicSize = 5f;
        private const float NarrowPortraitAspect = 9f / 16f;
        private const float NarrowPortraitOrthographicSize = 5.4f;
        private const float BrickDuelMinimumVisibleHalfWidth =
            NarrowPortraitAspect * NarrowPortraitOrthographicSize;
        private const string ArenaRootName = "ArenaRoot";
        private const string ObjPoolRootName = "ObjPool";
        private const string DebugCollisionOverlayName = "DebugCollisionOverlay";
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
        private const float ScoredBallVisualLifetime = 0.09f;
        private const float FallbackBallVisualWorldSize = 0.45f;

        private readonly Dictionary<int, Transform> _ballViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, int> _ballViewSlots = new Dictionary<int, int>();
        private readonly Dictionary<int, VisualPoseState> _ballVisualPoses = new Dictionary<int, VisualPoseState>();
        private readonly Dictionary<int, ScoredBallVisualState> _scoredBallVisuals = new Dictionary<int, ScoredBallVisualState>();
        private readonly Dictionary<int, Transform> _paddleViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, VisualPoseState> _paddleVisualPoses = new Dictionary<int, VisualPoseState>();
        private readonly Dictionary<int, Renderer> _paddleRenderers = new Dictionary<int, Renderer>();
        private readonly Dictionary<int, Renderer> _guardRenderers = new Dictionary<int, Renderer>();
        private readonly List<LineRenderer> _debugCollisionLines = new List<LineRenderer>();
        private readonly HashSet<int> _liveBallIds = new HashSet<int>();
        private readonly Dictionary<int, Stack<GameObject>> _ballViewPools = new Dictionary<int, Stack<GameObject>>();

        private GatebreakerMatchRuntime _runtime;
        private GatebreakerInputService _inputService;
        private GatebreakerArenaHudPresenter _hudPresenter;
        private GatebreakerArenaSceneBindingService _sceneBindingService;
        private LeaderboardPresenter _leaderboardPresenter;
        private HeroDeckSelectionPresenter _loadoutPresenter;
        private PhaseTechSelectionPresenter _phaseLoadoutPresenter;
        private PhaseProfileService _phaseProfileService;
        private PhaseSettlementCoordinator _phaseSettlementCoordinator;
        private readonly int[] _loadoutChipIndices = new int[5];
        private V1MatchLoadout _selectedLocalLoadout;
        private PhaseMatchLoadout _selectedPhaseLoadout;
        private bool _loadoutForLan;
        private bool _loadoutForBrickDuel;
        private LanRoomService _lanRoomService;
        private LanRoomService _subscribedLanRoomService;
        private LanDiagnosticsService _lanDiagnosticsService;
        private GatebreakerNetworkMatchController _networkMatchController;
        private ILanTransport _lanTransport;
        private GatebreakerVisualAssetService _visualAssetService;
        private BrickDuelSessionController _brickDuelSession;
        private GatebreakerModeCatalog _modeCatalog;
        private BrickDuelRuleDefinition _brickDuelRule;
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
        private readonly GatebreakerArenaInputPresenter _movementPresenter = new GatebreakerArenaInputPresenter();
        private readonly List<Touch> _screenTouches = new List<Touch>();
        private bool _pointerOwnsMovement = true;
        private bool _inputFocused = true;
        private bool _inputPaused;
        private float _screenMoveAxis;
        private bool _movementWasAllowed;
        private short _lastSubmittedMoveAxisQ;
#if UNITY_EDITOR
        private bool _hadRealTouches;
#endif
        private float _guiMoveAxis;
        private ulong _lanClientInstanceId;
        private string _lanPlayerName = "Player";
        private int _lanSelectedPlayerCount = 2;
        private string _lanRoomCodeInput = string.Empty;
        private string _cachedLocalLanAddress = "-";
        private string _lastLanDiagnosticsSummary = string.Empty;
        private string _lastLanDiagnosticsExportPath = string.Empty;
        private float _lanInputAccumulator;
        private ushort _pendingLanButtons;
        private float _nextLocalLanAddressRefreshTime;
        private Vector2 _lanDiagnosticsScroll;
        private bool _showLanDiagnostics;
        private bool _usePrefabVisuals;
        private bool _ownsVisualRoot;
        private bool _hasSceneVisualBounds;
        private bool _missingInputServiceWarningLogged;
        private bool _lanEntryUiHiddenForPlaying;
        private ulong _observedLanSessionId;
        private uint _observedLanRoundId;
        private LanRoomState _observedLanState = LanRoomState.Idle;
        private int _loadedScenePlayerCount;
        private bool _sceneReloadInProgress;
        private float _paddlePrefabLength = 1f;
        private StartupUiState _startupUiState = StartupUiState.ModeSelect;
        private float _localStartCountdownElapsed;
        private string _lastStartCountdownText;
        private int _lastVisualFrameIndex = int.MinValue;
        private bool _visualFrameAdvanced = true;
        private float _visualInterpolationElapsed;
        private float _brickDuelMoveAxis;
        private bool _brickDuelStarting;
        private int _lastBrickDuelUiFrame = int.MinValue;
        private string _brickDuelMatchId;
        private bool _brickDuelSettlementRequested;
        private bool _brickDuelSettlementInFlight;
        private string _brickDuelSettlementOperationMatchId;
        private float _nextBrickDuelSettlementRetryTime;
        private int _brickDuelMatchGeneration;
        private bool _brickDuelAbilityPressed;
        private bool _phaseLoadoutOperationInProgress;
        private int _phaseLoadoutOperationVersion;
        private int _pendingUnlockPhaseIndex = -1;
        private string _pendingUnlockTechId = string.Empty;
        private string _pendingUnlockHeroId = string.Empty;

        private enum StartupUiState
        {
            ModeSelect,
            LocalCountdown,
            LocalPlaying,
            OnlineMenu,
            OnlineRoom,
        }

        private sealed class VisualPoseState
        {
            public VisualPoseState(Vector3 position)
            {
                PreviousPosition = position;
                CurrentPosition = position;
            }

            public Vector3 PreviousPosition { get; set; }
            public Vector3 CurrentPosition { get; set; }
        }

        private sealed class ScoredBallVisualState
        {
            public ScoredBallVisualState(Vector3 position)
            {
                Position = position;
            }

            public Vector3 Position { get; }
            public float Elapsed { get; set; }
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
            _modeCatalog = context.ModeCatalog;
            _brickDuelSession = new BrickDuelSessionController(context.BrickDuelVisualAssetService);
            _modeCatalog.TryGetBrickDuelRule("BRICK_DUEL_V0", out _brickDuelRule);
            _networkMatchController = context.NetworkMatchController;
            if (_brickDuelRule != null && _networkMatchController != null)
            {
                _networkMatchController.ConfigureBrickDuel(
                    _brickDuelSession,
                    _modeCatalog,
                    _brickDuelRule,
                    _modeCatalog.GetBrickDuelAiRule(_brickDuelRule.BrickDuelAiRuleId));
            }
            if (_modeCatalog.AllPhaseHeroes.Count > 0)
            {
                string defaultPhaseHero = _modeCatalog.AllPhaseHeroes.ContainsKey("HERO_MIRAGE")
                    ? "HERO_MIRAGE"
                    : _modeCatalog.AllPhaseHeroes.Keys.OrderBy(id => id, StringComparer.Ordinal).First();
                _selectedPhaseLoadout = PhaseMatchLoadout.CreateDefault(_modeCatalog, defaultPhaseHero);
            }
            SubscribeLanRoomService(context.LanRoomService);
            _lanDiagnosticsService = context.LanDiagnosticsService;
            _lanTransport = context.Services?.Get<ILanTransport>();
            _sceneBindingService = context.SceneBindingService;
            await InitializeAsync(context.MatchRuntime, context.InputService, context.HudPresenter, DefaultLocalPlayerId);
            IGatebreakerArenaSceneUiBinding sceneUiBinding = ResolveSceneUiBinding(context.Services);
            _sceneBindingService?.Bind(
                sceneUiBinding,
                BuildSceneUiCallbacks(),
                context.Logger);
            _leaderboardPresenter = LeaderboardPresenter.TryCreate(
                sceneUiBinding,
                new LocalMockLeaderboardDataSource(),
                context.Logger);
            _loadoutPresenter = new HeroDeckSelectionPresenter(_runtime.ModeCatalog);
            IPersistence persistence = context.Services?.Get<IPersistence>();
            if (persistence != null && _modeCatalog.AllPhaseMetas.Count > 0)
            {
                _phaseProfileService = new PhaseProfileService(persistence, _modeCatalog, context.Logger);
                await _phaseProfileService.LoadAsync();
                _phaseSettlementCoordinator = new PhaseSettlementCoordinator(_phaseProfileService);
            }
            if (_modeCatalog.AllPhaseHeroes.Count > 0)
                _phaseLoadoutPresenter = new PhaseTechSelectionPresenter(_modeCatalog);
            InitializeLoadoutUi();
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
            ResetVisualInterpolation();
            SyncPlayerViews();
            SyncBallViews();
            _initialized = true;
        }

        // 先采集全屏移动输入，再交给当前模式消费，UI 命中不参与移动优先级判定。
        private void Update()
        {
            CaptureScreenMovement();
            if (!_initialized)
            {
                return;
            }

            if (!EnsureLocalRuntimeReady())
            {
                return;
            }

            HandleLanDiagnosticsShortcuts();

            if (_brickDuelStarting)
            {
                return;
            }

            if (_brickDuelSession != null && _brickDuelSession.IsActive)
            {
                TickBrickDuel();
                return;
            }

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

            if (EnsureSceneMatchesRuntime())
            {
                return;
            }

            float screenMoveAxis = _screenMoveAxis;
            _sceneBindingService?.PreviewMoveAxis(screenMoveAxis);
            float moveAxis = screenMoveAxis * GetLocalMoveAxisSign();
            bool servePressed = Input.GetKeyDown(KeyCode.Space) || _guiServePressed;
            bool abilityPressed = Input.GetKeyDown(KeyCode.E);
            _guiServePressed = false;
            var frame = new PlayerInputFrame(_localPlayerId, moveAxis, servePressed, BuildServeAimDirection(moveAxis), abilityPressed);
            if (IsLanPlaying())
            {
                SubmitLanInputAtFixedRate(frame);
                UpdateVisualInterpolationClock();
                SyncPlayerViews();
                SyncBallViews();
                RefreshBoundHud();
                return;
            }

            _lanInputAccumulator = 0f;
            _pendingLanButtons = 0;
            SetLocalInputFrame(frame);
            _runtime.ApplyInputFrame(frame);
            _runtime.TickLocalPrototype(Time.deltaTime);
            if (servePressed)
            {
                PlayerRuntimeState localPlayer = _runtime.FindPlayer(_localPlayerId);
                _lastServeBlockReason = localPlayer?.ServeResource?.LastBlockReason ?? ServeBlockReason.PlayerDisabled;
            }

            UpdateVisualInterpolationClock();
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

        private void SubscribeLanRoomService(LanRoomService roomService)
        {
            if (_subscribedLanRoomService != null)
            {
                _subscribedLanRoomService.SnapshotChanged -= OnLanRoomSnapshotChanged;
            }

            _lanRoomService = roomService;
            _subscribedLanRoomService = roomService;
            if (_subscribedLanRoomService != null)
            {
                _subscribedLanRoomService.SnapshotChanged += OnLanRoomSnapshotChanged;
                RoomSnapshot snapshot = _subscribedLanRoomService.CurrentSnapshot;
                _observedLanSessionId = snapshot.SessionId;
                _observedLanRoundId = snapshot.RoundId;
                _observedLanState = snapshot.State;
            }
        }

        private void OnLanRoomSnapshotChanged(RoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            bool invalidateMatchPresentation = ShouldInvalidateLanMatchPresentation(
                _observedLanSessionId,
                _observedLanRoundId,
                _observedLanState,
                snapshot.SessionId,
                snapshot.RoundId,
                snapshot.State);
            _observedLanSessionId = snapshot.SessionId;
            _observedLanRoundId = snapshot.RoundId;
            _observedLanState = snapshot.State;

            if (invalidateMatchPresentation)
            {
                InvalidatePhaseLoadoutOperation();
                ClearPendingPhaseUnlock();
                InvalidateLanBrickDuelPresentationState();
                _lanEntryUiHiddenForPlaying = false;
                SetLegacyVisualsActive(true);
                _sceneBindingService?.HideBrickDuelHud();
                if (snapshot.State == LanRoomState.Lobby)
                {
                    _startupUiState = StartupUiState.OnlineRoom;
                    _sceneBindingService?.ShowLanRoomStatus();
                }
                else if (snapshot.State == LanRoomState.Left || snapshot.State == LanRoomState.Idle)
                {
                    _startupUiState = StartupUiState.ModeSelect;
                    _sceneBindingService?.ShowModeSelect();
                }
                else
                {
                    _startupUiState = StartupUiState.OnlineRoom;
                    _sceneBindingService?.ShowLanRoomStatus();
                }
            }

            _sceneBindingService?.UpdateLanRoom(
                snapshot,
                GetLocalLanAddress(),
                GetRoomLanAddress(snapshot));
        }

        internal static bool ShouldInvalidateLanMatchPresentation(
            ulong previousSessionId,
            uint previousRoundId,
            LanRoomState previousState,
            ulong nextSessionId,
            uint nextRoundId,
            LanRoomState nextState)
        {
            bool hadMatchPresentation = previousState == LanRoomState.Loading ||
                                        previousState == LanRoomState.Playing;
            return hadMatchPresentation &&
                   (previousSessionId != nextSessionId ||
                    previousRoundId != nextRoundId ||
                    (nextState != LanRoomState.Loading && nextState != LanRoomState.Playing));
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

        // 沿用固定采样频率，并记录最后提交的轴供生命周期中断时释放。
        private void SubmitLanInputAtFixedRate(PlayerInputFrame frame)
        {
            if (frame.ServePressed) _pendingLanButtons |= GatebreakerLockstepInputConverter.ServeButton;
            if (frame.AbilityPressed) _pendingLanButtons |= GatebreakerLockstepInputConverter.AbilityButton;
            float frameDelta = 1f / Mathf.Max(1, LockstepSession.SimulationFps);
            int submitCount = AccumulateLanInputFrames(
                ref _lanInputAccumulator,
                Time.deltaTime,
                frameDelta);
            if (submitCount <= 0)
            {
                return;
            }

            short moveAxisQ = GatebreakerLockstepInputConverter.QuantizeSignedUnit(frame.MoveAxis);
            short aimXQ = GatebreakerLockstepInputConverter.QuantizeSignedUnit(frame.AimDirection.x);
            short aimYQ = GatebreakerLockstepInputConverter.QuantizeSignedUnit(frame.AimDirection.y);
            ushort buttons = _pendingLanButtons;
            _pendingLanButtons = 0;
            for (int i = 0; i < submitCount; i++)
            {
                _lanRoomService.Lockstep.SubmitLocalInput(moveAxisQ, aimXQ, aimYQ, buttons);
                _lastSubmittedMoveAxisQ = moveAxisQ;
                buttons = 0;
            }
        }

        internal static int AccumulateLanInputFrames(
            ref float accumulator,
            float deltaTime,
            float frameDelta)
        {
            if (frameDelta <= 0f)
            {
                accumulator = 0f;
                return 0;
            }

            accumulator = Mathf.Min(
                accumulator + Mathf.Max(0f, deltaTime),
                frameDelta * GatebreakerNetworkMatchController.MaxCatchUpStepsPerTick);
            int submitCount = Mathf.Min(
                Mathf.FloorToInt(accumulator / frameDelta),
                GatebreakerNetworkMatchController.MaxCatchUpStepsPerTick);
            accumulator -= submitCount * frameDelta;
            return submitCount;
        }

        // 对局或 UI 在本帧结束操作状态时立即释放输入，再执行既有镜头刷新。
        private void LateUpdate()
        {
            if (_movementWasAllowed && !CanReadMovement())
                ResetMovementInput();
            if (!_initialized)
            {
                return;
            }

            if (_brickDuelSession != null && _brickDuelSession.IsActive)
            {
                ConfigureBrickDuelCamera();
            }
            else
            {
                ConfigurePrototypeCamera();
            }
        }

        private void UpdateVisualInterpolationClock()
        {
            if (_runtime == null)
            {
                return;
            }

            int frameIndex = _runtime.LastFrameIndex;
            if (frameIndex != _lastVisualFrameIndex)
            {
                _lastVisualFrameIndex = frameIndex;
                _visualFrameAdvanced = true;
                _visualInterpolationElapsed = 0f;
                return;
            }

            _visualFrameAdvanced = false;
            _visualInterpolationElapsed += Mathf.Max(0f, Time.deltaTime);
        }

        private void ResetVisualInterpolation()
        {
            _ballVisualPoses.Clear();
            _scoredBallVisuals.Clear();
            _paddleVisualPoses.Clear();
            _lastVisualFrameIndex = _runtime != null ? _runtime.LastFrameIndex : int.MinValue;
            _visualFrameAdvanced = true;
            _visualInterpolationElapsed = 0f;
        }

        private float GetVisualInterpolationAlpha()
        {
            float frameDelta = _runtime != null ? _runtime.FrameDelta : 1f / 30f;
            return Mathf.Clamp01(_visualInterpolationElapsed / Mathf.Max(0.001f, frameDelta));
        }

        private Vector3 GetInterpolatedVisualPosition(
            Dictionary<int, VisualPoseState> poses,
            int id,
            Vector3 targetPosition)
        {
            if (!poses.TryGetValue(id, out VisualPoseState pose))
            {
                pose = new VisualPoseState(targetPosition);
                poses[id] = pose;
                return targetPosition;
            }

            if (_visualFrameAdvanced)
            {
                pose.PreviousPosition = pose.CurrentPosition;
                pose.CurrentPosition = targetPosition;
            }
            else
            {
                pose.CurrentPosition = targetPosition;
            }

            return Vector3.Lerp(pose.PreviousPosition, pose.CurrentPosition, GetVisualInterpolationAlpha());
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

            if (!string.IsNullOrEmpty(_runtime?.LastGoalContactDiagnostic))
            {
                GUILayout.Label("lastGoal=" + _runtime.LastGoalContactDiagnostic, eventStyle);
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
            InvalidatePhaseLoadoutOperation();
            _phaseSettlementCoordinator?.Dispose();
            _phaseSettlementCoordinator = null;
            SubscribeLanRoomService(null);

            _leaderboardPresenter?.Dispose();
            _leaderboardPresenter = null;

            _brickDuelSession?.Dispose();
            _brickDuelSession = null;
            _visualAssets?.Dispose();
            DestroyBallViewCache();
            DestroyPaddleViewCache();
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
            int scenePlayerCount = ResolveRuntimeScenePlayerCount();
            if (_visualRoot != null && _loadedScenePlayerCount == scenePlayerCount)
            {
                return;
            }

            if (_visualRoot == null)
            {
                ResolveArenaRoot();
            }
            else
            {
                ClearLoadedVisualObjects();
            }

            _visualAssets = _visualAssetService != null
                ? await _visualAssetService.LoadAsync(_runtime?.EffectiveRule, _runtime?.BallRule, scenePlayerCount)
                : null;

            if (_visualAssets != null && _visualAssets.IsComplete)
            {
                _usePrefabVisuals = true;
                _loadedScenePlayerCount = scenePlayerCount;
                CreatePrefabScene();
                ValidatePrefabPaddleLength();
                ValidatePrefabBallContactRadius();
                RebuildDebugCollisionOverlay();
                ConfigurePrototypeCamera();
                return;
            }

            _visualAssets?.Dispose();
            _visualAssets = null;
            _usePrefabVisuals = false;
            _loadedScenePlayerCount = scenePlayerCount;
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

        private void ClearLoadedVisualObjects()
        {
            ClearDebugCollisionOverlay();
            if (_sceneInstance != null)
            {
                Destroy(_sceneInstance);
                _sceneInstance = null;
            }

            DestroyBallViewCache();
            DestroyPaddleViewCache();
            _visualAssets?.Dispose();
            _visualAssets = null;
            _usePrefabVisuals = false;
            _hasSceneVisualBounds = false;
            _loadedScenePlayerCount = 0;
        }

        private int ResolveRuntimeScenePlayerCount()
        {
            int playerCount = _runtime?.Players != null ? _runtime.Players.Count : 0;
            if (playerCount <= 0)
            {
                playerCount = _runtime?.EffectiveRule?.Map?.DefaultPlayerCount ?? 0;
            }

            return Mathf.Clamp(playerCount > 0 ? playerCount : 3, 2, 4);
        }

        private void CreatePrefabScene()
        {
            _sceneInstance = Instantiate(_visualAssets.Scene.Prefab, _visualRoot, false);
            _sceneInstance.name = _visualAssets.Scene.Prefab.name;
            _sceneInstance.transform.localPosition = Vector3.zero;
            _sceneInstance.transform.localRotation = Quaternion.identity;
            _sceneInstance.transform.localScale = Vector3.one;
            ApplyScenePlayerSideColors();
            CalibrateGoalBandFromSceneControls();
            UpdateSceneVisualBounds();
        }

        private void ApplyScenePlayerSideColors()
        {
            int playerCount = ResolveRuntimeScenePlayerCount();
            IReadOnlyList<MapPlayerSideBindingDefinition> bindings =
                ResolveScenePlayerSideBindings(_runtime?.EffectiveRule?.Map, playerCount);
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

        private static IReadOnlyList<MapPlayerSideBindingDefinition> ResolveScenePlayerSideBindings(
            MapRuleDefinition map,
            int playerCount)
        {
            if (map?.CollisionLayouts != null)
            {
                for (int i = 0; i < map.CollisionLayouts.Count; i++)
                {
                    MapCollisionLayoutDefinition layout = map.CollisionLayouts[i];
                    if (layout?.PlayerCount == playerCount &&
                        layout.PlayerSideBindings != null &&
                        layout.PlayerSideBindings.Count > 0)
                    {
                        return layout.PlayerSideBindings;
                    }
                }
            }

            return ArenaGeometry.CreateScenePlayerSideBindings(playerCount, map?.PlayerSideBindings);
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

        private void CalibrateGoalBandFromSceneControls()
        {
            int playerCount = ResolveRuntimeScenePlayerCount();
            if (HasConfiguredCollisionLayout(_runtime?.EffectiveRule?.Map, playerCount))
            {
                return;
            }

            IReadOnlyList<MapPlayerSideBindingDefinition> bindings =
                ArenaGeometry.CreateScenePlayerSideBindings(playerCount, _runtime?.EffectiveRule?.Map?.PlayerSideBindings);
            if (_sceneInstance == null || _runtime?.Arena == null || bindings == null)
            {
                return;
            }

            Transform[] transforms = _sceneInstance.GetComponentsInChildren<Transform>(true);
            var dimensionsBySegmentIndex = new Dictionary<int, ArenaGoalBandDimensions>();
            for (int i = 0; i < bindings.Count; i++)
            {
                MapPlayerSideBindingDefinition binding = bindings[i];
                if (binding == null ||
                    string.IsNullOrEmpty(binding.ScenePosition) ||
                    binding.BoundarySegmentIndex < 0 ||
                    binding.BoundarySegmentIndex >= _runtime.Arena.BoundarySegments.Count)
                {
                    continue;
                }

                Transform position = FindSceneTransform(transforms, binding.ScenePosition);
                if (TryCalculateVisibleNetBody(position, out float halfLength, out float triggerInset))
                {
                    dimensionsBySegmentIndex[binding.BoundarySegmentIndex] =
                        new ArenaGoalBandDimensions(halfLength, triggerInset);
                }
            }

            if (dimensionsBySegmentIndex.Count <= 0)
            {
                return;
            }

            if (_runtime.SetArenaGoalBandDimensions(dimensionsBySegmentIndex))
            {
                Debug.LogFormat(
                    "GatebreakerPrototypeRunner: goal bands calibrated from scene controls. count={0}",
                    dimensionsBySegmentIndex.Count);
            }
        }

        private static bool HasConfiguredCollisionLayout(MapRuleDefinition map, int playerCount)
        {
            if (map?.CollisionLayouts == null)
            {
                return false;
            }

            for (int i = 0; i < map.CollisionLayouts.Count; i++)
            {
                MapCollisionLayoutDefinition layout = map.CollisionLayouts[i];
                if (layout?.PlayerCount == playerCount &&
                    layout.BoundarySegments != null &&
                    layout.BoundarySegments.Count >= 3)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryCalculateVisibleNetBody(
            Transform position,
            out float halfLength,
            out float triggerInset)
        {
            halfLength = 0f;
            triggerInset = 0f;
            if (position == null)
            {
                return false;
            }

            SpriteRenderer netRenderer = FindActiveNetRenderer(position);
            if (netRenderer == null || !TryCalculateRendererBoundsInSpace(position, netRenderer, out Bounds netBounds))
            {
                return false;
            }

            float minX = netBounds.min.x;
            float maxX = netBounds.max.x;
            SpriteRenderer[] renderers = position.GetComponentsInChildren<SpriteRenderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer == netRenderer || IsInsideNetTransform(renderer.transform))
                {
                    continue;
                }

                if (!TryCalculateRendererBoundsInSpace(position, renderer, out Bounds coverBounds) ||
                    !Overlaps(coverBounds.min.y, coverBounds.max.y, netBounds.min.y, netBounds.max.y) ||
                    !IsLikelyGoalEndCap(renderer, coverBounds, netBounds))
                {
                    continue;
                }

                if (coverBounds.center.x < netBounds.center.x && coverBounds.max.x > minX)
                {
                    minX = Mathf.Min(coverBounds.max.x, maxX);
                }
                else if (coverBounds.center.x > netBounds.center.x && coverBounds.min.x < maxX)
                {
                    maxX = Mathf.Max(coverBounds.min.x, minX);
                }
            }

            float visibleWidth = maxX - minX;
            float triggerLineInset = netBounds.size.y * 0.5f;
            if (visibleWidth <= 0.001f || triggerLineInset <= 0.001f)
            {
                return false;
            }

            halfLength = visibleWidth * 0.5f;
            triggerInset = triggerLineInset;
            return true;
        }

        private static bool IsLikelyGoalEndCap(SpriteRenderer renderer, Bounds coverBounds, Bounds netBounds)
        {
            if (renderer == null)
            {
                return false;
            }

            string name = renderer.name ?? string.Empty;
            bool hasEndCapName =
                name.StartsWith("Square", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Cap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Cover", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasEndCapName)
            {
                return false;
            }

            float netWidth = netBounds.size.x;
            float coverWidth = coverBounds.size.x;
            if (netWidth <= 0.001f || coverWidth <= 0.001f || coverWidth >= netWidth * 0.5f)
            {
                return false;
            }

            float edgeZone = netWidth * 0.35f;
            bool overlapsLeftEnd = coverBounds.min.x <= netBounds.min.x + edgeZone && coverBounds.max.x > netBounds.min.x;
            bool overlapsRightEnd = coverBounds.max.x >= netBounds.max.x - edgeZone && coverBounds.min.x < netBounds.max.x;
            return overlapsLeftEnd || overlapsRightEnd;
        }

        private static SpriteRenderer FindActiveNetRenderer(Transform position)
        {
            Transform[] children = position.GetComponentsInChildren<Transform>(false);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null ||
                    !child.gameObject.activeInHierarchy ||
                    !string.Equals(child.name, "net", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.enabled)
                {
                    return renderer;
                }
            }

            return null;
        }

        private static bool IsInsideNetTransform(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (string.Equals(current.name, "net", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool Overlaps(float minA, float maxA, float minB, float maxB)
        {
            return maxA > minB && maxB > minA;
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

        private void ValidatePrefabPaddleLength()
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
            float configuredLength = _runtime.Arena.PaddleLength;
            if (configuredLength <= 0f)
            {
                Debug.LogWarning("GatebreakerPrototypeRunner: configured paddle length is invalid; check gatebreaker_rules.bytes.");
            }
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

        private void ValidatePrefabBallContactRadius()
        {
            if (_runtime == null || !_usePrefabVisuals)
            {
                return;
            }

            float radius = 0f;
            bool hasRadius = false;
            for (int playerId = 1; playerId <= 4; playerId++)
            {
                GatebreakerLoadedPrefab ballPrefab = _visualAssets?.GetBallForPlayerId(playerId);
                if (TryCalculateBallContactRadius(ballPrefab?.Prefab, out float playerRadius))
                {
                    radius = Mathf.Max(radius, playerRadius);
                    hasRadius = true;
                }
            }

            if (hasRadius)
            {
                float configuredRadius = _runtime.BallContactRadius;
                if (Mathf.Abs(configuredRadius - radius) > 0.02f)
                {
                    Debug.LogWarningFormat(
                        "GatebreakerPrototypeRunner: ball contact radius config differs from prefab collider. configured={0:0.###}, prefab={1:0.###}",
                        configuredRadius,
                        radius);
                }
            }
        }

        private bool TryCalculateBallContactRadius(GameObject prefab, out float radius)
        {
            radius = 0f;
            if (prefab == null)
            {
                return false;
            }

            CircleCollider2D collider = prefab.GetComponentInChildren<CircleCollider2D>(true);
            if (collider == null || collider.radius <= 0.001f)
            {
                return false;
            }

            Vector3 colliderScale = GetRelativeScale(collider.transform, prefab.transform);
            float visualRadius = collider.radius * Mathf.Max(Mathf.Abs(colliderScale.x), Mathf.Abs(colliderScale.y));
            float prefabScale = GetPrefabVisualUniformScale();
            radius = visualRadius / Mathf.Max(0.001f, prefabScale);
            return radius > 0.001f;
        }

        private bool TryCalculateBallContactRadius(Transform ballView, out float radius)
        {
            radius = 0f;
            if (ballView == null)
            {
                return false;
            }

            CircleCollider2D collider = ballView.GetComponentInChildren<CircleCollider2D>(true);
            if (collider == null || collider.radius <= 0.001f)
            {
                return false;
            }

            Transform scaleRoot = _visualRoot != null ? _visualRoot : ballView.parent;
            Vector3 colliderScale = GetRelativeScale(collider.transform, scaleRoot);
            float visualRadius = collider.radius * Mathf.Max(Mathf.Abs(colliderScale.x), Mathf.Abs(colliderScale.y));
            float prefabScale = GetPrefabVisualUniformScale();
            radius = visualRadius / Mathf.Max(0.001f, prefabScale);
            return radius > 0.001f;
        }

        private static Vector3 GetRelativeScale(Transform transform, Transform root)
        {
            Vector3 scale = Vector3.one;
            Transform current = transform;
            while (current != null)
            {
                scale = Vector3.Scale(scale, current.localScale);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return scale;
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

        private static bool TryCalculateRendererBoundsInSpace(
            Transform root,
            Renderer renderer,
            out Bounds localBounds)
        {
            localBounds = default;
            if (root == null || renderer == null || !renderer.enabled)
            {
                return false;
            }

            if (renderer is SpriteRenderer spriteRenderer && spriteRenderer.sprite != null)
            {
                Vector2 size = spriteRenderer.drawMode == SpriteDrawMode.Simple
                    ? (Vector2)spriteRenderer.sprite.bounds.size
                    : spriteRenderer.size;
                if (size.x <= 0.0001f || size.y <= 0.0001f)
                {
                    return false;
                }

                Vector3 spriteMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 spriteMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                AccumulateRendererLocalCorner(root, spriteRenderer.transform, -size.x * 0.5f, -size.y * 0.5f, ref spriteMin, ref spriteMax);
                AccumulateRendererLocalCorner(root, spriteRenderer.transform, -size.x * 0.5f, size.y * 0.5f, ref spriteMin, ref spriteMax);
                AccumulateRendererLocalCorner(root, spriteRenderer.transform, size.x * 0.5f, -size.y * 0.5f, ref spriteMin, ref spriteMax);
                AccumulateRendererLocalCorner(root, spriteRenderer.transform, size.x * 0.5f, size.y * 0.5f, ref spriteMin, ref spriteMax);
                localBounds = new Bounds((spriteMin + spriteMax) * 0.5f, spriteMax - spriteMin);
                return true;
            }

            Bounds bounds = renderer.bounds;
            Vector3 extents = bounds.extents;
            if (extents.x <= 0.0001f && extents.y <= 0.0001f)
            {
                return false;
            }

            Vector3 center = bounds.center;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            AccumulateSceneBoundsCorner(root, center, extents, -1f, -1f, -1f, ref min, ref max);
            AccumulateSceneBoundsCorner(root, center, extents, -1f, -1f, 1f, ref min, ref max);
            AccumulateSceneBoundsCorner(root, center, extents, -1f, 1f, -1f, ref min, ref max);
            AccumulateSceneBoundsCorner(root, center, extents, -1f, 1f, 1f, ref min, ref max);
            AccumulateSceneBoundsCorner(root, center, extents, 1f, -1f, -1f, ref min, ref max);
            AccumulateSceneBoundsCorner(root, center, extents, 1f, -1f, 1f, ref min, ref max);
            AccumulateSceneBoundsCorner(root, center, extents, 1f, 1f, -1f, ref min, ref max);
            AccumulateSceneBoundsCorner(root, center, extents, 1f, 1f, 1f, ref min, ref max);
            localBounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private static void AccumulateRendererLocalCorner(
            Transform root,
            Transform rendererTransform,
            float localX,
            float localY,
            ref Vector3 min,
            ref Vector3 max)
        {
            Vector3 point = root.InverseTransformPoint(rendererTransform.TransformPoint(new Vector3(localX, localY, 0f)));
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
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
            GetDebugOverlayStyle(kind, out Color color, out float width, out int capVertices, out int cornerVertices);
            AddDebugCollisionLine(new[] { start, end }, color, width, capVertices, cornerVertices);
        }

        private void AddDebugCollisionLine(Vector3[] positions, Color color, float width)
        {
            AddDebugCollisionLine(positions, color, width, 2, 2);
        }

        private void AddDebugCollisionLine(
            Vector3[] positions,
            Color color,
            float width,
            int capVertices,
            int cornerVertices)
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
            line.numCornerVertices = Math.Max(0, cornerVertices);
            line.numCapVertices = Math.Max(0, capVertices);
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
            out float width,
            out int capVertices,
            out int cornerVertices)
        {
            capVertices = 2;
            cornerVertices = 2;
            switch (kind)
            {
                case GatebreakerCollisionOverlayLineKind.GoalTrigger:
                    color = new Color(0.10f, 1.00f, 0.95f, 1f);
                    width = 0.026f;
                    break;
                case GatebreakerCollisionOverlayLineKind.GoalBand:
                    color = new Color(1.00f, 0.92f, 0.10f, 0.78f);
                    width = 0.006f;
                    capVertices = 0;
                    cornerVertices = 0;
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
                paddle.localPosition = GetInterpolatedVisualPosition(
                    _paddleVisualPoses,
                    player.PlayerId,
                    GetPaddlePosition(player));
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
            _paddleVisualPoses.Remove(playerId);
            _paddleRenderers.Remove(playerId);
            if (_guardRenderers.TryGetValue(playerId, out Renderer guard) && guard != null)
            {
                Destroy(guard.gameObject);
            }

            _guardRenderers.Remove(playerId);
        }

        private void DestroyPaddleViewCache()
        {
            foreach (Transform paddle in _paddleViews.Values)
            {
                if (paddle != null)
                {
                    Destroy(paddle.gameObject);
                }
            }

            foreach (Renderer guard in _guardRenderers.Values)
            {
                if (guard != null)
                {
                    Destroy(guard.gameObject);
                }
            }

            _paddleViews.Clear();
            _paddleVisualPoses.Clear();
            _paddleRenderers.Clear();
            _guardRenderers.Clear();
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
            if (!ShouldShowBallViews())
            {
                HideLiveBallViews();
                return;
            }

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
                _scoredBallVisuals.Remove(ball.BallId);
                Vector3 targetPosition = ToVisualPosition(ball.Position, 0.35f);
                Transform ballView = EnsureBallView(ball, targetPosition);
                ballView.localPosition = GetInterpolatedVisualPosition(
                    _ballVisualPoses,
                    ball.BallId,
                    targetPosition);
                ballView.localScale = GetBallVisualScale(ball.OwnerPlayerId);
            }

            RemoveStaleBallViews();
        }

        private bool ShouldShowBallViews()
        {
            return _startupUiState == StartupUiState.LocalPlaying || IsLanPlaying();
        }

        private void HideLiveBallViews()
        {
            _liveBallIds.Clear();
            foreach (KeyValuePair<int, Transform> pair in _ballViews)
            {
                Transform ballView = pair.Value;
                if (ballView == null)
                {
                    continue;
                }

                DisableBallViewPhysics(ballView.gameObject);
                SetBallTrailEmission(ballView.gameObject, false);
                ballView.gameObject.SetActive(false);
            }

            _ballVisualPoses.Clear();
            _scoredBallVisuals.Clear();
        }

        private Transform EnsureBallView(BallRuntimeState ball, Vector3 initialPosition)
        {
            int ballId = ball != null ? ball.BallId : 0;
            if (_ballViews.TryGetValue(ballId, out Transform ballView))
            {
                if (ballView != null && !ballView.gameObject.activeSelf)
                {
                    ballView.gameObject.SetActive(true);
                    ResetBallTrails(ballView.gameObject, true);
                }

                return ballView;
            }

            int ownerPlayerId = ball != null ? ball.OwnerPlayerId : 1;
            GameObject ballObject = AcquireBallViewObject(ownerPlayerId);
            ballObject.name = $"Ball {ballId}";
            ballObject.transform.SetParent(_visualRoot, false);
            DisableBallViewPhysics(ballObject);
            SetBallTrailEmission(ballObject, false);
            ballObject.transform.localPosition = initialPosition;
            ballObject.transform.localRotation = Quaternion.identity;
            ballObject.transform.localScale = GetBallVisualScale(ownerPlayerId);
            ballObject.SetActive(true);
            ResetBallTrails(ballObject, true);

            _ballViewSlots[ballId] = ownerPlayerId;
            _ballViews[ballId] = ballObject.transform;
            RefreshBallContactRadiusFromView(ball, ballObject.transform);
            return ballObject.transform;
        }

        private void RefreshBallContactRadiusFromView(BallRuntimeState ball, Transform ballView)
        {
            if (_runtime == null || ball == null || ballView == null)
            {
                return;
            }

            if (TryCalculateBallContactRadius(ballView, out float radius))
            {
                _runtime.SetBallContactRadiusForBall(ball.BallId, radius);
            }
        }

        public bool RefreshBallContactRadiusFromView(int ballId)
        {
            if (_runtime == null ||
                !_ballViews.TryGetValue(ballId, out Transform ballView) ||
                ballView == null)
            {
                return false;
            }

            BallRuntimeState ball = FindRuntimeBall(ballId);
            if (ball == null || !TryCalculateBallContactRadius(ballView, out float radius))
            {
                return false;
            }

            return _runtime.SetBallContactRadiusForBall(ballId, radius);
        }

        private BallRuntimeState FindRuntimeBall(int ballId)
        {
            if (_runtime == null)
            {
                return null;
            }

            IReadOnlyList<BallRuntimeState> balls = _runtime.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                BallRuntimeState ball = balls[i];
                if (ball != null && ball.BallId == ballId)
                {
                    return ball;
                }
            }

            return null;
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
                    if (UpdateScoredBallVisual(ballId, ballView))
                    {
                        continue;
                    }

                    ReleaseBallViewObject(ballId, ballView.gameObject);
                }

                _ballViews.Remove(ballId);
                _ballViewSlots.Remove(ballId);
                _ballVisualPoses.Remove(ballId);
                _scoredBallVisuals.Remove(ballId);
            }
        }

        private bool UpdateScoredBallVisual(int ballId, Transform ballView)
        {
            if (ballView == null)
            {
                return false;
            }

            if (!_scoredBallVisuals.TryGetValue(ballId, out ScoredBallVisualState scoredVisual))
            {
                scoredVisual = CreateScoredBallVisual(ballId, ballView);
                if (scoredVisual == null)
                {
                    return false;
                }

                _scoredBallVisuals[ballId] = scoredVisual;
            }

            float frameDelta = _runtime != null ? _runtime.FrameDelta : 1f / 30f;
            scoredVisual.Elapsed += Mathf.Max(Time.deltaTime, frameDelta);
            float alpha = Mathf.Clamp01(scoredVisual.Elapsed / Mathf.Max(0.001f, ScoredBallVisualLifetime));
            ballView.localPosition = scoredVisual.Position;
            int ownerPlayerId = _ballViewSlots.TryGetValue(ballId, out int playerId) ? playerId : 1;
            ballView.localScale = GetBallVisualScale(ownerPlayerId);
            return alpha < 1f;
        }

        private ScoredBallVisualState CreateScoredBallVisual(int ballId, Transform ballView)
        {
            DisableBallViewPhysics(ballView != null ? ballView.gameObject : null);
            SetBallTrailEmission(ballView != null ? ballView.gameObject : null, false);
            if (_runtime != null && _runtime.LastGoalContactBallId == ballId)
            {
                return new ScoredBallVisualState(ToVisualPosition(_runtime.LastGoalContactPosition, 0.35f));
            }

            return ballView != null
                ? new ScoredBallVisualState(ballView.localPosition)
                : null;
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

            DisableBallViewPhysics(ballObject);
            ResetBallTrails(ballObject, false);
            ballObject.SetActive(false);
            ballObject.transform.SetParent(_poolRoot != null ? _poolRoot : _visualRoot, false);
            int ownerPlayerId = _ballViewSlots.TryGetValue(ballId, out int playerId) ? playerId : 1;
            GetBallViewPool(ownerPlayerId).Push(ballObject);
        }

        private static void DisableBallViewPhysics(GameObject ballObject)
        {
            if (ballObject == null)
            {
                return;
            }

            Rigidbody2D[] bodies = ballObject.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D body = bodies[i];
                if (body == null)
                {
                    continue;
                }

                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.gravityScale = 0f;
                body.simulated = false;
            }

            Collider2D[] colliders = ballObject.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        private static void ResetBallTrails(GameObject ballObject, bool emitting)
        {
            SetBallTrailEmission(ballObject, emitting);
            ClearBallTrails(ballObject);
        }

        private static void SetBallTrailEmission(GameObject ballObject, bool emitting)
        {
            if (ballObject == null)
            {
                return;
            }

            TrailRenderer[] trails = ballObject.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer trail = trails[i];
                if (trail != null)
                {
                    trail.emitting = emitting;
                }
            }
        }

        private static void ClearBallTrails(GameObject ballObject)
        {
            if (ballObject == null)
            {
                return;
            }

            TrailRenderer[] trails = ballObject.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer trail = trails[i];
                if (trail != null)
                {
                    trail.Clear();
                }
            }
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

        // 仅在可操作战斗中开启移动；页面、生命周期和对局状态共同约束输入。
        private bool CanReadMovement()
        {
            if (!_initialized || !_inputFocused || _inputPaused || !isActiveAndEnabled ||
                _brickDuelStarting || _sceneReloadInProgress)
                return false;
            if (_brickDuelSession != null && _brickDuelSession.IsActive)
            {
                BrickDuelRuntime duel = _brickDuelSession.Runtime;
                return duel != null && duel.Phase == BrickDuelPhase.Playing && !duel.IsPaused &&
                       !(IsLanPlaying() && (_lanRoomService?.CurrentSnapshot.MatchCompleted ?? false));
            }
            return (_startupUiState == StartupUiState.LocalPlaying || IsLanPlaying()) &&
                   _runtime != null && (_runtime.Phase == MatchPhase.Playing || _runtime.Phase == MatchPhase.Overtime);
        }

        // 每帧直接读取原始触摸，编辑器鼠标复用同一判定路径，不创建遮挡按钮的 UI。
        private void CaptureScreenMovement()
        {
            bool allowed = CanReadMovement();
            if (_movementWasAllowed && !allowed)
                ResetMovementInput();
            _movementWasAllowed = allowed;
            CollectScreenTouches();
            float fallbackAxis = ReadMoveAxis();
            _screenMoveAxis = _movementPresenter.ResolveMoveAxis(
                _screenTouches, Screen.width, allowed, fallbackAxis, out _pointerOwnsMovement);
            if (_pointerOwnsMovement)
            {
                // 清除旧 UI 轴，防止 UI 的延后释放回调或另一手指改变触摸方向。
                _guiMoveAxis = 0f;
                _brickDuelMoveAxis = 0f;
            }
        }

        // 复用原始触点采样，状态切换时也记录当前手指，防止恢复按钮的 Began 穿透。
        private void CollectScreenTouches()
        {
            _screenTouches.Clear();
            int touchCount = Input.touchCount;
            for (int i = 0; i < touchCount; i++)
                _screenTouches.Add(Input.GetTouch(i));
#if UNITY_EDITOR
            // 真实触摸及其结束后一帧屏蔽模拟鼠标，避免触摸转鼠标重复输入。
            if (touchCount == 0 && !_hadRealTouches && (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)))
            {
                _screenTouches.Add(new Touch
                {
                    fingerId = -1,
                    position = Input.mousePosition,
                    phase = Input.GetMouseButtonUp(0) ? TouchPhase.Ended :
                        Input.GetMouseButtonDown(0) ? TouchPhase.Began : TouchPhase.Stationary,
                });
            }
            _hadRealTouches = touchCount > 0;
#endif
        }

        // 无触摸时沿用键盘和当前模式的显式 UI 输入。
        private float ReadMoveAxis()
        {
            float moveAxis = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                moveAxis -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                moveAxis += 1f;
            moveAxis += _brickDuelSession != null && _brickDuelSession.IsActive ? _brickDuelMoveAxis : _guiMoveAxis;
            return Mathf.Clamp(moveAxis, -1f, 1f);
        }

        // 生命周期中断须清空实际输入；联机立即发送中性帧，避免停更后远端继续沿用旧轴。
        private void ResetMovementInput()
        {
            _movementWasAllowed = false;
            CollectScreenTouches();
            _movementPresenter.ResetMovement(_screenTouches);
            _screenTouches.Clear();
            _pointerOwnsMovement = true;
            _screenMoveAxis = _guiMoveAxis = _brickDuelMoveAxis = 0f;
            var neutral = new PlayerInputFrame(_localPlayerId, 0f, false, Vector2.zero);
            _inputService?.SetFrame(neutral);
            _runtime?.ApplyInputFrame(neutral);
            _sceneBindingService?.PreviewMoveAxis(0f);
            _sceneBindingService?.PreviewBrickDuelMoveAxis(0f);
            _pendingLanButtons = 0;
            if (_lastSubmittedMoveAxisQ != 0 && IsLanPlaying() && !_lanRoomService.CurrentSnapshot.MatchCompleted)
            {
                // Update 可能不再执行，使用既有提交通道排入零轴；恢复后仍保持固定频率采样。
                _lanRoomService.Lockstep.SubmitLocalInput(0, 0, 0, 0);
                _lastSubmittedMoveAxisQ = 0;
            }
        }

        // 失焦清空控制手指，恢复焦点不能继续使用原按住状态。
        private void OnApplicationFocus(bool focused)
        {
            _inputFocused = focused;
            ResetMovementInput();
        }

        // 切后台立即归零，返回前台后等待新的按下事件。
        private void OnApplicationPause(bool paused)
        {
            _inputPaused = paused;
            ResetMovementInput();
        }

        // 组件停用时 Update 不再执行，必须主动释放输入。
        private void OnDisable()
        {
            ResetMovementInput();
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

        // 重置比赛并释放旧输入，进入页面后等待新一轮战斗。
        private void ResetPrototypeMatchForEntryUi()
        {
            ResetMovementInput();
            _runtime?.StartLocalPrototype(localLoadout: _selectedLocalLoadout);
            _runtime?.SetLocalPlayer(_localPlayerId);
            EnsureSceneMatchesRuntime();
            ValidatePrefabBallContactRadius();
            _lastServeBlockReason = ServeBlockReason.None;
            _guiServePressed = false;
            _guiMoveAxis = 0f;
            ResetVisualInterpolation();
            RebuildDebugCollisionOverlay();
            RefreshBoundHud();
        }

        // UI 仅在没有全屏触摸接管时提供后备移动，不能覆盖触摸方向。
        private void SetGuiMoveAxis(float moveAxis)
        {
            _guiMoveAxis = !_pointerOwnsMovement && CanReadMovement() ? Mathf.Clamp(moveAxis, -1f, 1f) : 0f;
        }

        private bool EnsureSceneMatchesRuntime()
        {
            if (_runtime == null || _visualRoot == null)
            {
                return false;
            }

            if (_loadedScenePlayerCount == ResolveRuntimeScenePlayerCount())
            {
                return false;
            }

            if (_sceneReloadInProgress)
            {
                return true;
            }

            _sceneReloadInProgress = true;
            StartCoroutine(ReloadSceneForRuntimeAsync());
            return true;
        }

        private IEnumerator ReloadSceneForRuntimeAsync()
        {
            Task reloadTask = EnsureSceneAsync();
            while (!reloadTask.IsCompleted)
            {
                yield return null;
            }

            if (reloadTask.Exception != null)
            {
                Debug.LogException(reloadTask.Exception);
            }

            ResetVisualInterpolation();
            _sceneReloadInProgress = false;
        }

        private GatebreakerArenaSceneUiCallbacks BuildSceneUiCallbacks()
        {
            return new GatebreakerArenaSceneUiCallbacks
            {
                ServeRequested = RequestGuiServe,
                LocalBattleRequested = BeginLocalBattle,
                OnlineBattleRequested = ShowOnlineBattleMenu,
                SingleBattleRequested = ShowSingleBattleMenu,
                BrickDuelRequested = StartBrickDuel,
                SingleSelectBackRequested = ReturnFromSingleSelect,
                BrickDuelPauseRequested = ToggleBrickDuelPause,
                BrickDuelAbilityRequested = RequestBrickDuelAbility,
                LoadoutHeroChanged = SelectLoadoutHero,
                LoadoutPathChanged = SelectLoadoutPath,
                LoadoutSignatureChanged = SelectLoadoutSignature,
                LoadoutUniversalChipChanged = SelectPhaseTech,
                LoadoutUseDefaultRequested = UseDefaultLoadout,
                LoadoutConfirmRequested = ConfirmLoadout,
                LoadoutBackRequested = ReturnFromLoadout,
                LoadoutUnlockConfirmRequested = ConfirmPendingPhaseUnlock,
                LoadoutUnlockCancelRequested = CancelPendingPhaseUnlock,
                CreateLanHostRequested = CreateLanHost,
                StartLanDiscoveryRequested = StartLanDiscovery,
                JoinLanRoomRequested = JoinLanRoom,
                ToggleLanReadyRequested = ToggleLanReady,
                StartLanLoadingRequested = StartLanLoading,
                LeaveLanRoomRequested = LeaveLanRoom,
                AcknowledgeLanStartRequested = AcknowledgeLanStart,
                LanPlayerNameChanged = SetLanPlayerName,
                LanRoomPlayerCountChanged = SetLanRoomPlayerCount,
                LanRoomCodeChanged = SetLanRoomCode,
                MoveAxisChanged = SetGuiMoveAxis,
                BrickDuelMoveAxisChanged = SetBrickDuelMoveAxis,
                HitOffsetInfluenceChanged = value => _runtime?.BounceTuning?.SetHitOffsetInfluenceValue(value),
                PaddleVelocityInfluenceChanged = value => _runtime?.BounceTuning?.SetPaddleVelocityInfluenceValue(value),
                MinimumOutwardShareChanged = value => _runtime?.BounceTuning?.SetMinimumOutwardShareValue(value),
                RestartMatchRequested = RequestResultRestart,
                ResultBackRequested = RequestResultBack,
                InitialLanPlayerName = _lanPlayerName,
                InitialLanRoomPlayerCount = _lanSelectedPlayerCount,
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

            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();

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

            RoomSnapshot snapshot = _lanRoomService.CreateHost(
                _lanPlayerName,
                _lanClientInstanceId,
                maxPlayers: _lanSelectedPlayerCount,
                tcpPort: tcpPort);
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

            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();

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

            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();

            _lanRoomService.RecordUiAction("JoinClicked", "roomCode=" + _lanRoomCodeInput);
            EnsureLanIdentity();
            ResetLocalLanSessionTransport();
            _lanTransport?.StartDiscovery();
            if (_lanRoomService.JoinRoomByCode(_lanRoomCodeInput, _lanClientInstanceId, _lanPlayerName))
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
            RoomSnapshot snapshot = _lanRoomService.CurrentSnapshot;
            RoomPlayerSnapshot local = snapshot?.Players?.FirstOrDefault(player => player.IsLocal);
            if (local != null && !local.IsReady &&
                _modeCatalog != null && _modeCatalog.AllPhaseHeroes.Count > 0 &&
                !local.HasConfirmedPhaseLoadoutThisLobby)
            {
                ShowLoadout(true);
                return;
            }
            ToggleLanReady(snapshot);
        }

        private void StartLanLoading()
        {
            _lanRoomService?.RecordUiAction("StartClicked", BuildLanUiSnapshotDetail(_lanRoomService.CurrentSnapshot));
            _lanRoomService?.StartLoading();
        }

        private void LeaveLanRoom()
        {
            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();
            _brickDuelAbilityPressed = false;
            InvalidateLanBrickDuelPresentationState();
            _lanRoomService?.Leave("ui");
            ResetLocalLanSessionAfterLeave("ui");
            _startupUiState = StartupUiState.ModeSelect;
            _lanEntryUiHiddenForPlaying = false;
            _sceneBindingService?.ShowModeSelect();
            RefreshBoundHud();
        }

        private void RequestResultRestart()
        {
            if (_brickDuelSession != null && _brickDuelSession.IsActive)
            {
                if (IsLanPlaying())
                {
                    if (!(_lanRoomService?.CurrentSnapshot.MatchCompleted ?? false))
                    {
                        _sceneBindingService?.SetHeroHud("等待双方终局结果确认…");
                        return;
                    }
                    bool brickDuelReadyAfterReturn = _lanRoomService.CurrentSnapshot.IsHost;
                    if (!_lanRoomService.ReturnToLobbyFromResult(brickDuelReadyAfterReturn))
                    {
                        LeaveLanRoom();
                        return;
                    }
                    InvalidateLanBrickDuelPresentationState();
                    _startupUiState = StartupUiState.OnlineRoom;
                    _lanEntryUiHiddenForPlaying = false;
                    SetLegacyVisualsActive(true);
                    _sceneBindingService?.HideBrickDuelHud();
                    _sceneBindingService?.ShowLanRoomStatus();
                    RefreshBoundHud();
                    return;
                }
                RestartBrickDuel();
                return;
            }

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
            InvalidateLanBrickDuelPresentationState();

            _startupUiState = StartupUiState.OnlineRoom;
            _lanEntryUiHiddenForPlaying = false;
            ResetPrototypeMatchForEntryUi();
            _sceneBindingService?.ShowLanRoomStatus();
            RefreshBoundHud();
        }

        private void RequestResultBack()
        {
            if (_brickDuelSession != null && _brickDuelSession.IsActive)
            {
                if (IsLanPlaying())
                {
                    if (!(_lanRoomService?.CurrentSnapshot.MatchCompleted ?? false))
                    {
                        _sceneBindingService?.SetHeroHud("等待双方终局结果确认…");
                        return;
                    }
                    LeaveLanRoom();
                    return;
                }
                StopBrickDuelAndShowModeSelect();
                return;
            }

            RoomSnapshot snapshot = _lanRoomService?.CurrentSnapshot;
            if (!IsLanResultRoom(snapshot) || IsLanRoomTerminal(snapshot))
            {
                ReturnToModeSelectFromResult();
                return;
            }

            _lanRoomService.Leave(snapshot.IsHost ? "resultBackHost" : "resultBack");
            InvalidateLanBrickDuelPresentationState();
            ResetLocalLanSessionAfterLeave(snapshot.IsHost ? "resultBackHost" : "resultBack");
            ReturnToModeSelectFromResult();
        }

        private void ReturnToModeSelectFromResult()
        {
            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();
            InvalidateLanBrickDuelPresentationState();
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

        private void SetLanRoomPlayerCount(int value)
        {
            _lanSelectedPlayerCount = 2;
        }

        private void SetLanRoomCode(string value)
        {
            _lanRoomCodeInput = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void InitializeLoadoutUi()
        {
            if (_phaseLoadoutPresenter == null || _sceneBindingService == null) return;
            string heroId = _phaseProfileService?.Current.LastSelectedHeroId;
            if (!string.IsNullOrEmpty(heroId) && _modeCatalog.AllPhaseHeroes.ContainsKey(heroId))
                _phaseLoadoutPresenter.SelectHero(heroId);
            RestoreSavedLoadout(_phaseLoadoutPresenter.SelectedHeroId);
            RefreshLoadoutOptions();
        }

        private void ShowLoadout(bool forLan)
        {
            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();
            _loadoutForLan = forLan;
            _loadoutForBrickDuel = false;
            if (_phaseLoadoutPresenter == null) InitializeLoadoutUi();
            if (_sceneBindingService == null || !_sceneBindingService.HasLoadoutBindings)
            {
                Debug.LogError("Phase loadout UI bindings are missing; automatic confirmation is disabled.");
                if (forLan) _sceneBindingService?.ShowLanRoomStatus();
                else _sceneBindingService?.ShowModeSelect();
                return;
            }
            _sceneBindingService?.ShowLoadout();
        }

        private void BeginLocalBattle()
        {
            _loadoutForLan = false;
            _loadoutForBrickDuel = false;
            InvalidatePhaseLoadoutOperation();
            StartLocalBattleCountdown();
        }

        private void ShowBrickDuelLoadout()
        {
            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();
            _loadoutForLan = false;
            _loadoutForBrickDuel = true;
            if (_phaseLoadoutPresenter == null) InitializeLoadoutUi();
            if (_sceneBindingService == null || !_sceneBindingService.HasLoadoutBindings)
            {
                Debug.LogError("Phase loadout UI bindings are missing; automatic confirmation is disabled.");
                _sceneBindingService?.ShowSingleSelect(_brickDuelRule != null, "相位配装界面缺少显式绑定");
                return;
            }
            _sceneBindingService.ShowLoadout();
        }

        private void ReturnFromLoadout()
        {
            bool returnToLan = _loadoutForLan;
            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();
            if (_phaseLoadoutPresenter != null)
            {
                RestoreSavedLoadout(_phaseLoadoutPresenter.SelectedHeroId);
                RefreshLoadoutOptions(false);
            }
            _loadoutForLan = false;
            _loadoutForBrickDuel = false;
            if (returnToLan && _lanRoomService?.CurrentSnapshot.State == LanRoomState.Lobby)
            {
                _sceneBindingService?.ShowLanRoomStatus();
                return;
            }

            _sceneBindingService?.ShowSingleSelect(_brickDuelRule != null);
        }

        private void ShowSingleBattleMenu()
        {
            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();
            bool available = _brickDuelRule != null;
            _startupUiState = StartupUiState.ModeSelect;
            _sceneBindingService?.ShowSingleSelect(available);
        }

        private void ReturnFromSingleSelect()
        {
            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();
            _startupUiState = StartupUiState.ModeSelect;
            _sceneBindingService?.ShowModeSelect();
        }

        private void StartBrickDuel() => ShowBrickDuelLoadout();

        private async void StartBrickDuelConfigured()
        {
            if (_brickDuelStarting || (_brickDuelSession != null && _brickDuelSession.IsActive))
            {
                return;
            }

            if (_brickDuelRule == null || _modeCatalog == null ||
                !_modeCatalog.TryGetBrickDuelRule("BRICK_DUEL_V0", out _brickDuelRule))
            {
                _sceneBindingService?.ShowSingleSelect(false);
                return;
            }

            _brickDuelStarting = true;
            _sceneBindingService?.ShowStartCountdown("资源加载中");
            try
            {
                BrickDuelAiRuleDefinition aiRule =
                    _modeCatalog.GetBrickDuelAiRule(_brickDuelRule.BrickDuelAiRuleId);
                PhaseMatchLoadout phaseLoadout = _selectedPhaseLoadout ??
                    PhaseMatchLoadout.CreateDefault(_modeCatalog, "HERO_MIRAGE");
                bool started = await _brickDuelSession.StartAsync(
                    _brickDuelRule,
                    aiRule,
                    null,
                    _modeCatalog,
                    phaseLoadout,
                    phaseLoadout.Clone());
                if (!started)
                {
                    Debug.LogWarning(_brickDuelSession.LastError);
                    _sceneBindingService?.ShowSingleSelect(true, _brickDuelSession.LastError);
                    return;
                }

                _brickDuelSession.ConfigureLocalPerspective(false);
                SetLegacyVisualsActive(false);
                _brickDuelMatchGeneration++;
                _brickDuelMatchId = Guid.NewGuid().ToString("N");
                _brickDuelSettlementRequested = false;
                _brickDuelSettlementInFlight = false;
                _nextBrickDuelSettlementRetryTime = 0f;
                _brickDuelSettlementOperationMatchId = null;
                _brickDuelAbilityPressed = false;
                _brickDuelMoveAxis = 0f;
                _lastBrickDuelUiFrame = int.MinValue;
                _sceneBindingService?.SetBrickDuelSettlementStatus(string.Empty);
                _sceneBindingService?.ShowBrickDuelHud();
                _sceneBindingService?.UpdateBrickDuel(
                    _brickDuelSession.Snapshot,
                    _brickDuelRule,
                    null);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _brickDuelSession?.Stop();
                SetLegacyVisualsActive(true);
                _sceneBindingService?.ShowSingleSelect(true, "1v1 启动失败，请重试");
            }
            finally
            {
                _brickDuelStarting = false;
            }
        }

        // 消费统一屏幕输入；联机仍通过现有方向转换与固定频率通道提交。
        private void TickBrickDuel()
        {
            BrickDuelRuntime runtime = _brickDuelSession.Runtime;
            if (runtime == null)
            {
                return;
            }

            bool isLanBrickDuel = IsLanPlaying() && _networkMatchController != null && _networkMatchController.UsesBrickDuel;
            if (!isLanBrickDuel && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)))
            {
                _brickDuelSession.SetPaused(!runtime.IsPaused);
                ResetMovementInput();
            }

            float moveAxis = CanReadMovement() ? _screenMoveAxis : 0f;
            bool abilityPressed = Input.GetKeyDown(KeyCode.E) || _brickDuelAbilityPressed;
            _brickDuelAbilityPressed = false;
            if (isLanBrickDuel)
            {
                SetLegacyVisualsActive(false);
                _sceneBindingService?.ShowBrickDuelHud();
                float logicalAxis = _networkMatchController.LocalIsTop ? -moveAxis : moveAxis;
                RoomSnapshot roomSnapshot = _lanRoomService.CurrentSnapshot;
                if (runtime.Phase != BrickDuelPhase.Result && !roomSnapshot.MatchCompleted)
                {
                    var input = new PlayerInputFrame(_localPlayerId, logicalAxis, false, Vector2.up, abilityPressed);
                    SubmitLanInputAtFixedRate(input);
                }
                string lanMatchId = BuildLanBrickDuelMatchId(roomSnapshot);
                if (_brickDuelMatchId != lanMatchId)
                {
                    _brickDuelMatchId = lanMatchId;
                    _brickDuelMatchGeneration++;
                    _brickDuelSettlementRequested = false;
                    _brickDuelSettlementInFlight = false;
                    _brickDuelSettlementOperationMatchId = null;
                    _nextBrickDuelSettlementRetryTime = 0f;
                    _lastBrickDuelUiFrame = int.MinValue;
                    _sceneBindingService?.SetBrickDuelSettlementStatus(string.Empty);
                }
            }
            else
            {
                _brickDuelSession.Tick(Time.deltaTime, moveAxis, abilityPressed);
            }
            _sceneBindingService?.PreviewBrickDuelMoveAxis(moveAxis);

            BrickDuelSnapshot snapshot = _brickDuelSession.Snapshot;
            BrickDuelFrameEvents events = snapshot != null &&
                                          snapshot.SimulationFrame != _lastBrickDuelUiFrame
                ? runtime.LastFrameEvents
                : null;
            if (snapshot != null)
            {
                _lastBrickDuelUiFrame = snapshot.SimulationFrame;
            }
            bool localIsTop = isLanBrickDuel && _networkMatchController.LocalIsTop;
            _sceneBindingService?.UpdateBrickDuel(snapshot, _brickDuelRule, events, localIsTop);
            BrickDuelPhaseSideState localPhaseState = localIsTop
                ? snapshot?.TopPhaseState
                : snapshot?.BottomPhaseState;
            _sceneBindingService?.UpdateBrickDuelAbility(
                localPhaseState,
                _brickDuelRule != null ? _brickDuelRule.SimulationFps : LockstepSession.SimulationFps);
            bool terminalConsensus = !isLanBrickDuel ||
                                     (_lanRoomService?.CurrentSnapshot.MatchCompleted ?? false);
            _sceneBindingService?.SetResultActionsInteractable(
                snapshot == null || snapshot.Phase != BrickDuelPhase.Result || terminalConsensus);
            if (snapshot != null && snapshot.Phase == BrickDuelPhase.Result && terminalConsensus &&
                !_brickDuelSettlementRequested && !_brickDuelSettlementInFlight &&
                Time.unscaledTime >= _nextBrickDuelSettlementRetryTime)
            {
                SettleBrickDuelAsync(_brickDuelMatchId, localIsTop ? SwapBrickDuelResult(snapshot.Result) : snapshot.Result);
            }
        }

        // 双向砖潮 UI 轴遵循与普通对局相同的触摸优先级。
        private void SetBrickDuelMoveAxis(float moveAxis)
        {
            _brickDuelMoveAxis = !_pointerOwnsMovement && CanReadMovement() ? Mathf.Clamp(moveAxis, -1f, 1f) : 0f;
        }

        // 暂停与恢复都释放当前手指，按钮点击不能在恢复后形成残留移动。
        private void ToggleBrickDuelPause()
        {
            BrickDuelRuntime runtime = _brickDuelSession?.Runtime;
            if (runtime != null && runtime.Phase == BrickDuelPhase.Playing)
            {
                _brickDuelSession.SetPaused(!runtime.IsPaused);
                ResetMovementInput();
            }
        }

        private void RequestBrickDuelAbility()
        {
            BrickDuelRuntime runtime = _brickDuelSession?.Runtime;
            if (runtime != null && runtime.Phase == BrickDuelPhase.Playing)
            {
                _brickDuelAbilityPressed = true;
            }
        }

        // 重开双向砖潮并重新加载资源，旧手指不得带入新对局。
        private async void RestartBrickDuel()
        {
            if (_brickDuelStarting || _brickDuelRule == null || _modeCatalog == null)
            {
                return;
            }

            ResetMovementInput();
            _brickDuelStarting = true;
            _sceneBindingService?.UpdateBrickDuelResult(BrickDuelResult.None);
            _sceneBindingService?.ShowStartCountdown("资源重新加载中");
            try
            {
                BrickDuelAiRuleDefinition aiRule =
                    _modeCatalog.GetBrickDuelAiRule(_brickDuelRule.BrickDuelAiRuleId);
                PhaseMatchLoadout phaseLoadout = _selectedPhaseLoadout ??
                    PhaseMatchLoadout.CreateDefault(_modeCatalog, "HERO_MIRAGE");
                bool started = await _brickDuelSession.StartAsync(
                    _brickDuelRule,
                    aiRule,
                    null,
                    _modeCatalog,
                    phaseLoadout,
                    phaseLoadout.Clone());
                if (!started)
                {
                    SetLegacyVisualsActive(true);
                    _sceneBindingService?.ShowSingleSelect(true, _brickDuelSession.LastError);
                    return;
                }

                _brickDuelSession.ConfigureLocalPerspective(false);
                _brickDuelMoveAxis = 0f;
                _brickDuelMatchGeneration++;
                _brickDuelMatchId = Guid.NewGuid().ToString("N");
                _brickDuelSettlementRequested = false;
                _brickDuelSettlementInFlight = false;
                _nextBrickDuelSettlementRetryTime = 0f;
                _brickDuelSettlementOperationMatchId = null;
                _brickDuelAbilityPressed = false;
                _lastBrickDuelUiFrame = int.MinValue;
                _sceneBindingService?.SetBrickDuelSettlementStatus(string.Empty);
                _sceneBindingService?.ShowBrickDuelHud();
                _sceneBindingService?.UpdateBrickDuel(
                    _brickDuelSession.Snapshot,
                    _brickDuelRule,
                    null);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _brickDuelSession?.Stop();
                SetLegacyVisualsActive(true);
                _sceneBindingService?.ShowSingleSelect(true, "1v1 重开失败，请重试");
            }
            finally
            {
                _brickDuelStarting = false;
            }
        }

        private void StopBrickDuelAndShowModeSelect()
        {
            _brickDuelSession?.Stop();
            _brickDuelMatchGeneration++;
            _brickDuelMatchId = string.Empty;
            _brickDuelSettlementRequested = false;
            _brickDuelSettlementInFlight = false;
            _brickDuelSettlementOperationMatchId = null;
            _nextBrickDuelSettlementRetryTime = 0f;
            _brickDuelMoveAxis = 0f;
            _brickDuelAbilityPressed = false;
            _lastBrickDuelUiFrame = int.MinValue;
            SetLegacyVisualsActive(true);
            _startupUiState = StartupUiState.ModeSelect;
            _sceneBindingService?.HideBrickDuelHud();
            _sceneBindingService?.ShowModeSelect();
            RefreshBoundHud();
        }

        private async void SettleBrickDuelAsync(string matchId, BrickDuelResult result)
        {
            if (_brickDuelSettlementInFlight || _brickDuelSettlementRequested) return;
            if (_phaseSettlementCoordinator == null)
            {
                _brickDuelSettlementRequested = true;
                return;
            }

            _brickDuelSettlementInFlight = true;
            _brickDuelSettlementOperationMatchId = matchId;
            int requestedGeneration = _brickDuelMatchGeneration;
            try
            {
                PhaseSettlementResult settlement = await _phaseSettlementCoordinator.EnsureSettlementAsync(matchId, result, true);
                if (_brickDuelMatchGeneration != requestedGeneration ||
                    !string.Equals(_brickDuelMatchId, matchId, StringComparison.Ordinal))
                {
                    return;
                }
                if (settlement.IsFinal)
                {
                    _brickDuelSettlementRequested = true;
                    if (settlement.Reward > 0)
                        _sceneBindingService?.SetBrickDuelSettlementStatus($"相位结算 +{settlement.Reward} · 余额 {_phaseProfileService.Current.Currency}");
                    else
                        _sceneBindingService?.SetBrickDuelSettlementStatus("相位结算已完成");
                }
                else
                {
                    _nextBrickDuelSettlementRetryTime = Time.unscaledTime + 2f;
                    _sceneBindingService?.SetBrickDuelSettlementStatus("相位结算保存失败，正在重试…");
                }
            }
            catch (Exception exception)
            {
                if (_brickDuelMatchGeneration != requestedGeneration ||
                    !string.Equals(_brickDuelMatchId, matchId, StringComparison.Ordinal))
                {
                    Debug.LogException(exception);
                    return;
                }
                _nextBrickDuelSettlementRetryTime = Time.unscaledTime + 2f;
                _sceneBindingService?.SetBrickDuelSettlementStatus("相位结算异常，正在重试…");
                Debug.LogException(exception);
            }
            finally
            {
                if (string.Equals(_brickDuelSettlementOperationMatchId, matchId, StringComparison.Ordinal))
                {
                    _brickDuelSettlementInFlight = false;
                    _brickDuelSettlementOperationMatchId = null;
                }
            }
        }

        internal static string BuildLanBrickDuelMatchId(RoomSnapshot snapshot)
        {
            return snapshot == null
                ? string.Empty
                : "lan-" + snapshot.SessionId + "-" + snapshot.RoundId;
        }

        private void InvalidateLanBrickDuelPresentationState()
        {
            _brickDuelMatchGeneration++;
            _brickDuelMatchId = string.Empty;
            _brickDuelSettlementRequested = false;
            _brickDuelSettlementInFlight = false;
            _brickDuelSettlementOperationMatchId = null;
            _nextBrickDuelSettlementRetryTime = 0f;
            _brickDuelAbilityPressed = false;
            _lastBrickDuelUiFrame = int.MinValue;
            _sceneBindingService?.SetBrickDuelSettlementStatus(string.Empty);
        }

        private static BrickDuelResult SwapBrickDuelResult(BrickDuelResult result)
        {
            if (result == BrickDuelResult.PlayerWin) return BrickDuelResult.PlayerLose;
            if (result == BrickDuelResult.PlayerLose) return BrickDuelResult.PlayerWin;
            return result;
        }

        private void SetLegacyVisualsActive(bool active)
        {
            if (_visualRoot != null && _visualRoot.gameObject.activeSelf != active)
            {
                _visualRoot.gameObject.SetActive(active);
            }
            if (_debugCollisionOverlayRoot != null &&
                _debugCollisionOverlayRoot.gameObject.activeSelf != active)
            {
                _debugCollisionOverlayRoot.gameObject.SetActive(active);
            }
        }

        private void SelectLoadoutHero(int index)
        {
            if (_phaseLoadoutOperationInProgress || _phaseLoadoutPresenter == null ||
                index < 0 || index >= _phaseLoadoutPresenter.AvailableHeroes.Count) return;
            string heroId = _phaseLoadoutPresenter.AvailableHeroes[index].HeroId;
            ClearPendingPhaseUnlock();
            _phaseLoadoutPresenter.SelectHero(heroId);
            RestoreSavedLoadout(heroId);
            RefreshLoadoutOptions(false);
        }

        private void SelectLoadoutPath(int index) { }

        private void SelectLoadoutSignature(int index) { }

        private void UseDefaultLoadout()
        {
            if (_phaseLoadoutPresenter == null) return;
            ClearPendingPhaseUnlock();
            _phaseLoadoutPresenter.SelectHero(_phaseLoadoutPresenter.SelectedHeroId);
            for (int i = 0; i < _loadoutChipIndices.Length; i++) _loadoutChipIndices[i] = 0;
            _sceneBindingService?.SetLoadoutChipSelections(_loadoutChipIndices);
            _sceneBindingService?.SetLoadoutError(string.Empty);
        }

        private async void ConfirmLoadout()
        {
            if (_phaseLoadoutOperationInProgress || _pendingUnlockPhaseIndex >= 0 ||
                _phaseLoadoutPresenter == null) return;
            int operation = BeginPhaseLoadoutOperation();
            bool forBrickDuel = _loadoutForBrickDuel;
            bool forLan = _loadoutForLan;
            RoomSnapshot requestedRoom = forLan ? _lanRoomService?.CurrentSnapshot : null;
            ulong requestedSessionId = requestedRoom?.SessionId ?? 0UL;
            uint requestedRoundId = requestedRoom?.RoundId ?? 0U;
            PhaseMatchLoadout requestedLoadout;
            try
            {
                requestedLoadout = _phaseLoadoutPresenter.Build(GetUnlockedPhaseTechIds());
            }
            catch (Exception exception)
            {
                _sceneBindingService?.SetLoadoutError(exception.Message);
                CompletePhaseLoadoutOperation(operation);
                return;
            }

            try
            {
                if (_phaseProfileService != null && !await _phaseProfileService.SaveLoadoutAsync(requestedLoadout))
                {
                    if (IsPhaseLoadoutOperationCurrent(operation))
                        _sceneBindingService?.SetLoadoutError("相位配装保存失败，请重试。");
                    return;
                }
                if (!IsPhaseLoadoutOperationCurrent(operation) ||
                    !IsLoadoutContextCurrent(forBrickDuel, forLan, requestedSessionId, requestedRoundId))
                    return;
                _selectedPhaseLoadout = requestedLoadout;
                if (forBrickDuel)
                {
                    _loadoutForBrickDuel = false;
                    StartBrickDuelConfigured();
                    return;
                }
                if (forLan)
                {
                    _selectedLocalLoadout = CreateLegacyCompatibilityLoadout();
                    if (_lanRoomService == null ||
                        !_lanRoomService.SetLocalPhaseLoadout(requestedLoadout) ||
                        !_lanRoomService.SetLocalLoadout(_selectedLocalLoadout))
                    {
                        _sceneBindingService?.SetLoadoutError("房间已冻结或相位配装校验失败。");
                        return;
                    }
                    _sceneBindingService?.ShowLanRoomStatus();
                    ToggleLanReady(_lanRoomService.CurrentSnapshot);
                }
            }
            catch (Exception exception)
            {
                if (IsPhaseLoadoutOperationCurrent(operation))
                    _sceneBindingService?.SetLoadoutError("相位配装操作失败：" + exception.Message);
            }
            finally
            {
                CompletePhaseLoadoutOperation(operation);
            }
        }

        private bool IsLoadoutContextCurrent(
            bool forBrickDuel,
            bool forLan,
            ulong requestedSessionId,
            uint requestedRoundId)
        {
            if (_loadoutForBrickDuel != forBrickDuel || _loadoutForLan != forLan)
                return false;
            if (!forLan) return true;
            RoomSnapshot current = _lanRoomService?.CurrentSnapshot;
            return current != null &&
                   current.State == LanRoomState.Lobby &&
                   current.SessionId == requestedSessionId &&
                   current.RoundId == requestedRoundId;
        }

        private void RefreshLoadoutOptions(bool includeHeroes = true)
        {
            if (_phaseLoadoutPresenter == null) return;
            PhaseHeroDefinition hero = _modeCatalog.GetPhaseHero(_phaseLoadoutPresenter.SelectedHeroId);
            string[] heroes = _phaseLoadoutPresenter.AvailableHeroes
                .Select(item => item.DisplayName + " · " + item.Dimension).ToArray();
            var options = new List<IReadOnlyList<string>>(5);
            ISet<string> unlocked = GetUnlockedPhaseTechIds();
            for (int phase = 0; phase < 5; phase++)
            {
                options.Add(_phaseLoadoutPresenter.GetOptions(phase).Select(tech =>
                    $"P{phase + 1} {tech.DisplayName}" +
                    (tech.Kind == "Default" ? " · 免费" : unlocked.Contains(tech.TechId) ? " · 已解锁" : $" · {tech.CostCurrency}币") +
                    (string.IsNullOrEmpty(tech.MechanicEffect) ? string.Empty : " · " + tech.MechanicEffect)).ToArray());
                _loadoutChipIndices[phase] = _phaseLoadoutPresenter.GetSelectedOptionIndex(phase);
            }
            PhaseHeroActiveAbilityDefinition ability = hero.PhaseLevels.FirstOrDefault(level => level.PhaseLevel == "P3")?.ActiveAbility;
            string abilityName = hero.HeroId == "HERO_MIRAGE" ? "幻潮" :
                hero.HeroId == "HERO_PULSE" ? "爆点" :
                hero.HeroId == "HERO_RIFT" ? "贯裂" :
                hero.HeroId == "HERO_REFRACT" ? "镜界" : "主动技能";
            string abilityText = ability == null ? "无主动技能" : $"P3 解锁 · {abilityName} · 冷却 {ability.CooldownSeconds:0.#} 秒";
            _sceneBindingService?.ConfigurePhaseLoadout(heroes, "维度 · " + hero.Dimension, abilityText, options);
            int heroIndex = _phaseLoadoutPresenter.AvailableHeroes
                .Select((item, index) => new { item.HeroId, Index = index })
                .Where(item => item.HeroId == hero.HeroId)
                .Select(item => item.Index)
                .DefaultIfEmpty(0)
                .First();
            _sceneBindingService?.SetLoadoutHeroSelection(heroIndex);
            _sceneBindingService?.SetLoadoutChipSelections(_loadoutChipIndices);
            int currency = _phaseProfileService?.Current.Currency ?? 0;
            _sceneBindingService?.SetLoadoutError($"相位币 {currency} · 每个 P 槽选择 1 项科技");
        }

        private void SelectPhaseTech(int phaseIndex, int optionIndex)
        {
            if (_phaseLoadoutOperationInProgress || _phaseLoadoutPresenter == null ||
                phaseIndex < 0 || phaseIndex >= 5) return;
            IReadOnlyList<PhaseTechDefinition> options = _phaseLoadoutPresenter.GetOptions(phaseIndex);
            if (optionIndex < 0 || optionIndex >= options.Count) return;
            PhaseTechDefinition selected = options[optionIndex];
            ISet<string> unlocked = GetUnlockedPhaseTechIds();
            if (selected.Kind != "Default" && !unlocked.Contains(selected.TechId))
            {
                _pendingUnlockPhaseIndex = phaseIndex;
                _pendingUnlockTechId = selected.TechId;
                _pendingUnlockHeroId = _phaseLoadoutPresenter.SelectedHeroId;
                _sceneBindingService?.SetLoadoutChipSelections(_loadoutChipIndices);
                int currency = _phaseProfileService?.Current.Currency ?? 0;
                _sceneBindingService?.ShowLoadoutUnlockConfirmation(
                    $"确认解锁 {selected.DisplayName}？\n费用 {selected.CostCurrency} 相位币 · 当前 {currency}");
                return;
            }

            if (!_phaseLoadoutPresenter.TrySelectTech(phaseIndex, optionIndex, unlocked, out string error))
            {
                _sceneBindingService?.SetLoadoutError(error);
                _sceneBindingService?.SetLoadoutChipSelections(_loadoutChipIndices);
                return;
            }

            _loadoutChipIndices[phaseIndex] = optionIndex;
            RefreshLoadoutOptions(false);
        }

        private async void ConfirmPendingPhaseUnlock()
        {
            if (_phaseLoadoutOperationInProgress || _pendingUnlockPhaseIndex < 0 ||
                string.IsNullOrEmpty(_pendingUnlockTechId) || _phaseLoadoutPresenter == null)
                return;

            int requestedPhaseIndex = _pendingUnlockPhaseIndex;
            string requestedTechId = _pendingUnlockTechId;
            string requestedHeroId = _pendingUnlockHeroId;
            bool forLan = _loadoutForLan;
            bool forBrickDuel = _loadoutForBrickDuel;
            RoomSnapshot requestedRoom = forLan ? _lanRoomService?.CurrentSnapshot : null;
            ulong requestedSessionId = requestedRoom?.SessionId ?? 0UL;
            uint requestedRoundId = requestedRoom?.RoundId ?? 0U;
            int operation = BeginPhaseLoadoutOperation();
            _sceneBindingService?.HideLoadoutUnlockConfirmation();
            try
            {
                bool unlocked = _phaseProfileService != null &&
                                await _phaseProfileService.UnlockAsync(requestedTechId);
                if (!IsPhaseLoadoutOperationCurrent(operation))
                    return;
                if (!IsLoadoutContextCurrent(forBrickDuel, forLan, requestedSessionId, requestedRoundId) ||
                    _phaseLoadoutPresenter.SelectedHeroId != requestedHeroId)
                {
                    ClearPendingPhaseUnlock();
                    return;
                }
                if (!unlocked)
                {
                    ClearPendingPhaseUnlock();
                    _sceneBindingService?.SetLoadoutError("相位币不足或保存失败，无法完成解锁。");
                    _sceneBindingService?.SetLoadoutChipSelections(_loadoutChipIndices);
                    return;
                }

                IReadOnlyList<PhaseTechDefinition> currentOptions = _phaseLoadoutPresenter.GetOptions(requestedPhaseIndex);
                int currentIndex = currentOptions
                    .Select((tech, index) => new { tech.TechId, Index = index })
                    .Where(item => item.TechId == requestedTechId)
                    .Select(item => item.Index)
                    .DefaultIfEmpty(-1)
                    .First();
                string selectionError = string.Empty;
                if (currentIndex < 0 || !_phaseLoadoutPresenter.TrySelectTech(
                        requestedPhaseIndex,
                        currentIndex,
                        GetUnlockedPhaseTechIds(),
                        out selectionError))
                {
                    ClearPendingPhaseUnlock();
                    _sceneBindingService?.SetLoadoutError(
                        currentIndex < 0 ? "相位科技选项已经变化，请重试。" : selectionError);
                    return;
                }

                _loadoutChipIndices[requestedPhaseIndex] = currentIndex;
                ClearPendingPhaseUnlock();
                RefreshLoadoutOptions(false);
            }
            catch (Exception exception)
            {
                if (IsPhaseLoadoutOperationCurrent(operation))
                {
                    ClearPendingPhaseUnlock();
                    _sceneBindingService?.SetLoadoutError("相位科技解锁失败：" + exception.Message);
                }
            }
            finally
            {
                CompletePhaseLoadoutOperation(operation);
            }
        }

        private void CancelPendingPhaseUnlock()
        {
            ClearPendingPhaseUnlock();
            _sceneBindingService?.SetLoadoutChipSelections(_loadoutChipIndices);
        }

        private void RestoreSavedLoadout(string heroId)
        {
            if (_phaseLoadoutPresenter == null || string.IsNullOrEmpty(heroId)) return;
            PhaseMatchLoadout saved = _phaseProfileService?.GetLoadout(heroId) ??
                                      PhaseMatchLoadout.CreateDefault(_modeCatalog, heroId);
            if (!_phaseLoadoutPresenter.TryApplyLoadout(saved, GetUnlockedPhaseTechIds(), out _))
                _phaseLoadoutPresenter.SelectHero(heroId);
        }

        private int BeginPhaseLoadoutOperation()
        {
            _phaseLoadoutOperationInProgress = true;
            _sceneBindingService?.SetPhaseLoadoutBusy(true);
            return ++_phaseLoadoutOperationVersion;
        }

        private bool IsPhaseLoadoutOperationCurrent(int operation) =>
            _phaseLoadoutOperationInProgress && operation == _phaseLoadoutOperationVersion;

        private void CompletePhaseLoadoutOperation(int operation)
        {
            if (operation == _phaseLoadoutOperationVersion)
            {
                _phaseLoadoutOperationInProgress = false;
                _sceneBindingService?.SetPhaseLoadoutBusy(false);
            }
        }

        private void InvalidatePhaseLoadoutOperation()
        {
            _phaseLoadoutOperationVersion++;
            _phaseLoadoutOperationInProgress = false;
            _sceneBindingService?.SetPhaseLoadoutBusy(false);
        }

        private void ClearPendingPhaseUnlock()
        {
            _pendingUnlockPhaseIndex = -1;
            _pendingUnlockTechId = string.Empty;
            _pendingUnlockHeroId = string.Empty;
            _sceneBindingService?.HideLoadoutUnlockConfirmation();
        }

        private ISet<string> GetUnlockedPhaseTechIds() =>
            _phaseProfileService?.Current.CreateUnlockedSet() ??
            new HashSet<string>(_modeCatalog.AllPhaseTechs.Values
                .Where(tech => tech.Kind == "Default").Select(tech => tech.TechId), StringComparer.Ordinal);

        private static V1MatchLoadout CreateLegacyCompatibilityLoadout() =>
            new V1MatchLoadout("HERO_FROST_QUEEN", "PATH_FROST_EXTREME",
                "SIG_FROST_DEEP_FREEZE_TOUCH",
                new[] { "STRIKE_SERVE", "GUARD_LENGTH" },
                new[] { "STRIKE_POWER", "GUARD_GOAL", "STRIKE_OVERCHARGE" });

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
            InvalidatePhaseLoadoutOperation();
            ClearPendingPhaseUnlock();
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
            bool showScorePanel = _startupUiState == StartupUiState.LocalPlaying || IsLanPlaying();
            _sceneBindingService.UpdateHud(snapshot, _lastServeBlockReason, showScorePanel);
            PlayerRuntimeState localPlayer = _runtime?.FindPlayer(_localPlayerId);
            HeroRuntimeState hero = localPlayer?.Hero;
            int milestone = hero?.PathStates?.FirstOrDefault()?.Level ?? 0;
            _sceneBindingService.SetHeroHud(hero == null || string.IsNullOrEmpty(hero.HeroId)
                ? string.Empty
                : $"路线：{hero.PathId}  专属：{hero.SignatureChipId}  M{milestone}  已激活：{hero.ActiveChipIds?.Count ?? 0}/5");
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
            return GetPrefabSceneHalfWidth() / Mathf.Max(0.001f, ArenaHalfWidth);
        }

        private float GetPrefabVisualYScale()
        {
            return GetPrefabSceneHalfHeight() / Mathf.Max(0.001f, ArenaHalfHeight);
        }

        private float GetPrefabSceneHalfWidth()
        {
            return _hasSceneVisualBounds
                ? Mathf.Max(0.001f, _sceneVisualBounds.extents.x)
                : Mathf.Max(0.001f, ArenaHalfWidth);
        }

        private float GetPrefabSceneHalfHeight()
        {
            return _hasSceneVisualBounds
                ? Mathf.Max(0.001f, _sceneVisualBounds.extents.y)
                : Mathf.Max(0.001f, ArenaHalfHeight);
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

            _prototypeCamera.orthographicSize = MainCameraOrthographicSize;
            _prototypeCamera.nearClipPlane = 0.1f;
            _prototypeCamera.farClipPlane = CameraHeight + 10f;
            _prototypeCamera.cullingMask &= ~(1 << GetSceneDebugLayer());
            _prototypeCamera.clearFlags = CameraClearFlags.SolidColor;
            _prototypeCamera.backgroundColor = new Color(0.03f, 0.04f, 0.05f);
        }

        private void ConfigureBrickDuelCamera()
        {
            if (_brickDuelRule == null)
            {
                return;
            }

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

            _prototypeCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _prototypeCamera.transform.position = new Vector3(0f, 0f, -CameraHeight);
            _prototypeCamera.transform.rotation = Quaternion.identity;
            _prototypeCamera.orthographic = true;
            _prototypeCamera.orthographicSize = CalculateBrickDuelOrthographicSize(_prototypeCamera.aspect);
            _prototypeCamera.nearClipPlane = 0.1f;
            _prototypeCamera.farClipPlane = CameraHeight + 10f;
            _prototypeCamera.cullingMask &= ~(1 << GetSceneDebugLayer());
            _prototypeCamera.clearFlags = CameraClearFlags.SolidColor;
            _prototypeCamera.backgroundColor = new Color(0.03f, 0.04f, 0.05f);
        }

        private static float CalculateBrickDuelOrthographicSize(float aspect)
        {
            float safeAspect = Mathf.Max(0.1f, aspect);
            return Mathf.Max(
                MainCameraOrthographicSize,
                BrickDuelMinimumVisibleHalfWidth / safeAspect);
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

        private Vector3 GetBallVisualScale(int ownerPlayerId)
        {
            if (_usePrefabVisuals)
            {
                int safePlayerId = Mathf.Clamp(ownerPlayerId, 1, 4);
                GatebreakerLoadedPrefab ballPrefab = _visualAssets?.GetBallForPlayerId(safePlayerId);
                if (ballPrefab?.Prefab != null)
                {
                    return ballPrefab.Prefab.transform.localScale;
                }
            }

            return GetCompensatedVisualScale(FallbackBallVisualWorldSize);
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
                    new Vector2(-GetPrefabSceneHalfWidth(), -GetPrefabSceneHalfHeight()),
                    new Vector2(-GetPrefabSceneHalfWidth(), GetPrefabSceneHalfHeight()),
                    new Vector2(GetPrefabSceneHalfWidth(), -GetPrefabSceneHalfHeight()),
                    new Vector2(GetPrefabSceneHalfWidth(), GetPrefabSceneHalfHeight()),
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
