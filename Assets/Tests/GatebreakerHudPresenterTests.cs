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
            Assert.AreEqual(1, snapshot.CurrentServeAmmo);
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
    }
}
