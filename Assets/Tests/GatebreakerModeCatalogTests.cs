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

            Assert.AreEqual(150, pve.MatchDuration);
            Assert.AreEqual(1, pve.InitialBallsInMatch);
            Assert.AreEqual(4, pve.MaxBallsInMatch);
            Assert.AreEqual(1, pve.InitialServeAmmo);
            Assert.AreEqual(2, pve.MaxServeAmmo);
            Assert.AreEqual(1, pve.MaxOwnedBallsInField);
            Assert.AreEqual(OvertimeRuleType.SuddenDeath, pve.OvertimeRuleType);
            Assert.AreEqual(7.5f, ball.InitialSpeed);
            Assert.AreEqual(14.0f, ball.MaxSpeed);
            Assert.AreEqual(0.18f, ai.ReactionDelay);
            Assert.AreEqual(SpawnLayoutType.FourSide, map.SpawnLayoutType);
            Assert.AreEqual(1, effective.InitialBallsInMatch);
            Assert.AreEqual(4, effective.MaxBallsInMatch);
            Assert.AreEqual(6.0f, effective.BaseServeCooldown);
        }
    }
}
