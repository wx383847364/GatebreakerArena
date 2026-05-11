using System.Collections;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Bootstrap;
using App.HotUpdate.GatebreakerArena.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
                Assert.IsNotNull(context.HudPresenter, "HUD signal: HUD presenter should be registered.");
                Assert.IsNotNull(GameObject.Find("Gatebreaker Prototype Runner"), "Prototype runner GameObject should be created.");
                Assert.IsNotNull(GameObject.Find("Arena Floor"), "Runtime-generated arena floor should be visible.");
                Assert.IsNotNull(GameObject.Find("Player 1 Paddle"), "Local paddle primitive should be visible.");

                Assert.AreEqual(MatchPhase.Playing, context.MatchRuntime.Phase);
                Assert.GreaterOrEqual(context.MatchRuntime.Players.Count, 2);
                Assert.GreaterOrEqual(context.MatchRuntime.Balls.Count, 1);

                var hud = context.HudPresenter.BuildSnapshot(1);
                Assert.AreEqual(1, hud.LocalPlayerId);
                Assert.AreEqual(MatchPhase.Playing, hud.Phase);
                Assert.GreaterOrEqual(hud.PlayerScores.Count, 2);
                Assert.Greater(hud.MaxServeAmmo, 0);

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
    }
}
