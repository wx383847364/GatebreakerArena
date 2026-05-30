using App.Shared.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.AOT.Bootstrap
{
    /// <summary>
    /// AOT 场景引用桥：只保存 Unity 场景中的 UI 对象引用，不承载玩法或 UI 业务。
    /// </summary>
    public sealed class GatebreakerArenaSceneUiBinding : MonoBehaviour, IGatebreakerArenaSceneUiBinding
    {
        [SerializeField] private Button _skillButton;
        [SerializeField] private TMP_Text _ballCountText;
        [SerializeField] private RectTransform _movementPad;
        [SerializeField] private RectTransform _movementHandle;
        [SerializeField] private RectTransform _movementLeftArrowInput;
        [SerializeField] private RectTransform _movementRightArrowInput;
        [SerializeField] private Graphic _movementLeftArrowHighlight;
        [SerializeField] private Graphic _movementRightArrowHighlight;
        [SerializeField] private GameObject _hudRoot;
        [SerializeField] private TMP_Text _hudTitleText;
        [SerializeField] private TMP_Text _hudStatusText;
        [SerializeField] private TMP_Text _hudScoreText;
        [SerializeField] private TMP_Text _hudServeText;
        [SerializeField] private TMP_Text _hudBallText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text[] _playerScoreTexts;
        [SerializeField] private TMP_Text[] _playerHitTexts;
        [SerializeField] private GameObject _resultRoot;
        [SerializeField] private TMP_Text _resultTitleText;
        [SerializeField] private TMP_Text _resultBodyText;
        [SerializeField] private TMP_Text _resultScoreText;
        [SerializeField] private TMP_Text[] _resultRankLabelTexts;
        [SerializeField] private TMP_Text[] _resultRankNameTexts;
        [SerializeField] private Button _resultRestartButton;
        [SerializeField] private Button _resultBackButton;
        [SerializeField] private GameObject _gmRoot;
        [SerializeField] private Slider _gmHitOffsetSlider;
        [SerializeField] private TMP_Text _gmHitOffsetValueText;
        [SerializeField] private Slider _gmPaddleVelocitySlider;
        [SerializeField] private TMP_Text _gmPaddleVelocityValueText;
        [SerializeField] private Slider _gmMinimumOutwardSlider;
        [SerializeField] private TMP_Text _gmMinimumOutwardValueText;
        [SerializeField] private GameObject _lanRoot;
        [SerializeField] private GameObject _modeSelectRoot;
        [SerializeField] private Button _localBattleButton;
        [SerializeField] private Button _onlineBattleButton;
        [SerializeField] private GameObject _lanMenuRoot;
        [SerializeField] private GameObject _lanRoomInfoRoot;
        [SerializeField] private GameObject _lanStatusRoot;
        [SerializeField] private Button _lanBackButton;
        [SerializeField] private Button _lanCreateButton;
        [SerializeField] private Button _lanDiscoverButton;
        [SerializeField] private Button _lanJoinButton;
        [SerializeField] private Button _lanReadyButton;
        [SerializeField] private Button _lanStartButton;
        [SerializeField] private Button _lanLeaveButton;
        [SerializeField] private Button _lanAcknowledgeStartButton;
        [SerializeField] private TMP_InputField _lanPlayerNameInput;
        [SerializeField] private TMP_InputField _lanRoomCodeInput;
        [SerializeField] private TMP_Text _lanStateText;
        [SerializeField] private TMP_Text _lanRoomCodeText;
        [SerializeField] private TMP_Text _lanPlayerCountText;
        [SerializeField] private TMP_Text _lanLocalIpText;
        [SerializeField] private TMP_Text _lanRoomIpText;
        [SerializeField] private TMP_Text _lanErrorText;
        [SerializeField] private GameObject _startCountdownRoot;
        [SerializeField] private TMP_Text _startCountdownText;

        public Object SkillButtonObject => _skillButton;
        public Object BallCountTextObject => _ballCountText;
        public Object MovementPadObject => _movementPad;
        public Object MovementHandleObject => _movementHandle;
        public Object MovementLeftArrowInputObject => _movementLeftArrowInput;
        public Object MovementRightArrowInputObject => _movementRightArrowInput;
        public Object MovementLeftArrowHighlightObject => _movementLeftArrowHighlight;
        public Object MovementRightArrowHighlightObject => _movementRightArrowHighlight;
        public Object HudRootObject => _hudRoot;
        public Object HudTitleTextObject => _hudTitleText;
        public Object HudStatusTextObject => _hudStatusText;
        public Object HudScoreTextObject => _hudScoreText;
        public Object HudServeTextObject => _hudServeText;
        public Object HudBallTextObject => _hudBallText;
        public Object TimeTextObject => _timeText;
        public Object[] PlayerScoreTextObjects => _playerScoreTexts;
        public Object[] PlayerHitTextObjects => _playerHitTexts;
        public Object ResultRootObject => _resultRoot;
        public Object ResultTitleTextObject => _resultTitleText;
        public Object ResultBodyTextObject => _resultBodyText;
        public Object ResultScoreTextObject => _resultScoreText;
        public Object[] ResultRankLabelTextObjects => _resultRankLabelTexts;
        public Object[] ResultRankNameTextObjects => _resultRankNameTexts;
        public Object ResultRestartButtonObject => _resultRestartButton;
        public Object ResultBackButtonObject => _resultBackButton;
        public Object GmRootObject => _gmRoot;
        public Object GmHitOffsetSliderObject => _gmHitOffsetSlider;
        public Object GmHitOffsetValueTextObject => _gmHitOffsetValueText;
        public Object GmPaddleVelocitySliderObject => _gmPaddleVelocitySlider;
        public Object GmPaddleVelocityValueTextObject => _gmPaddleVelocityValueText;
        public Object GmMinimumOutwardSliderObject => _gmMinimumOutwardSlider;
        public Object GmMinimumOutwardValueTextObject => _gmMinimumOutwardValueText;
        public Object LanRootObject => _lanRoot;
        public Object ModeSelectRootObject => _modeSelectRoot;
        public Object LocalBattleButtonObject => _localBattleButton;
        public Object OnlineBattleButtonObject => _onlineBattleButton;
        public Object LanMenuRootObject => _lanMenuRoot;
        public Object LanRoomInfoRootObject => _lanRoomInfoRoot;
        public Object LanStatusRootObject => _lanStatusRoot;
        public Object LanBackButtonObject => _lanBackButton;
        public Object LanCreateButtonObject => _lanCreateButton;
        public Object LanDiscoverButtonObject => _lanDiscoverButton;
        public Object LanJoinButtonObject => _lanJoinButton;
        public Object LanReadyButtonObject => _lanReadyButton;
        public Object LanStartButtonObject => _lanStartButton;
        public Object LanLeaveButtonObject => _lanLeaveButton;
        public Object LanAcknowledgeStartButtonObject => _lanAcknowledgeStartButton;
        public Object LanPlayerNameInputObject => _lanPlayerNameInput;
        public Object LanRoomCodeInputObject => _lanRoomCodeInput;
        public Object LanStateTextObject => _lanStateText;
        public Object LanRoomCodeTextObject => _lanRoomCodeText;
        public Object LanPlayerCountTextObject => _lanPlayerCountText;
        public Object LanLocalIpTextObject => _lanLocalIpText;
        public Object LanRoomIpTextObject => _lanRoomIpText;
        public Object LanErrorTextObject => _lanErrorText;
        public Object StartCountdownRootObject => _startCountdownRoot;
        public Object StartCountdownTextObject => _startCountdownText;

        public bool HasRequiredBindings =>
            _skillButton != null &&
            _ballCountText != null &&
            _movementPad != null &&
            _movementHandle != null &&
            _movementLeftArrowInput != null &&
            _movementRightArrowInput != null &&
            _movementLeftArrowHighlight != null &&
            _movementRightArrowHighlight != null &&
            _timeText != null &&
            HasTextBindings(_playerScoreTexts) &&
            HasTextBindings(_playerHitTexts) &&
            _resultRoot != null &&
            _resultTitleText != null &&
            HasTextBindings(_resultRankLabelTexts) &&
            HasTextBindings(_resultRankNameTexts) &&
            _resultRestartButton != null &&
            _resultBackButton != null &&
            _gmRoot != null &&
            _gmHitOffsetSlider != null &&
            _gmHitOffsetValueText != null &&
            _gmPaddleVelocitySlider != null &&
            _gmPaddleVelocityValueText != null &&
            _gmMinimumOutwardSlider != null &&
            _gmMinimumOutwardValueText != null &&
            _lanRoot != null &&
            _modeSelectRoot != null &&
            _localBattleButton != null &&
            _onlineBattleButton != null &&
            _lanMenuRoot != null &&
            _lanRoomInfoRoot != null &&
            _lanStatusRoot != null &&
            _lanBackButton != null &&
            _lanCreateButton != null &&
            _lanDiscoverButton != null &&
            _lanJoinButton != null &&
            _lanReadyButton != null &&
            _lanStartButton != null &&
            _lanLeaveButton != null &&
            _lanAcknowledgeStartButton != null &&
            _lanPlayerNameInput != null &&
            _lanRoomCodeInput != null &&
            _lanStateText != null &&
            _lanRoomCodeText != null &&
            _lanPlayerCountText != null &&
            _lanLocalIpText != null &&
            _lanRoomIpText != null &&
            _lanErrorText != null &&
            _startCountdownRoot != null &&
            _startCountdownText != null;

        private void Awake()
        {
            GatebreakerArenaSceneUiBindingRegistry.Register(this);
        }

        private void OnDestroy()
        {
            GatebreakerArenaSceneUiBindingRegistry.Clear(this);
        }

#if UNITY_EDITOR
        public void AssignForEditor(Button skillButton, TMP_Text ballCountText)
        {
            _skillButton = skillButton;
            _ballCountText = ballCountText;
        }
#endif

        private static bool HasTextBindings(TMP_Text[] texts)
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
    }
}
