using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Ball
{
    public sealed class BallSimulationSystem
    {
        public BallRuntimeState SpawnBall(
            int ballId,
            int ownerPlayerId,
            int ownerTeamId,
            string spawnSourceType,
            BallRuleDefinition rule,
            Vector2 position,
            Vector2 direction)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            return new BallRuntimeState
            {
                BallId = ballId,
                OwnerPlayerId = ownerPlayerId,
                OwnerTeamId = ownerTeamId,
                SpawnSourceType = spawnSourceType,
                BallTypeId = rule.BallTypeId,
                BallState = BallState.Flying,
                Position = position,
                Velocity = normalized * rule.InitialSpeed,
            };
        }

        public void Tick(IReadOnlyList<BallRuntimeState> balls, float deltaTime)
        {
            if (balls == null)
            {
                return;
            }

            float safeDelta = Math.Max(0f, deltaTime);
            for (int i = 0; i < balls.Count; i++)
            {
                BallRuntimeState ball = balls[i];
                if (ball == null || ball.BallState != BallState.Flying)
                {
                    continue;
                }

                ball.Position += ball.Velocity * safeDelta;
            }
        }

        public void ClampSpeed(BallRuntimeState ball, BallRuleDefinition rule)
        {
            if (ball == null || rule == null)
            {
                return;
            }

            float speed = ball.Velocity.magnitude;
            if (speed > rule.MaxSpeed)
            {
                ball.Velocity = ball.Velocity.normalized * rule.MaxSpeed;
            }
        }
    }
}
