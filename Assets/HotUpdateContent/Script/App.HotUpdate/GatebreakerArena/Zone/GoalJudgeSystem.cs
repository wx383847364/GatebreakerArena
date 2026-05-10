using System;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Zone
{
    public sealed class GoalJudgeSystem
    {
        public GoalJudgeResult ResolveGoalEntry(
            BallRuntimeState ball,
            int zoneOwnerPlayerId,
            int zoneOwnerTeamId,
            BallRuleDefinition rule,
            Vector2 reboundDirection)
        {
            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }

            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (ball.OwnerPlayerId != zoneOwnerPlayerId)
            {
                ball.BallState = BallState.ScoredOut;
                return GoalJudgeResult.Score(ball.OwnerPlayerId, ball.OwnerTeamId, zoneOwnerPlayerId, zoneOwnerTeamId, ball.BallId);
            }

            Vector2 direction = reboundDirection.sqrMagnitude > 0.0001f ? reboundDirection.normalized : Vector2.up;
            float speed = Mathf.Clamp(ball.Velocity.magnitude * rule.GoalReboundFactor, rule.InitialSpeed, rule.MaxSpeed);
            ball.Velocity = direction * speed;
            ball.BallState = BallState.GoalRebound;
            return GoalJudgeResult.Rebound(ball.OwnerPlayerId, zoneOwnerPlayerId, ball.BallId);
        }

        public void FinishRebound(BallRuntimeState ball)
        {
            if (ball != null && ball.BallState == BallState.GoalRebound)
            {
                ball.BallState = BallState.Flying;
            }
        }
    }

    public readonly struct GoalJudgeResult
    {
        private GoalJudgeResult(
            bool scored,
            bool rebounded,
            int scoringPlayerId,
            int scoringTeamId,
            int zoneOwnerPlayerId,
            int zoneOwnerTeamId,
            int ballId)
        {
            Scored = scored;
            Rebounded = rebounded;
            ScoringPlayerId = scoringPlayerId;
            ScoringTeamId = scoringTeamId;
            ZoneOwnerPlayerId = zoneOwnerPlayerId;
            ZoneOwnerTeamId = zoneOwnerTeamId;
            BallId = ballId;
        }

        public bool Scored { get; }
        public bool Rebounded { get; }
        public int ScoringPlayerId { get; }
        public int ScoringTeamId { get; }
        public int ZoneOwnerPlayerId { get; }
        public int ZoneOwnerTeamId { get; }
        public int BallId { get; }

        public static GoalJudgeResult Score(
            int scoringPlayerId,
            int scoringTeamId,
            int zoneOwnerPlayerId,
            int zoneOwnerTeamId,
            int ballId)
        {
            return new GoalJudgeResult(true, false, scoringPlayerId, scoringTeamId, zoneOwnerPlayerId, zoneOwnerTeamId, ballId);
        }

        public static GoalJudgeResult Rebound(int ownerPlayerId, int zoneOwnerPlayerId, int ballId)
        {
            return new GoalJudgeResult(false, true, ownerPlayerId, 0, zoneOwnerPlayerId, 0, ballId);
        }
    }
}
