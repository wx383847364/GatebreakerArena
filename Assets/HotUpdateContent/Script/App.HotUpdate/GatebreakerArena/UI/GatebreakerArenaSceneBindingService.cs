using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using App.HotUpdate.GatebreakerArena.BrickDuel;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Paddle;
using App.Shared.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class GatebreakerArenaSceneUiCallbacks
    {
        public Action ServeRequested { get; set; }
        public Action LocalBattleRequested { get; set; }
        public Action OnlineBattleRequested { get; set; }
        public Action SingleBattleRequested { get; set; }
        public Action BrickDuelRequested { get; set; }
        public Action SingleSelectBackRequested { get; set; }
        public Action BrickDuelPauseRequested { get; set; }
        public Action<int> LoadoutHeroChanged { get; set; }
        public Action<int> LoadoutPathChanged { get; set; }
        public Action<int> LoadoutSignatureChanged { get; set; }
        public Action<int, int> LoadoutUniversalChipChanged { get; set; }
        public Action LoadoutUseDefaultRequested { get; set; }
        public Action LoadoutConfirmRequested { get; set; }
        public Action CreateLanHostRequested { get; set; }
        public Action StartLanDiscoveryRequested { get; set; }
        public Action JoinLanRoomRequested { get; set; }
        public Action ToggleLanReadyRequested { get; set; }
        public Action StartLanLoadingRequested { get; set; }
        public Action LeaveLanRoomRequested { get; set; }
        public Action AcknowledgeLanStartRequested { get; set; }
        public Action<string> LanPlayerNameChanged { get; set; }
        public Action<int> LanRoomPlayerCountChanged { get; set; }
        public Action<string> LanRoomCodeChanged { get; set; }
        public Action<float> MoveAxisChanged { get; set; }
        public Action<float> BrickDuelMoveAxisChanged { get; set; }
        public Action<int> HitOffsetInfluenceChanged { get; set; }
        public Action<int> PaddleVelocityInfluenceChanged { get; set; }
        public Action<int> MinimumOutwardShareChanged { get; set; }
        public Action RestartMatchRequested { get; set; }
        public Action ResultBackRequested { get; set; }
        public string InitialLanPlayerName { get; set; }
        public int InitialLanRoomPlayerCount { get; set; } = 2;
        public string InitialLanRoomCode { get; set; }
    }

    public sealed class GatebreakerArenaSceneBindingService
    {
        private const float BrickDuelCoreHitFlashDurationSeconds = 0.14f;
        private readonly List<ButtonListener> _buttonListeners = new List<ButtonListener>();
        private readonly List<InputListener> _inputListeners = new List<InputListener>();
        private readonly List<DropdownListener> _dropdownListeners = new List<DropdownListener>();
        private readonly List<SliderListener> _sliderListeners = new List<SliderListener>();
        private readonly List<EventTriggerListener> _eventTriggerListeners = new List<EventTriggerListener>();
        private Button _skillButton;
        private Button _localBattleButton;
        private Button _onlineBattleButton;
        private Button _singleBattleButton;
        private Button _brickDuel1v1Button;
        private Button _brickDuel1v2Button;
        private Button _brickDuel1v3Button;
        private Button _singleSelectBackButton;
        private TMP_Text _singleSelectTitleText;
        private GameObject _singleSelectRoot;
        private GameObject _brickDuelHudRoot;
        private TMP_Text _brickDuelOpponentHealthText;
        private TMP_Text _brickDuelPlayerHealthText;
        private TMP_Text _brickDuelCenterText;
        private TMP_Text _brickDuelStatusText;
        private Graphic _brickDuelBottomCoreHitFeedback;
        private Graphic _brickDuelTopCoreHitFeedback;
        private Button _brickDuelPauseButton;
        private RectTransform _brickDuelMovementPad;
        private RectTransform _brickDuelMovementHandle;
        private RectTransform _brickDuelMovementLeftArrowInput;
        private RectTransform _brickDuelMovementRightArrowInput;
        private Graphic _brickDuelMovementLeftArrowHighlight;
        private Graphic _brickDuelMovementRightArrowHighlight;
        private Vector2 _brickDuelMovementHandleRestPosition;
        private bool _hasBrickDuelMovementHandleRestPosition;
        private Color _brickDuelMovementLeftArrowRestColor;
        private Color _brickDuelMovementRightArrowRestColor;
        private bool _hasBrickDuelMovementLeftArrowRestColor;
        private bool _hasBrickDuelMovementRightArrowRestColor;
        private bool _brickDuelPressurePulsePendingReset;
        private bool _brickDuelImpactPulsePendingReset;
        private bool _brickDuelBottomCoreFlashPendingReset;
        private bool _brickDuelTopCoreFlashPendingReset;
        private float _brickDuelBottomCoreFlashUntil;
        private float _brickDuelTopCoreFlashUntil;
        private Vector3 _brickDuelCenterTextRestScale = Vector3.one;
        private Vector3 _brickDuelStatusTextRestScale = Vector3.one;
        private Color _brickDuelStatusTextRestColor = Color.white;
        private int _brickDuelLastPressureLevel = -1;
        private int _brickDuelLastBottomHealth = -1;
        private int _brickDuelLastTopHealth = -1;
        private GameObject _loadoutRoot;
        private TMP_Dropdown _loadoutHeroDropdown;
        private TMP_Dropdown _loadoutPathDropdown;
        private TMP_Dropdown _loadoutSignatureDropdown;
        private TMP_Dropdown[] _loadoutUniversalChipDropdowns = Array.Empty<TMP_Dropdown>();
        private Button _loadoutUseDefaultButton;
        private Button _loadoutConfirmButton;
        private TMP_Text _loadoutErrorText;
        private TMP_Text _heroHudText;
        private Button _lanCreateButton;
        private Button _lanBackButton;
        private Button _lanDiscoverButton;
        private Button _lanJoinButton;
        private Button _lanReadyButton;
        private Button _lanStartButton;
        private Button _lanLeaveButton;
        private Button _lanAcknowledgeStartButton;
        private TMP_InputField _lanPlayerNameInput;
        private TMP_Dropdown _lanRoomTypeDropdown;
        private TMP_InputField _lanRoomCodeInput;
        private RectTransform _movementPad;
        private RectTransform _movementHandle;
        private RectTransform _movementLeftArrowInput;
        private RectTransform _movementRightArrowInput;
        private Graphic _movementLeftArrowHighlight;
        private Graphic _movementRightArrowHighlight;
        private Vector2 _movementHandleRestPosition;
        private bool _hasMovementHandleRestPosition;
        private Color _movementLeftArrowRestColor;
        private Color _movementRightArrowRestColor;
        private bool _hasMovementLeftArrowRestColor;
        private bool _hasMovementRightArrowRestColor;
        private TMP_Text _ballCountText;
        private TMP_Text _hudTitleText;
        private TMP_Text _hudStatusText;
        private TMP_Text _hudScoreText;
        private TMP_Text _hudServeText;
        private TMP_Text _hudBallText;
        private TMP_Text _timeText;
        private TMP_Text[] _playerScoreTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _playerHitTexts = Array.Empty<TMP_Text>();
        private TMP_Text _topPanel2PTimeText;
        private TMP_Text _topPanel3PTimeText;
        private TMP_Text _topPanel4PTimeText;
        private TMP_Text[] _playerScore2PTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _playerHit2PTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _playerScore3PTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _playerHit3PTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _playerScore4PTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _playerHit4PTexts = Array.Empty<TMP_Text>();
        private TMP_Text _resultTitleText;
        private TMP_Text _resultBodyText;
        private TMP_Text _resultScoreText;
        private TMP_Text[] _resultRankLabelTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _resultRankNameTexts = Array.Empty<TMP_Text>();
        private Button _resultRestartButton;
        private Button _resultBackButton;
        private TMP_Text _gmHitOffsetValueText;
        private TMP_Text _gmPaddleVelocityValueText;
        private TMP_Text _gmMinimumOutwardValueText;
        private TMP_Text _lanStateText;
        private TMP_Text _lanRoomCodeText;
        private TMP_Text _lanPlayerCountText;
        private TMP_Text _lanLocalIpText;
        private TMP_Text _lanRoomIpText;
        private TMP_Text _lanErrorText;
        private TMP_Text[] _lanRoomPlayerInfoTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _lanRoomPlayerNameTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _lanRoomPlayerReadyTexts = Array.Empty<TMP_Text>();
        private Slider _gmHitOffsetSlider;
        private Slider _gmPaddleVelocitySlider;
        private Slider _gmMinimumOutwardSlider;
        private GameObject _hudRoot;
        private GameObject _topPanel2PRoot;
        private GameObject _topPanel3PRoot;
        private GameObject _topPanel4PRoot;
        private GameObject _resultRoot;
        private GameObject _gmRoot;
        private GameObject _lanRoot;
        private GameObject _modeSelectRoot;
        private GameObject _lanMenuRoot;
        private GameObject _lanRoomInfoRoot;
        private GameObject _lanStatusRoot;
        private GameObject _startCountdownRoot;
        private TMP_Text _startCountdownText;
        private GatebreakerArenaSceneUiCallbacks _callbacks;
        private IAppLogger _logger;
        private bool _suppressSliderEvents;
        private string _lastBallCountText;

        public bool IsBound { get; private set; }
        public bool HasSkillButtonBinding => _skillButton != null;
        public bool HasBallCountTextBinding => _ballCountText != null;
        public bool HasGmSliderBindings =>
            _gmHitOffsetSlider != null &&
            _gmPaddleVelocitySlider != null &&
            _gmMinimumOutwardSlider != null;

        public bool HasPlayerScorePanelBindings =>
            _topPanel2PRoot != null &&
            _topPanel3PRoot != null &&
            _topPanel4PRoot != null &&
            _topPanel2PTimeText != null &&
            _topPanel3PTimeText != null &&
            _topPanel4PTimeText != null &&
            HasExactTextArrayBindings(_playerScore2PTexts, 2) &&
            HasExactTextArrayBindings(_playerHit2PTexts, 2) &&
            HasExactTextArrayBindings(_playerScore3PTexts, 3) &&
            HasExactTextArrayBindings(_playerHit3PTexts, 3) &&
            HasExactTextArrayBindings(_playerScore4PTexts, 4) &&
            HasExactTextArrayBindings(_playerHit4PTexts, 4);

        public bool HasLanButtonBindings =>
            _lanCreateButton != null &&
            _lanBackButton != null &&
            _lanDiscoverButton != null &&
            _lanJoinButton != null &&
            _lanReadyButton != null &&
            _lanStartButton != null &&
            _lanLeaveButton != null &&
            _lanAcknowledgeStartButton != null;
        public bool HasLoadoutBindings => _loadoutRoot != null && _loadoutHeroDropdown != null &&
            _loadoutPathDropdown != null && _loadoutSignatureDropdown != null &&
            _loadoutUniversalChipDropdowns.Length == 5 && _loadoutUniversalChipDropdowns.All(item => item != null) &&
            _loadoutUseDefaultButton != null && _loadoutConfirmButton != null && _loadoutErrorText != null;

        public bool HasBrickDuelBindings =>
            _singleBattleButton != null &&
            _singleSelectRoot != null &&
            _brickDuel1v1Button != null &&
            _singleSelectBackButton != null &&
            _brickDuelHudRoot != null &&
            _brickDuelOpponentHealthText != null &&
            _brickDuelPlayerHealthText != null &&
            _brickDuelCenterText != null &&
            _brickDuelStatusText != null &&
            _brickDuelBottomCoreHitFeedback != null &&
            _brickDuelTopCoreHitFeedback != null &&
            _brickDuelPauseButton != null &&
            _brickDuelMovementPad != null &&
            _brickDuelMovementHandle != null;

        public void Bind(
            IGatebreakerArenaSceneUiBinding binding,
            Action serveRequested,
            IAppLogger logger)
        {
            Bind(
                binding,
                new GatebreakerArenaSceneUiCallbacks { ServeRequested = serveRequested },
                logger);
        }

        public void Bind(
            IGatebreakerArenaSceneUiBinding binding,
            GatebreakerArenaSceneUiCallbacks callbacks,
            IAppLogger logger)
        {
            Clear();
            _callbacks = callbacks ?? new GatebreakerArenaSceneUiCallbacks();
            _logger = logger;
            IsBound = true;

            if (binding == null)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: scene UI binding is missing.");
                return;
            }

            _skillButton = Require<Button>(binding.SkillButtonObject, nameof(binding.SkillButtonObject));
            _ballCountText = Require<TMP_Text>(binding.BallCountTextObject, nameof(binding.BallCountTextObject));
            _movementPad = Require<RectTransform>(binding.MovementPadObject, nameof(binding.MovementPadObject));
            _movementHandle = Require<RectTransform>(binding.MovementHandleObject, nameof(binding.MovementHandleObject));
            _movementLeftArrowInput = Require<RectTransform>(binding.MovementLeftArrowInputObject, nameof(binding.MovementLeftArrowInputObject));
            _movementRightArrowInput = Require<RectTransform>(binding.MovementRightArrowInputObject, nameof(binding.MovementRightArrowInputObject));
            _movementLeftArrowHighlight = Require<Graphic>(binding.MovementLeftArrowHighlightObject, nameof(binding.MovementLeftArrowHighlightObject));
            _movementRightArrowHighlight = Require<Graphic>(binding.MovementRightArrowHighlightObject, nameof(binding.MovementRightArrowHighlightObject));
            if (_movementHandle != null)
            {
                _movementHandleRestPosition = _movementHandle.anchoredPosition;
                _hasMovementHandleRestPosition = true;
            }
            CaptureMovementArrowRestColors();

            _hudRoot = OptionalGameObject(binding.HudRootObject);
            _hudTitleText = Optional<TMP_Text>(binding.HudTitleTextObject);
            _hudStatusText = Optional<TMP_Text>(binding.HudStatusTextObject);
            _hudScoreText = Optional<TMP_Text>(binding.HudScoreTextObject);
            _hudServeText = Optional<TMP_Text>(binding.HudServeTextObject);
            _hudBallText = Optional<TMP_Text>(binding.HudBallTextObject);
            _timeText = Require<TMP_Text>(binding.TimeTextObject, nameof(binding.TimeTextObject));
            _playerScoreTexts = RequireTextArray(binding.PlayerScoreTextObjects, nameof(binding.PlayerScoreTextObjects));
            _playerHitTexts = RequireTextArray(binding.PlayerHitTextObjects, nameof(binding.PlayerHitTextObjects));
            _topPanel2PRoot = RequireGameObject(binding.TopPanel2PRootObject, nameof(binding.TopPanel2PRootObject));
            _topPanel3PRoot = RequireGameObject(binding.TopPanel3PRootObject, nameof(binding.TopPanel3PRootObject));
            _topPanel4PRoot = RequireGameObject(binding.TopPanel4PRootObject, nameof(binding.TopPanel4PRootObject));
            _topPanel2PTimeText = Require<TMP_Text>(binding.TopPanel2PTimeTextObject, nameof(binding.TopPanel2PTimeTextObject));
            _topPanel3PTimeText = Require<TMP_Text>(binding.TopPanel3PTimeTextObject, nameof(binding.TopPanel3PTimeTextObject));
            _topPanel4PTimeText = Require<TMP_Text>(binding.TopPanel4PTimeTextObject, nameof(binding.TopPanel4PTimeTextObject));
            _playerScore2PTexts = RequireTextArray(binding.PlayerScore2PTextObjects, nameof(binding.PlayerScore2PTextObjects));
            _playerHit2PTexts = RequireTextArray(binding.PlayerHit2PTextObjects, nameof(binding.PlayerHit2PTextObjects));
            _playerScore3PTexts = RequireTextArray(binding.PlayerScore3PTextObjects, nameof(binding.PlayerScore3PTextObjects));
            _playerHit3PTexts = RequireTextArray(binding.PlayerHit3PTextObjects, nameof(binding.PlayerHit3PTextObjects));
            _playerScore4PTexts = RequireTextArray(binding.PlayerScore4PTextObjects, nameof(binding.PlayerScore4PTextObjects));
            _playerHit4PTexts = RequireTextArray(binding.PlayerHit4PTextObjects, nameof(binding.PlayerHit4PTextObjects));
            _resultRoot = RequireGameObject(binding.ResultRootObject, nameof(binding.ResultRootObject));
            _resultTitleText = Require<TMP_Text>(binding.ResultTitleTextObject, nameof(binding.ResultTitleTextObject));
            _resultBodyText = Optional<TMP_Text>(binding.ResultBodyTextObject);
            _resultScoreText = Optional<TMP_Text>(binding.ResultScoreTextObject);
            _resultRankLabelTexts = RequireTextArray(binding.ResultRankLabelTextObjects, nameof(binding.ResultRankLabelTextObjects));
            _resultRankNameTexts = RequireTextArray(binding.ResultRankNameTextObjects, nameof(binding.ResultRankNameTextObjects));
            _resultRestartButton = Require<Button>(binding.ResultRestartButtonObject, nameof(binding.ResultRestartButtonObject));
            _resultBackButton = Require<Button>(binding.ResultBackButtonObject, nameof(binding.ResultBackButtonObject));
            _gmRoot = RequireGameObject(binding.GmRootObject, nameof(binding.GmRootObject));
            _gmHitOffsetSlider = Require<Slider>(binding.GmHitOffsetSliderObject, nameof(binding.GmHitOffsetSliderObject));
            _gmHitOffsetValueText = Require<TMP_Text>(binding.GmHitOffsetValueTextObject, nameof(binding.GmHitOffsetValueTextObject));
            _gmPaddleVelocitySlider = Require<Slider>(binding.GmPaddleVelocitySliderObject, nameof(binding.GmPaddleVelocitySliderObject));
            _gmPaddleVelocityValueText = Require<TMP_Text>(binding.GmPaddleVelocityValueTextObject, nameof(binding.GmPaddleVelocityValueTextObject));
            _gmMinimumOutwardSlider = Require<Slider>(binding.GmMinimumOutwardSliderObject, nameof(binding.GmMinimumOutwardSliderObject));
            _gmMinimumOutwardValueText = Require<TMP_Text>(binding.GmMinimumOutwardValueTextObject, nameof(binding.GmMinimumOutwardValueTextObject));
            _lanRoot = RequireGameObject(binding.LanRootObject, nameof(binding.LanRootObject));
            _modeSelectRoot = RequireGameObject(binding.ModeSelectRootObject, nameof(binding.ModeSelectRootObject));
            _localBattleButton = Require<Button>(binding.LocalBattleButtonObject, nameof(binding.LocalBattleButtonObject));
            _onlineBattleButton = Require<Button>(binding.OnlineBattleButtonObject, nameof(binding.OnlineBattleButtonObject));
            _singleBattleButton = Require<Button>(binding.SingleBattleButtonObject, nameof(binding.SingleBattleButtonObject));
            _singleSelectRoot = RequireGameObject(binding.SingleSelectRootObject, nameof(binding.SingleSelectRootObject));
            _singleSelectTitleText = Require<TMP_Text>(binding.SingleSelectTitleTextObject, nameof(binding.SingleSelectTitleTextObject));
            _brickDuel1v1Button = Require<Button>(binding.BrickDuel1v1ButtonObject, nameof(binding.BrickDuel1v1ButtonObject));
            _brickDuel1v2Button = Require<Button>(binding.BrickDuel1v2ButtonObject, nameof(binding.BrickDuel1v2ButtonObject));
            _brickDuel1v3Button = Require<Button>(binding.BrickDuel1v3ButtonObject, nameof(binding.BrickDuel1v3ButtonObject));
            _singleSelectBackButton = Require<Button>(binding.SingleSelectBackButtonObject, nameof(binding.SingleSelectBackButtonObject));
            _brickDuelHudRoot = RequireGameObject(binding.BrickDuelHudRootObject, nameof(binding.BrickDuelHudRootObject));
            _brickDuelOpponentHealthText = Require<TMP_Text>(
                binding.BrickDuelOpponentHealthTextObject,
                nameof(binding.BrickDuelOpponentHealthTextObject));
            _brickDuelPlayerHealthText = Require<TMP_Text>(
                binding.BrickDuelPlayerHealthTextObject,
                nameof(binding.BrickDuelPlayerHealthTextObject));
            _brickDuelCenterText = Require<TMP_Text>(
                binding.BrickDuelCenterTextObject,
                nameof(binding.BrickDuelCenterTextObject));
            _brickDuelStatusText = Require<TMP_Text>(
                binding.BrickDuelStatusTextObject,
                nameof(binding.BrickDuelStatusTextObject));
            _brickDuelBottomCoreHitFeedback = Require<Graphic>(
                binding.BrickDuelBottomCoreHitFeedbackObject,
                nameof(binding.BrickDuelBottomCoreHitFeedbackObject));
            _brickDuelTopCoreHitFeedback = Require<Graphic>(
                binding.BrickDuelTopCoreHitFeedbackObject,
                nameof(binding.BrickDuelTopCoreHitFeedbackObject));
            _brickDuelPauseButton = Require<Button>(
                binding.BrickDuelPauseButtonObject,
                nameof(binding.BrickDuelPauseButtonObject));
            _brickDuelCenterTextRestScale = _brickDuelCenterText.rectTransform.localScale;
            _brickDuelStatusTextRestScale = _brickDuelStatusText.rectTransform.localScale;
            _brickDuelStatusTextRestColor = _brickDuelStatusText.color;
            SetActive(_brickDuelBottomCoreHitFeedback.gameObject, false);
            SetActive(_brickDuelTopCoreHitFeedback.gameObject, false);
            _brickDuelMovementPad = Require<RectTransform>(
                binding.BrickDuelMovementPadObject,
                nameof(binding.BrickDuelMovementPadObject));
            _brickDuelMovementHandle = Require<RectTransform>(
                binding.BrickDuelMovementHandleObject,
                nameof(binding.BrickDuelMovementHandleObject));
            _brickDuelMovementLeftArrowInput = Require<RectTransform>(
                binding.BrickDuelMovementLeftArrowInputObject,
                nameof(binding.BrickDuelMovementLeftArrowInputObject));
            _brickDuelMovementRightArrowInput = Require<RectTransform>(
                binding.BrickDuelMovementRightArrowInputObject,
                nameof(binding.BrickDuelMovementRightArrowInputObject));
            _brickDuelMovementLeftArrowHighlight = Require<Graphic>(
                binding.BrickDuelMovementLeftArrowHighlightObject,
                nameof(binding.BrickDuelMovementLeftArrowHighlightObject));
            _brickDuelMovementRightArrowHighlight = Require<Graphic>(
                binding.BrickDuelMovementRightArrowHighlightObject,
                nameof(binding.BrickDuelMovementRightArrowHighlightObject));
            if (_brickDuelMovementHandle != null)
            {
                _brickDuelMovementHandleRestPosition = _brickDuelMovementHandle.anchoredPosition;
                _hasBrickDuelMovementHandleRestPosition = true;
            }
            CaptureBrickDuelMovementArrowRestColors();
            _loadoutRoot = RequireGameObject(binding.LoadoutRootObject, nameof(binding.LoadoutRootObject));
            _loadoutHeroDropdown = Require<TMP_Dropdown>(binding.LoadoutHeroDropdownObject, nameof(binding.LoadoutHeroDropdownObject));
            _loadoutPathDropdown = Require<TMP_Dropdown>(binding.LoadoutPathDropdownObject, nameof(binding.LoadoutPathDropdownObject));
            _loadoutSignatureDropdown = Require<TMP_Dropdown>(binding.LoadoutSignatureDropdownObject, nameof(binding.LoadoutSignatureDropdownObject));
            _loadoutUniversalChipDropdowns = RequireDropdownArray(binding.LoadoutUniversalChipDropdownObjects, nameof(binding.LoadoutUniversalChipDropdownObjects));
            _loadoutUseDefaultButton = Require<Button>(binding.LoadoutUseDefaultButtonObject, nameof(binding.LoadoutUseDefaultButtonObject));
            _loadoutConfirmButton = Require<Button>(binding.LoadoutConfirmButtonObject, nameof(binding.LoadoutConfirmButtonObject));
            _loadoutErrorText = Require<TMP_Text>(binding.LoadoutErrorTextObject, nameof(binding.LoadoutErrorTextObject));
            _heroHudText = Require<TMP_Text>(binding.HeroHudTextObject, nameof(binding.HeroHudTextObject));
            _lanMenuRoot = RequireGameObject(binding.LanMenuRootObject, nameof(binding.LanMenuRootObject));
            _lanRoomInfoRoot = RequireGameObject(binding.LanRoomInfoRootObject, nameof(binding.LanRoomInfoRootObject));
            _lanStatusRoot = RequireGameObject(binding.LanStatusRootObject, nameof(binding.LanStatusRootObject));
            _lanBackButton = Require<Button>(binding.LanBackButtonObject, nameof(binding.LanBackButtonObject));
            _lanCreateButton = Require<Button>(binding.LanCreateButtonObject, nameof(binding.LanCreateButtonObject));
            _lanDiscoverButton = Require<Button>(binding.LanDiscoverButtonObject, nameof(binding.LanDiscoverButtonObject));
            _lanJoinButton = Require<Button>(binding.LanJoinButtonObject, nameof(binding.LanJoinButtonObject));
            _lanReadyButton = Require<Button>(binding.LanReadyButtonObject, nameof(binding.LanReadyButtonObject));
            _lanStartButton = Require<Button>(binding.LanStartButtonObject, nameof(binding.LanStartButtonObject));
            _lanLeaveButton = Require<Button>(binding.LanLeaveButtonObject, nameof(binding.LanLeaveButtonObject));
            _lanAcknowledgeStartButton = Require<Button>(binding.LanAcknowledgeStartButtonObject, nameof(binding.LanAcknowledgeStartButtonObject));
            _lanPlayerNameInput = Require<TMP_InputField>(binding.LanPlayerNameInputObject, nameof(binding.LanPlayerNameInputObject));
            _lanRoomTypeDropdown = Require<TMP_Dropdown>(binding.LanRoomTypeDropdownObject, nameof(binding.LanRoomTypeDropdownObject));
            _lanRoomCodeInput = Require<TMP_InputField>(binding.LanRoomCodeInputObject, nameof(binding.LanRoomCodeInputObject));
            _lanStateText = Require<TMP_Text>(binding.LanStateTextObject, nameof(binding.LanStateTextObject));
            _lanRoomCodeText = Require<TMP_Text>(binding.LanRoomCodeTextObject, nameof(binding.LanRoomCodeTextObject));
            _lanPlayerCountText = Require<TMP_Text>(binding.LanPlayerCountTextObject, nameof(binding.LanPlayerCountTextObject));
            _lanLocalIpText = Require<TMP_Text>(binding.LanLocalIpTextObject, nameof(binding.LanLocalIpTextObject));
            _lanRoomIpText = Require<TMP_Text>(binding.LanRoomIpTextObject, nameof(binding.LanRoomIpTextObject));
            _lanErrorText = Require<TMP_Text>(binding.LanErrorTextObject, nameof(binding.LanErrorTextObject));
            _startCountdownRoot = RequireGameObject(binding.StartCountdownRootObject, nameof(binding.StartCountdownRootObject));
            _startCountdownText = Require<TMP_Text>(binding.StartCountdownTextObject, nameof(binding.StartCountdownTextObject));
            _lanRoomPlayerInfoTexts = OptionalTextArray(binding.LanRoomPlayerInfoTextObjects, nameof(binding.LanRoomPlayerInfoTextObjects));
            _lanRoomPlayerNameTexts = OptionalTextArray(binding.LanRoomPlayerNameTextObjects, nameof(binding.LanRoomPlayerNameTextObjects), false);
            _lanRoomPlayerReadyTexts = OptionalTextArray(binding.LanRoomPlayerReadyTextObjects, nameof(binding.LanRoomPlayerReadyTextObjects), false);
            ResolveLanRoomNativeChildTextBindings();

            AddButtonListener(_skillButton, HandleSkillButtonClicked);
            AddButtonListener(_localBattleButton, () => _callbacks.LocalBattleRequested?.Invoke());
            AddButtonListener(_onlineBattleButton, () => _callbacks.OnlineBattleRequested?.Invoke());
            AddButtonListener(_singleBattleButton, () => _callbacks.SingleBattleRequested?.Invoke());
            AddButtonListener(_brickDuel1v1Button, () => _callbacks.BrickDuelRequested?.Invoke());
            AddButtonListener(_singleSelectBackButton, () => _callbacks.SingleSelectBackRequested?.Invoke());
            AddButtonListener(_brickDuelPauseButton, () => _callbacks.BrickDuelPauseRequested?.Invoke());
            AddButtonListener(_loadoutUseDefaultButton, () => _callbacks.LoadoutUseDefaultRequested?.Invoke());
            AddButtonListener(_loadoutConfirmButton, () => _callbacks.LoadoutConfirmRequested?.Invoke());
            AddDropdownListener(_loadoutHeroDropdown, 0, value => _callbacks.LoadoutHeroChanged?.Invoke(value));
            AddDropdownListener(_loadoutPathDropdown, 0, value => _callbacks.LoadoutPathChanged?.Invoke(value));
            AddDropdownListener(_loadoutSignatureDropdown, 0, value => _callbacks.LoadoutSignatureChanged?.Invoke(value));
            for (int i = 0; i < _loadoutUniversalChipDropdowns.Length; i++)
            {
                int slot = i;
                AddDropdownListener(_loadoutUniversalChipDropdowns[i], i, value => _callbacks.LoadoutUniversalChipChanged?.Invoke(slot, value));
            }
            AddButtonListener(_lanBackButton, () => _callbacks.LeaveLanRoomRequested?.Invoke());
            AddButtonListener(_lanCreateButton, () => _callbacks.CreateLanHostRequested?.Invoke());
            AddButtonListener(_lanDiscoverButton, () => _callbacks.StartLanDiscoveryRequested?.Invoke());
            AddButtonListener(_lanJoinButton, () => _callbacks.JoinLanRoomRequested?.Invoke());
            AddButtonListener(_lanReadyButton, () => _callbacks.ToggleLanReadyRequested?.Invoke());
            AddButtonListener(_lanStartButton, () => _callbacks.StartLanLoadingRequested?.Invoke());
            AddButtonListener(_lanLeaveButton, () => _callbacks.LeaveLanRoomRequested?.Invoke());
            AddButtonListener(_lanAcknowledgeStartButton, () => _callbacks.AcknowledgeLanStartRequested?.Invoke());
            AddButtonListener(_resultRestartButton, () => _callbacks.RestartMatchRequested?.Invoke());
            AddButtonListener(_resultBackButton, () => _callbacks.ResultBackRequested?.Invoke());
            AddMovementListeners(_movementPad);
            AddMovementListeners(_movementHandle);
            AddFixedMovementListeners(_movementLeftArrowInput, -1f);
            AddFixedMovementListeners(_movementRightArrowInput, 1f);
            AddBrickDuelMovementListeners(_brickDuelMovementPad);
            AddBrickDuelMovementListeners(_brickDuelMovementHandle);
            AddBrickDuelFixedMovementListeners(_brickDuelMovementLeftArrowInput, -1f);
            AddBrickDuelFixedMovementListeners(_brickDuelMovementRightArrowInput, 1f);
            AddInputListener(_lanPlayerNameInput, _callbacks.InitialLanPlayerName, value => _callbacks.LanPlayerNameChanged?.Invoke(value));
            AddDropdownListener(
                _lanRoomTypeDropdown,
                ResolveLanRoomTypeIndex(_lanRoomTypeDropdown, _callbacks.InitialLanRoomPlayerCount),
                value => _callbacks.LanRoomPlayerCountChanged?.Invoke(ResolveLanRoomTypePlayerCount(_lanRoomTypeDropdown, value)));
            AddInputListener(_lanRoomCodeInput, _callbacks.InitialLanRoomCode, value => _callbacks.LanRoomCodeChanged?.Invoke(value));
            ConfigureSlider(
                _gmHitOffsetSlider,
                PaddleBounceTuning.HitOffsetInfluenceMin,
                PaddleBounceTuning.HitOffsetInfluenceMax,
                value => _callbacks.HitOffsetInfluenceChanged?.Invoke(value));
            ConfigureSlider(
                _gmPaddleVelocitySlider,
                PaddleBounceTuning.PaddleVelocityInfluenceMin,
                PaddleBounceTuning.PaddleVelocityInfluenceMax,
                value => _callbacks.PaddleVelocityInfluenceChanged?.Invoke(value));
            ConfigureSlider(
                _gmMinimumOutwardSlider,
                PaddleBounceTuning.MinimumOutwardShareMin,
                PaddleBounceTuning.MinimumOutwardShareMax,
                value => _callbacks.MinimumOutwardShareChanged?.Invoke(value));

            SetActive(_hudRoot, true);
            SetActive(_gmRoot, true);
            _brickDuel1v2Button.interactable = false;
            _brickDuel1v3Button.interactable = false;
            ShowModeSelect();
            SetActive(_resultRoot, false);
        }

        public void ConfigureLoadout(IReadOnlyList<string> heroes, IReadOnlyList<string> paths,
            IReadOnlyList<string> signatures, IReadOnlyList<string> universalChips)
        {
            SetDropdownOptions(_loadoutHeroDropdown, heroes);
            SetDropdownOptions(_loadoutPathDropdown, paths);
            SetDropdownOptions(_loadoutSignatureDropdown, signatures);
            for (int i = 0; i < _loadoutUniversalChipDropdowns.Length; i++)
                SetDropdownOptions(_loadoutUniversalChipDropdowns[i], universalChips, i);
        }

        public void UpdateLoadoutPaths(IReadOnlyList<string> paths, IReadOnlyList<string> signatures)
        {
            SetDropdownOptions(_loadoutPathDropdown, paths);
            SetDropdownOptions(_loadoutSignatureDropdown, signatures);
        }

        public void UpdateLoadoutSignatures(IReadOnlyList<string> signatures) => SetDropdownOptions(_loadoutSignatureDropdown, signatures);
        public void SetLoadoutChipSelections(IReadOnlyList<int> indices)
        {
            for (int i = 0; i < _loadoutUniversalChipDropdowns.Length; i++)
            {
                TMP_Dropdown dropdown = _loadoutUniversalChipDropdowns[i];
                int value = indices != null && i < indices.Count ? indices[i] : i;
                dropdown?.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, (dropdown?.options.Count ?? 1) - 1)));
                dropdown?.RefreshShownValue();
            }
        }
        public void ShowLoadout() { SetActive(_modeSelectRoot, false); SetActive(_loadoutRoot, true); SetText(_loadoutErrorText, string.Empty); }
        public void SetLoadoutError(string message) => SetText(_loadoutErrorText, message ?? string.Empty);
        public void SetHeroHud(string text) => SetText(_heroHudText, text ?? string.Empty);

        public void MarkBound()
        {
            IsBound = true;
        }

        public void UpdateHud(GatebreakerHudSnapshot snapshot, ServeBlockReason lastServeBlockReason)
        {
            UpdateBallCount(snapshot);
            if (snapshot == null)
            {
                UpdateTimeText(0f);
                UpdatePlayerScorePanel(null);
                return;
            }

            UpdateTimeText(snapshot.RemainingTime);
            SetText(_hudTitleText, "Gatebreaker Arena 原型");
            SetText(_hudStatusText, $"阶段：{FormatPhase(snapshot.Phase)}    时间：{FormatTime(snapshot.RemainingTime)}");
            SetText(_hudScoreText, $"比分：{FormatScoreLine(snapshot)}");
            UpdatePlayerScorePanel(snapshot);
            SetText(
                _hudServeText,
                $"弹药：{snapshot.CurrentServeAmmo}/{snapshot.MaxServeAmmo}    回复：{snapshot.ServeCooldownRemaining:0.0}秒");
            SetText(
                _hudBallText,
                $"场上球：{snapshot.OwnedBallsInField}/{snapshot.MaxOwnedBallsInField}    发球限制：{FormatServeBlockReason(snapshot.ServeBlockReason)}    上次空格：{FormatServeBlockReason(lastServeBlockReason)}");

            if (_skillButton != null)
            {
                _skillButton.interactable = snapshot.Phase != MatchPhase.Result;
            }
        }

        public void UpdateBallCount(GatebreakerHudSnapshot snapshot)
        {
            if (_ballCountText == null || snapshot == null)
            {
                return;
            }

            string countText = snapshot.CurrentServeAmmo.ToString(CultureInfo.InvariantCulture);
            if (countText == _lastBallCountText)
            {
                return;
            }

            _ballCountText.text = countText;
            _lastBallCountText = countText;
        }

        public void UpdateResult(GatebreakerHudSnapshot snapshot)
        {
            bool isResult = snapshot != null && snapshot.Phase == MatchPhase.Result;
            SetActive(_resultRoot, isResult);
            if (!isResult)
            {
                return;
            }

            SetText(_resultTitleText, "比赛结束");
            SetText(_resultBodyText, BuildWinnerText(snapshot));
            SetText(_resultScoreText, BuildScoreRows(snapshot) + "\n按 R 重新开始");
            SetActive(_resultScoreText != null ? _resultScoreText.gameObject : null, true);
            SetTextObjectsActive(_resultRankLabelTexts, true);
            SetTextObjectsActive(_resultRankNameTexts, true);
            UpdateResultRanking(snapshot);
        }

        public void UpdateBrickDuel(
            BrickDuelSnapshot snapshot,
            BrickDuelRuleDefinition rule,
            BrickDuelFrameEvents frameEvents)
        {
            if (snapshot == null || rule == null)
            {
                return;
            }

            if (_brickDuelPressurePulsePendingReset && _brickDuelCenterText != null)
            {
                _brickDuelCenterText.rectTransform.localScale = _brickDuelCenterTextRestScale;
                _brickDuelPressurePulsePendingReset = false;
            }
            if (_brickDuelImpactPulsePendingReset && _brickDuelStatusText != null)
            {
                _brickDuelStatusText.rectTransform.localScale = _brickDuelStatusTextRestScale;
                _brickDuelStatusText.color = _brickDuelStatusTextRestColor;
                _brickDuelImpactPulsePendingReset = false;
            }
            if (_brickDuelBottomCoreFlashPendingReset &&
                Time.unscaledTime >= _brickDuelBottomCoreFlashUntil)
            {
                SetActive(
                    _brickDuelBottomCoreHitFeedback != null
                        ? _brickDuelBottomCoreHitFeedback.gameObject
                        : null,
                    false);
                _brickDuelBottomCoreFlashPendingReset = false;
            }
            if (_brickDuelTopCoreFlashPendingReset &&
                Time.unscaledTime >= _brickDuelTopCoreFlashUntil)
            {
                SetActive(
                    _brickDuelTopCoreHitFeedback != null
                        ? _brickDuelTopCoreHitFeedback.gameObject
                        : null,
                    false);
                _brickDuelTopCoreFlashPendingReset = false;
            }

            SetText(_brickDuelOpponentHealthText, FormatCoreHealth(snapshot.TopCoreHealth));
            SetText(_brickDuelPlayerHealthText, FormatCoreHealth(snapshot.BottomCoreHealth));
            string elapsed = FormatElapsed(snapshot.ElapsedFrames, rule.SimulationFps);
            SetText(
                _brickDuelCenterText,
                $"{elapsed} / Lv.{snapshot.PressureLevel} / {snapshot.PressureMultiplier:0.00}×");

            float secondsUntilPressure = snapshot.FramesUntilPressureIncrease /
                                         (float)Mathf.Max(1, rule.SimulationFps);
            bool isDanger = snapshot.BottomDangerDistance <= rule.DangerDistance;
            SetText(
                _brickDuelStatusText,
                snapshot.IsPaused
                    ? "已暂停 · 点击继续"
                    : isDanger
                    ? $"危险：核心线逼近  ·  下次提速 {secondsUntilPressure:0.0}s · 点击暂停"
                    : $"下次提速 {secondsUntilPressure:0.0}s · 点击暂停");

            Color currentColor = ResolvePressureColor(snapshot.PressureLevel);
            if (snapshot.Phase == BrickDuelPhase.Playing && secondsUntilPressure <= 3f)
            {
                float elapsedSeconds = snapshot.ElapsedFrames / (float)Mathf.Max(1, rule.SimulationFps);
                bool useNext = Mathf.FloorToInt(elapsedSeconds * 2f) % 2 != 0;
                currentColor = useNext
                    ? ResolvePressureColor(snapshot.PressureLevel + 1)
                    : currentColor;
            }

            if (_brickDuelCenterText != null)
            {
                _brickDuelCenterText.color = currentColor;
            }

            bool pressureChanged = frameEvents != null && frameEvents.PressureLevelChanged;
            pressureChanged |= _brickDuelLastPressureLevel >= 0 &&
                               snapshot.PressureLevel != _brickDuelLastPressureLevel;
            if (pressureChanged && _brickDuelCenterText != null)
            {
                _brickDuelCenterText.rectTransform.localScale =
                    _brickDuelCenterTextRestScale * 1.15f;
                _brickDuelCenterText.color = Color.white;
                _brickDuelPressurePulsePendingReset = true;
            }

            bool bottomDamaged = (frameEvents != null && frameEvents.BottomCoreDamage > 0) ||
                                 (_brickDuelLastBottomHealth >= 0 &&
                                  snapshot.BottomCoreHealth < _brickDuelLastBottomHealth);
            bool topDamaged = (frameEvents != null && frameEvents.TopCoreDamage > 0) ||
                              (_brickDuelLastTopHealth >= 0 &&
                               snapshot.TopCoreHealth < _brickDuelLastTopHealth);
            if ((bottomDamaged || topDamaged) && _brickDuelStatusText != null)
            {
                _brickDuelStatusText.rectTransform.localScale =
                    _brickDuelStatusTextRestScale * 1.08f;
                _brickDuelStatusText.color = bottomDamaged
                    ? new Color32(255, 86, 61, 255)
                    : new Color32(234, 247, 255, 255);
                _brickDuelImpactPulsePendingReset = true;
            }
            if (bottomDamaged && _brickDuelBottomCoreHitFeedback != null)
            {
                SetActive(_brickDuelBottomCoreHitFeedback.gameObject, true);
                _brickDuelBottomCoreFlashPendingReset = true;
                _brickDuelBottomCoreFlashUntil =
                    Time.unscaledTime + BrickDuelCoreHitFlashDurationSeconds;
            }
            if (topDamaged && _brickDuelTopCoreHitFeedback != null)
            {
                SetActive(_brickDuelTopCoreHitFeedback.gameObject, true);
                _brickDuelTopCoreFlashPendingReset = true;
                _brickDuelTopCoreFlashUntil =
                    Time.unscaledTime + BrickDuelCoreHitFlashDurationSeconds;
            }

            _brickDuelLastPressureLevel = snapshot.PressureLevel;
            _brickDuelLastBottomHealth = snapshot.BottomCoreHealth;
            _brickDuelLastTopHealth = snapshot.TopCoreHealth;

            bool isCountdown = snapshot.Phase == BrickDuelPhase.Countdown;
            SetActive(_startCountdownRoot, isCountdown);
            if (isCountdown)
            {
                int seconds = Mathf.Max(
                    1,
                    Mathf.CeilToInt(snapshot.CountdownFramesRemaining /
                                    (float)Mathf.Max(1, rule.SimulationFps)));
                SetText(_startCountdownText, seconds.ToString(CultureInfo.InvariantCulture));
            }

            if (snapshot.Phase == BrickDuelPhase.Result)
            {
                UpdateBrickDuelResult(snapshot.Result);
            }
        }

        public void UpdateBrickDuelResult(BrickDuelResult result)
        {
            SetActive(_resultRoot, result != BrickDuelResult.None);
            if (result == BrickDuelResult.None)
            {
                return;
            }

            string title;
            switch (result)
            {
                case BrickDuelResult.PlayerWin:
                    title = "胜利";
                    break;
                case BrickDuelResult.PlayerLose:
                    title = "失败";
                    break;
                case BrickDuelResult.Draw:
                    title = "平局";
                    break;
                default:
                    title = "比赛结束";
                    break;
            }

            SetText(_resultTitleText, title);
            SetText(_resultBodyText, title);
            SetText(_resultScoreText, string.Empty);
            SetActive(_resultScoreText != null ? _resultScoreText.gameObject : null, false);
            SetTextObjectsActive(_resultRankLabelTexts, false);
            SetTextObjectsActive(_resultRankNameTexts, false);
        }

        public void UpdateBounceTuning(PaddleBounceTuning tuning, MatchPhase phase)
        {
            bool isVisible = tuning != null && phase != MatchPhase.Result;
            SetActive(_gmRoot, isVisible);
            if (!isVisible)
            {
                return;
            }

            _suppressSliderEvents = true;
            SetSliderValue(_gmHitOffsetSlider, tuning.HitOffsetInfluenceValue);
            SetSliderValue(_gmPaddleVelocitySlider, tuning.PaddleVelocityInfluenceValue);
            SetSliderValue(_gmMinimumOutwardSlider, tuning.MinimumOutwardShareValue);
            _suppressSliderEvents = false;

            SetText(_gmHitOffsetValueText, FormatTuningValue("命中位置影响", tuning.HitOffsetInfluenceValue, tuning.HitOffsetInfluence));
            SetText(_gmPaddleVelocityValueText, FormatTuningValue("板速影响", tuning.PaddleVelocityInfluenceValue, tuning.PaddleVelocityInfluence));
            SetText(_gmMinimumOutwardValueText, FormatTuningValue("最小离板分量", tuning.MinimumOutwardShareValue, tuning.MinimumOutwardShare));
        }

        public void UpdateLanRoom(RoomSnapshot snapshot, string localIp, string roomIp)
        {
            if (snapshot == null)
            {
                return;
            }

            SetText(_lanStateText, $"状态：{FormatLanRoomState(snapshot.State)}");
            SetText(_lanRoomCodeText, $"房间号：{(string.IsNullOrEmpty(snapshot.RoomCode) ? "-" : snapshot.RoomCode)}");
            SetText(_lanPlayerCountText, FormatLanPlayerCount(snapshot));
            SetText(_lanLocalIpText, $"本机 IP：{(string.IsNullOrEmpty(localIp) ? "-" : localIp)}");
            SetText(_lanRoomIpText, $"房间 IP：{(string.IsNullOrEmpty(roomIp) ? "-" : roomIp)}");
            SetText(_lanErrorText, string.IsNullOrEmpty(snapshot.Error)
                ? FormatLanRoster(snapshot)
                : TruncateLanStatus(snapshot.Error));
            UpdateLanRosterRows(snapshot);

            if (_lanStartButton != null)
            {
                _lanStartButton.interactable = snapshot.CanStart;
            }

            SetActive(
                _lanAcknowledgeStartButton != null ? _lanAcknowledgeStartButton.gameObject : null,
                snapshot.State == LanRoomState.Loading && !snapshot.IsHost);
        }

        public void ShowModeSelect()
        {
            SetActive(_lanRoot, true);
            SetActive(_modeSelectRoot, true);
            SetActive(_singleSelectRoot, false);
            SetActive(_brickDuelHudRoot, false);
            HideBrickDuelCoreHitFeedback();
            SetActive(_hudRoot, true);
            SetActive(_gmRoot, true);
            SetActive(_skillButton != null ? _skillButton.gameObject : null, true);
            SetActive(_ballCountText != null ? _ballCountText.gameObject : null, true);
            SetActive(_heroHudText != null ? _heroHudText.gameObject : null, true);
            SetActive(_loadoutRoot, false);
            SetActive(_lanBackButton != null ? _lanBackButton.gameObject : null, false);
            SetActive(_lanMenuRoot, false);
            SetActive(_lanRoomInfoRoot, false);
            SetActive(_lanStatusRoot, false);
            SetActive(_startCountdownRoot, false);
            SetActive(_resultRoot, false);
        }

        public void ShowSingleSelect(bool brickDuelAvailable, string message = null)
        {
            SetActive(_lanRoot, true);
            SetActive(_modeSelectRoot, false);
            SetActive(_singleSelectRoot, true);
            SetActive(_brickDuelHudRoot, false);
            HideBrickDuelCoreHitFeedback();
            SetActive(_loadoutRoot, false);
            SetActive(_lanMenuRoot, false);
            SetActive(_lanRoomInfoRoot, false);
            SetActive(_lanStatusRoot, false);
            SetActive(_startCountdownRoot, false);
            SetActive(_resultRoot, false);
            if (_brickDuel1v1Button != null)
            {
                _brickDuel1v1Button.interactable = brickDuelAvailable;
            }
            if (_brickDuel1v2Button != null)
            {
                _brickDuel1v2Button.interactable = false;
            }
            if (_brickDuel1v3Button != null)
            {
                _brickDuel1v3Button.interactable = false;
            }
            SetText(
                _singleSelectTitleText,
                !string.IsNullOrWhiteSpace(message)
                    ? $"{message} · 返回"
                    : brickDuelAvailable
                        ? "挑战模式 · 返回"
                        : "挑战模式 · 配置未更新 · 返回");
        }

        public void ShowBrickDuelHud()
        {
            SetActive(_lanRoot, false);
            SetActive(_modeSelectRoot, false);
            SetActive(_singleSelectRoot, false);
            SetActive(_loadoutRoot, false);
            SetActive(_brickDuelHudRoot, true);
            SetActive(_hudRoot, false);
            SetActive(_topPanel2PRoot, false);
            SetActive(_topPanel3PRoot, false);
            SetActive(_topPanel4PRoot, false);
            SetActive(_gmRoot, false);
            SetActive(_skillButton != null ? _skillButton.gameObject : null, false);
            SetActive(_ballCountText != null ? _ballCountText.gameObject : null, false);
            SetActive(_heroHudText != null ? _heroHudText.gameObject : null, false);
            SetActive(_resultRoot, false);
            HideBrickDuelCoreHitFeedback();
            _brickDuelLastPressureLevel = -1;
            _brickDuelLastBottomHealth = -1;
            _brickDuelLastTopHealth = -1;
        }

        public void HideBrickDuelHud()
        {
            SetActive(_brickDuelHudRoot, false);
            SetActive(_startCountdownRoot, false);
            HideBrickDuelCoreHitFeedback();
            PreviewBrickDuelMoveAxis(0f);
        }

        public void ShowOnlineMenu()
        {
            SetActive(_lanRoot, true);
            SetActive(_modeSelectRoot, false);
            SetActive(_lanBackButton != null ? _lanBackButton.gameObject : null, true);
            SetActive(_lanMenuRoot, true);
            SetActive(_lanRoomInfoRoot, false);
            SetActive(_lanStatusRoot, true);
            SetActive(_startCountdownRoot, false);
        }

        public void ShowLanRoomStatus()
        {
            SetActive(_lanRoot, true);
            SetActive(_modeSelectRoot, false);
            SetActive(_lanBackButton != null ? _lanBackButton.gameObject : null, true);
            SetActive(_lanMenuRoot, false);
            SetActive(_lanRoomInfoRoot, true);
            SetActive(_lanStatusRoot, true);
            SetActive(_startCountdownRoot, false);
        }

        public void HideEntryUi()
        {
            SetActive(_lanRoot, false);
            SetActive(_startCountdownRoot, false);
        }

        public void ShowStartCountdown(string text)
        {
            SetActive(_lanRoot, false);
            SetActive(_startCountdownRoot, true);
            SetText(_startCountdownText, text ?? string.Empty);
        }

        public void Clear()
        {
            for (int i = 0; i < _buttonListeners.Count; i++)
            {
                ButtonListener listener = _buttonListeners[i];
                if (listener.Button != null)
                {
                    listener.Button.onClick.RemoveListener(listener.Action);
                }
            }

            _buttonListeners.Clear();
            _localBattleButton = null;
            _onlineBattleButton = null;
            _singleBattleButton = null;
            _brickDuel1v1Button = null;
            _brickDuel1v2Button = null;
            _brickDuel1v3Button = null;
            _singleSelectBackButton = null;
            _singleSelectTitleText = null;
            _singleSelectRoot = null;
            _brickDuelHudRoot = null;
            _brickDuelOpponentHealthText = null;
            _brickDuelPlayerHealthText = null;
            _brickDuelCenterText = null;
            _brickDuelStatusText = null;
            _brickDuelBottomCoreHitFeedback = null;
            _brickDuelTopCoreHitFeedback = null;
            _brickDuelPauseButton = null;
            _brickDuelMovementPad = null;
            _brickDuelMovementHandle = null;
            _brickDuelMovementLeftArrowInput = null;
            _brickDuelMovementRightArrowInput = null;
            _brickDuelMovementLeftArrowHighlight = null;
            _brickDuelMovementRightArrowHighlight = null;
            _brickDuelMovementHandleRestPosition = Vector2.zero;
            _hasBrickDuelMovementHandleRestPosition = false;
            _brickDuelMovementLeftArrowRestColor = Color.clear;
            _brickDuelMovementRightArrowRestColor = Color.clear;
            _hasBrickDuelMovementLeftArrowRestColor = false;
            _hasBrickDuelMovementRightArrowRestColor = false;
            _brickDuelPressurePulsePendingReset = false;
            _brickDuelImpactPulsePendingReset = false;
            _brickDuelBottomCoreFlashPendingReset = false;
            _brickDuelTopCoreFlashPendingReset = false;
            _brickDuelBottomCoreFlashUntil = 0f;
            _brickDuelTopCoreFlashUntil = 0f;
            _brickDuelCenterTextRestScale = Vector3.one;
            _brickDuelStatusTextRestScale = Vector3.one;
            _brickDuelStatusTextRestColor = Color.white;
            _brickDuelLastPressureLevel = -1;
            _brickDuelLastBottomHealth = -1;
            _brickDuelLastTopHealth = -1;
            _loadoutRoot = null;
            _loadoutHeroDropdown = null;
            _loadoutPathDropdown = null;
            _loadoutSignatureDropdown = null;
            _loadoutUniversalChipDropdowns = Array.Empty<TMP_Dropdown>();
            _loadoutUseDefaultButton = null;
            _loadoutConfirmButton = null;
            _loadoutErrorText = null;
            _heroHudText = null;
            for (int i = 0; i < _inputListeners.Count; i++)
            {
                InputListener listener = _inputListeners[i];
                if (listener.Input != null)
                {
                    listener.Input.onValueChanged.RemoveListener(listener.Action);
                }
            }

            _inputListeners.Clear();
            for (int i = 0; i < _dropdownListeners.Count; i++)
            {
                DropdownListener listener = _dropdownListeners[i];
                if (listener.Dropdown != null)
                {
                    listener.Dropdown.onValueChanged.RemoveListener(listener.Action);
                }
            }

            _dropdownListeners.Clear();
            for (int i = 0; i < _sliderListeners.Count; i++)
            {
                SliderListener listener = _sliderListeners[i];
                if (listener.Slider != null)
                {
                    listener.Slider.onValueChanged.RemoveListener(listener.Action);
                }
            }

            _sliderListeners.Clear();
            for (int i = 0; i < _eventTriggerListeners.Count; i++)
            {
                EventTriggerListener listener = _eventTriggerListeners[i];
                if (listener.Trigger != null && listener.Trigger.triggers != null)
                {
                    listener.Trigger.triggers.Remove(listener.Entry);
                }
            }

            _eventTriggerListeners.Clear();
            _skillButton = null;
            _lanBackButton = null;
            _lanCreateButton = null;
            _lanDiscoverButton = null;
            _lanJoinButton = null;
            _lanReadyButton = null;
            _lanStartButton = null;
            _lanLeaveButton = null;
            _lanAcknowledgeStartButton = null;
            _lanPlayerNameInput = null;
            _lanRoomTypeDropdown = null;
            _lanRoomCodeInput = null;
            _movementPad = null;
            _movementHandle = null;
            _movementLeftArrowInput = null;
            _movementRightArrowInput = null;
            _movementLeftArrowHighlight = null;
            _movementRightArrowHighlight = null;
            _movementHandleRestPosition = Vector2.zero;
            _hasMovementHandleRestPosition = false;
            _movementLeftArrowRestColor = Color.clear;
            _movementRightArrowRestColor = Color.clear;
            _hasMovementLeftArrowRestColor = false;
            _hasMovementRightArrowRestColor = false;
            _ballCountText = null;
            _hudTitleText = null;
            _hudStatusText = null;
            _hudScoreText = null;
            _hudServeText = null;
            _hudBallText = null;
            _timeText = null;
            _playerScoreTexts = Array.Empty<TMP_Text>();
            _playerHitTexts = Array.Empty<TMP_Text>();
            _topPanel2PTimeText = null;
            _topPanel3PTimeText = null;
            _topPanel4PTimeText = null;
            _playerScore2PTexts = Array.Empty<TMP_Text>();
            _playerHit2PTexts = Array.Empty<TMP_Text>();
            _playerScore3PTexts = Array.Empty<TMP_Text>();
            _playerHit3PTexts = Array.Empty<TMP_Text>();
            _playerScore4PTexts = Array.Empty<TMP_Text>();
            _playerHit4PTexts = Array.Empty<TMP_Text>();
            _resultTitleText = null;
            _resultBodyText = null;
            _resultScoreText = null;
            _resultRankLabelTexts = Array.Empty<TMP_Text>();
            _resultRankNameTexts = Array.Empty<TMP_Text>();
            _resultRestartButton = null;
            _resultBackButton = null;
            _gmHitOffsetValueText = null;
            _gmPaddleVelocityValueText = null;
            _gmMinimumOutwardValueText = null;
            _lanStateText = null;
            _lanRoomCodeText = null;
            _lanPlayerCountText = null;
            _lanLocalIpText = null;
            _lanRoomIpText = null;
            _lanErrorText = null;
            _lanRoomPlayerInfoTexts = Array.Empty<TMP_Text>();
            _lanRoomPlayerNameTexts = Array.Empty<TMP_Text>();
            _lanRoomPlayerReadyTexts = Array.Empty<TMP_Text>();
            _gmHitOffsetSlider = null;
            _gmPaddleVelocitySlider = null;
            _gmMinimumOutwardSlider = null;
            _hudRoot = null;
            _topPanel2PRoot = null;
            _topPanel3PRoot = null;
            _topPanel4PRoot = null;
            _resultRoot = null;
            _gmRoot = null;
            _lanRoot = null;
            _modeSelectRoot = null;
            _lanMenuRoot = null;
            _lanRoomInfoRoot = null;
            _lanStatusRoot = null;
            _startCountdownRoot = null;
            _startCountdownText = null;
            _callbacks = null;
            _logger = null;
            _lastBallCountText = null;
            _suppressSliderEvents = false;
            IsBound = false;
        }

        private T Require<T>(UnityEngine.Object source, string bindingName) where T : UnityEngine.Object
        {
            var value = source as T;
            if (value == null)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: {0} is not a {1}.", bindingName, typeof(T).Name);
            }

            return value;
        }

        private static T Optional<T>(UnityEngine.Object source) where T : UnityEngine.Object
        {
            return source as T;
        }

        private static GameObject OptionalGameObject(UnityEngine.Object source)
        {
            if (source is GameObject gameObject)
            {
                return gameObject;
            }

            return source is Component component ? component.gameObject : null;
        }

        private GameObject RequireGameObject(UnityEngine.Object source, string bindingName)
        {
            if (source is GameObject gameObject)
            {
                return gameObject;
            }

            if (source is Component component)
            {
                return component.gameObject;
            }

            _logger?.LogWarning("GatebreakerArenaSceneBindingService: {0} is not a GameObject or Component.", bindingName);
            return null;
        }

        private TMP_Text[] RequireTextArray(UnityEngine.Object[] sources, string bindingName)
        {
            if (sources == null || sources.Length == 0)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: {0} has no text bindings.", bindingName);
                return Array.Empty<TMP_Text>();
            }

            var texts = new TMP_Text[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                texts[i] = Require<TMP_Text>(sources[i], $"{bindingName}[{i}]");
            }

            return texts;
        }

        private TMP_Dropdown[] RequireDropdownArray(UnityEngine.Object[] sources, string bindingName)
        {
            if (sources == null || sources.Length != 5)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: {0} must contain five dropdowns.", bindingName);
                return Array.Empty<TMP_Dropdown>();
            }
            var result = new TMP_Dropdown[sources.Length];
            for (int i = 0; i < sources.Length; i++) result[i] = Require<TMP_Dropdown>(sources[i], bindingName + "[" + i + "]");
            return result;
        }

        private static void SetDropdownOptions(TMP_Dropdown dropdown, IReadOnlyList<string> options, int selectedIndex = 0)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            dropdown.AddOptions((options ?? Array.Empty<string>()).Select(value => value ?? string.Empty).ToList());
            dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, dropdown.options.Count - 1)));
            dropdown.RefreshShownValue();
        }

        private TMP_Text[] OptionalTextArray(UnityEngine.Object[] sources, string bindingName, bool warnWhenMissing = true)
        {
            if (sources == null || sources.Length == 0)
            {
                if (warnWhenMissing)
                {
                    _logger?.LogWarning("GatebreakerArenaSceneBindingService: {0} has no optional text bindings.", bindingName);
                }

                return Array.Empty<TMP_Text>();
            }

            var texts = new TMP_Text[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                texts[i] = Require<TMP_Text>(sources[i], $"{bindingName}[{i}]");
            }

            return texts;
        }

        private void ResolveLanRoomNativeChildTextBindings()
        {
            if (_lanRoomPlayerInfoTexts.Length <= 0)
            {
                return;
            }

            if (_lanRoomPlayerNameTexts.Length <= 0)
            {
                _lanRoomPlayerNameTexts = ResolveLanRoomNativeChildTextBindings("Name");
            }

            if (_lanRoomPlayerReadyTexts.Length <= 0)
            {
                _lanRoomPlayerReadyTexts = ResolveLanRoomNativeChildTextBindings("Status");
            }

            if (_lanRoomPlayerNameTexts.Length <= 0)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: LanRoomPlayerNameTextObjects has no optional text bindings.");
            }

            if (_lanRoomPlayerReadyTexts.Length <= 0)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: LanRoomPlayerReadyTextObjects has no optional text bindings.");
            }
        }

        private TMP_Text[] ResolveLanRoomNativeChildTextBindings(string childName)
        {
            var texts = new TMP_Text[_lanRoomPlayerInfoTexts.Length];
            for (int i = 0; i < _lanRoomPlayerInfoTexts.Length; i++)
            {
                TMP_Text rowLabel = _lanRoomPlayerInfoTexts[i];
                Transform rowTransform = rowLabel != null ? rowLabel.transform : null;
                if (rowTransform == null)
                {
                    return Array.Empty<TMP_Text>();
                }

                TMP_Text text = null;
                for (int childIndex = 0; childIndex < rowTransform.childCount; childIndex++)
                {
                    Transform child = rowTransform.GetChild(childIndex);
                    if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                    {
                        text = child.GetComponent<TMP_Text>();
                        break;
                    }
                }

                if (text == null)
                {
                    return Array.Empty<TMP_Text>();
                }

                texts[i] = text;
            }

            return texts;
        }

        private void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(action);
            _buttonListeners.Add(new ButtonListener(button, action));
        }

        private void AddInputListener(TMP_InputField input, string initialValue, UnityEngine.Events.UnityAction<string> action)
        {
            if (input == null)
            {
                return;
            }

            input.SetTextWithoutNotify(initialValue ?? string.Empty);
            input.onValueChanged.AddListener(action);
            _inputListeners.Add(new InputListener(input, action));
        }

        private void AddDropdownListener(TMP_Dropdown dropdown, int initialIndex, UnityEngine.Events.UnityAction<int> action)
        {
            if (dropdown == null)
            {
                return;
            }

            int clampedIndex = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, dropdown.options.Count - 1));
            dropdown.SetValueWithoutNotify(clampedIndex);
            dropdown.onValueChanged.AddListener(action);
            _dropdownListeners.Add(new DropdownListener(dropdown, action));
            action?.Invoke(clampedIndex);
        }

        private void ConfigureSlider(Slider slider, int min, int max, Action<int> setter)
        {
            if (slider == null)
            {
                return;
            }

            slider.wholeNumbers = true;
            slider.minValue = min;
            slider.maxValue = max;
            UnityEngine.Events.UnityAction<float> action = value =>
            {
                if (_suppressSliderEvents)
                {
                    return;
                }

                setter?.Invoke(Mathf.RoundToInt(value));
            };
            slider.onValueChanged.AddListener(action);
            _sliderListeners.Add(new SliderListener(slider, action));
        }

        private void AddMovementListeners(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            AddEventTriggerListener(target, EventTriggerType.PointerDown, HandleMovementPointer);
            AddEventTriggerListener(target, EventTriggerType.Drag, HandleMovementPointer);
            AddEventTriggerListener(target, EventTriggerType.PointerUp, HandleMovementRelease);
            AddEventTriggerListener(target, EventTriggerType.EndDrag, HandleMovementRelease);
        }

        private void AddFixedMovementListeners(RectTransform target, float axis)
        {
            if (target == null)
            {
                return;
            }

            AddEventTriggerListener(target, EventTriggerType.PointerDown, _ => SetMoveAxis(axis));
            AddEventTriggerListener(target, EventTriggerType.Drag, _ => SetMoveAxis(axis));
            AddEventTriggerListener(target, EventTriggerType.PointerUp, HandleMovementRelease);
            AddEventTriggerListener(target, EventTriggerType.EndDrag, HandleMovementRelease);
        }

        private void AddBrickDuelMovementListeners(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            AddEventTriggerListener(target, EventTriggerType.PointerDown, HandleBrickDuelMovementPointer);
            AddEventTriggerListener(target, EventTriggerType.Drag, HandleBrickDuelMovementPointer);
            AddEventTriggerListener(target, EventTriggerType.PointerUp, HandleBrickDuelMovementRelease);
            AddEventTriggerListener(target, EventTriggerType.EndDrag, HandleBrickDuelMovementRelease);
        }

        private void AddBrickDuelFixedMovementListeners(RectTransform target, float axis)
        {
            if (target == null)
            {
                return;
            }

            AddEventTriggerListener(target, EventTriggerType.PointerDown, _ => SetBrickDuelMoveAxis(axis));
            AddEventTriggerListener(target, EventTriggerType.Drag, _ => SetBrickDuelMoveAxis(axis));
            AddEventTriggerListener(target, EventTriggerType.PointerUp, HandleBrickDuelMovementRelease);
            AddEventTriggerListener(target, EventTriggerType.EndDrag, HandleBrickDuelMovementRelease);
        }

        private void AddEventTriggerListener(
            Component target,
            EventTriggerType eventType,
            UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            if (target == null)
            {
                return;
            }

            EventTrigger trigger = target.GetComponent<EventTrigger>() ?? target.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null)
            {
                trigger.triggers = new List<EventTrigger.Entry>();
            }

            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
            _eventTriggerListeners.Add(new EventTriggerListener(trigger, entry));
        }

        private void HandleMovementPointer(BaseEventData eventData)
        {
            if (_movementPad == null)
            {
                SetMoveAxis(0f);
                return;
            }

            var pointerEvent = eventData as PointerEventData;
            if (pointerEvent == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _movementPad,
                    pointerEvent.position,
                    pointerEvent.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            float halfWidth = Mathf.Max(1f, _movementPad.rect.width * 0.5f);
            SetMoveAxis(Mathf.Clamp(localPoint.x / halfWidth, -1f, 1f));
        }

        private void HandleMovementRelease(BaseEventData eventData)
        {
            SetMoveAxis(0f);
        }

        private void HandleBrickDuelMovementPointer(BaseEventData eventData)
        {
            if (_brickDuelMovementPad == null)
            {
                SetBrickDuelMoveAxis(0f);
                return;
            }

            var pointerEvent = eventData as PointerEventData;
            if (pointerEvent == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _brickDuelMovementPad,
                    pointerEvent.position,
                    pointerEvent.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            float halfWidth = Mathf.Max(1f, _brickDuelMovementPad.rect.width * 0.5f);
            SetBrickDuelMoveAxis(Mathf.Clamp(localPoint.x / halfWidth, -1f, 1f));
        }

        private void HandleBrickDuelMovementRelease(BaseEventData eventData)
        {
            SetBrickDuelMoveAxis(0f);
        }

        private void SetMoveAxis(float axis)
        {
            float clampedAxis = Mathf.Clamp(axis, -1f, 1f);
            _callbacks?.MoveAxisChanged?.Invoke(clampedAxis);
            PreviewMoveAxis(clampedAxis);
        }

        public void PreviewMoveAxis(float axis)
        {
            float clampedAxis = Mathf.Clamp(axis, -1f, 1f);
            UpdateMovementHandle(clampedAxis);
            UpdateMovementArrowHighlights(clampedAxis);
        }

        private void SetBrickDuelMoveAxis(float axis)
        {
            float clampedAxis = Mathf.Clamp(axis, -1f, 1f);
            _callbacks?.BrickDuelMoveAxisChanged?.Invoke(clampedAxis);
            PreviewBrickDuelMoveAxis(clampedAxis);
        }

        public void PreviewBrickDuelMoveAxis(float axis)
        {
            float clampedAxis = Mathf.Clamp(axis, -1f, 1f);
            if (_brickDuelMovementHandle != null && _hasBrickDuelMovementHandleRestPosition)
            {
                float padWidth = _brickDuelMovementPad != null ? _brickDuelMovementPad.rect.width : 0f;
                float handleWidth = _brickDuelMovementHandle.rect.width *
                                    Mathf.Abs(_brickDuelMovementHandle.localScale.x);
                float maxOffset = Mathf.Max(0f, (padWidth - handleWidth) * 0.5f);
                _brickDuelMovementHandle.anchoredPosition =
                    _brickDuelMovementHandleRestPosition + Vector2.right * maxOffset * clampedAxis;
            }

            SetMovementArrowHighlight(
                _brickDuelMovementLeftArrowHighlight,
                _brickDuelMovementLeftArrowRestColor,
                _hasBrickDuelMovementLeftArrowRestColor,
                clampedAxis < -0.01f);
            SetMovementArrowHighlight(
                _brickDuelMovementRightArrowHighlight,
                _brickDuelMovementRightArrowRestColor,
                _hasBrickDuelMovementRightArrowRestColor,
                clampedAxis > 0.01f);
        }

        private void UpdateMovementHandle(float axis)
        {
            if (_movementHandle == null || !_hasMovementHandleRestPosition)
            {
                return;
            }

            float padWidth = _movementPad != null ? _movementPad.rect.width : 0f;
            float handleWidth = _movementHandle.rect.width * Mathf.Abs(_movementHandle.localScale.x);
            float maxOffset = Mathf.Max(0f, (padWidth - handleWidth) * 0.5f);
            _movementHandle.anchoredPosition = _movementHandleRestPosition + Vector2.right * maxOffset * axis;
        }

        private void CaptureMovementArrowRestColors()
        {
            if (_movementLeftArrowHighlight != null)
            {
                _movementLeftArrowRestColor = _movementLeftArrowHighlight.color;
                _hasMovementLeftArrowRestColor = true;
            }

            if (_movementRightArrowHighlight != null)
            {
                _movementRightArrowRestColor = _movementRightArrowHighlight.color;
                _hasMovementRightArrowRestColor = true;
            }

            UpdateMovementArrowHighlights(0f);
        }

        private void CaptureBrickDuelMovementArrowRestColors()
        {
            if (_brickDuelMovementLeftArrowHighlight != null)
            {
                _brickDuelMovementLeftArrowRestColor = _brickDuelMovementLeftArrowHighlight.color;
                _hasBrickDuelMovementLeftArrowRestColor = true;
            }

            if (_brickDuelMovementRightArrowHighlight != null)
            {
                _brickDuelMovementRightArrowRestColor = _brickDuelMovementRightArrowHighlight.color;
                _hasBrickDuelMovementRightArrowRestColor = true;
            }

            PreviewBrickDuelMoveAxis(0f);
        }

        private void UpdateMovementArrowHighlights(float axis)
        {
            SetMovementArrowHighlight(
                _movementLeftArrowHighlight,
                _movementLeftArrowRestColor,
                _hasMovementLeftArrowRestColor,
                axis < -0.01f);
            SetMovementArrowHighlight(
                _movementRightArrowHighlight,
                _movementRightArrowRestColor,
                _hasMovementRightArrowRestColor,
                axis > 0.01f);
        }

        private static void SetMovementArrowHighlight(
            Graphic graphic,
            Color restColor,
            bool hasRestColor,
            bool active)
        {
            if (graphic == null)
            {
                return;
            }

            graphic.color = active
                ? new Color(1f, 0.08f, 0.06f, Mathf.Max(0.85f, graphic.color.a))
                : hasRestColor ? restColor : graphic.color;
        }

        private static void SetSliderValue(Slider slider, int value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            GatebreakerRuntimeTmpFontResolver.EnsureFontSupportsText(text, value);
            if (text.text != value)
            {
                text.text = value;
            }
        }

        private static void SetTextObjectsActive(TMP_Text[] texts, bool active)
        {
            if (texts == null)
            {
                return;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                SetActive(texts[i] != null ? texts[i].gameObject : null, active);
            }
        }

        private void UpdateLanRosterRows(RoomSnapshot snapshot)
        {
            int rowCount = Math.Min(
                _lanRoomPlayerInfoTexts.Length,
                Math.Min(_lanRoomPlayerNameTexts.Length, _lanRoomPlayerReadyTexts.Length));
            if (rowCount <= 0)
            {
                return;
            }

            int visibleRowCount = Mathf.Clamp(snapshot?.MaxPlayers ?? rowCount, 1, rowCount);
            RoomPlayerSnapshot[] players = BuildLanRosterRows(snapshot, visibleRowCount);

            for (int i = 0; i < rowCount; i++)
            {
                bool visible = i < visibleRowCount;
                SetActive(_lanRoomPlayerInfoTexts[i] != null ? _lanRoomPlayerInfoTexts[i].gameObject : null, visible);
                SetActive(_lanRoomPlayerNameTexts[i] != null ? _lanRoomPlayerNameTexts[i].gameObject : null, visible);
                SetActive(_lanRoomPlayerReadyTexts[i] != null ? _lanRoomPlayerReadyTexts[i].gameObject : null, visible);
                if (!visible)
                {
                    SetText(_lanRoomPlayerInfoTexts[i], string.Empty);
                    SetText(_lanRoomPlayerNameTexts[i], string.Empty);
                    SetText(_lanRoomPlayerReadyTexts[i], string.Empty);
                    continue;
                }

                RoomPlayerSnapshot player = i < players.Length ? players[i] : null;
                int playerId = player != null ? player.PlayerId : i + 1;
                string playerName = player == null || player.IsAi || string.IsNullOrWhiteSpace(player.PlayerName)
                    ? "AI"
                    : player.PlayerName;
                string ready = player == null || player.IsAi || player.IsReady ? "ready" : "not ready";

                SetText(_lanRoomPlayerInfoTexts[i], "Player" + playerId.ToString(CultureInfo.InvariantCulture) + ":");
                SetText(_lanRoomPlayerNameTexts[i], playerName);
                SetText(_lanRoomPlayerReadyTexts[i], ready);
            }
        }

        private static RoomPlayerSnapshot[] BuildLanRosterRows(RoomSnapshot snapshot, int rowCount)
        {
            var rows = new RoomPlayerSnapshot[Mathf.Max(0, rowCount)];
            RoomPlayerSnapshot[] players = snapshot?.Players ?? Array.Empty<RoomPlayerSnapshot>();
            var overflow = new List<RoomPlayerSnapshot>(players.Length);

            for (int i = 0; i < players.Length; i++)
            {
                RoomPlayerSnapshot player = players[i];
                if (player == null)
                {
                    continue;
                }

                int rowIndex = player.SlotIndex >= 0 ? player.SlotIndex : player.SideOrder;
                if (rowIndex >= 0 && rowIndex < rows.Length && rows[rowIndex] == null)
                {
                    rows[rowIndex] = player;
                }
                else
                {
                    overflow.Add(player);
                }
            }

            overflow.Sort(CompareLanRosterPlayers);
            int overflowIndex = 0;
            for (int i = 0; i < rows.Length && overflowIndex < overflow.Count; i++)
            {
                if (rows[i] != null)
                {
                    continue;
                }

                rows[i] = overflow[overflowIndex++];
            }

            return rows;
        }

        private static int CompareLanRosterPlayers(RoomPlayerSnapshot left, RoomPlayerSnapshot right)
        {
            int leftOrder = GetLanRosterOrder(left);
            int rightOrder = GetLanRosterOrder(right);
            int orderCompare = leftOrder.CompareTo(rightOrder);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            int leftPlayerId = left != null && left.PlayerId > 0 ? left.PlayerId : int.MaxValue;
            int rightPlayerId = right != null && right.PlayerId > 0 ? right.PlayerId : int.MaxValue;
            return leftPlayerId.CompareTo(rightPlayerId);
        }

        private static int GetLanRosterOrder(RoomPlayerSnapshot player)
        {
            if (player == null)
            {
                return int.MaxValue;
            }

            if (player.SlotIndex >= 0)
            {
                return player.SlotIndex;
            }

            if (player.SideOrder >= 0)
            {
                return player.SideOrder;
            }

            return player.PlayerId > 0 ? player.PlayerId - 1 : int.MaxValue;
        }

        private static void SetActive(GameObject gameObject, bool isActive)
        {
            if (gameObject != null && gameObject.activeSelf != isActive)
            {
                gameObject.SetActive(isActive);
            }
        }

        private void HideBrickDuelCoreHitFeedback()
        {
            SetActive(
                _brickDuelBottomCoreHitFeedback != null
                    ? _brickDuelBottomCoreHitFeedback.gameObject
                    : null,
                false);
            SetActive(
                _brickDuelTopCoreHitFeedback != null
                    ? _brickDuelTopCoreHitFeedback.gameObject
                    : null,
                false);
            _brickDuelBottomCoreFlashPendingReset = false;
            _brickDuelTopCoreFlashPendingReset = false;
            _brickDuelBottomCoreFlashUntil = 0f;
            _brickDuelTopCoreFlashUntil = 0f;
        }

        private void HandleSkillButtonClicked()
        {
            _callbacks?.ServeRequested?.Invoke();
        }

        private void UpdatePlayerScorePanel(GatebreakerHudSnapshot snapshot)
        {
            if (snapshot?.PlayerScores == null)
            {
                ClearPlayerScorePanel();
                SetActiveScorePanel(0);
                return;
            }

            List<PlayerScoreSnapshot> visibleScores = BuildVisiblePlayerScoreList(snapshot.PlayerScores);
            int panelPlayerCount = SelectScorePanelPlayerCount(visibleScores.Count);
            TMP_Text[] scoreTexts = GetScoreTextsForPlayerCount(panelPlayerCount);
            TMP_Text[] hitTexts = GetHitTextsForPlayerCount(panelPlayerCount);
            SetActiveScorePanel(panelPlayerCount);
            ClearInactiveScorePanels(panelPlayerCount);
            UpdateScoreRows(scoreTexts, hitTexts, visibleScores);
            UpdateScoreRows(_playerScoreTexts, _playerHitTexts, visibleScores);
        }

        private void UpdateScoreRows(
            TMP_Text[] scoreTexts,
            TMP_Text[] hitTexts,
            IReadOnlyList<PlayerScoreSnapshot> visibleScores)
        {
            int rowCount = Math.Max(scoreTexts != null ? scoreTexts.Length : 0, hitTexts != null ? hitTexts.Length : 0);
            for (int i = 0; i < rowCount; i++)
            {
                if (i < visibleScores.Count)
                {
                    PlayerScoreSnapshot score = visibleScores[i];
                    SetTextAt(scoreTexts, i, score.Score.ToString(CultureInfo.InvariantCulture));
                    SetTextAt(hitTexts, i, FormatHitScore(score.HitScore));
                }
                else
                {
                    SetTextAt(scoreTexts, i, string.Empty);
                    SetTextAt(hitTexts, i, string.Empty);
                }
            }
        }

        private void ClearPlayerScorePanel()
        {
            ClearScoreRows(_playerScoreTexts, _playerHitTexts);
            ClearScoreRows(_playerScore2PTexts, _playerHit2PTexts);
            ClearScoreRows(_playerScore3PTexts, _playerHit3PTexts);
            ClearScoreRows(_playerScore4PTexts, _playerHit4PTexts);
        }

        private static void ClearScoreRows(TMP_Text[] scoreTexts, TMP_Text[] hitTexts)
        {
            int rowCount = Math.Max(scoreTexts != null ? scoreTexts.Length : 0, hitTexts != null ? hitTexts.Length : 0);
            for (int i = 0; i < rowCount; i++)
            {
                SetTextAt(scoreTexts, i, string.Empty);
                SetTextAt(hitTexts, i, string.Empty);
            }
        }

        private void ClearInactiveScorePanels(int activePlayerCount)
        {
            if (activePlayerCount != 2)
            {
                ClearScoreRows(_playerScore2PTexts, _playerHit2PTexts);
            }

            if (activePlayerCount != 3)
            {
                ClearScoreRows(_playerScore3PTexts, _playerHit3PTexts);
            }

            if (activePlayerCount != 4)
            {
                ClearScoreRows(_playerScore4PTexts, _playerHit4PTexts);
            }
        }

        private void SetActiveScorePanel(int activePlayerCount)
        {
            SetActive(_topPanel2PRoot, activePlayerCount == 2);
            SetActive(_topPanel3PRoot, activePlayerCount == 3);
            SetActive(_topPanel4PRoot, activePlayerCount == 4);
        }

        private static int SelectScorePanelPlayerCount(int visiblePlayerCount)
        {
            if (visiblePlayerCount >= 4)
            {
                return 4;
            }

            return visiblePlayerCount == 3 ? 3 : 2;
        }

        private TMP_Text[] GetScoreTextsForPlayerCount(int playerCount)
        {
            switch (playerCount)
            {
                case 2:
                    return _playerScore2PTexts;
                case 3:
                    return _playerScore3PTexts;
                case 4:
                    return _playerScore4PTexts;
                default:
                    return Array.Empty<TMP_Text>();
            }
        }

        private TMP_Text[] GetHitTextsForPlayerCount(int playerCount)
        {
            switch (playerCount)
            {
                case 2:
                    return _playerHit2PTexts;
                case 3:
                    return _playerHit3PTexts;
                case 4:
                    return _playerHit4PTexts;
                default:
                    return Array.Empty<TMP_Text>();
            }
        }

        private void UpdateTimeText(float remainingTime)
        {
            string value = FormatTime(remainingTime);
            SetText(_timeText, value);
            SetText(_topPanel2PTimeText, value);
            SetText(_topPanel3PTimeText, value);
            SetText(_topPanel4PTimeText, value);
        }

        private static List<PlayerScoreSnapshot> BuildVisiblePlayerScoreList(IReadOnlyList<PlayerScoreSnapshot> playerScores)
        {
            var visibleScores = new List<PlayerScoreSnapshot>();
            for (int i = 0; i < playerScores.Count; i++)
            {
                PlayerScoreSnapshot score = playerScores[i];
                if (!score.IsDisabled)
                {
                    visibleScores.Add(score);
                }
            }

            visibleScores.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return visibleScores;
        }

        private static void SetTextAt(TMP_Text[] texts, int index, string value)
        {
            if (texts != null && index >= 0 && index < texts.Length)
            {
                SetText(texts[index], value);
            }
        }

        private static string FormatTime(float remainingTime)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static string FormatElapsed(int elapsedFrames, int simulationFps)
        {
            int totalSeconds = Mathf.Max(0, elapsedFrames) / Mathf.Max(1, simulationFps);
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private static string FormatCoreHealth(int health)
        {
            return Mathf.Max(0, health).ToString();
        }

        private static Color ResolvePressureColor(int pressureLevel)
        {
            string colorCode;
            switch (pressureLevel)
            {
                case 0:
                    colorCode = "#EAF7FF";
                    break;
                case 1:
                    colorCode = "#FFE66D";
                    break;
                case 2:
                    colorCode = "#FFC247";
                    break;
                case 3:
                    colorCode = "#FF8A3D";
                    break;
                case 4:
                    colorCode = "#FF563D";
                    break;
                case 5:
                    colorCode = "#FF334F";
                    break;
                case 6:
                    colorCode = "#FF2D7A";
                    break;
                case 7:
                    colorCode = "#D946EF";
                    break;
                default:
                    colorCode = pressureLevel % 2 == 0 ? "#FF334F" : "#D946EF";
                    break;
            }

            return ColorUtility.TryParseHtmlString(colorCode, out Color color)
                ? color
                : Color.white;
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
                    return "库存回复中";
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

        private static string FormatScoreLine(GatebreakerHudSnapshot snapshot)
        {
            if (snapshot.PlayerScores == null || snapshot.PlayerScores.Count == 0)
            {
                return "无玩家";
            }

            var parts = new List<string>(snapshot.PlayerScores.Count);
            for (int i = 0; i < snapshot.PlayerScores.Count; i++)
            {
                PlayerScoreSnapshot score = snapshot.PlayerScores[i];
                string marker = score.PlayerId == snapshot.LocalPlayerId ? "*" : string.Empty;
                parts.Add($"P{score.PlayerId}{marker}:S{score.Score}/H{FormatHitScore(score.HitScore)}/T{score.TrueScore}");
            }

            return string.Join("  ", parts);
        }

        private static string BuildScoreRows(GatebreakerHudSnapshot snapshot)
        {
            if (snapshot.PlayerScores == null || snapshot.PlayerScores.Count == 0)
            {
                return "无玩家";
            }

            var rows = new List<string>(snapshot.PlayerScores.Count);
            for (int i = 0; i < snapshot.PlayerScores.Count; i++)
            {
                PlayerScoreSnapshot score = snapshot.PlayerScores[i];
                string marker = score.PlayerId == snapshot.LocalPlayerId ? "*" : string.Empty;
                rows.Add($"玩家{score.PlayerId}{marker}：SCORE {score.Score}  HIT {FormatHitScore(score.HitScore)}  TRUE {score.TrueScore}");
            }

            return string.Join("\n", rows);
        }

        private void UpdateResultRanking(GatebreakerHudSnapshot snapshot)
        {
            List<PlayerScoreSnapshot> rows = BuildResultRankRows(snapshot?.PlayerScores);
            int rowCount = Math.Max(_resultRankLabelTexts.Length, _resultRankNameTexts.Length);
            for (int i = 0; i < rowCount; i++)
            {
                if (i < rows.Count)
                {
                    PlayerScoreSnapshot score = rows[i];
                    SetTextAt(_resultRankLabelTexts, i, FormatResultRankLabel(i));
                    SetTextAt(_resultRankNameTexts, i, FormatResultPlayer(score, snapshot));
                }
                else
                {
                    SetTextAt(_resultRankLabelTexts, i, string.Empty);
                    SetTextAt(_resultRankNameTexts, i, string.Empty);
                }
            }
        }

        private static string FormatResultRankLabel(int rowIndex)
        {
            switch (rowIndex)
            {
                case 0:
                    return "\u7B2C\u4E00\u540D:";
                case 1:
                    return "\u7B2C\u4E8C\u540D:";
                case 2:
                    return "\u7B2C\u4E09\u540D:";
                case 3:
                    return "\u7B2C\u56DB\u540D:";
                default:
                    return $"No.{rowIndex + 1}:";
            }
        }

        private static List<PlayerScoreSnapshot> BuildResultRankRows(IReadOnlyList<PlayerScoreSnapshot> playerScores)
        {
            var rows = new List<PlayerScoreSnapshot>();
            if (playerScores == null)
            {
                return rows;
            }

            for (int i = 0; i < playerScores.Count; i++)
            {
                PlayerScoreSnapshot score = playerScores[i];
                if (!score.IsDisabled)
                {
                    rows.Add(score);
                }
            }

            return rows;
        }

        private static string FormatResultPlayer(PlayerScoreSnapshot score, GatebreakerHudSnapshot snapshot)
        {
            string winnerMarker = snapshot != null && snapshot.HasWinner && snapshot.WinnerPlayerId == score.PlayerId
                ? "  WIN"
                : string.Empty;
            return $"Player{score.PlayerId}  SCORE {score.Score}  HIT {FormatHitScore(score.HitScore)}{winnerMarker}";
        }

        private static string BuildWinnerText(GatebreakerHudSnapshot snapshot)
        {
            if (!snapshot.HasWinner || snapshot.WinnerPlayerId <= 0)
            {
                return "本局没有胜者";
            }

            PlayerScoreSnapshot score = FindPlayerScore(snapshot, snapshot.WinnerPlayerId);
            return $"玩家{snapshot.WinnerPlayerId} 获胜！SCORE {score.Score}，真实得分 {score.TrueScore}";
        }

        private static PlayerScoreSnapshot FindPlayerScore(GatebreakerHudSnapshot snapshot, int playerId)
        {
            if (snapshot.PlayerScores == null)
            {
                return new PlayerScoreSnapshot();
            }

            for (int i = 0; i < snapshot.PlayerScores.Count; i++)
            {
                PlayerScoreSnapshot score = snapshot.PlayerScores[i];
                if (score.PlayerId == playerId)
                {
                    return score;
                }
            }

            return new PlayerScoreSnapshot();
        }

        private static string FormatHitScore(int hitScore)
        {
            return hitScore.ToString(CultureInfo.InvariantCulture);
        }

        private static bool HasTextArrayBindings(TMP_Text[] texts)
        {
            if (texts == null || texts.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasExactTextArrayBindings(TMP_Text[] texts, int expectedLength)
        {
            return texts != null &&
                   texts.Length == expectedLength &&
                   HasTextArrayBindings(texts);
        }

        private static string FormatTuningValue(string label, int value, float actualValue)
        {
            return $"{label}：{value}（实际：{actualValue:0.00}）";
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

        public static int ResolveLanRoomTypePlayerCount(TMP_Dropdown dropdown, int optionIndex)
        {
            string text = null;
            if (dropdown != null && dropdown.options != null && optionIndex >= 0 && optionIndex < dropdown.options.Count)
            {
                text = dropdown.options[optionIndex]?.text;
            }

            return ResolveLanRoomTypePlayerCount(text);
        }

        public static int ResolveLanRoomTypePlayerCount(string optionText)
        {
            if (string.IsNullOrWhiteSpace(optionText))
            {
                return 2;
            }

            string text = optionText.Trim();
            if (text.Contains("四") || text.Contains("4"))
            {
                return 4;
            }

            if (text.Contains("三") || text.Contains("3"))
            {
                return 3;
            }

            return 2;
        }

        private static int ResolveLanRoomTypeIndex(TMP_Dropdown dropdown, int playerCount)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count <= 0)
            {
                return 0;
            }

            int targetPlayerCount = Mathf.Clamp(playerCount, 2, 4);
            for (int i = 0; i < dropdown.options.Count; i++)
            {
                if (ResolveLanRoomTypePlayerCount(dropdown, i) == targetPlayerCount)
                {
                    return i;
                }
            }

            return 0;
        }

        private static string FormatLanPlayerCount(RoomSnapshot snapshot)
        {
            int totalPlayers = Mathf.Max(0, snapshot?.MaxPlayers ?? 0);
            int humanPlayers = 0;
            RoomPlayerSnapshot[] players = snapshot?.Players ?? Array.Empty<RoomPlayerSnapshot>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && !players[i].IsAi)
                {
                    humanPlayers++;
                }
            }

            return $"人数：{humanPlayers}真人/{Mathf.Max(1, totalPlayers)}总";
        }

        private static string FormatLanRoster(RoomSnapshot snapshot)
        {
            RoomPlayerSnapshot[] players = snapshot?.Players ?? Array.Empty<RoomPlayerSnapshot>();
            if (players.Length <= 0)
            {
                return string.Empty;
            }

            var rows = new List<string>(players.Length);
            for (int i = 0; i < players.Length; i++)
            {
                RoomPlayerSnapshot player = players[i];
                if (player == null)
                {
                    continue;
                }

                rows.Add(FormatLanRosterSummary(player));
            }

            return string.Join(" / ", rows);
        }

        private static string FormatLanRosterSummary(RoomPlayerSnapshot player)
        {
            string name = player.IsAi || string.IsNullOrWhiteSpace(player.PlayerName)
                ? "AI"
                : player.PlayerName;
            string ready = player.IsAi || player.IsReady ? "ready" : "not ready";
            return "Player" + player.PlayerId.ToString(CultureInfo.InvariantCulture) + " " + name + " " + ready;
        }

        private static string TruncateLanStatus(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 44)
            {
                return value;
            }

            return value.Substring(0, 41) + "...";
        }

        private readonly struct ButtonListener
        {
            public ButtonListener(Button button, UnityEngine.Events.UnityAction action)
            {
                Button = button;
                Action = action;
            }

            public Button Button { get; }
            public UnityEngine.Events.UnityAction Action { get; }
        }

        private readonly struct InputListener
        {
            public InputListener(TMP_InputField input, UnityEngine.Events.UnityAction<string> action)
            {
                Input = input;
                Action = action;
            }

            public TMP_InputField Input { get; }
            public UnityEngine.Events.UnityAction<string> Action { get; }
        }

        private readonly struct DropdownListener
        {
            public DropdownListener(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction<int> action)
            {
                Dropdown = dropdown;
                Action = action;
            }

            public TMP_Dropdown Dropdown { get; }
            public UnityEngine.Events.UnityAction<int> Action { get; }
        }

        private readonly struct SliderListener
        {
            public SliderListener(Slider slider, UnityEngine.Events.UnityAction<float> action)
            {
                Slider = slider;
                Action = action;
            }

            public Slider Slider { get; }
            public UnityEngine.Events.UnityAction<float> Action { get; }
        }

        private readonly struct EventTriggerListener
        {
            public EventTriggerListener(EventTrigger trigger, EventTrigger.Entry entry)
            {
                Trigger = trigger;
                Entry = entry;
            }

            public EventTrigger Trigger { get; }
            public EventTrigger.Entry Entry { get; }
        }
    }
}
