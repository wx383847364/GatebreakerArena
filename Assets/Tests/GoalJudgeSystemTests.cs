using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Zone;
using NUnit.Framework;
using UnityEngine;

namespace Gatebreaker.Tests
{
    public sealed class GoalJudgeSystemTests
    {
        [Test]
        public void EnemyBallEnteringZoneScoresAndLeavesField()
        {
            var system = new GoalJudgeSystem();
            BallRuntimeState ball = CreateBall(ownerPlayerId: 2);

            GoalJudgeResult result = system.ResolveGoalEntry(ball, 1, 1, CreateRule(), Vector2.up);

            Assert.IsTrue(result.Scored);
            Assert.IsFalse(result.Rebounded);
            Assert.AreEqual(2, result.ScoringPlayerId);
            Assert.AreEqual(BallState.ScoredOut, ball.BallState);
        }

        [Test]
        public void OwnBallEnteringOwnZoneReboundsWithoutScoring()
        {
            var system = new GoalJudgeSystem();
            BallRuntimeState ball = CreateBall(ownerPlayerId: 1);

            GoalJudgeResult result = system.ResolveGoalEntry(ball, 1, 1, CreateRule(), Vector2.up);

            Assert.IsFalse(result.Scored);
            Assert.IsTrue(result.Rebounded);
            Assert.AreEqual(BallState.GoalRebound, ball.BallState);
            Assert.Greater(ball.Velocity.y, 0f);
        }

        private static BallRuntimeState CreateBall(int ownerPlayerId)
        {
            return new BallRuntimeState
            {
                BallId = 7,
                OwnerPlayerId = ownerPlayerId,
                OwnerTeamId = ownerPlayerId,
                BallState = BallState.Flying,
                Velocity = new Vector2(0f, -8f),
            };
        }

        private static BallRuleDefinition CreateRule()
        {
            return GatebreakerModeCatalog.CreateDefault().GetBall("BALL_NORMAL");
        }
    }
}
