using System.Collections;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using App.Shared.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerConfigRuntimeLoaderTests
    {
        [UnityTest]
        public IEnumerator LoadAsync_LoadsRulesBytesThroughAssetsRuntime()
        {
            var handle = new FakeAssetHandle(new TextAsset(CreateRulesJson("TeamScore", "FourSide")));
            var assetsRuntime = new FakeAssetsRuntime(handle);
            var loader = new GatebreakerConfigRuntimeLoader();

            Task<GatebreakerConfigLoadResult> loadTask = loader.LoadAsync(assetsRuntime);
            yield return WaitForTask(loadTask);
            GatebreakerConfigLoadResult result = loadTask.Result;

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.AreEqual(GatebreakerConfigRuntimeLoader.RulesAssetLocation, assetsRuntime.LoadedLocation);
            Assert.AreEqual(7, result.Version);
            Assert.IsTrue(handle.Released);

            ModeRuleDefinition mode = result.Catalog.GetMode("PVP_TEAM");
            BallRuleDefinition ball = result.Catalog.GetBall("BALL_FAST");
            AiRuleDefinition ai = result.Catalog.GetAi("AI_HARD");
            MapRuleDefinition map = result.Catalog.GetMap("MAP_RING");
            EffectiveMatchRule effective = result.Catalog.BuildEffectiveRule("PVP_TEAM", "MAP_RING");

            Assert.AreEqual(ScoreRuleType.TeamScore, mode.ScoreRuleType);
            Assert.AreEqual(OvertimeRuleType.TimedScore, mode.OvertimeRuleType);
            Assert.AreEqual(180, mode.Time);
            Assert.AreEqual(180, mode.MatchDuration);
            Assert.AreEqual(9.25f, ball.InitialSpeed);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Ball02.prefab", ball.PrefabLocation);
            Assert.AreEqual("Blue", result.Catalog.GetPlayerColor(2).ColorName);
            Assert.AreEqual(0.48f, result.Catalog.GetPlayerColor(2).Green);
            Assert.AreEqual(0.1f, ai.ReactionDelay);
            Assert.AreEqual(SpawnLayoutType.FourSide, map.SpawnLayoutType);
            CollectionAssert.AreEqual(new[] { 2, 4 }, map.SupportedPlayerCount);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab", map.ScenePrefabLocation);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Baffle.prefab", map.PaddlePrefabLocation);
            Assert.AreEqual(3, map.DefaultPlayerCount);
            Assert.AreEqual(3, map.PlayerSideBindings.Count);
            Assert.AreEqual(1, map.PlayerSideBindings[0].PlayerId);
            Assert.AreEqual("Position01", map.PlayerSideBindings[0].ScenePosition);
            Assert.AreEqual(5, map.PlayerSideBindings[0].BoundarySegmentIndex);
            Assert.AreEqual(2, map.PlayerSideBindings[1].PlayerId);
            Assert.AreEqual("Position03", map.PlayerSideBindings[1].ScenePosition);
            Assert.AreEqual(1, map.PlayerSideBindings[1].BoundarySegmentIndex);
            Assert.AreEqual(3, map.PlayerSideBindings[2].PlayerId);
            Assert.AreEqual("Position05", map.PlayerSideBindings[2].ScenePosition);
            Assert.AreEqual(3, map.PlayerSideBindings[2].BoundarySegmentIndex);
            Assert.AreEqual(3, effective.InitialBallsInMatch);
            Assert.AreEqual(7, effective.MaxBallsInMatch);
            Assert.AreEqual(1, effective.InitialServeAmmo);
            Assert.AreEqual(5, effective.MaxServeAmmo);
            Assert.AreEqual(4, effective.MaxOwnedBallsInField);
            Assert.AreEqual(4.0f, effective.ServeRechargeSeconds);
        }

        [Test]
        public void ParseJson_AcceptsNumericEnumsAndStringNumbers()
        {
            GatebreakerConfigLoadResult result = GatebreakerConfigRuntimeLoader.ParseJson(CreateRulesJson("1", 1));

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.AreEqual(ScoreRuleType.TeamScore, result.Catalog.GetMode("PVP_TEAM").ScoreRuleType);
            Assert.AreEqual(SpawnLayoutType.Ring, result.Catalog.GetMap("MAP_RING").SpawnLayoutType);
            CollectionAssert.AreEqual(new[] { 2, 4 }, result.Catalog.GetMap("MAP_RING").SupportedPlayerCount);
        }

        [UnityTest]
        public IEnumerator LoadAsync_ReturnsFallbackInfoWhenAssetIsMissing()
        {
            var loader = new GatebreakerConfigRuntimeLoader();

            Task<GatebreakerConfigLoadResult> loadTask = loader.LoadAsync(new FakeAssetsRuntime(null));
            yield return WaitForTask(loadTask);
            GatebreakerConfigLoadResult result = loadTask.Result;

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(GatebreakerConfigLoadFailureReason.AssetLoadFailed, result.FailureReason);
            Assert.IsTrue(result.CanUseDefaultCatalogFallback);
            Assert.AreEqual(GatebreakerConfigRuntimeLoader.RulesAssetLocation, result.Source);
            StringAssert.Contains("Failed to load", result.Message);
        }

        [UnityTest]
        public IEnumerator LoadAsync_ReturnsParseFailureForInvalidJson()
        {
            var handle = new FakeAssetHandle(new TextAsset("{\"DT_ModeRule\": ["));
            var loader = new GatebreakerConfigRuntimeLoader();

            Task<GatebreakerConfigLoadResult> loadTask = loader.LoadAsync(new FakeAssetsRuntime(handle));
            yield return WaitForTask(loadTask);
            GatebreakerConfigLoadResult result = loadTask.Result;

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(GatebreakerConfigLoadFailureReason.ParseFailed, result.FailureReason);
            Assert.IsTrue(result.CanUseDefaultCatalogFallback);
            Assert.IsTrue(handle.Released);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception;
            }

            if (task.IsCanceled)
            {
                Assert.Fail("Task was canceled.");
            }
        }

        private static string CreateRulesJson(object scoreRuleType, object spawnLayoutType)
        {
            return string.Format(
                @"{{
  ""Version"": 7,
  ""DT_ModeRule"": [
    {{
      ""ModeId"": ""PVP_TEAM"",
      ""ModeName"": ""Team"",
      ""Time"": ""180"",
      ""MatchDuration"": ""180"",
      ""InitialBallsInMatch"": 2,
      ""MaxBallsInMatch"": 6,
      ""BaseServeCooldown"": ""5.25"",
      ""InitialServeAmmo"": 1,
      ""MaxServeAmmo"": 3,
      ""MaxOwnedBallsInField"": 2,
      ""GoalPauseTime"": 0.5,
      ""ScoreRuleType"": {0},
      ""EnableOvertime"": ""true"",
      ""OvertimeRuleType"": ""TimedScore"",
      ""OvertimeDuration"": 45,
      ""OvertimeEligibleOnly"": false,
      ""OvertimeWinScore"": 2,
      ""AllowAimServe"": true,
      ""FinalPhaseStartTime"": 20,
      ""FinalPhaseBallSpeedScale"": 1.2,
      ""FinalPhaseCooldownScale"": 0.8
    }}
  ],
  ""DT_BallRule"": [
    {{
      ""BallTypeId"": ""BALL_FAST"",
      ""BallTypeName"": ""Fast"",
      ""InitialSpeed"": 9.25,
      ""MaxSpeed"": 16.5,
      ""PaddleBounceFactor"": 1.1,
      ""WallBounceFactor"": 1.0,
      ""GoalReboundFactor"": 0.9,
      ""SpeedGainOnPaddleHit"": 0.2,
      ""MinVerticalVelocity"": 2.5,
      ""DangerPromptThreshold"": 1.1,
      ""TrailStyle"": ""Fast"",
      ""ColorTag"": ""Red"",
      ""PrefabLocation"": ""Assets/HotUpdateContent/Res/prefabs/Ball02.prefab""
    }}
  ],
  ""DT_AIRule"": [
    {{
      ""AILevelId"": ""AI_HARD"",
      ""AILevelName"": ""Hard"",
      ""ReactionDelay"": 0.1,
      ""PredictError"": 0.15,
      ""ServeDecisionInterval"": 0.4,
      ""AggressionWeight"": 0.8,
      ""DefenseWeight"": 0.7,
      ""MultiBallPriority"": 0.9,
      ""AimAccuracy"": 0.75,
      ""TargetSwitchFrequency"": 0.6
    }}
  ],
  ""DT_MapRule"": [
    {{
      ""MapId"": ""MAP_RING"",
      ""MapName"": ""Ring"",
      ""SupportedPlayerCount"": [""2"", 4],
      ""SpawnLayoutType"": {1},
      ""HasObstacle"": true,
      ""InitialBallsModifier"": 1,
      ""MaxBallsModifier"": 1,
      ""ServeCooldownModifier"": -0.5,
      ""MaxServeAmmo"": 5,
      ""MaxOwnedBallsInField"": 4,
      ""ServeRechargeSeconds"": 4.0,
      ""BallSpeedModifier"": 0.2,
      ""GoalSizeModifier"": -0.1,
      ""ScenePrefabLocation"": ""Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab"",
      ""PaddlePrefabLocation"": ""Assets/HotUpdateContent/Res/prefabs/Baffle.prefab"",
      ""DefaultPlayerCount"": 3,
      ""PlayerSideBindings"": [
        {{
          ""PlayerId"": 1,
          ""ScenePosition"": ""Position01"",
          ""BoundarySegmentIndex"": 5
        }},
        {{
          ""PlayerId"": 2,
          ""ScenePosition"": ""Position03"",
          ""BoundarySegmentIndex"": 1
        }},
        {{
          ""PlayerId"": 3,
          ""ScenePosition"": ""Position05"",
          ""BoundarySegmentIndex"": 3
        }}
      ]
    }}
  ],
  ""DT_PlayerColorRule"": [
    {{
      ""PlayerId"": 1,
      ""ColorName"": ""Red"",
      ""Red"": 1.0,
      ""Green"": 0.18,
      ""Blue"": 0.16,
      ""Alpha"": 1.0
    }},
    {{
      ""PlayerId"": 2,
      ""ColorName"": ""Blue"",
      ""Red"": 0.20,
      ""Green"": 0.48,
      ""Blue"": 1.0,
      ""Alpha"": 1.0
    }},
    {{
      ""PlayerId"": 3,
      ""ColorName"": ""Green"",
      ""Red"": 0.24,
      ""Green"": 0.86,
      ""Blue"": 0.34,
      ""Alpha"": 1.0
    }},
    {{
      ""PlayerId"": 4,
      ""ColorName"": ""Yellow"",
      ""Red"": 1.0,
      ""Green"": 0.86,
      ""Blue"": 0.18,
      ""Alpha"": 1.0
    }}
  ]
}}",
                FormatJsonValue(scoreRuleType),
                FormatJsonValue(spawnLayoutType));
        }

        private static string FormatJsonValue(object value)
        {
            return value is string text ? $"\"{text}\"" : value.ToString();
        }

        private sealed class FakeAssetsRuntime : IAssetsRuntime
        {
            private readonly IAssetHandle _handle;

            public FakeAssetsRuntime(IAssetHandle handle)
            {
                _handle = handle;
            }

            public string LoadedLocation { get; private set; }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public Task<bool> RunPatchFlowAsync(string packageVersion = null)
            {
                return Task.FromResult(true);
            }

            public Task<IAssetHandle> LoadAssetAsync(string location)
            {
                LoadedLocation = location;
                return Task.FromResult(_handle);
            }

            public void Shutdown()
            {
            }
        }

        private sealed class FakeAssetHandle : IAssetHandle
        {
            public FakeAssetHandle(Object assetObject)
            {
                AssetObject = assetObject;
            }

            public Object AssetObject { get; }
            public bool Released { get; private set; }

            public void Release()
            {
                Released = true;
            }
        }
    }
}
