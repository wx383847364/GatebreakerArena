using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.AI
{
    public sealed class GatebreakerAiService
    {
        public PlayerInputFrame BuildFrame(PlayerRuntimeState player, GatebreakerMatchRuntime runtime)
        {
            if (player == null || player.Paddle == null)
            {
                return new PlayerInputFrame(0, 0f, false, Vector2.zero);
            }

            float targetAxis = player.Paddle.AxisPosition;
            float bestThreat = float.MaxValue;
            if (runtime != null)
            {
                for (int i = 0; i < runtime.Balls.Count; i++)
                {
                    BallRuntimeState ball = runtime.Balls[i];
                    if (ball == null || (ball.BallState != BallState.Flying && ball.BallState != BallState.GoalRebound))
                    {
                        continue;
                    }

                    float approach = Vector2.Dot(ball.Velocity, -player.Paddle.Normal);
                    if (approach <= 0f)
                    {
                        continue;
                    }

                    float distance = Mathf.Abs(Vector2.Dot(ball.Position - player.Paddle.Position, player.Paddle.Normal));
                    if (distance >= bestThreat)
                    {
                        continue;
                    }

                    bestThreat = distance;
                    targetAxis = Vector2.Dot(ball.Position, player.Paddle.Tangent);
                }
            }

            float delta = targetAxis - player.Paddle.AxisPosition;
            float moveAxis = Mathf.Abs(delta) < 0.05f ? 0f : Mathf.Sign(delta);
            bool shouldServe = runtime != null &&
                               player.ServeResource != null &&
                               player.ServeResource.CurrentServeAmmo > 0 &&
                               player.ServeResource.ServeCooldownRemaining <= 0f;
            return new PlayerInputFrame(player.PlayerId, moveAxis, shouldServe, player.Paddle.Normal);
        }
    }
}
