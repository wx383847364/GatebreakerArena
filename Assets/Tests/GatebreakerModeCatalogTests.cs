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

            Assert.AreEqual(60, pve.Time);
            Assert.AreEqual(60, pve.MatchDuration);
            Assert.AreEqual(1, pve.InitialBallsInMatch);
            Assert.AreEqual(4, pve.MaxBallsInMatch);
            Assert.AreEqual(1, pve.InitialServeAmmo);
            Assert.AreEqual(2, pve.MaxServeAmmo);
            Assert.AreEqual(1, pve.MaxOwnedBallsInField);
            Assert.IsTrue(pve.AllowAimServe);
            Assert.AreEqual(OvertimeRuleType.SuddenDeath, pve.OvertimeRuleType);
            Assert.AreEqual(5.25f, ball.InitialSpeed);
            Assert.AreEqual(9.8f, ball.MaxSpeed);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Ball01.prefab", ball.PrefabLocation);
            Assert.AreEqual(0.18f, ai.ReactionDelay);
            Assert.AreEqual(SpawnLayoutType.FourSide, map.SpawnLayoutType);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab", map.ScenePrefabLocation);
            Assert.AreEqual("Assets/HotUpdateContent/Res/prefabs/Baffle.prefab", map.PaddlePrefabLocation);
            Assert.AreEqual(1, effective.InitialBallsInMatch);
            Assert.AreEqual(20, effective.MaxBallsInMatch);
            Assert.AreEqual(1, effective.InitialServeAmmo);
            Assert.AreEqual(5, effective.MaxServeAmmo);
            Assert.AreEqual(5, effective.MaxOwnedBallsInField);
            Assert.AreEqual(5.0f, effective.ServeRechargeSeconds);
        }
    }
}
