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
using UnityEngine.UI;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class GatebreakerArenaSceneUiCallbacks
    {
        public Action ServeRequested { get; set; }
        public Action CreateLanHostRequested { get; set; }
        public Action StartLanDiscoveryRequested { get; set; }
        public Action JoinLanRoomRequested { get; set; }
        public Action ToggleLanReadyRequested { get; set; }
        public Action StartLanLoadingRequested { get; set; }
        public Action LeaveLanRoomRequested { get; set; }
        public Action AcknowledgeLanStartRequested { get; set; }
        public Action<string> LanPlayerNameChanged { get; set; }
        public Action<string> LanRoomCodeChanged { get; set; }
        public Action<int> HitOffsetInfluenceChanged { get; set; }
        public Action<int> PaddleVelocityInfluenceChanged { get; set; }
        public Action<int> MinimumOutwardShareChanged { get; set; }
        public string InitialLanPlayerName { get; set; }
        public string InitialLanRoomCode { get; set; }
    }

    public sealed class GatebreakerArenaSceneBindingService
    {
        private readonly List<ButtonListener> _buttonListeners = new List<ButtonListener>();
        private readonly List<InputListener> _inputListeners = new List<InputListener>();
        private readonly List<SliderListener> _sliderListeners = new List<SliderListener>();
        private Button _skillButton;
        private Button _lanCreateButton;
        private Button _lanDiscoverButton;
        private Button _lanJoinButton;
        private Button _lanReadyButton;
        private Button _lanStartButton;
        private Button _lanLeaveButton;
        private Button _lanAcknowledgeStartButton;
        private TMP_InputField _lanPlayerNameInput;
        private TMP_InputField _lanRoomCodeInput;
        private TMP_Text _ballCountText;
        private TMP_Text _hudTitleText;
        private TMP_Text _hudStatusText;
        private TMP_Text _hudScoreText;
        private TMP_Text _hudServeText;
        private TMP_Text _hudBallText;
        private TMP_Text[] _playerScoreTexts = Array.Empty<TMP_Text>();
        private TMP_Text[] _playerHitTexts = Array.Empty<TMP_Text>();
        private TMP_Text _resultTitleText;
        private TMP_Text _resultBodyText;
        private TMP_Text _resultScoreText;
        private TMP_Text _gmHitOffsetValueText;
        private TMP_Text _gmPaddleVelocityValueText;
        private TMP_Text _gmMinimumOutwardValueText;
        private TMP_Text _lanStateText;
        private TMP_Text _lanRoomCodeText;
        private TMP_Text _lanPlayerCountText;
        private TMP_Text _lanLocalIpText;
        private TMP_Text _lanRoomIpText;
        private TMP_Text _lanErrorText;
        private Slider _gmHitOffsetSlider;
        private Slider _gmPaddleVelocitySlider;
        private Slider _gmMinimumOutwardSlider;
        private GameObject _hudRoot;
        private GameObject _resultRoot;
        private GameObject _gmRoot;
        private GameObject _lanRoot;
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
            _hudRoot = RequireGameObject(binding.HudRootObject, nameof(binding.HudRootObject));
            _hudTitleText = Require<TMP_Text>(binding.HudTitleTextObject, nameof(binding.HudTitleTextObject));
            _hudStatusText = Require<TMP_Text>(binding.HudStatusTextObject, nameof(binding.HudStatusTextObject));
            _hudScoreText = Require<TMP_Text>(binding.HudScoreTextObject, nameof(binding.HudScoreTextObject));
            _hudServeText = Require<TMP_Text>(binding.HudServeTextObject, nameof(binding.HudServeTextObject));
            _hudBallText = Require<TMP_Text>(binding.HudBallTextObject, nameof(binding.HudBallTextObject));
            _playerScoreTexts = RequireTextArray(binding.PlayerScoreTextObjects, nameof(binding.PlayerScoreTextObjects));
            _playerHitTexts = RequireTextArray(binding.PlayerHitTextObjects, nameof(binding.PlayerHitTextObjects));
            _resultRoot = RequireGameObject(binding.ResultRootObject, nameof(binding.ResultRootObject));
            _resultTitleText = Require<TMP_Text>(binding.ResultTitleTextObject, nameof(binding.ResultTitleTextObject));
            _resultBodyText = Require<TMP_Text>(binding.ResultBodyTextObject, nameof(binding.ResultBodyTextObject));
            _resultScoreText = Require<TMP_Text>(binding.ResultScoreTextObject, nameof(binding.ResultScoreTextObject));
            _gmRoot = RequireGameObject(binding.GmRootObject, nameof(binding.GmRootObject));
            _gmHitOffsetSlider = Require<Slider>(binding.GmHitOffsetSliderObject, nameof(binding.GmHitOffsetSliderObject));
            _gmHitOffsetValueText = Require<TMP_Text>(binding.GmHitOffsetValueTextObject, nameof(binding.GmHitOffsetValueTextObject));
            _gmPaddleVelocitySlider = Require<Slider>(binding.GmPaddleVelocitySliderObject, nameof(binding.GmPaddleVelocitySliderObject));
            _gmPaddleVelocityValueText = Require<TMP_Text>(binding.GmPaddleVelocityValueTextObject, nameof(binding.GmPaddleVelocityValueTextObject));
            _gmMinimumOutwardSlider = Require<Slider>(binding.GmMinimumOutwardSliderObject, nameof(binding.GmMinimumOutwardSliderObject));
            _gmMinimumOutwardValueText = Require<TMP_Text>(binding.GmMinimumOutwardValueTextObject, nameof(binding.GmMinimumOutwardValueTextObject));
            _lanRoot = RequireGameObject(binding.LanRootObject, nameof(binding.LanRootObject));
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

            AddButtonListener(_skillButton, HandleSkillButtonClicked);
            AddButtonListener(_lanCreateButton, () => _callbacks.CreateLanHostRequested?.Invoke());
            AddButtonListener(_lanDiscoverButton, () => _callbacks.StartLanDiscoveryRequested?.Invoke());
            AddButtonListener(_lanJoinButton, () => _callbacks.JoinLanRoomRequested?.Invoke());
            AddButtonListener(_lanReadyButton, () => _callbacks.ToggleLanReadyRequested?.Invoke());
            AddButtonListener(_lanStartButton, () => _callbacks.StartLanLoadingRequested?.Invoke());
            AddButtonListener(_lanLeaveButton, () => _callbacks.LeaveLanRoomRequested?.Invoke());
            AddButtonListener(_lanAcknowledgeStartButton, () => _callbacks.AcknowledgeLanStartRequested?.Invoke());
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
            SetActive(_lanRoot, true);
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
                return;
            }

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
            SetActive(_lanRoot, snapshot != null);
            if (snapshot == null)
            {
                return;
            }

            SetText(_lanStateText, $"状态：{FormatLanRoomState(snapshot.State)}");
            SetText(_lanRoomCodeText, $"房间号：{(string.IsNullOrEmpty(snapshot.RoomCode) ? "-" : snapshot.RoomCode)}");
            SetText(_lanPlayerCountText, $"人数：{snapshot.Players.Length}/{Mathf.Max(1, snapshot.MaxPlayers)}");
            SetText(_lanLocalIpText, $"本机 IP：{(string.IsNullOrEmpty(localIp) ? "-" : localIp)}");
            SetText(_lanRoomIpText, $"房间 IP：{(string.IsNullOrEmpty(roomIp) ? "-" : roomIp)}");
            SetText(_lanErrorText, string.IsNullOrEmpty(snapshot.Error) ? string.Empty : TruncateLanStatus(snapshot.Error));

            if (_lanStartButton != null)
            {
                _lanStartButton.interactable = snapshot.CanStart;
            }

            SetActive(
                _lanAcknowledgeStartButton != null ? _lanAcknowledgeStartButton.gameObject : null,
                snapshot.State == LanRoomState.Loading && !snapshot.IsHost);
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
            _skillButton = null;
            _lanCreateButton = null;
            _lanDiscoverButton = null;
            _lanJoinButton = null;
            _lanReadyButton = null;
            _lanStartButton = null;
            _lanLeaveButton = null;
            _lanAcknowledgeStartButton = null;
            _lanPlayerNameInput = null;
            _lanRoomCodeInput = null;
            _ballCountText = null;
            _hudTitleText = null;
            _hudStatusText = null;
            _hudScoreText = null;
            _hudServeText = null;
            _hudBallText = null;
            _playerScoreTexts = Array.Empty<TMP_Text>();
            _playerHitTexts = Array.Empty<TMP_Text>();
            _resultTitleText = null;
            _resultBodyText = null;
            _resultScoreText = null;
            _gmHitOffsetValueText = null;
            _gmPaddleVelocityValueText = null;
            _gmMinimumOutwardValueText = null;
            _lanStateText = null;
            _lanRoomCodeText = null;
            _lanPlayerCountText = null;
            _lanLocalIpText = null;
            _lanRoomIpText = null;
            _lanErrorText = null;
            _gmHitOffsetSlider = null;
            _gmPaddleVelocitySlider = null;
            _gmMinimumOutwardSlider = null;
            _hudRoot = null;
            _resultRoot = null;
            _gmRoot = null;
            _lanRoot = null;
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

        private static void SetSliderValue(Slider slider, int value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value;
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
    }
}
