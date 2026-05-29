using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Paddle;
using App.HotUpdate.GatebreakerArena.UI;
using App.Shared.Contracts;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerSceneBindingServiceTests
    {
        private GameObject _root;
        private TestSceneUiBinding _binding;
        private GatebreakerArenaSceneBindingService _service;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Gatebreaker UI Test Root");
            _binding = TestSceneUiBinding.Create(_root.transform);
            _service = new GatebreakerArenaSceneBindingService();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Clear();
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void SkillButtonClickRequestsServeAndBallCountDisplaysSnapshotAmmo()
        {
            int serveRequests = 0;
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks { ServeRequested = () => serveRequests++ },
                null);

            _binding.SkillButton.onClick.Invoke();
            _service.UpdateHud(
                new GatebreakerHudSnapshot
                {
                    Phase = MatchPhase.Playing,
                    CurrentServeAmmo = 3,
                    MaxServeAmmo = 5,
                    MaxOwnedBallsInField = 2,
                },
                ServeBlockReason.None);

            Assert.AreEqual(1, serveRequests);
            Assert.AreEqual("3", _binding.BallCountText.text);
        }

        [Test]
        public void TimeTextDisplaysRemainingCountdown()
        {
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks(),
                null);

            _service.UpdateHud(
                new GatebreakerHudSnapshot
                {
                    Phase = MatchPhase.Playing,
                    RemainingTime = 59.1f,
                },
                ServeBlockReason.None);

            Assert.AreEqual("01:00", _binding.TimeText.text);
        }

        [Test]
        public void PlayerScorePanelDisplaysScoreAndHitFromSnapshot()
        {
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks(),
                null);

            _service.UpdateHud(
                new GatebreakerHudSnapshot
                {
                    Phase = MatchPhase.Playing,
                    PlayerScores = new[]
                    {
                        new PlayerScoreSnapshot(3, 3, false, 0, -1, 0),
                        new PlayerScoreSnapshot(1, 1, false, 1, 0, 1),
                        new PlayerScoreSnapshot(2, 2, true, 5, -2, 2),
                    },
                },
                ServeBlockReason.None);

            Assert.AreEqual("1", _binding.PlayerScoreTexts[0].text);
            Assert.AreEqual("0", _binding.PlayerHitTexts[0].text);
            Assert.AreEqual("0", _binding.PlayerScoreTexts[1].text);
            Assert.AreEqual("-1", _binding.PlayerHitTexts[1].text);
            Assert.AreEqual(string.Empty, _binding.PlayerScoreTexts[2].text);
            Assert.AreEqual(string.Empty, _binding.PlayerHitTexts[2].text);
        }

        [Test]
        public void ResultPanelDisplaysRankRowsWhenMatchEnds()
        {
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks(),
                null);

            _service.UpdateResult(
                new GatebreakerHudSnapshot
                {
                    Phase = MatchPhase.Result,
                    HasWinner = true,
                    WinnerPlayerId = 3,
                    PlayerScores = new[]
                    {
                        new PlayerScoreSnapshot(3, 3, false, 4, 0, 3),
                        new PlayerScoreSnapshot(1, 1, false, 2, -1, 1),
                        new PlayerScoreSnapshot(2, 2, false, 1, -2, 2),
                    },
                });

            Assert.IsTrue(_binding.ResultRoot.activeSelf);
            Assert.AreEqual("No.1:", _binding.ResultRankLabelTexts[0].text);
            StringAssert.Contains("Player3", _binding.ResultRankNameTexts[0].text);
            StringAssert.Contains("WIN", _binding.ResultRankNameTexts[0].text);
        }

        [Test]
        public void MovementPadForwardsAxisAndResetsHandle()
        {
            float moveAxis = 0f;
            _binding.MovementPad.sizeDelta = new Vector2(200f, 68f);
            _binding.MovementHandle.sizeDelta = new Vector2(40f, 40f);
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks { MoveAxisChanged = axis => moveAxis = axis },
                null);

            InvokeMovementTrigger(_binding.MovementPad, EventTriggerType.PointerDown, new Vector2(100f, 0f));

            Assert.Greater(moveAxis, 0.9f);
            Assert.Greater(_binding.MovementHandle.anchoredPosition.x, 0f);

            InvokeMovementTrigger(_binding.MovementPad, EventTriggerType.PointerUp, new Vector2(100f, 0f));

            Assert.AreEqual(0f, moveAxis);
            Assert.AreEqual(Vector2.zero, _binding.MovementHandle.anchoredPosition);
        }

        private static void InvokeMovementTrigger(RectTransform target, EventTriggerType eventType, Vector2 screenPosition)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            Assert.NotNull(trigger);
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition,
            };

            for (int i = 0; i < trigger.triggers.Count; i++)
            {
                EventTrigger.Entry entry = trigger.triggers[i];
                if (entry.eventID == eventType)
                {
                    entry.callback.Invoke(eventData);
                    return;
                }
            }

            Assert.Fail($"Missing movement trigger {eventType}.");
        }

        [Test]
        public void GmSlidersForwardRoundedValuesAndRefreshValueText()
        {
            int hitOffsetValue = -1;
            int paddleVelocityValue = -1;
            int minimumOutwardValue = -1;
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks
                {
                    HitOffsetInfluenceChanged = value => hitOffsetValue = value,
                    PaddleVelocityInfluenceChanged = value => paddleVelocityValue = value,
                    MinimumOutwardShareChanged = value => minimumOutwardValue = value,
                },
                null);

            _binding.HitOffsetSlider.value = 121f;
            _binding.PaddleVelocitySlider.value = 67f;
            _binding.MinimumOutwardSlider.value = 34f;
            PaddleBounceTuning tuning = PaddleBounceTuning.CreateDefault();
            tuning.SetHitOffsetInfluenceValue(121);
            tuning.SetPaddleVelocityInfluenceValue(67);
            tuning.SetMinimumOutwardShareValue(34);
            _service.UpdateBounceTuning(tuning, MatchPhase.Playing);

            Assert.AreEqual(121, hitOffsetValue);
            Assert.AreEqual(67, paddleVelocityValue);
            Assert.AreEqual(34, minimumOutwardValue);
            StringAssert.Contains("命中位置影响：121", _binding.HitOffsetValueText.text);
            StringAssert.Contains("板速影响：67", _binding.PaddleVelocityValueText.text);
            StringAssert.Contains("最小离板分量：34", _binding.MinimumOutwardValueText.text);
        }

        [Test]
        public void LanButtonsAndInputsForwardCallbacks()
        {
            int createCount = 0;
            int discoverCount = 0;
            int joinCount = 0;
            int readyCount = 0;
            int startCount = 0;
            int leaveCount = 0;
            int acknowledgeCount = 0;
            string playerName = null;
            string roomCode = null;
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks
                {
                    CreateLanHostRequested = () => createCount++,
                    StartLanDiscoveryRequested = () => discoverCount++,
                    JoinLanRoomRequested = () => joinCount++,
                    ToggleLanReadyRequested = () => readyCount++,
                    StartLanLoadingRequested = () => startCount++,
                    LeaveLanRoomRequested = () => leaveCount++,
                    AcknowledgeLanStartRequested = () => acknowledgeCount++,
                    LanPlayerNameChanged = value => playerName = value,
                    LanRoomCodeChanged = value => roomCode = value,
                },
                null);

            _binding.LanCreateButton.onClick.Invoke();
            _binding.LanDiscoverButton.onClick.Invoke();
            _binding.LanJoinButton.onClick.Invoke();
            _binding.LanReadyButton.onClick.Invoke();
            _binding.LanStartButton.onClick.Invoke();
            _binding.LanLeaveButton.onClick.Invoke();
            _binding.LanAcknowledgeStartButton.onClick.Invoke();
            _binding.LanPlayerNameInput.onValueChanged.Invoke("Bruce");
            _binding.LanRoomCodeInput.onValueChanged.Invoke("ROOM42");

            Assert.AreEqual(1, createCount);
            Assert.AreEqual(1, discoverCount);
            Assert.AreEqual(1, joinCount);
            Assert.AreEqual(1, readyCount);
            Assert.AreEqual(1, startCount);
            Assert.AreEqual(1, leaveCount);
            Assert.AreEqual(1, acknowledgeCount);
            Assert.AreEqual("Bruce", playerName);
            Assert.AreEqual("ROOM42", roomCode);
        }

        private sealed class TestSceneUiBinding : IGatebreakerArenaSceneUiBinding
        {
            public Button SkillButton { get; private set; }
            public TMP_Text BallCountText { get; private set; }
            public RectTransform MovementPad { get; private set; }
            public RectTransform MovementHandle { get; private set; }
            public GameObject HudRoot { get; private set; }
            public TMP_Text HudTitleText { get; private set; }
            public TMP_Text HudStatusText { get; private set; }
            public TMP_Text HudScoreText { get; private set; }
            public TMP_Text HudServeText { get; private set; }
            public TMP_Text HudBallText { get; private set; }
            public TMP_Text TimeText { get; private set; }
            public TMP_Text[] PlayerScoreTexts { get; private set; }
            public TMP_Text[] PlayerHitTexts { get; private set; }
            public GameObject ResultRoot { get; private set; }
            public TMP_Text ResultTitleText { get; private set; }
            public TMP_Text ResultBodyText { get; private set; }
            public TMP_Text ResultScoreText { get; private set; }
            public TMP_Text[] ResultRankLabelTexts { get; private set; }
            public TMP_Text[] ResultRankNameTexts { get; private set; }
            public Button ResultRestartButton { get; private set; }
            public Button ResultBackButton { get; private set; }
            public GameObject GmRoot { get; private set; }
            public Slider HitOffsetSlider { get; private set; }
            public TMP_Text HitOffsetValueText { get; private set; }
            public Slider PaddleVelocitySlider { get; private set; }
            public TMP_Text PaddleVelocityValueText { get; private set; }
            public Slider MinimumOutwardSlider { get; private set; }
            public TMP_Text MinimumOutwardValueText { get; private set; }
            public GameObject LanRoot { get; private set; }
            public Button LanCreateButton { get; private set; }
            public Button LanDiscoverButton { get; private set; }
            public Button LanJoinButton { get; private set; }
            public Button LanReadyButton { get; private set; }
            public Button LanStartButton { get; private set; }
            public Button LanLeaveButton { get; private set; }
            public Button LanAcknowledgeStartButton { get; private set; }
            public TMP_InputField LanPlayerNameInput { get; private set; }
            public TMP_InputField LanRoomCodeInput { get; private set; }
            public TMP_Text LanStateText { get; private set; }
            public TMP_Text LanRoomCodeText { get; private set; }
            public TMP_Text LanPlayerCountText { get; private set; }
            public TMP_Text LanLocalIpText { get; private set; }
            public TMP_Text LanRoomIpText { get; private set; }
            public TMP_Text LanErrorText { get; private set; }

            public Object SkillButtonObject => SkillButton;
            public Object BallCountTextObject => BallCountText;
            public Object MovementPadObject => MovementPad;
            public Object MovementHandleObject => MovementHandle;
            public Object HudRootObject => HudRoot;
            public Object HudTitleTextObject => HudTitleText;
            public Object HudStatusTextObject => HudStatusText;
            public Object HudScoreTextObject => HudScoreText;
            public Object HudServeTextObject => HudServeText;
            public Object HudBallTextObject => HudBallText;
            public Object TimeTextObject => TimeText;
            public Object[] PlayerScoreTextObjects => PlayerScoreTexts;
            public Object[] PlayerHitTextObjects => PlayerHitTexts;
            public Object ResultRootObject => ResultRoot;
            public Object ResultTitleTextObject => ResultTitleText;
            public Object ResultBodyTextObject => ResultBodyText;
            public Object ResultScoreTextObject => ResultScoreText;
            public Object[] ResultRankLabelTextObjects => ResultRankLabelTexts;
            public Object[] ResultRankNameTextObjects => ResultRankNameTexts;
            public Object ResultRestartButtonObject => ResultRestartButton;
            public Object ResultBackButtonObject => ResultBackButton;
            public Object GmRootObject => GmRoot;
            public Object GmHitOffsetSliderObject => HitOffsetSlider;
            public Object GmHitOffsetValueTextObject => HitOffsetValueText;
            public Object GmPaddleVelocitySliderObject => PaddleVelocitySlider;
            public Object GmPaddleVelocityValueTextObject => PaddleVelocityValueText;
            public Object GmMinimumOutwardSliderObject => MinimumOutwardSlider;
            public Object GmMinimumOutwardValueTextObject => MinimumOutwardValueText;
            public Object LanRootObject => LanRoot;
            public Object LanCreateButtonObject => LanCreateButton;
            public Object LanDiscoverButtonObject => LanDiscoverButton;
            public Object LanJoinButtonObject => LanJoinButton;
            public Object LanReadyButtonObject => LanReadyButton;
            public Object LanStartButtonObject => LanStartButton;
            public Object LanLeaveButtonObject => LanLeaveButton;
            public Object LanAcknowledgeStartButtonObject => LanAcknowledgeStartButton;
            public Object LanPlayerNameInputObject => LanPlayerNameInput;
            public Object LanRoomCodeInputObject => LanRoomCodeInput;
            public Object LanStateTextObject => LanStateText;
            public Object LanRoomCodeTextObject => LanRoomCodeText;
            public Object LanPlayerCountTextObject => LanPlayerCountText;
            public Object LanLocalIpTextObject => LanLocalIpText;
            public Object LanRoomIpTextObject => LanRoomIpText;
            public Object LanErrorTextObject => LanErrorText;

            public static TestSceneUiBinding Create(Transform parent)
            {
                return new TestSceneUiBinding
                {
                    SkillButton = Add<Button>(parent, "SkillButton"),
                    BallCountText = Add<TextMeshProUGUI>(parent, "BallCount"),
                    MovementPad = Add<RectTransform>(parent, "MovementPad"),
                    MovementHandle = Add<RectTransform>(parent, "MovementHandle"),
                    HudRoot = CreateRoot(parent, "HudRoot"),
                    HudTitleText = Add<TextMeshProUGUI>(parent, "HudTitle"),
                    HudStatusText = Add<TextMeshProUGUI>(parent, "HudStatus"),
                    HudScoreText = Add<TextMeshProUGUI>(parent, "HudScore"),
                    HudServeText = Add<TextMeshProUGUI>(parent, "HudServe"),
                    HudBallText = Add<TextMeshProUGUI>(parent, "HudBall"),
                    TimeText = Add<TextMeshProUGUI>(parent, "Time"),
                    PlayerScoreTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Player1Score"),
                        Add<TextMeshProUGUI>(parent, "Player2Score"),
                        Add<TextMeshProUGUI>(parent, "Player3Score"),
                    },
                    PlayerHitTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Player1Hit"),
                        Add<TextMeshProUGUI>(parent, "Player2Hit"),
                        Add<TextMeshProUGUI>(parent, "Player3Hit"),
                    },
                    ResultRoot = CreateRoot(parent, "ResultRoot"),
                    ResultTitleText = Add<TextMeshProUGUI>(parent, "ResultTitle"),
                    ResultBodyText = Add<TextMeshProUGUI>(parent, "ResultBody"),
                    ResultScoreText = Add<TextMeshProUGUI>(parent, "ResultScore"),
                    ResultRankLabelTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "ResultRankLabel1"),
                        Add<TextMeshProUGUI>(parent, "ResultRankLabel2"),
                        Add<TextMeshProUGUI>(parent, "ResultRankLabel3"),
                    },
                    ResultRankNameTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "ResultRankName1"),
                        Add<TextMeshProUGUI>(parent, "ResultRankName2"),
                        Add<TextMeshProUGUI>(parent, "ResultRankName3"),
                    },
                    ResultRestartButton = Add<Button>(parent, "ResultRestart"),
                    ResultBackButton = Add<Button>(parent, "ResultBack"),
                    GmRoot = CreateRoot(parent, "GmRoot"),
                    HitOffsetSlider = Add<Slider>(parent, "HitOffsetSlider"),
                    HitOffsetValueText = Add<TextMeshProUGUI>(parent, "HitOffsetValue"),
                    PaddleVelocitySlider = Add<Slider>(parent, "PaddleVelocitySlider"),
                    PaddleVelocityValueText = Add<TextMeshProUGUI>(parent, "PaddleVelocityValue"),
                    MinimumOutwardSlider = Add<Slider>(parent, "MinimumOutwardSlider"),
                    MinimumOutwardValueText = Add<TextMeshProUGUI>(parent, "MinimumOutwardValue"),
                    LanRoot = CreateRoot(parent, "LanRoot"),
                    LanCreateButton = Add<Button>(parent, "LanCreate"),
                    LanDiscoverButton = Add<Button>(parent, "LanDiscover"),
                    LanJoinButton = Add<Button>(parent, "LanJoin"),
                    LanReadyButton = Add<Button>(parent, "LanReady"),
                    LanStartButton = Add<Button>(parent, "LanStart"),
                    LanLeaveButton = Add<Button>(parent, "LanLeave"),
                    LanAcknowledgeStartButton = Add<Button>(parent, "LanAck"),
                    LanPlayerNameInput = Add<TMP_InputField>(parent, "LanPlayerName"),
                    LanRoomCodeInput = Add<TMP_InputField>(parent, "LanRoomCode"),
                    LanStateText = Add<TextMeshProUGUI>(parent, "LanState"),
                    LanRoomCodeText = Add<TextMeshProUGUI>(parent, "LanRoomCodeText"),
                    LanPlayerCountText = Add<TextMeshProUGUI>(parent, "LanPlayerCount"),
                    LanLocalIpText = Add<TextMeshProUGUI>(parent, "LanLocalIp"),
                    LanRoomIpText = Add<TextMeshProUGUI>(parent, "LanRoomIp"),
                    LanErrorText = Add<TextMeshProUGUI>(parent, "LanError"),
                };
            }

            private static GameObject CreateRoot(Transform parent, string name)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(parent, false);
                return gameObject;
            }

            private static T Add<T>(Transform parent, string name) where T : Component
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(parent, false);
                return gameObject.AddComponent<T>();
            }
        }
    }
}
