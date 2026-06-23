using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Network;
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
            Assert.AreEqual("01:00", _binding.TopPanel2PTimeText.text);
            Assert.AreEqual("01:00", _binding.TopPanel3PTimeText.text);
            Assert.AreEqual("01:00", _binding.TopPanel4PTimeText.text);
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
            Assert.IsTrue(_binding.TopPanel2PRoot.activeSelf);
            Assert.IsFalse(_binding.TopPanel3PRoot.activeSelf);
            Assert.IsFalse(_binding.TopPanel4PRoot.activeSelf);
            Assert.AreEqual("1", _binding.PlayerScore2PTexts[0].text);
            Assert.AreEqual("0", _binding.PlayerHit2PTexts[0].text);
            Assert.AreEqual("0", _binding.PlayerScore2PTexts[1].text);
            Assert.AreEqual("-1", _binding.PlayerHit2PTexts[1].text);
        }

        [Test]
        public void PlayerScorePanelSwitchesToFourPlayerBindingsForFourVisiblePlayers()
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
                        new PlayerScoreSnapshot(1, 1, false, 1, 0, 1),
                        new PlayerScoreSnapshot(2, 2, false, 2, -1, 2),
                        new PlayerScoreSnapshot(3, 3, false, 3, -2, 3),
                        new PlayerScoreSnapshot(4, 4, false, 4, -3, 4),
                    },
                },
                ServeBlockReason.None);

            Assert.IsFalse(_binding.TopPanel2PRoot.activeSelf);
            Assert.IsFalse(_binding.TopPanel3PRoot.activeSelf);
            Assert.IsTrue(_binding.TopPanel4PRoot.activeSelf);
            Assert.AreEqual("1", _binding.PlayerScore4PTexts[0].text);
            Assert.AreEqual("-3", _binding.PlayerHit4PTexts[3].text);
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
            Assert.AreEqual("\u7B2C\u4E00\u540D:", _binding.ResultRankLabelTexts[0].text);
            Assert.AreEqual("\u7B2C\u4E8C\u540D:", _binding.ResultRankLabelTexts[1].text);
            Assert.AreEqual("\u7B2C\u4E09\u540D:", _binding.ResultRankLabelTexts[2].text);
            Assert.AreEqual(string.Empty, _binding.ResultRankLabelTexts[3].text);
            Assert.AreEqual(string.Empty, _binding.ResultRankNameTexts[3].text);
            StringAssert.Contains("Player3", _binding.ResultRankNameTexts[0].text);
            StringAssert.Contains("WIN", _binding.ResultRankNameTexts[0].text);
        }

        [Test]
        public void ResultPanelDisplaysFourthRankForFourPlayerMatch()
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
                        new PlayerScoreSnapshot(3, 3, false, 11, 0, 3),
                        new PlayerScoreSnapshot(2, 2, false, 11, -1, 2),
                        new PlayerScoreSnapshot(4, 4, false, 11, -3, 4),
                        new PlayerScoreSnapshot(1, 1, false, 10, -5, 1),
                    },
                });

            Assert.AreEqual("\u7B2C\u56DB\u540D:", _binding.ResultRankLabelTexts[3].text);
            StringAssert.Contains("Player1", _binding.ResultRankNameTexts[3].text);
            StringAssert.Contains("SCORE 10", _binding.ResultRankNameTexts[3].text);
            StringAssert.Contains("HIT -5", _binding.ResultRankNameTexts[3].text);
        }

        [Test]
        public void LeftArrowPointerDownForwardsNegativeAxisMovesHandleAndHighlightsLeft()
        {
            float moveAxis = 0f;
            _binding.MovementPad.sizeDelta = new Vector2(200f, 68f);
            _binding.MovementHandle.sizeDelta = new Vector2(40f, 40f);
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks { MoveAxisChanged = axis => moveAxis = axis },
                null);

            InvokeMovementTrigger(_binding.MovementLeftArrowInput, EventTriggerType.PointerDown, Vector2.zero);

            Assert.AreEqual(-1f, moveAxis);
            Assert.Less(_binding.MovementHandle.anchoredPosition.x, 0f);
            AssertMovementHighlightActive(_binding.MovementLeftArrowHighlight);
            AssertMovementHighlightRestored(_binding.MovementRightArrowHighlight);

            InvokeMovementTrigger(_binding.MovementLeftArrowInput, EventTriggerType.PointerUp, Vector2.zero);

            Assert.AreEqual(0f, moveAxis);
            Assert.AreEqual(Vector2.zero, _binding.MovementHandle.anchoredPosition);
            AssertMovementHighlightRestored(_binding.MovementLeftArrowHighlight);
            AssertMovementHighlightRestored(_binding.MovementRightArrowHighlight);
        }

        [Test]
        public void RightArrowPointerDownForwardsPositiveAxisMovesHandleAndHighlightsRight()
        {
            float moveAxis = 0f;
            _binding.MovementPad.sizeDelta = new Vector2(200f, 68f);
            _binding.MovementHandle.sizeDelta = new Vector2(40f, 40f);
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks { MoveAxisChanged = axis => moveAxis = axis },
                null);

            InvokeMovementTrigger(_binding.MovementRightArrowInput, EventTriggerType.PointerDown, Vector2.zero);

            Assert.AreEqual(1f, moveAxis);
            Assert.Greater(_binding.MovementHandle.anchoredPosition.x, 0f);
            AssertMovementHighlightRestored(_binding.MovementLeftArrowHighlight);
            AssertMovementHighlightActive(_binding.MovementRightArrowHighlight);

            InvokeMovementTrigger(_binding.MovementRightArrowInput, EventTriggerType.EndDrag, Vector2.zero);

            Assert.AreEqual(0f, moveAxis);
            Assert.AreEqual(Vector2.zero, _binding.MovementHandle.anchoredPosition);
            AssertMovementHighlightRestored(_binding.MovementLeftArrowHighlight);
            AssertMovementHighlightRestored(_binding.MovementRightArrowHighlight);
        }

        [Test]
        public void MovementPadDragForwardsContinuousAxisAndResetsOnEndDrag()
        {
            float moveAxis = 0f;
            _binding.MovementPad.sizeDelta = new Vector2(200f, 68f);
            _binding.MovementHandle.sizeDelta = new Vector2(40f, 40f);
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks { MoveAxisChanged = axis => moveAxis = axis },
                null);

            InvokeMovementTrigger(_binding.MovementPad, EventTriggerType.Drag, new Vector2(50f, 0f));

            Assert.Greater(moveAxis, 0.45f);
            Assert.Less(moveAxis, 0.55f);
            Assert.Greater(_binding.MovementHandle.anchoredPosition.x, 0f);

            InvokeMovementTrigger(_binding.MovementPad, EventTriggerType.EndDrag, Vector2.zero);

            Assert.AreEqual(0f, moveAxis);
            Assert.AreEqual(Vector2.zero, _binding.MovementHandle.anchoredPosition);
        }

        [Test]
        public void PreviewMoveAxisUpdatesHandleAndHighlightsWithoutForwardingCallback()
        {
            float moveAxis = 0f;
            _binding.MovementPad.sizeDelta = new Vector2(200f, 68f);
            _binding.MovementHandle.sizeDelta = new Vector2(40f, 40f);
            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks { MoveAxisChanged = axis => moveAxis = axis },
                null);

            _service.PreviewMoveAxis(-1f);

            Assert.AreEqual(0f, moveAxis);
            Assert.Less(_binding.MovementHandle.anchoredPosition.x, 0f);
            AssertMovementHighlightActive(_binding.MovementLeftArrowHighlight);
            AssertMovementHighlightRestored(_binding.MovementRightArrowHighlight);

            _service.PreviewMoveAxis(1f);

            Assert.AreEqual(0f, moveAxis);
            Assert.Greater(_binding.MovementHandle.anchoredPosition.x, 0f);
            AssertMovementHighlightRestored(_binding.MovementLeftArrowHighlight);
            AssertMovementHighlightActive(_binding.MovementRightArrowHighlight);

            _service.PreviewMoveAxis(0f);

            Assert.AreEqual(Vector2.zero, _binding.MovementHandle.anchoredPosition);
            AssertMovementHighlightRestored(_binding.MovementLeftArrowHighlight);
            AssertMovementHighlightRestored(_binding.MovementRightArrowHighlight);
        }

        private static void InvokeMovementTrigger(Component target, EventTriggerType eventType, Vector2 screenPosition)
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

        private static void AssertMovementHighlightActive(Graphic graphic)
        {
            Assert.AreEqual(1f, graphic.color.r, 0.001f);
            Assert.Less(graphic.color.g, 0.1f);
            Assert.Less(graphic.color.b, 0.1f);
            Assert.Greater(graphic.color.a, 0.6f);
        }

        private static void AssertMovementHighlightRestored(Graphic graphic)
        {
            Assert.AreEqual(Color.clear, graphic.color);
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
            int localBattleCount = 0;
            int onlineBattleCount = 0;
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
                    LocalBattleRequested = () => localBattleCount++,
                    OnlineBattleRequested = () => onlineBattleCount++,
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

            _binding.LocalBattleButton.onClick.Invoke();
            _binding.OnlineBattleButton.onClick.Invoke();
            _binding.LanBackButton.onClick.Invoke();
            _binding.LanCreateButton.onClick.Invoke();
            _binding.LanDiscoverButton.onClick.Invoke();
            _binding.LanJoinButton.onClick.Invoke();
            _binding.LanReadyButton.onClick.Invoke();
            _binding.LanStartButton.onClick.Invoke();
            _binding.LanLeaveButton.onClick.Invoke();
            _binding.LanAcknowledgeStartButton.onClick.Invoke();
            _binding.LanPlayerNameInput.onValueChanged.Invoke("Bruce");
            _binding.LanRoomCodeInput.onValueChanged.Invoke("ROOM42");

            Assert.AreEqual(1, localBattleCount);
            Assert.AreEqual(1, onlineBattleCount);
            Assert.AreEqual(1, createCount);
            Assert.AreEqual(1, discoverCount);
            Assert.AreEqual(1, joinCount);
            Assert.AreEqual(1, readyCount);
            Assert.AreEqual(1, startCount);
            Assert.AreEqual(2, leaveCount);
            Assert.AreEqual(1, acknowledgeCount);
            Assert.AreEqual("Bruce", playerName);
            Assert.AreEqual("ROOM42", roomCode);
        }

        [Test]
        public void EntryPanelsSwitchBetweenModeSelectOnlineMenuRoomAndCountdown()
        {
            _service.Bind(_binding, new GatebreakerArenaSceneUiCallbacks(), null);

            Assert.IsTrue(_binding.LanRoot.activeSelf);
            Assert.IsTrue(_binding.ModeSelectRoot.activeSelf);
            Assert.IsFalse(_binding.LanBackButton.gameObject.activeSelf);
            Assert.IsFalse(_binding.LanMenuRoot.activeSelf);
            Assert.IsFalse(_binding.LanRoomInfoRoot.activeSelf);
            Assert.IsFalse(_binding.LanStatusRoot.activeSelf);
            Assert.IsFalse(_binding.StartCountdownRoot.activeSelf);

            _service.ShowOnlineMenu();
            Assert.IsFalse(_binding.ModeSelectRoot.activeSelf);
            Assert.IsTrue(_binding.LanBackButton.gameObject.activeSelf);
            Assert.IsTrue(_binding.LanMenuRoot.activeSelf);
            Assert.IsFalse(_binding.LanRoomInfoRoot.activeSelf);
            Assert.IsTrue(_binding.LanStatusRoot.activeSelf);

            _service.ShowLanRoomStatus();
            Assert.IsFalse(_binding.LanMenuRoot.activeSelf);
            Assert.IsTrue(_binding.LanBackButton.gameObject.activeSelf);
            Assert.IsTrue(_binding.LanRoomInfoRoot.activeSelf);
            Assert.IsTrue(_binding.LanStatusRoot.activeSelf);

            _service.ShowStartCountdown("5");
            Assert.IsFalse(_binding.LanRoot.activeSelf);
            Assert.IsTrue(_binding.StartCountdownRoot.activeSelf);
            Assert.AreEqual("5", _binding.StartCountdownText.text);

            _service.ShowStartCountdown("开始游戏");
            Assert.AreEqual("开始游戏", _binding.StartCountdownText.text);
        }

        [Test]
        public void LanRoomUpdateRefreshesNativePlayerInfoRows()
        {
            _service.Bind(_binding, new GatebreakerArenaSceneUiCallbacks(), null);

            _service.UpdateLanRoom(
                new RoomSnapshot
                {
                    State = LanRoomState.Lobby,
                    RoomCode = "ABC123",
                    MaxPlayers = 3,
                    Players = new[]
                    {
                        new RoomPlayerSnapshot { PlayerId = 1, PlayerName = "Host", IsReady = true, IsActive = true },
                        new RoomPlayerSnapshot { PlayerId = 2, PlayerName = "Client", IsReady = false, IsActive = true },
                        new RoomPlayerSnapshot { PlayerId = 3, PlayerName = "Computer 3", IsReady = true, IsActive = true, IsAi = true },
                    },
                },
                "192.168.0.220",
                "192.168.0.115");

            Assert.AreEqual("Player1:", _binding.LanRoomPlayerInfoTexts[0].text);
            Assert.AreEqual("Host", _binding.LanRoomPlayerNameTexts[0].text);
            Assert.AreEqual("ready", _binding.LanRoomPlayerReadyTexts[0].text);
            Assert.AreEqual("Player2:", _binding.LanRoomPlayerInfoTexts[1].text);
            Assert.AreEqual("Client", _binding.LanRoomPlayerNameTexts[1].text);
            Assert.AreEqual("not ready", _binding.LanRoomPlayerReadyTexts[1].text);
            Assert.AreEqual("Player3:", _binding.LanRoomPlayerInfoTexts[2].text);
            Assert.AreEqual("AI", _binding.LanRoomPlayerNameTexts[2].text);
            Assert.AreEqual("ready", _binding.LanRoomPlayerReadyTexts[2].text);
            Assert.IsFalse(_binding.LanRoomPlayerInfoTexts[3].gameObject.activeSelf);
        }

        [Test]
        public void LanRoomUpdateShowsOnlySelectedPlayerRowsAndUsesAiPlaceholder()
        {
            _service.Bind(_binding, new GatebreakerArenaSceneUiCallbacks(), null);

            _service.UpdateLanRoom(
                new RoomSnapshot
                {
                    State = LanRoomState.Lobby,
                    RoomCode = "ABC123",
                    MaxPlayers = 2,
                    Players = new[]
                    {
                        new RoomPlayerSnapshot { PlayerId = 1, PlayerName = "Host", IsReady = true, IsActive = true },
                        new RoomPlayerSnapshot { PlayerId = 2, PlayerName = "Computer 2", IsReady = true, IsActive = true, IsAi = true },
                    },
                },
                "192.168.0.220",
                "192.168.0.115");

            Assert.IsTrue(_binding.LanRoomPlayerInfoTexts[0].gameObject.activeSelf);
            Assert.IsTrue(_binding.LanRoomPlayerInfoTexts[1].gameObject.activeSelf);
            Assert.IsFalse(_binding.LanRoomPlayerInfoTexts[2].gameObject.activeSelf);
            Assert.IsFalse(_binding.LanRoomPlayerInfoTexts[3].gameObject.activeSelf);
            Assert.AreEqual("Host", _binding.LanRoomPlayerNameTexts[0].text);
            Assert.AreEqual("AI", _binding.LanRoomPlayerNameTexts[1].text);
            Assert.AreEqual(string.Empty, _binding.LanRoomPlayerNameTexts[2].text);
            Assert.AreEqual(string.Empty, _binding.LanRoomPlayerNameTexts[3].text);
        }

        [Test]
        public void LanRoomUpdateUsesSlotRowsWhenSnapshotPlayersArriveOutOfOrder()
        {
            _service.Bind(_binding, new GatebreakerArenaSceneUiCallbacks(), null);

            var players = new[]
            {
                new RoomPlayerSnapshot { SlotIndex = 1, SideOrder = 1, PlayerId = 2, PlayerName = "Client", IsReady = false, IsActive = true },
                new RoomPlayerSnapshot { SlotIndex = 0, SideOrder = 0, PlayerId = 1, PlayerName = "Host", IsReady = true, IsActive = true },
                new RoomPlayerSnapshot { SlotIndex = 2, SideOrder = 2, PlayerId = 3, PlayerName = "Computer 3", IsReady = true, IsActive = true, IsAi = true },
            };

            _service.UpdateLanRoom(
                new RoomSnapshot
                {
                    State = LanRoomState.Lobby,
                    RoomCode = "ABC123",
                    MaxPlayers = 3,
                    Players = players,
                },
                "192.168.0.220",
                "192.168.0.115");

            Assert.AreEqual("Player1:", _binding.LanRoomPlayerInfoTexts[0].text);
            Assert.AreEqual("Host", _binding.LanRoomPlayerNameTexts[0].text);
            Assert.AreEqual("Player2:", _binding.LanRoomPlayerInfoTexts[1].text);
            Assert.AreEqual("Client", _binding.LanRoomPlayerNameTexts[1].text);
            Assert.AreEqual("not ready", _binding.LanRoomPlayerReadyTexts[1].text);
            Assert.AreEqual(2, players[0].PlayerId);
            Assert.AreEqual(1, players[1].PlayerId);
        }

        [Test]
        public void LanRoomUpdateFallsBackToNativePlayerInfoChildren()
        {
            _binding.UseNativeLanRoomInfoChildrenOnly();
            _service.Bind(_binding, new GatebreakerArenaSceneUiCallbacks(), null);

            _service.UpdateLanRoom(
                new RoomSnapshot
                {
                    State = LanRoomState.Lobby,
                    RoomCode = "ABC123",
                    MaxPlayers = 3,
                    Players = new[]
                    {
                        new RoomPlayerSnapshot { PlayerId = 1, PlayerName = "Host", IsReady = true, IsActive = true },
                        new RoomPlayerSnapshot { PlayerId = 2, PlayerName = "Computer 2", IsReady = true, IsActive = true, IsAi = true },
                        new RoomPlayerSnapshot { PlayerId = 3, PlayerName = "Computer 3", IsReady = true, IsActive = true, IsAi = true },
                    },
                },
                "192.168.0.220",
                "192.168.0.115");

            Assert.AreEqual("Player1:", _binding.LanRoomPlayerInfoTexts[0].text);
            Assert.AreEqual("Host", _binding.NativeLanRoomPlayerNameTexts[0].text);
            Assert.AreEqual("ready", _binding.NativeLanRoomPlayerReadyTexts[0].text);
            Assert.AreEqual("Player2:", _binding.LanRoomPlayerInfoTexts[1].text);
            Assert.AreEqual("AI", _binding.NativeLanRoomPlayerNameTexts[1].text);
            Assert.AreEqual("ready", _binding.NativeLanRoomPlayerReadyTexts[1].text);
            Assert.AreEqual("Player3:", _binding.LanRoomPlayerInfoTexts[2].text);
            Assert.AreEqual("AI", _binding.NativeLanRoomPlayerNameTexts[2].text);
            Assert.AreEqual("ready", _binding.NativeLanRoomPlayerReadyTexts[2].text);
            Assert.IsFalse(_binding.LanRoomPlayerInfoTexts[3].gameObject.activeSelf);
        }

        [Test]
        public void RoomTypeDropdownReportsSelectedPlayerCount()
        {
            int selectedPlayerCount = 0;

            _service.Bind(
                _binding,
                new GatebreakerArenaSceneUiCallbacks
                {
                    InitialLanRoomPlayerCount = 3,
                    LanRoomPlayerCountChanged = value => selectedPlayerCount = value,
                },
                null);

            Assert.AreEqual(3, selectedPlayerCount);
            Assert.AreEqual(1, _binding.LanRoomTypeDropdown.value);

            _binding.LanRoomTypeDropdown.onValueChanged.Invoke(2);

            Assert.AreEqual(4, selectedPlayerCount);
        }

        private sealed class TestSceneUiBinding : IGatebreakerArenaSceneUiBinding
        {
            public Button SkillButton { get; private set; }
            public TMP_Text BallCountText { get; private set; }
            public RectTransform MovementPad { get; private set; }
            public RectTransform MovementHandle { get; private set; }
            public RectTransform MovementLeftArrowInput { get; private set; }
            public RectTransform MovementRightArrowInput { get; private set; }
            public Graphic MovementLeftArrowHighlight { get; private set; }
            public Graphic MovementRightArrowHighlight { get; private set; }
            public GameObject HudRoot { get; private set; }
            public TMP_Text HudTitleText { get; private set; }
            public TMP_Text HudStatusText { get; private set; }
            public TMP_Text HudScoreText { get; private set; }
            public TMP_Text HudServeText { get; private set; }
            public TMP_Text HudBallText { get; private set; }
            public TMP_Text TimeText { get; private set; }
            public TMP_Text[] PlayerScoreTexts { get; private set; }
            public TMP_Text[] PlayerHitTexts { get; private set; }
            public GameObject TopPanel2PRoot { get; private set; }
            public GameObject TopPanel3PRoot { get; private set; }
            public GameObject TopPanel4PRoot { get; private set; }
            public TMP_Text TopPanel2PTimeText { get; private set; }
            public TMP_Text TopPanel3PTimeText { get; private set; }
            public TMP_Text TopPanel4PTimeText { get; private set; }
            public TMP_Text[] PlayerScore2PTexts { get; private set; }
            public TMP_Text[] PlayerHit2PTexts { get; private set; }
            public TMP_Text[] PlayerScore3PTexts { get; private set; }
            public TMP_Text[] PlayerHit3PTexts { get; private set; }
            public TMP_Text[] PlayerScore4PTexts { get; private set; }
            public TMP_Text[] PlayerHit4PTexts { get; private set; }
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
            public GameObject ModeSelectRoot { get; private set; }
            public Button LocalBattleButton { get; private set; }
            public Button OnlineBattleButton { get; private set; }
            public GameObject LanMenuRoot { get; private set; }
            public GameObject LanRoomInfoRoot { get; private set; }
            public GameObject LanStatusRoot { get; private set; }
            public Button LanBackButton { get; private set; }
            public Button LanCreateButton { get; private set; }
            public Button LanDiscoverButton { get; private set; }
            public Button LanJoinButton { get; private set; }
            public Button LanReadyButton { get; private set; }
            public Button LanStartButton { get; private set; }
            public Button LanLeaveButton { get; private set; }
            public Button LanAcknowledgeStartButton { get; private set; }
            public TMP_InputField LanPlayerNameInput { get; private set; }
            public TMP_Dropdown LanRoomTypeDropdown { get; private set; }
            public TMP_InputField LanRoomCodeInput { get; private set; }
            public TMP_Text LanStateText { get; private set; }
            public TMP_Text LanRoomCodeText { get; private set; }
            public TMP_Text LanPlayerCountText { get; private set; }
            public TMP_Text LanLocalIpText { get; private set; }
            public TMP_Text LanRoomIpText { get; private set; }
            public TMP_Text LanErrorText { get; private set; }
            public TMP_Text[] LanRoomPlayerInfoTexts { get; private set; }
            public TMP_Text[] LanRoomPlayerNameTexts { get; private set; }
            public TMP_Text[] LanRoomPlayerReadyTexts { get; private set; }
            public TMP_Text[] NativeLanRoomPlayerNameTexts { get; private set; } = System.Array.Empty<TMP_Text>();
            public TMP_Text[] NativeLanRoomPlayerReadyTexts { get; private set; } = System.Array.Empty<TMP_Text>();
            public GameObject StartCountdownRoot { get; private set; }
            public TMP_Text StartCountdownText { get; private set; }

            public Object SkillButtonObject => SkillButton;
            public Object BallCountTextObject => BallCountText;
            public Object MovementPadObject => MovementPad;
            public Object MovementHandleObject => MovementHandle;
            public Object MovementLeftArrowInputObject => MovementLeftArrowInput;
            public Object MovementRightArrowInputObject => MovementRightArrowInput;
            public Object MovementLeftArrowHighlightObject => MovementLeftArrowHighlight;
            public Object MovementRightArrowHighlightObject => MovementRightArrowHighlight;
            public Object HudRootObject => HudRoot;
            public Object HudTitleTextObject => HudTitleText;
            public Object HudStatusTextObject => HudStatusText;
            public Object HudScoreTextObject => HudScoreText;
            public Object HudServeTextObject => HudServeText;
            public Object HudBallTextObject => HudBallText;
            public Object TimeTextObject => TimeText;
            public Object[] PlayerScoreTextObjects => PlayerScoreTexts;
            public Object[] PlayerHitTextObjects => PlayerHitTexts;
            public Object TopPanel2PRootObject => TopPanel2PRoot;
            public Object TopPanel3PRootObject => TopPanel3PRoot;
            public Object TopPanel4PRootObject => TopPanel4PRoot;
            public Object TopPanel2PTimeTextObject => TopPanel2PTimeText;
            public Object TopPanel3PTimeTextObject => TopPanel3PTimeText;
            public Object TopPanel4PTimeTextObject => TopPanel4PTimeText;
            public Object[] PlayerScore2PTextObjects => PlayerScore2PTexts;
            public Object[] PlayerHit2PTextObjects => PlayerHit2PTexts;
            public Object[] PlayerScore3PTextObjects => PlayerScore3PTexts;
            public Object[] PlayerHit3PTextObjects => PlayerHit3PTexts;
            public Object[] PlayerScore4PTextObjects => PlayerScore4PTexts;
            public Object[] PlayerHit4PTextObjects => PlayerHit4PTexts;
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
            public Object ModeSelectRootObject => ModeSelectRoot;
            public Object LocalBattleButtonObject => LocalBattleButton;
            public Object OnlineBattleButtonObject => OnlineBattleButton;
            public Object LanMenuRootObject => LanMenuRoot;
            public Object LanRoomInfoRootObject => LanRoomInfoRoot;
            public Object LanStatusRootObject => LanStatusRoot;
            public Object LanBackButtonObject => LanBackButton;
            public Object LanCreateButtonObject => LanCreateButton;
            public Object LanDiscoverButtonObject => LanDiscoverButton;
            public Object LanJoinButtonObject => LanJoinButton;
            public Object LanReadyButtonObject => LanReadyButton;
            public Object LanStartButtonObject => LanStartButton;
            public Object LanLeaveButtonObject => LanLeaveButton;
            public Object LanAcknowledgeStartButtonObject => LanAcknowledgeStartButton;
            public Object LanPlayerNameInputObject => LanPlayerNameInput;
            public Object LanRoomTypeDropdownObject => LanRoomTypeDropdown;
            public Object LanRoomCodeInputObject => LanRoomCodeInput;
            public Object LanStateTextObject => LanStateText;
            public Object LanRoomCodeTextObject => LanRoomCodeText;
            public Object LanPlayerCountTextObject => LanPlayerCountText;
            public Object LanLocalIpTextObject => LanLocalIpText;
            public Object LanRoomIpTextObject => LanRoomIpText;
            public Object LanErrorTextObject => LanErrorText;
            public Object[] LanRoomPlayerInfoTextObjects => LanRoomPlayerInfoTexts;
            public Object[] LanRoomPlayerNameTextObjects => LanRoomPlayerNameTexts;
            public Object[] LanRoomPlayerReadyTextObjects => LanRoomPlayerReadyTexts;
            public Object StartCountdownRootObject => StartCountdownRoot;
            public Object StartCountdownTextObject => StartCountdownText;

            public static TestSceneUiBinding Create(Transform parent)
            {
                return new TestSceneUiBinding
                {
                    SkillButton = Add<Button>(parent, "SkillButton"),
                    BallCountText = Add<TextMeshProUGUI>(parent, "BallCount"),
                    MovementPad = Add<RectTransform>(parent, "MovementPad"),
                    MovementHandle = Add<RectTransform>(parent, "MovementHandle"),
                    MovementLeftArrowInput = Add<RectTransform>(parent, "MovementLeftArrowInput"),
                    MovementRightArrowInput = Add<RectTransform>(parent, "MovementRightArrowInput"),
                    MovementLeftArrowHighlight = AddClearImage(parent, "MovementLeftArrowHighlight"),
                    MovementRightArrowHighlight = AddClearImage(parent, "MovementRightArrowHighlight"),
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
                    TopPanel2PRoot = CreateRoot(parent, "TopPanel_2P"),
                    TopPanel3PRoot = CreateRoot(parent, "TopPanel_3P"),
                    TopPanel4PRoot = CreateRoot(parent, "TopPanel_4P"),
                    TopPanel2PTimeText = Add<TextMeshProUGUI>(parent, "TopPanel2PTime"),
                    TopPanel3PTimeText = Add<TextMeshProUGUI>(parent, "TopPanel3PTime"),
                    TopPanel4PTimeText = Add<TextMeshProUGUI>(parent, "TopPanel4PTime"),
                    PlayerScore2PTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Player1Score2P"),
                        Add<TextMeshProUGUI>(parent, "Player2Score2P"),
                    },
                    PlayerHit2PTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Player1Hit2P"),
                        Add<TextMeshProUGUI>(parent, "Player2Hit2P"),
                    },
                    PlayerScore3PTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Player1Score3P"),
                        Add<TextMeshProUGUI>(parent, "Player2Score3P"),
                        Add<TextMeshProUGUI>(parent, "Player3Score3P"),
                    },
                    PlayerHit3PTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Player1Hit3P"),
                        Add<TextMeshProUGUI>(parent, "Player2Hit3P"),
                        Add<TextMeshProUGUI>(parent, "Player3Hit3P"),
                    },
                    PlayerScore4PTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Player1Score4P"),
                        Add<TextMeshProUGUI>(parent, "Player2Score4P"),
                        Add<TextMeshProUGUI>(parent, "Player3Score4P"),
                        Add<TextMeshProUGUI>(parent, "Player4Score4P"),
                    },
                    PlayerHit4PTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Player1Hit4P"),
                        Add<TextMeshProUGUI>(parent, "Player2Hit4P"),
                        Add<TextMeshProUGUI>(parent, "Player3Hit4P"),
                        Add<TextMeshProUGUI>(parent, "Player4Hit4P"),
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
                        Add<TextMeshProUGUI>(parent, "ResultRankLabel4"),
                    },
                    ResultRankNameTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "ResultRankName1"),
                        Add<TextMeshProUGUI>(parent, "ResultRankName2"),
                        Add<TextMeshProUGUI>(parent, "ResultRankName3"),
                        Add<TextMeshProUGUI>(parent, "ResultRankName4"),
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
                    ModeSelectRoot = CreateRoot(parent, "ModeSelectRoot"),
                    LocalBattleButton = Add<Button>(parent, "LocalBattle"),
                    OnlineBattleButton = Add<Button>(parent, "OnlineBattle"),
                    LanMenuRoot = CreateRoot(parent, "LanMenuRoot"),
                    LanRoomInfoRoot = CreateRoot(parent, "LanRoomInfoRoot"),
                    LanStatusRoot = CreateRoot(parent, "LanStatusRoot"),
                    LanBackButton = Add<Button>(parent, "LanBack"),
                    LanCreateButton = Add<Button>(parent, "LanCreate"),
                    LanDiscoverButton = Add<Button>(parent, "LanDiscover"),
                    LanJoinButton = Add<Button>(parent, "LanJoin"),
                    LanReadyButton = Add<Button>(parent, "LanReady"),
                    LanStartButton = Add<Button>(parent, "LanStart"),
                    LanLeaveButton = Add<Button>(parent, "LanLeave"),
                    LanAcknowledgeStartButton = Add<Button>(parent, "LanAck"),
                    LanPlayerNameInput = Add<TMP_InputField>(parent, "LanPlayerName"),
                    LanRoomTypeDropdown = CreateRoomTypeDropdown(parent),
                    LanRoomCodeInput = Add<TMP_InputField>(parent, "LanRoomCode"),
                    LanStateText = Add<TextMeshProUGUI>(parent, "LanState"),
                    LanRoomCodeText = Add<TextMeshProUGUI>(parent, "LanRoomCodeText"),
                    LanPlayerCountText = Add<TextMeshProUGUI>(parent, "LanPlayerCount"),
                    LanLocalIpText = Add<TextMeshProUGUI>(parent, "LanLocalIp"),
                    LanRoomIpText = Add<TextMeshProUGUI>(parent, "LanRoomIp"),
                    LanErrorText = Add<TextMeshProUGUI>(parent, "LanError"),
                    LanRoomPlayerInfoTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Playerinfo_1"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_2"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_3"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_4"),
                    },
                    LanRoomPlayerNameTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Playerinfo_1_Name"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_2_Name"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_3_Name"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_4_Name"),
                    },
                    LanRoomPlayerReadyTexts = new[]
                    {
                        Add<TextMeshProUGUI>(parent, "Playerinfo_1_Status"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_2_Status"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_3_Status"),
                        Add<TextMeshProUGUI>(parent, "Playerinfo_4_Status"),
                    },
                    StartCountdownRoot = CreateRoot(parent, "StartCountdownRoot"),
                    StartCountdownText = Add<TextMeshProUGUI>(parent, "StartCountdownText"),
                };
            }

            public void UseNativeLanRoomInfoChildrenOnly()
            {
                NativeLanRoomPlayerNameTexts = new TMP_Text[LanRoomPlayerInfoTexts.Length];
                NativeLanRoomPlayerReadyTexts = new TMP_Text[LanRoomPlayerInfoTexts.Length];
                for (int i = 0; i < LanRoomPlayerInfoTexts.Length; i++)
                {
                    Transform row = LanRoomPlayerInfoTexts[i].transform;
                    NativeLanRoomPlayerNameTexts[i] = Add<TextMeshProUGUI>(row, "Name");
                    NativeLanRoomPlayerReadyTexts[i] = Add<TextMeshProUGUI>(row, "Status");
                }

                LanRoomPlayerNameTexts = System.Array.Empty<TMP_Text>();
                LanRoomPlayerReadyTexts = System.Array.Empty<TMP_Text>();
            }

            private static GameObject CreateRoot(Transform parent, string name)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(parent, false);
                return gameObject;
            }

            private static TMP_Dropdown CreateRoomTypeDropdown(Transform parent)
            {
                TMP_Dropdown dropdown = Add<TMP_Dropdown>(parent, "LanRoomType");
                dropdown.options.Clear();
                dropdown.options.Add(new TMP_Dropdown.OptionData("二人对战"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("三人对战"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("四人对战"));
                return dropdown;
            }

            private static T Add<T>(Transform parent, string name) where T : Component
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(parent, false);
                return gameObject.AddComponent<T>();
            }

            private static Image AddClearImage(Transform parent, string name)
            {
                Image image = Add<Image>(parent, name);
                image.color = Color.clear;
                return image;
            }
        }
    }
}
