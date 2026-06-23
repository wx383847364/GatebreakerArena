using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.AI
{
    public sealed class GatebreakerAiService
    {
        private const float MinimumServeIntervalSeconds = 1.1f;

        private readonly Dictionary<int, float> _nextServeElapsedTimes = new Dictionary<int, float>();

        public void Reset()
        {
            _nextServeElapsedTimes.Clear();
        }

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
            bool shouldServe = CanRequestServe(player, runtime);
            if (shouldServe)
            {
                _nextServeElapsedTimes[player.PlayerId] = ResolveMatchElapsedSeconds(runtime) + MinimumServeIntervalSeconds;
            }

            return new PlayerInputFrame(player.PlayerId, moveAxis, shouldServe, player.Paddle.Normal);
        }

        private bool CanRequestServe(PlayerRuntimeState player, GatebreakerMatchRuntime runtime)
        {
            if (runtime == null ||
                runtime.EffectiveRule == null ||
                player.ServeResource == null ||
                player.IsDisabled ||
                player.ServeResource.CurrentServeAmmo <= 0 ||
                player.ServeResource.OwnedBallsInField >= player.ServeResource.MaxOwnedBallsInField ||
                CountActiveBalls(runtime) >= runtime.EffectiveRule.MaxBallsInMatch)
            {
                return false;
            }

            float elapsed = ResolveMatchElapsedSeconds(runtime);
            return !_nextServeElapsedTimes.TryGetValue(player.PlayerId, out float nextServeElapsed) ||
                   elapsed >= nextServeElapsed;
        }

        private static float ResolveMatchElapsedSeconds(GatebreakerMatchRuntime runtime)
        {
            return runtime?.EffectiveRule?.Mode != null
                ? Mathf.Max(0f, runtime.EffectiveRule.Mode.CountdownSeconds - runtime.RemainingTime)
                : 0f;
        }

        private static int CountActiveBalls(GatebreakerMatchRuntime runtime)
        {
            int count = 0;
            for (int i = 0; i < runtime.Balls.Count; i++)
            {
                BallRuntimeState ball = runtime.Balls[i];
                if (ball != null && (ball.BallState == BallState.Flying || ball.BallState == BallState.GoalRebound))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
