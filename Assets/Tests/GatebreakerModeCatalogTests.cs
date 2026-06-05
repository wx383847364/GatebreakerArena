using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerModeCatalogTests
    {
        [Test]
        public void DefaultRulesMatchGddV03Samples()
        {
            GatebreakerModeCatalog catalog = GatebreakerModeCatalog.CreateDefault();

            ModeRuleDefinition pve = catalog.GetMode("PVE_STANDARD");
            BallRuleDefinition ball = catalog.GetBall("BALL_NORMAL");
            AiRuleDefinition ai = catalog.GetAi("AI_NORMAL");
            MapRuleDefinition map = catalog.GetMap("MAP_ARENA_01");
            EffectiveMatchRule effective = catalog.BuildEffectiveRule("PVE_STANDARD", "MAP_ARENA_01");

            Assert.AreEqual(60, pve.MatchDuration);
            Assert.AreEqual(0, pve.InitialBallsInMatch);
            Assert.AreEqual(4, pve.MaxBallsInMatch);
            Assert.AreEqual(2, pve.InitialServeAmmo);
            Assert.AreEqual(2, pve.MaxServeAmmo);
            Assert.AreEqual(1, pve.MaxOwnedBallsInField);
            Assert.IsTrue(pve.AllowAimServe);
            Assert.AreEqual(OvertimeRuleType.SuddenDeath, pve.OvertimeRuleType);
            Assert.AreEqual(3, pve.BallSpeedByTime.Count);
            Assert.AreEqual(15f, pve.BallSpeedByTime[0].TimeSeconds);
            Assert.AreEqual(10f, pve.BallSpeedByTime[0].Speed);
            Assert.AreEqual(5.25f, ball.InitialSpeed);
            Assert.AreEqual(9.8f, ball.MaxSpeed);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Ball01.prefab", ball.PrefabLocation);
            Assert.AreEqual("Red", catalog.GetPlayerColor(1).ColorName);
            Assert.AreEqual(1.0f, catalog.GetPlayerColor(1).Red);
            Assert.AreEqual("Blue", catalog.GetPlayerColor(2).ColorName);
            Assert.AreEqual("Green", catalog.GetPlayerColor(3).ColorName);
            Assert.AreEqual("Yellow", catalog.GetPlayerColor(4).ColorName);
            Assert.AreEqual(0.18f, ai.ReactionDelay);
            Assert.AreEqual(SpawnLayoutType.FourSide, map.SpawnLayoutType);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab", map.ScenePrefabLocation);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Baffle.prefab", map.PaddlePrefabLocation);
            Assert.AreEqual(3.2f, map.PaddleMoveSpeed);
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
            Assert.AreEqual(0, effective.InitialBallsInMatch);
            Assert.AreEqual(20, effective.MaxBallsInMatch);
            Assert.AreEqual(2, effective.InitialServeAmmo);
            Assert.AreEqual(5, effective.MaxServeAmmo);
            Assert.AreEqual(5, effective.MaxOwnedBallsInField);
            Assert.AreEqual(5.0f, effective.ServeRechargeSeconds);
        }
    }
}
