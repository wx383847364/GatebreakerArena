using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.Zone;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GatebreakerMatchRuntimeTests
    {
        [Test]
        public void LocalPrototypeStartsWithConfiguredPlayersAndInitialBalls()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();

            runtime.StartLocalPrototype(aiCount: 3);

            Assert.AreEqual(MatchPhase.Playing, runtime.Phase);
            Assert.AreEqual(4, runtime.Players.Count);
            Assert.AreEqual(1, runtime.Balls.Count);
            Assert.AreEqual(1, runtime.FindPlayer(1).ServeResource.OwnedBallsInField);
            Assert.AreEqual(150f, runtime.RemainingTime);
        }

        [Test]
        public void ScoredBallUpdatesScoreAndRestoresOwnerBallCount()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            int initialBallId = runtime.Balls[0].BallId;

            runtime.ResolveGoalEntry(initialBallId, 2, Vector2.down);

            Assert.AreEqual(1, runtime.FindPlayer(1).Score);
            Assert.AreEqual(0, runtime.FindPlayer(1).ServeResource.OwnedBallsInField);
        }

        [Test]
        public void OwnedBallLimitBlocksConsecutiveServe()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);

            bool servedAgain = runtime.TryServe(1, out ServeBlockReason reason);

            Assert.IsFalse(servedAgain);
            Assert.AreEqual(ServeBlockReason.OwnedBallLimit, reason);
        }

        [Test]
        public void UniqueLeaderWinsWhenRegularTimeExpires()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 2;
            runtime.FindPlayer(2).Score = 1;

            runtime.Tick(200f);

            Assert.AreEqual(MatchPhase.Result, runtime.Phase);
            Assert.IsTrue(runtime.HasWinner);
            Assert.AreEqual(1, runtime.WinnerPlayerId);
        }

        [Test]
        public void TiedHighestScoreEntersSuddenDeathOvertime()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 2;
            runtime.FindPlayer(2).Score = 2;

            runtime.Tick(200f);
            ScoreboardSnapshot snapshot = runtime.CreateScoreboardSnapshot();

            Assert.AreEqual(MatchPhase.Overtime, runtime.Phase);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, snapshot.OvertimeEligiblePlayerIds);
            Assert.AreEqual(60f, runtime.RemainingTime);
        }

        [Test]
        public void OvertimeEligiblePlayerWinsOnNextScore()
        {
            GatebreakerMatchRuntime runtime = CreateRuntime();
            runtime.StartLocalPrototype(aiCount: 1);
            runtime.FindPlayer(1).Score = 2;
            runtime.FindPlayer(2).Score = 2;
            runtime.Tick(200f);
            int initialBallId = runtime.Balls[0].BallId;

            runtime.ResolveGoalEntry(initialBallId, 2, Vector2.down);

            Assert.AreEqual(MatchPhase.Result, runtime.Phase);
            Assert.AreEqual(1, runtime.WinnerPlayerId);
        }

        private static GatebreakerMatchRuntime CreateRuntime()
        {
            return new GatebreakerMatchRuntime(
                GatebreakerModeCatalog.CreateDefault(),
                new BallSimulationSystem(),
                new ServeResourceSystem(),
                new GoalJudgeSystem(),
                new ScoreSystem(),
                null);
        }
    }
}
