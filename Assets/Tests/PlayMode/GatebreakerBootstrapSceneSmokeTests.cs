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
                AssertPlayerScorePanelTexts(sceneBinding, hud);

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
