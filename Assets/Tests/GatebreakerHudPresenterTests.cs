using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.UI;
using App.HotUpdate.GatebreakerArena.Zone;
using NUnit.Framework;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerHudPresenterTests
    {
        [Test]
        public void HudSnapshotExposesP0MatchAndServeState()
        {
            var runtime = new GatebreakerMatchRuntime(
                GatebreakerModeCatalog.CreateDefault(),
                new BallSimulationSystem(),
                new ServeResourceSystem(),
                new GoalJudgeSystem(),
                new ScoreSystem(),
                null);
            runtime.StartLocalPrototype(aiCount: 1);
            var presenter = new GatebreakerArenaHudPresenter(runtime);

            GatebreakerHudSnapshot snapshot = presenter.BuildSnapshot(1);

            Assert.AreEqual(1, snapshot.LocalPlayerId);
            Assert.AreEqual(2, snapshot.CurrentServeAmmo);
            Assert.AreEqual(5, snapshot.MaxServeAmmo);
            Assert.AreEqual(0, snapshot.OwnedBallsInField);
            Assert.AreEqual(5, snapshot.MaxOwnedBallsInField);
            Assert.AreEqual(2, snapshot.PlayerScores.Count);
            Assert.AreEqual(0, snapshot.PlayerScores[0].HitScore);
            Assert.AreEqual(snapshot.PlayerScores[0].Score, snapshot.PlayerScores[0].TrueScore);
            Assert.IsFalse(snapshot.HasDanger);
            Assert.IsFalse(snapshot.HasWinner);
            Assert.AreEqual(0, snapshot.WinnerPlayerId);
        }

        [Test]
        public void HudSnapshotExposesResultWinner()
        {
            var runtime = new GatebreakerMatchRuntime(
                GatebreakerModeCatalog.CreateDefault(),
                new BallSimulationSystem(),
                new ServeResourceSystem(),
                new GoalJudgeSystem(),
                new ScoreSystem(),
                null);
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 7;
            runtime.FindPlayer(1).HitScore = -2;
            runtime.FindPlayer(2).Score = 3;
            runtime.Tick(200f);
            var presenter = new GatebreakerArenaHudPresenter(runtime);

            GatebreakerHudSnapshot snapshot = presenter.BuildSnapshot(1);

            Assert.IsTrue(snapshot.HasWinner);
            Assert.AreEqual(1, snapshot.WinnerPlayerId);
            Assert.AreEqual(5, snapshot.PlayerScores[0].TrueScore);
        }

        [Test]
        public void HudSnapshotCopiesHeroRuntimeStateWithoutCalculatingIt()
        {
            var runtime = new GatebreakerMatchRuntime(
                GatebreakerModeCatalog.CreateDefault(),
                new BallSimulationSystem(),
                new ServeResourceSystem(),
                new GoalJudgeSystem(),
                new ScoreSystem(),
                null);
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Hero = new HeroRuntimeState
            {
                HeroId = "HERO_FROST_QUEEN",
                ActiveChipIds = new[] { "STRIKE_POWER", "GUARD_LENGTH", "FLOW_SPEED" },
                PathStates = new[]
                {
                    new HeroPathRuntimeState { PathId = "PATH_FROST_DEEP_FREEZE", Level = 2 },
                    new HeroPathRuntimeState { PathId = "PATH_FROST_ICE_CRYSTAL", Level = 1 },
                },
                AbilityCooldownRemainingFrames = 42,
                TemporaryStatuses = new[]
                {
                    new HeroTemporaryStatusState
                    {
                        StatusType = HeroTemporaryStatusType.Frozen,
                        RemainingFrames = 18,
                        Magnitude = 1f,
                    },
                },
            };

            GatebreakerHudSnapshot snapshot = new GatebreakerArenaHudPresenter(runtime).BuildSnapshot(1);

            Assert.AreEqual("HERO_FROST_QUEEN", snapshot.Hero.HeroId);
            CollectionAssert.AreEqual(
                new[] { "STRIKE_POWER", "GUARD_LENGTH", "FLOW_SPEED" },
                snapshot.Hero.ActiveChipIds);
            Assert.AreEqual(2, snapshot.Hero.PathLevels.Count);
            Assert.AreEqual(2, snapshot.Hero.PathLevels[0].Level);
            Assert.AreEqual(42, snapshot.Hero.AbilityCooldownRemainingFrames);
            Assert.AreEqual(HeroTemporaryStatusType.Frozen, snapshot.Hero.TemporaryStatuses[0].StatusType);
            Assert.AreEqual(18, snapshot.Hero.TemporaryStatuses[0].RemainingFrames);
            Assert.IsNull(snapshot.CountdownReveal, "Playing HUD should not expose the countdown flip-card payload.");
        }
    }
}
