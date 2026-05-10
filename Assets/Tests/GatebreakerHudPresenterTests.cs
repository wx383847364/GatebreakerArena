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
            Assert.AreEqual(2, snapshot.MaxServeAmmo);
            Assert.AreEqual(1, snapshot.OwnedBallsInField);
            Assert.AreEqual(1, snapshot.MaxOwnedBallsInField);
            Assert.AreEqual(2, snapshot.PlayerScores.Count);
            Assert.IsFalse(snapshot.HasDanger);
        }
    }
}
