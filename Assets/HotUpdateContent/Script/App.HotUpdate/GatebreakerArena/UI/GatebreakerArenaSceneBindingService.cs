using System;
using System.Collections.Generic;
using System.Globalization;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
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
        public Action CreateLanHostRequested { get; set; }
        public Action StartLanDiscoveryRequested { get; set; }
        public Action JoinLanRoomRequested { get; set; }
        public Action ToggleLanReadyRequested { get; set; }
        public Action StartLanLoadingRequested { get; set; }
        public Action LeaveLanRoomRequested { get; set; }
        public Action AcknowledgeLanStartRequested { get; set; }
        public Action<string> LanPlayerNameChanged { get; set; }
        public Action<string> LanRoomCodeChanged { get; set; }
        public Action<float> MoveAxisChanged { get; set; }
        public Action<int> HitOffsetInfluenceChanged { get; set; }
        public Action<int> PaddleVelocityInfluenceChanged { get; set; }
        public Action<int> MinimumOutwardShareChanged { get; set; }
        public Action RestartMatchRequested { get; set; }
        public Action ResultBackRequested { get; set; }
        public string InitialLanPlayerName { get; set; }
        public string InitialLanRoomCode { get; set; }
    }

    public sealed class GatebreakerArenaSceneBindingService
    {
        private readonly List<ButtonListener> _buttonListeners = new List<ButtonListener>();
        private readonly List<InputListener> _inputListeners = new List<InputListener>();
        private readonly List<SliderListener> _sliderListeners = new List<SliderListener>();
        private readonly List<EventTriggerListener> _eventTriggerListeners = new List<EventTriggerListener>();
        private Button _skillButton;
        private Button _localBattleButton;
        private Button _onlineBattleButton;
        private Button _lanCreateButton;
        private Button _lanBackButton;
        private Button _lanDiscoverButton;
        private Button _lanJoinButton;
        private Button _lanReadyButton;
        private Button _lanStartButton;
        private Button _lanLeaveButton;
        private Button _lanAcknowledgeStartButton;
        private TMP_InputField _lanPlayerNameInput;
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
            HasTextArrayBindings(_playerScoreTexts) &&
            HasTextArrayBindings(_playerHitTexts);

        public bool HasLanButtonBindings =>
            _lanCreateButton != null &&
            _lanBackButton != null &&
            _lanDiscoverButton != null &&
            _lanJoinButton != null &&
            _lanReadyButton != null &&
            _lanStartButton != null &&
            _lanLeaveButton != null &&
            _lanAcknowledgeStartButton != null;

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
            AddInputListener(_lanPlayerNameInput, _callbacks.InitialLanPlayerName, value => _callbacks.LanPlayerNameChanged?.Invoke(value));
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
            ShowModeSelect();
            SetActive(_resultRoot, false);
        }

        public void MarkBound()
        {
            IsBound = true;
        }

        public void UpdateHud(GatebreakerHudSnapshot snapshot, ServeBlockReason lastServeBlockReason)
        {
            UpdateBallCount(snapshot);
            if (snapshot == null)
            {
                SetText(_timeText, FormatTime(0f));
                return;
            }

            SetText(_hudTitleText, "Gatebreaker Arena 原型");
            SetText(_hudStatusText, $"阶段：{FormatPhase(snapshot.Phase)}    时间：{FormatTime(snapshot.RemainingTime)}");
            SetText(_timeText, FormatTime(snapshot.RemainingTime));
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
            UpdateResultRanking(snapshot);
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
            SetActive(_lanBackButton != null ? _lanBackButton.gameObject : null, false);
            SetActive(_lanMenuRoot, false);
            SetActive(_lanRoomInfoRoot, false);
            SetActive(_lanStatusRoot, false);
            SetActive(_startCountdownRoot, false);
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
            for (int i = 0; i < _inputListeners.Count; i++)
            {
                InputListener listener = _inputListeners[i];
                if (listener.Input != null)
                {
                    listener.Input.onValueChanged.RemoveListener(listener.Action);
                }
            }

            _inputListeners.Clear();
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

        private void UpdateLanRosterRows(RoomSnapshot snapshot)
        {
            int rowCount = Math.Min(
                _lanRoomPlayerInfoTexts.Length,
                Math.Min(_lanRoomPlayerNameTexts.Length, _lanRoomPlayerReadyTexts.Length));
            if (rowCount <= 0)
            {
                return;
            }

            RoomPlayerSnapshot[] players = snapshot?.Players ?? Array.Empty<RoomPlayerSnapshot>();
            Array.Sort(players, (left, right) =>
            {
                int leftOrder = left != null ? left.PlayerId : int.MaxValue;
                int rightOrder = right != null ? right.PlayerId : int.MaxValue;
                return leftOrder.CompareTo(rightOrder);
            });

            for (int i = 0; i < rowCount; i++)
            {
                RoomPlayerSnapshot player = i < players.Length ? players[i] : null;
                int playerId = player != null ? player.PlayerId : i + 1;
                string fallbackName = "Computer " + playerId.ToString(CultureInfo.InvariantCulture);
                string playerName = player == null || string.IsNullOrWhiteSpace(player.PlayerName)
                    ? fallbackName
                    : player.PlayerName;
                string ready = player == null || player.IsAi || player.IsReady ? "ready" : "not ready";

                SetText(_lanRoomPlayerInfoTexts[i], "Player" + playerId.ToString(CultureInfo.InvariantCulture) + ":");
                SetText(_lanRoomPlayerNameTexts[i], playerName);
                SetText(_lanRoomPlayerReadyTexts[i], ready);
            }
        }

        private static void SetActive(GameObject gameObject, bool isActive)
        {
            if (gameObject != null && gameObject.activeSelf != isActive)
            {
                gameObject.SetActive(isActive);
            }
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
                return;
            }

            List<PlayerScoreSnapshot> visibleScores = BuildVisiblePlayerScoreList(snapshot.PlayerScores);
            int rowCount = Math.Max(_playerScoreTexts.Length, _playerHitTexts.Length);
            for (int i = 0; i < rowCount; i++)
            {
                if (i < visibleScores.Count)
                {
                    PlayerScoreSnapshot score = visibleScores[i];
                    SetTextAt(_playerScoreTexts, i, score.Score.ToString(CultureInfo.InvariantCulture));
                    SetTextAt(_playerHitTexts, i, FormatHitScore(score.HitScore));
                }
                else
                {
                    SetTextAt(_playerScoreTexts, i, string.Empty);
                    SetTextAt(_playerHitTexts, i, string.Empty);
                }
            }
        }

        private void ClearPlayerScorePanel()
        {
            int rowCount = Math.Max(_playerScoreTexts.Length, _playerHitTexts.Length);
            for (int i = 0; i < rowCount; i++)
            {
                SetTextAt(_playerScoreTexts, i, string.Empty);
                SetTextAt(_playerHitTexts, i, string.Empty);
            }
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
                    SetTextAt(_resultRankLabelTexts, i, FormatResultRankLabel(i));
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

        private static string FormatLanPlayerCount(RoomSnapshot snapshot)
        {
            int totalPlayers = snapshot?.Players != null ? snapshot.Players.Length : 0;
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
            string name = string.IsNullOrWhiteSpace(player.PlayerName)
                ? "Player" + player.PlayerId.ToString(CultureInfo.InvariantCulture)
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
