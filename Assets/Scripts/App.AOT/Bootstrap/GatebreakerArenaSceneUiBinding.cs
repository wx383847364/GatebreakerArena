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
        [SerializeField] private GameObject _topPanel2PRoot;
        [SerializeField] private GameObject _topPanel3PRoot;
        [SerializeField] private GameObject _topPanel4PRoot;
        [SerializeField] private TMP_Text _topPanel2PTimeText;
        [SerializeField] private TMP_Text _topPanel3PTimeText;
        [SerializeField] private TMP_Text _topPanel4PTimeText;
        [SerializeField] private TMP_Text[] _playerScore2PTexts;
        [SerializeField] private TMP_Text[] _playerHit2PTexts;
        [SerializeField] private TMP_Text[] _playerScore3PTexts;
        [SerializeField] private TMP_Text[] _playerHit3PTexts;
        [SerializeField] private TMP_Text[] _playerScore4PTexts;
        [SerializeField] private TMP_Text[] _playerHit4PTexts;
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
        [SerializeField] private Button _singleBattleButton;
        [SerializeField] private GameObject _singleSelectRoot;
        [SerializeField] private TMP_Text _singleSelectTitleText;
        [SerializeField] private Button _brickDuel1v1Button;
        [SerializeField] private Button _brickDuel1v2Button;
        [SerializeField] private Button _brickDuel1v3Button;
        [SerializeField] private Button _singleSelectBackButton;
        [SerializeField] private GameObject _brickDuelHudRoot;
        [SerializeField] private TMP_Text _brickDuelOpponentHealthText;
        [SerializeField] private TMP_Text _brickDuelPlayerHealthText;
        [SerializeField] private TMP_Text _brickDuelCenterText;
        [SerializeField] private TMP_Text _brickDuelStatusText;
        [SerializeField] private Graphic _brickDuelBottomCoreHitFeedback;
        [SerializeField] private Graphic _brickDuelTopCoreHitFeedback;
        [SerializeField] private Button _brickDuelPauseButton;
        [SerializeField] private RectTransform _brickDuelMovementPad;
        [SerializeField] private RectTransform _brickDuelMovementHandle;
        [SerializeField] private RectTransform _brickDuelMovementLeftArrowInput;
        [SerializeField] private RectTransform _brickDuelMovementRightArrowInput;
        [SerializeField] private Graphic _brickDuelMovementLeftArrowHighlight;
        [SerializeField] private Graphic _brickDuelMovementRightArrowHighlight;
        [SerializeField] private GameObject _loadoutRoot;
        [SerializeField] private TMP_Dropdown _loadoutHeroDropdown;
        [SerializeField] private TMP_Dropdown _loadoutPathDropdown;
        [SerializeField] private TMP_Dropdown _loadoutSignatureDropdown;
        [SerializeField] private TMP_Dropdown[] _loadoutUniversalChipDropdowns;
        [SerializeField] private Button _loadoutUseDefaultButton;
        [SerializeField] private Button _loadoutConfirmButton;
        [SerializeField] private Button _loadoutBackButton;
        [SerializeField] private TMP_Text _loadoutErrorText;
        [SerializeField] private GameObject _loadoutUnlockConfirmRoot;
        [SerializeField] private TMP_Text _loadoutUnlockConfirmText;
        [SerializeField] private Button _loadoutUnlockConfirmButton;
        [SerializeField] private Button _loadoutUnlockCancelButton;
        [SerializeField] private TMP_Text _heroHudText;
        [SerializeField] private Button _brickDuelAbilityButton;
        [SerializeField] private TMP_Text _brickDuelAbilityText;
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
        [SerializeField] private TMP_Dropdown _lanRoomTypeDropdown;
        [SerializeField] private TMP_InputField _lanRoomCodeInput;
        [SerializeField] private TMP_Text _lanStateText;
        [SerializeField] private TMP_Text _lanRoomCodeText;
        [SerializeField] private TMP_Text _lanPlayerCountText;
        [SerializeField] private TMP_Text _lanLocalIpText;
        [SerializeField] private TMP_Text _lanRoomIpText;
        [SerializeField] private TMP_Text _lanErrorText;
        [SerializeField] private TMP_Text[] _lanRoomPlayerInfoTexts;
        [SerializeField] private TMP_Text[] _lanRoomPlayerNameTexts;
        [SerializeField] private TMP_Text[] _lanRoomPlayerReadyTexts;
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
        public Object TopPanel2PRootObject => _topPanel2PRoot;
        public Object TopPanel3PRootObject => _topPanel3PRoot;
        public Object TopPanel4PRootObject => _topPanel4PRoot;
        public Object TopPanel2PTimeTextObject => _topPanel2PTimeText;
        public Object TopPanel3PTimeTextObject => _topPanel3PTimeText;
        public Object TopPanel4PTimeTextObject => _topPanel4PTimeText;
        public Object[] PlayerScore2PTextObjects => _playerScore2PTexts;
        public Object[] PlayerHit2PTextObjects => _playerHit2PTexts;
        public Object[] PlayerScore3PTextObjects => _playerScore3PTexts;
        public Object[] PlayerHit3PTextObjects => _playerHit3PTexts;
        public Object[] PlayerScore4PTextObjects => _playerScore4PTexts;
        public Object[] PlayerHit4PTextObjects => _playerHit4PTexts;
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
        public Object SingleBattleButtonObject => _singleBattleButton;
        public Object SingleSelectRootObject => _singleSelectRoot;
        public Object SingleSelectTitleTextObject => _singleSelectTitleText;
        public Object BrickDuel1v1ButtonObject => _brickDuel1v1Button;
        public Object BrickDuel1v2ButtonObject => _brickDuel1v2Button;
        public Object BrickDuel1v3ButtonObject => _brickDuel1v3Button;
        public Object SingleSelectBackButtonObject => _singleSelectBackButton;
        public Object BrickDuelHudRootObject => _brickDuelHudRoot;
        public Object BrickDuelOpponentHealthTextObject => _brickDuelOpponentHealthText;
        public Object BrickDuelPlayerHealthTextObject => _brickDuelPlayerHealthText;
        public Object BrickDuelCenterTextObject => _brickDuelCenterText;
        public Object BrickDuelStatusTextObject => _brickDuelStatusText;
        public Object BrickDuelBottomCoreHitFeedbackObject => _brickDuelBottomCoreHitFeedback;
        public Object BrickDuelTopCoreHitFeedbackObject => _brickDuelTopCoreHitFeedback;
        public Object BrickDuelPauseButtonObject => _brickDuelPauseButton;
        public Object BrickDuelMovementPadObject => _brickDuelMovementPad;
        public Object BrickDuelMovementHandleObject => _brickDuelMovementHandle;
        public Object BrickDuelMovementLeftArrowInputObject => _brickDuelMovementLeftArrowInput;
        public Object BrickDuelMovementRightArrowInputObject => _brickDuelMovementRightArrowInput;
        public Object BrickDuelMovementLeftArrowHighlightObject => _brickDuelMovementLeftArrowHighlight;
        public Object BrickDuelMovementRightArrowHighlightObject => _brickDuelMovementRightArrowHighlight;
        public Object LoadoutRootObject => _loadoutRoot;
        public Object LoadoutHeroDropdownObject => _loadoutHeroDropdown;
        public Object LoadoutPathDropdownObject => _loadoutPathDropdown;
        public Object LoadoutSignatureDropdownObject => _loadoutSignatureDropdown;
        public Object[] LoadoutUniversalChipDropdownObjects => _loadoutUniversalChipDropdowns;
        public Object LoadoutUseDefaultButtonObject => _loadoutUseDefaultButton;
        public Object LoadoutConfirmButtonObject => _loadoutConfirmButton;
        public Object LoadoutBackButtonObject => _loadoutBackButton;
        public Object LoadoutErrorTextObject => _loadoutErrorText;
        public Object LoadoutUnlockConfirmRootObject => _loadoutUnlockConfirmRoot;
        public Object LoadoutUnlockConfirmTextObject => _loadoutUnlockConfirmText;
        public Object LoadoutUnlockConfirmButtonObject => _loadoutUnlockConfirmButton;
        public Object LoadoutUnlockCancelButtonObject => _loadoutUnlockCancelButton;
        public Object HeroHudTextObject => _heroHudText;
        public Object BrickDuelAbilityButtonObject => _brickDuelAbilityButton;
        public Object BrickDuelAbilityTextObject => _brickDuelAbilityText;
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
        public Object LanRoomTypeDropdownObject => _lanRoomTypeDropdown;
        public Object LanRoomCodeInputObject => _lanRoomCodeInput;
        public Object LanStateTextObject => _lanStateText;
        public Object LanRoomCodeTextObject => _lanRoomCodeText;
        public Object LanPlayerCountTextObject => _lanPlayerCountText;
        public Object LanLocalIpTextObject => _lanLocalIpText;
        public Object LanRoomIpTextObject => _lanRoomIpText;
        public Object LanErrorTextObject => _lanErrorText;
        public Object[] LanRoomPlayerInfoTextObjects => _lanRoomPlayerInfoTexts;
        public Object[] LanRoomPlayerNameTextObjects => _lanRoomPlayerNameTexts;
        public Object[] LanRoomPlayerReadyTextObjects => _lanRoomPlayerReadyTexts;
        public Object StartCountdownRootObject => _startCountdownRoot;
        public Object StartCountdownTextObject => _startCountdownText;

        public bool HasRequiredBindings =>
            HasStaticCoreBindings && HasPhaseV03Bindings && _resultBodyText != null;

        public bool HasStaticCoreBindings =>
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
            _topPanel2PRoot != null &&
            _topPanel3PRoot != null &&
            _topPanel4PRoot != null &&
            _topPanel2PTimeText != null &&
            _topPanel3PTimeText != null &&
            _topPanel4PTimeText != null &&
            HasExactTextBindings(_playerScore2PTexts, 2) &&
            HasExactTextBindings(_playerHit2PTexts, 2) &&
            HasExactTextBindings(_playerScore3PTexts, 3) &&
            HasExactTextBindings(_playerHit3PTexts, 3) &&
            HasExactTextBindings(_playerScore4PTexts, 4) &&
            HasExactTextBindings(_playerHit4PTexts, 4) &&
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
            _singleBattleButton != null &&
            _singleSelectRoot != null &&
            _singleSelectTitleText != null &&
            _brickDuel1v1Button != null &&
            _brickDuel1v2Button != null &&
            _brickDuel1v3Button != null &&
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
            _brickDuelMovementHandle != null &&
            _brickDuelMovementLeftArrowInput != null &&
            _brickDuelMovementRightArrowInput != null &&
            _brickDuelMovementLeftArrowHighlight != null &&
            _brickDuelMovementRightArrowHighlight != null &&
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
            _lanRoomTypeDropdown != null &&
            _lanRoomCodeInput != null &&
            _lanStateText != null &&
            _lanRoomCodeText != null &&
            _lanPlayerCountText != null &&
            _lanLocalIpText != null &&
            _lanRoomIpText != null &&
            _lanErrorText != null &&
            HasTextBindings(_lanRoomPlayerInfoTexts) &&
            HasTextBindings(_lanRoomPlayerNameTexts) &&
            HasTextBindings(_lanRoomPlayerReadyTexts) &&
            _startCountdownRoot != null &&
            _startCountdownText != null;

        private bool HasPhaseV03Bindings =>
            _brickDuelAbilityButton != null &&
            _brickDuelAbilityText != null &&
            _loadoutRoot != null &&
            _loadoutHeroDropdown != null &&
            _loadoutPathDropdown != null &&
            _loadoutSignatureDropdown != null &&
            HasExactDropdownBindings(_loadoutUniversalChipDropdowns, 5) &&
            _loadoutUseDefaultButton != null &&
            _loadoutConfirmButton != null &&
            _loadoutBackButton != null &&
            _loadoutErrorText != null &&
            _loadoutUnlockConfirmRoot != null &&
            _loadoutUnlockConfirmText != null &&
            _loadoutUnlockConfirmButton != null &&
            _loadoutUnlockCancelButton != null &&
            _heroHudText != null;

        private void Awake()
        {
            EnsureResultBodyRuntimeBinding();
            EnsurePhaseV03RuntimeBindings();
            GatebreakerArenaSceneUiBindingRegistry.Register(this);
        }

        private void OnDestroy()
        {
            GatebreakerArenaSceneUiBindingRegistry.Clear(this);
        }

        private void EnsurePhaseV03RuntimeBindings()
        {
            if (HasPhaseV03Bindings)
            {
                return;
            }

            if (_lanRoot == null || _lanRoomTypeDropdown == null || _brickDuelHudRoot == null)
            {
                return;
            }

            TMP_Text textTemplate = _lanErrorText != null
                ? _lanErrorText
                : _lanRoomTypeDropdown.captionText;
            Transform panel = _loadoutRoot != null
                ? _loadoutRoot.transform
                : ResolveDirectParent(
                    _loadoutHeroDropdown,
                    _loadoutPathDropdown,
                    _loadoutSignatureDropdown,
                    _loadoutUseDefaultButton,
                    _loadoutConfirmButton,
                    _loadoutBackButton,
                    _loadoutErrorText);
            bool createdPanel = panel == null;
            if (createdPanel)
            {
                panel = CreatePanel(
                    _lanRoot.transform,
                    "PhaseV03LoadoutPanel",
                    new Vector2(560f, 650f),
                    new Color(0f, 0f, 0f, 0.84f));
                CreateText(panel, textTemplate, "Title", "英雄与相位科技", 26f,
                    new Vector2(0f, 292f), new Vector2(500f, 36f));
            }

            _loadoutRoot = panel.gameObject;
            if (_loadoutHeroDropdown == null)
                _loadoutHeroDropdown = CloneDropdown(panel, "HeroDropdown", new Vector2(60f, 232f));
            if (_loadoutPathDropdown == null)
                _loadoutPathDropdown = CloneDropdown(panel, "DimensionDropdown", new Vector2(60f, 178f));
            if (_loadoutSignatureDropdown == null)
                _loadoutSignatureDropdown = CloneDropdown(panel, "AbilityDropdown", new Vector2(60f, 124f));

            TMP_Dropdown[] phaseDropdowns = new TMP_Dropdown[5];
            if (_loadoutUniversalChipDropdowns != null)
            {
                for (int i = 0; i < Mathf.Min(phaseDropdowns.Length, _loadoutUniversalChipDropdowns.Length); i++)
                    phaseDropdowns[i] = _loadoutUniversalChipDropdowns[i];
            }
            for (int i = 0; i < phaseDropdowns.Length; i++)
            {
                if (phaseDropdowns[i] == null)
                {
                    phaseDropdowns[i] = CloneDropdown(
                        panel,
                        "PhaseTechDropdown" + (i + 1),
                        new Vector2(60f, 70f - i * 54f));
                }
            }
            _loadoutUniversalChipDropdowns = phaseDropdowns;

            if (_loadoutUseDefaultButton == null)
                _loadoutUseDefaultButton = CreateButton(panel, textTemplate, "UseDefaultButton", "一键使用",
                    new Vector2(-112f, -236f), new Vector2(180f, 42f), new Color(0.12f, 0.38f, 0.72f, 1f));
            if (_loadoutConfirmButton == null)
                _loadoutConfirmButton = CreateButton(panel, textTemplate, "ConfirmButton", "确认构筑",
                    new Vector2(112f, -236f), new Vector2(180f, 42f), new Color(0.08f, 0.62f, 0.22f, 1f));
            if (_loadoutBackButton == null)
                _loadoutBackButton = CreateButton(panel, textTemplate, "BackButton", "返回",
                    new Vector2(-220f, 292f), new Vector2(92f, 36f), new Color(0.16f, 0.16f, 0.2f, 1f));
            if (_loadoutErrorText == null)
                _loadoutErrorText = CreateText(panel, textTemplate, "ErrorText", string.Empty, 13f,
                    new Vector2(0f, -282f), new Vector2(500f, 42f));

            Transform confirm = _loadoutUnlockConfirmRoot != null
                ? _loadoutUnlockConfirmRoot.transform
                : ResolveDirectParent(
                    _loadoutUnlockConfirmText,
                    _loadoutUnlockConfirmButton,
                    _loadoutUnlockCancelButton);
            if (confirm == null)
            {
                confirm = CreatePanel(
                    panel,
                    "UnlockConfirmRoot",
                    Vector2.zero,
                    new Color(0f, 0f, 0f, 0.9f),
                    true);
            }
            _loadoutUnlockConfirmRoot = confirm.gameObject;
            if (_loadoutUnlockConfirmText == null)
                _loadoutUnlockConfirmText = CreateText(confirm, textTemplate, "ConfirmText", "确认解锁相位科技？", 20f,
                    new Vector2(0f, 48f), new Vector2(470f, 100f));
            if (_loadoutUnlockConfirmButton == null)
                _loadoutUnlockConfirmButton = CreateButton(confirm, textTemplate, "ConfirmButton", "确认解锁",
                    new Vector2(-105f, -42f), new Vector2(180f, 44f), new Color(0.08f, 0.62f, 0.22f, 1f));
            if (_loadoutUnlockCancelButton == null)
                _loadoutUnlockCancelButton = CreateButton(confirm, textTemplate, "CancelButton", "取消",
                    new Vector2(105f, -42f), new Vector2(180f, 44f), new Color(0.32f, 0.32f, 0.36f, 1f));
            confirm.gameObject.SetActive(false);
            if (createdPanel) panel.gameObject.SetActive(false);

            if (_brickDuelAbilityButton == null)
            {
                _brickDuelAbilityButton = CreateButton(
                    _brickDuelHudRoot.transform,
                    textTemplate,
                    "BrickDuelAbilityButton",
                    "主动技能 · P3解锁",
                    new Vector2(250f, -360f),
                    new Vector2(210f, 46f),
                    new Color(0.42f, 0.16f, 0.68f, 1f),
                    out _brickDuelAbilityText);
            }
            else if (_brickDuelAbilityText == null)
            {
                _brickDuelAbilityText = CreateText(
                    _brickDuelAbilityButton.transform,
                    textTemplate,
                    "Text (TMP)",
                    "主动技能 · P3解锁",
                    20f,
                    Vector2.zero,
                    new Vector2(210f, 46f));
            }

            if (_heroHudText == null && _skillButton != null)
            {
                _heroHudText = CreateText(
                    _skillButton.transform.parent,
                    textTemplate,
                    "PhaseHeroHudText",
                    string.Empty,
                    13f,
                    new Vector2(0f, 82f),
                    new Vector2(520f, 42f));
            }
        }

        private void EnsureResultBodyRuntimeBinding()
        {
            if (_resultBodyText != null || _resultRoot == null || _resultTitleText == null)
            {
                return;
            }

            _resultBodyText = CreateText(
                _resultRoot.transform,
                _resultTitleText,
                "BrickDuelResultBodyText",
                string.Empty,
                Mathf.Max(18f, _resultTitleText.fontSize * 0.55f),
                new Vector2(0f, -72f),
                new Vector2(520f, 92f));
        }

        private static Transform ResolveDirectParent(params Component[] components)
        {
            if (components == null) return null;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].transform.parent != null)
                    return components[i].transform.parent;
            }
            return null;
        }

        private TMP_Dropdown CloneDropdown(Transform parent, string name, Vector2 anchoredPosition)
        {
            TMP_Dropdown dropdown = Instantiate(_lanRoomTypeDropdown, parent, false);
            dropdown.name = name;
            dropdown.onValueChanged.RemoveAllListeners();
            RectTransform rect = dropdown.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(360f, 36f);
            return dropdown;
        }

        private static Transform CreatePanel(
            Transform parent,
            string name,
            Vector2 size,
            Color color,
            bool stretch = false)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = stretch ? Vector2.zero : new Vector2(0.5f, 0.52f);
            rect.anchorMax = stretch ? Vector2.one : new Vector2(0.5f, 0.52f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = stretch ? Vector2.zero : size;
            gameObject.GetComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text CreateText(
            Transform parent,
            TMP_Text template,
            string name,
            string value,
            float fontSize,
            Vector2 position,
            Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = gameObject.GetComponent<TMP_Text>();
            if (template != null)
            {
                text.font = template.font;
                text.fontSharedMaterial = template.fontSharedMaterial;
                text.color = template.color;
            }
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            TMP_Text textTemplate,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            return CreateButton(parent, textTemplate, name, label, position, size, color, out _);
        }

        private static Button CreateButton(
            Transform parent,
            TMP_Text textTemplate,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color,
            out TMP_Text labelText)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            labelText = CreateText(rect, textTemplate, "Text (TMP)", label, 20f, Vector2.zero, size);
            return button;
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

        private static bool HasExactDropdownBindings(TMP_Dropdown[] dropdowns, int expected)
        {
            if (dropdowns == null || dropdowns.Length != expected)
            {
                return false;
            }

            for (int i = 0; i < dropdowns.Length; i++)
            {
                if (dropdowns[i] == null) return false;
            }

            return true;
        }

        private static bool HasExactTextBindings(TMP_Text[] texts, int expectedLength)
        {
            return texts != null &&
                   texts.Length == expectedLength &&
                   HasTextBindings(texts);
        }
    }
}
