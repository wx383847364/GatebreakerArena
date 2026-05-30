using System.Collections;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Bootstrap;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.Shared.Contracts;
using TMPro;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gatebreaker.Tests.PlayMode
{
    public sealed class GatebreakerBootstrapSceneSmokeTests
    {
        private const string BootstrapSceneName = "BootstrapScene";
        private const string BootstrapScenePath = "Assets/Scenes/BootstrapScene.scene";
        private const float StartupTimeoutSeconds = 10f;
        private const float SmokeDurationSeconds = 6f;

        [UnityTest]
        public IEnumerator BootstrapSceneStartsPrototypeRunnerArenaAndHudWithoutErrors()
        {
            if (!Application.CanStreamedLevelBeLoaded(BootstrapSceneName) &&
                !Application.CanStreamedLevelBeLoaded(BootstrapScenePath))
            {
                Assert.Inconclusive("BootstrapScene is not available in build settings yet.");
            }

            var failures = new List<string>();
            Application.LogCallback logCallback = (condition, stackTrace, type) =>
            {
                if (type == LogType.Assert || type == LogType.Error || type == LogType.Exception)
                {
                    failures.Add($"{type}: {condition}\n{stackTrace}");
                }
                else if (type == LogType.Warning && condition.Contains("GatebreakerArenaSceneBindingService:"))
                {
                    failures.Add($"{type}: {condition}\n{stackTrace}");
                }
            };

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += logCallback;

            try
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Single);
                Assert.IsNotNull(loadOperation, "BootstrapScene load should create an async operation.");

                float loadDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (!loadOperation.isDone && Time.realtimeSinceStartup < loadDeadline)
                {
                    yield return null;
                }

                Assert.IsTrue(loadOperation.isDone, "BootstrapScene should finish loading within the smoke timeout.");
                Scene loadedScene = SceneManager.GetSceneByName(BootstrapSceneName);
                Assert.IsTrue(loadedScene.IsValid() && loadedScene.isLoaded, "BootstrapScene should be the loaded scene.");
                Assert.IsNotNull(GameObject.Find("BootstrapRoot"), "BootstrapScene should contain the AOT bootstrap root.");

                float startupStart = Time.realtimeSinceStartup;
                while (GatebreakerArenaGameBootstrap.Context == null &&
                       Time.realtimeSinceStartup - startupStart < StartupTimeoutSeconds)
                {
                    yield return null;
                }

                AssertNoFailures(failures);
                Assert.IsNotNull(
                    GatebreakerArenaGameBootstrap.Context,
                    "Gatebreaker prototype runner should publish a bootstrap context after scene startup.");

                var context = GatebreakerArenaGameBootstrap.Context;
                Assert.IsNotNull(context.MatchRuntime, "Prototype runner signal: MatchRuntime should be registered.");
                Assert.IsNotNull(context.SceneBindingService, "Arena signal: scene binding service should be registered.");
                Assert.IsTrue(context.SceneBindingService.IsBound, "Arena signal: scene binding service should be marked bound after runner startup.");
                Assert.IsTrue(context.SceneBindingService.HasSkillButtonBinding, "Skill button should be explicitly bound.");
                Assert.IsTrue(context.SceneBindingService.HasBallCountTextBinding, "BallCount text should be explicitly bound.");
                Assert.IsTrue(context.SceneBindingService.HasPlayerScorePanelBindings, "Player score/hit panel texts should be explicitly bound.");
                Assert.IsTrue(context.SceneBindingService.HasGmSliderBindings, "GM tuning sliders should be explicitly bound.");
                Assert.IsTrue(context.SceneBindingService.HasLanButtonBindings, "LAN buttons should be explicitly bound.");
                Assert.IsNotNull(context.VisualAssetService, "Arena signal: visual asset service should be registered.");
                Assert.IsNotNull(context.HudPresenter, "HUD signal: HUD presenter should be registered.");
                Assert.IsNotNull(GameObject.Find("Gatebreaker Prototype Runner"), "Prototype runner GameObject should be created.");
                Assert.IsNotNull(GameObject.Find("ArenaRoot"), "Static arena root should contain configured gameplay visuals.");
                Assert.IsTrue(
                    GameObject.Find("Scene3v3") != null || GameObject.Find("Arena Floor") != null,
                    "Configured scene prefab or procedural fallback arena should be visible.");
                Assert.IsNotNull(GameObject.Find("Player 1 Paddle"), "Local paddle view should be visible.");

                Assert.AreEqual(MatchPhase.Playing, context.MatchRuntime.Phase);
                Assert.GreaterOrEqual(context.MatchRuntime.Players.Count, 2);
                Assert.GreaterOrEqual(context.MatchRuntime.Balls.Count, 1);

                var hud = context.HudPresenter.BuildSnapshot(1);
                Assert.AreEqual(1, hud.LocalPlayerId);
                Assert.AreEqual(MatchPhase.Playing, hud.Phase);
                Assert.GreaterOrEqual(hud.PlayerScores.Count, 2);
                Assert.AreEqual(5, hud.MaxServeAmmo);

                GameObject ballCountObject = GameObject.Find("BallCount");
                Assert.IsNotNull(ballCountObject, "BootstrapScene should contain DownPanel/Skill_btn/BallCount.");
                TMP_Text ballCountText = ballCountObject.GetComponent<TMP_Text>();
                Assert.IsNotNull(ballCountText, "BallCount should use TMP_Text.");
                Assert.AreEqual(hud.CurrentServeAmmo.ToString(), ballCountText.text);
                IGatebreakerArenaSceneUiBinding sceneBinding = GatebreakerArenaSceneUiBindingRegistry.Current;
                Assert.IsNotNull(sceneBinding, "BootstrapScene should register the scene UI binding bridge.");
                AssertModeSelectVisible(sceneBinding);
                AssertCountdownAndMovementBindings(sceneBinding, hud);
                AssertPlayerScorePanelTexts(sceneBinding, hud);
                AssertLanBindings(sceneBinding);

                float remainingBeforeModeSelectWait = context.MatchRuntime.RemainingTime;
                yield return new WaitForSeconds(0.35f);
                Assert.AreEqual(
                    remainingBeforeModeSelectWait,
                    context.MatchRuntime.RemainingTime,
                    0.001f,
                    "Mode select should freeze local prototype time before the player chooses human battle.");

                Button localBattleButton = sceneBinding.LocalBattleButtonObject as Button;
                Assert.IsNotNull(localBattleButton, "Local battle mode button should be explicitly bound.");
                localBattleButton.onClick.Invoke();
                TMP_Text countdownText = sceneBinding.StartCountdownTextObject as TMP_Text;
                Assert.IsNotNull(countdownText, "Start countdown text should be explicitly bound.");

                GameObject countdownRoot = (sceneBinding.StartCountdownRootObject as Component)?.gameObject ??
                                           sceneBinding.StartCountdownRootObject as GameObject;
                Assert.IsNotNull(countdownRoot, "Start countdown root should be explicitly bound.");
                Assert.IsTrue(countdownRoot.activeSelf, "Local battle click should show the start countdown.");

                float countdownDeadline = Time.realtimeSinceStartup + 8f;
                while (countdownRoot.activeSelf && Time.realtimeSinceStartup < countdownDeadline)
                {
                    AssertNoFailures(failures);
                    yield return null;
                }

                Assert.IsFalse(countdownRoot.activeSelf, "Start countdown should hide after displaying 开始游戏.");

                int ballCountBeforeClick = context.MatchRuntime.Balls.Count;
                GameObject skillButtonObject = GameObject.Find("Skill_btn");
                Assert.IsNotNull(skillButtonObject, "BootstrapScene should contain DownPanel/Skill_btn.");
                Button skillButton = skillButtonObject.GetComponent<Button>();
                Assert.IsNotNull(skillButton, "Skill_btn should use Unity UI Button.");
                skillButton.onClick.Invoke();
                float serveDeadline = Time.realtimeSinceStartup + 2f;
                while (context.MatchRuntime.Balls.Count <= ballCountBeforeClick &&
                       Time.realtimeSinceStartup < serveDeadline)
                {
                    AssertNoFailures(failures);
                    yield return null;
                }

                Assert.Greater(context.MatchRuntime.Balls.Count, ballCountBeforeClick, "Skill_btn click should request a serve.");

                float smokeStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - smokeStart < SmokeDurationSeconds)
                {
                    AssertNoFailures(failures);
                    yield return null;
                }

                AssertNoFailures(failures);
            }
            finally
            {
                Application.logMessageReceived -= logCallback;
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        private static void AssertNoFailures(IReadOnlyList<string> failures)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail("Unexpected error/assert/exception log during Gatebreaker PlayMode smoke:\n" + failures[0]);
        }

        private static void AssertPlayerScorePanelTexts(
            IGatebreakerArenaSceneUiBinding sceneBinding,
            App.HotUpdate.GatebreakerArena.UI.GatebreakerHudSnapshot hud)
        {
            Assert.IsNotNull(sceneBinding.PlayerScoreTextObjects, "Player score text bindings should exist.");
            Assert.IsNotNull(sceneBinding.PlayerHitTextObjects, "Player hit text bindings should exist.");
            List<PlayerScoreSnapshot> visibleScores = BuildVisiblePlayerScores(hud);
            Assert.Greater(visibleScores.Count, 0, "HUD should contain at least one visible player score.");
            Assert.GreaterOrEqual(sceneBinding.PlayerScoreTextObjects.Length, visibleScores.Count);
            Assert.GreaterOrEqual(sceneBinding.PlayerHitTextObjects.Length, visibleScores.Count);

            for (int i = 0; i < visibleScores.Count; i++)
            {
                PlayerScoreSnapshot score = visibleScores[i];
                TMP_Text scoreText = sceneBinding.PlayerScoreTextObjects[i] as TMP_Text;
                TMP_Text hitText = sceneBinding.PlayerHitTextObjects[i] as TMP_Text;
                Assert.IsNotNull(scoreText, $"Player score binding {i} should be a TMP_Text.");
                Assert.IsNotNull(hitText, $"Player hit binding {i} should be a TMP_Text.");
                Assert.AreEqual(score.Score.ToString(), scoreText.text, $"playerId={score.PlayerId}");
                Assert.AreEqual(score.HitScore.ToString(), hitText.text, $"playerId={score.PlayerId}");
            }
        }

        private static void AssertCountdownAndMovementBindings(
            IGatebreakerArenaSceneUiBinding sceneBinding,
            App.HotUpdate.GatebreakerArena.UI.GatebreakerHudSnapshot hud)
        {
            TMP_Text timeText = sceneBinding.TimeTextObject as TMP_Text;
            Assert.IsNotNull(timeText, "Time binding should be a TMP_Text.");
            Assert.AreNotEqual("59:59", timeText.text, "Time should be driven by the runtime countdown, not the prefab placeholder.");
            Assert.LessOrEqual(hud.RemainingTime, 60f);
            Assert.Greater(hud.RemainingTime, 55f);
            Assert.IsInstanceOf<RectTransform>(sceneBinding.MovementPadObject, "MovementPad should bind the green control background.");
            Assert.IsInstanceOf<RectTransform>(sceneBinding.MovementHandleObject, "MovementHandle should bind the red joystick handle.");
            Assert.IsInstanceOf<RectTransform>(sceneBinding.MovementLeftArrowInputObject, "Left movement arrow should have an explicit input binding.");
            Assert.IsInstanceOf<RectTransform>(sceneBinding.MovementRightArrowInputObject, "Right movement arrow should have an explicit input binding.");
            Assert.IsInstanceOf<Graphic>(sceneBinding.MovementLeftArrowHighlightObject, "Left movement arrow should have an explicit highlight graphic binding.");
            Assert.IsInstanceOf<Graphic>(sceneBinding.MovementRightArrowHighlightObject, "Right movement arrow should have an explicit highlight graphic binding.");
        }

        private static void AssertLanBindings(IGatebreakerArenaSceneUiBinding sceneBinding)
        {
            Assert.IsInstanceOf<GameObject>(sceneBinding.ModeSelectRootObject, "Mode select root should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LocalBattleButtonObject, "Local battle button should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.OnlineBattleButtonObject, "Online battle button should be explicitly bound.");
            Assert.IsInstanceOf<GameObject>(sceneBinding.LanMenuRootObject, "LAN menu root should be explicitly bound.");
            Assert.IsInstanceOf<GameObject>(sceneBinding.LanRoomInfoRootObject, "LAN room-info root should be explicitly bound.");
            Assert.IsInstanceOf<GameObject>(sceneBinding.LanStatusRootObject, "LAN status root should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LanBackButtonObject, "LAN back button should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LanCreateButtonObject, "LAN create button should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LanDiscoverButtonObject, "LAN discover button should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LanJoinButtonObject, "LAN join button should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LanReadyButtonObject, "LAN ready button should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LanStartButtonObject, "LAN start button should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LanLeaveButtonObject, "LAN leave button should be explicitly bound.");
            Assert.IsInstanceOf<Button>(sceneBinding.LanAcknowledgeStartButtonObject, "LAN acknowledge-start button should be explicitly bound.");
            Assert.IsInstanceOf<TMP_InputField>(sceneBinding.LanPlayerNameInputObject, "LAN player-name input should be explicitly bound.");
            Assert.IsInstanceOf<TMP_InputField>(sceneBinding.LanRoomCodeInputObject, "LAN room-code input should be explicitly bound.");
            Assert.IsInstanceOf<TMP_Text>(sceneBinding.LanStateTextObject, "LAN state text should be explicitly bound.");
            Assert.IsInstanceOf<TMP_Text>(sceneBinding.LanRoomCodeTextObject, "LAN room-code text should be explicitly bound.");
            Assert.IsInstanceOf<TMP_Text>(sceneBinding.LanPlayerCountTextObject, "LAN player-count text should be explicitly bound.");
            Assert.IsInstanceOf<TMP_Text>(sceneBinding.LanLocalIpTextObject, "LAN local-IP text should be explicitly bound.");
            Assert.IsInstanceOf<TMP_Text>(sceneBinding.LanRoomIpTextObject, "LAN room-IP text should be explicitly bound.");
            Assert.IsInstanceOf<TMP_Text>(sceneBinding.LanErrorTextObject, "LAN error text should be explicitly bound.");
            Assert.IsInstanceOf<GameObject>(sceneBinding.StartCountdownRootObject, "Start countdown root should be explicitly bound.");
            Assert.IsInstanceOf<TMP_Text>(sceneBinding.StartCountdownTextObject, "Start countdown text should be explicitly bound.");
        }

        private static void AssertModeSelectVisible(IGatebreakerArenaSceneUiBinding sceneBinding)
        {
            GameObject modeSelect = sceneBinding.ModeSelectRootObject as GameObject;
            GameObject lanMenu = sceneBinding.LanMenuRootObject as GameObject;
            Button lanBackButton = sceneBinding.LanBackButtonObject as Button;
            GameObject countdown = sceneBinding.StartCountdownRootObject as GameObject;
            Assert.IsNotNull(modeSelect, "ModeSelectPanel should be bound.");
            Assert.IsNotNull(lanMenu, "LanPanel should be bound as the online menu root.");
            Assert.IsNotNull(lanBackButton, "LanBackButton should be bound.");
            Assert.IsNotNull(countdown, "StartCountdownPanel should be bound.");
            Assert.IsTrue(modeSelect.activeSelf, "BootstrapScene should start on mode select.");
            Assert.IsFalse(lanBackButton.gameObject.activeSelf, "LAN back button should stay hidden on mode select.");
            Assert.IsFalse(lanMenu.activeSelf, "Create/Join menu should stay hidden until Online Battle is selected.");
            Assert.IsFalse(countdown.activeSelf, "Start countdown should be hidden before Local Battle is selected.");
        }

        private static List<PlayerScoreSnapshot> BuildVisiblePlayerScores(
            App.HotUpdate.GatebreakerArena.UI.GatebreakerHudSnapshot hud)
        {
            var visibleScores = new List<PlayerScoreSnapshot>();
            for (int i = 0; i < hud.PlayerScores.Count; i++)
            {
                PlayerScoreSnapshot score = hud.PlayerScores[i];
                if (!score.IsDisabled)
                {
                    visibleScores.Add(score);
                }
            }

            visibleScores.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return visibleScores;
        }
    }
}
