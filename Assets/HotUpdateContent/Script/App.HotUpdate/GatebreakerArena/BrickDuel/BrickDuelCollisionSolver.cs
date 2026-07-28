using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public sealed class BrickDuelCollisionSolver
    {
        private const float SeparationEpsilon = 0.0005f;
        private const int MaxImpactsPerFrame = 6;

        public void StepBall(
            BrickDuelBallState ball,
            BrickDuelPaddleState paddle,
            Vector2 paddleStartPosition,
            Vector2 paddleVelocity,
            IList<BrickDuelBrickState> bricks,
            BrickDuelRuleDefinition rule,
            float deltaTime,
            float tideSpeed,
            ISet<int> hitBrickIds)
        {
            if (ball == null || !ball.IsActive || deltaTime <= 0f)
            {
                return;
            }

            float remaining = deltaTime;
            float elapsed = 0f;
            for (int impactIndex = 0; impactIndex < MaxImpactsPerFrame && remaining > 0.000001f; impactIndex++)
            {
                CollisionCandidate candidate = FindEarliestCollision(
                    ball,
                    paddleStartPosition,
                    paddleVelocity,
                    bricks,
                    rule,
                    elapsed,
                    remaining,
                    tideSpeed);
                if (!candidate.Hit)
                {
                    ball.Position += ball.Velocity * remaining;
                    break;
                }

                ball.Position += ball.Velocity * candidate.Time;
                remaining -= candidate.Time;
                elapsed += candidate.Time;

                if (candidate.Brick != null)
                {
                    hitBrickIds.Add(candidate.Brick.BrickId);
                }

                Vector2 relativeVelocity = ball.Velocity - candidate.ColliderVelocity;
                Vector2 reflected =
                    Vector2.Reflect(relativeVelocity, candidate.Normal) +
                    candidate.ColliderVelocity;
                if (candidate.IsPaddle)
                {
                    float hitOffset = (ball.Position.x - candidate.ColliderCenter.x) /
                                      Mathf.Max(0.001f, rule.PaddleHalfWidth);
                    float tangentShare = Mathf.Clamp(hitOffset, -1f, 1f) * 0.72f +
                                         paddle.MoveAxis * 0.18f;
                    float outwardY = ball.Side == BrickDuelSide.Bottom ? 1f : -1f;
                    float verticalShare = Mathf.Sqrt(Mathf.Max(0.16f, 1f - tangentShare * tangentShare));
                    reflected = new Vector2(tangentShare, outwardY * verticalShare);
                }

                Vector2 direction = reflected.sqrMagnitude > 0.0001f
                    ? reflected.normalized
                    : candidate.Normal;
                ball.Velocity = direction * rule.BallSpeed;
                ball.Position += candidate.Normal * SeparationEpsilon;
                float separationTime =
                    SeparationEpsilon / Mathf.Max(relativeVelocity.magnitude, 0.001f);
                remaining = Mathf.Max(0f, remaining - separationTime);
                elapsed = Mathf.Min(deltaTime, elapsed + separationTime);
            }
        }

        private static CollisionCandidate FindEarliestCollision(
            BrickDuelBallState ball,
            Vector2 paddleStartPosition,
            Vector2 paddleVelocity,
            IList<BrickDuelBrickState> bricks,
            BrickDuelRuleDefinition rule,
            float elapsed,
            float maxTime,
            float tideSpeed)
        {
            CollisionCandidate best = CollisionCandidate.None(maxTime);
            Vector2 velocity = ball.Velocity;
            Vector2 position = ball.Position;

            TryPlane(
                position,
                velocity,
                maxTime,
                -rule.ArenaHalfWidth + rule.BallRadius,
                true,
                Vector2.right,
                ref best);
            TryPlane(
                position,
                velocity,
                maxTime,
                rule.ArenaHalfWidth - rule.BallRadius,
                true,
                Vector2.left,
                ref best);

            float centerBoundary = ball.Side == BrickDuelSide.Bottom
                ? -rule.BallRadius
                : rule.BallRadius;
            TryPlane(
                position,
                velocity,
                maxTime,
                centerBoundary,
                false,
                ball.Side == BrickDuelSide.Bottom ? Vector2.down : Vector2.up,
                ref best);

            Vector2 paddleCenter = paddleStartPosition + paddleVelocity * elapsed;
            Vector2 paddleExtents = new Vector2(
                rule.PaddleHalfWidth + rule.BallRadius,
                rule.PaddleHalfHeight + rule.BallRadius);
            TryAabb(
                position,
                velocity - paddleVelocity,
                maxTime,
                paddleCenter,
                paddleExtents,
                null,
                true,
                paddleVelocity,
                ref best);

            Vector2 brickExtents = new Vector2(
                rule.BrickWidth * 0.5f + rule.BallRadius,
                rule.BrickHeight * 0.5f + rule.BallRadius);
            for (int i = 0; i < bricks.Count; i++)
            {
                BrickDuelBrickState brick = bricks[i];
                if (brick.Side != ball.Side || brick.Health <= 0)
                {
                    continue;
                }

                float direction = brick.Side == BrickDuelSide.Bottom ? -1f : 1f;
                Vector2 brickVelocity = new Vector2(0f, direction * tideSpeed);
                Vector2 brickCenter = brick.Position + brickVelocity * elapsed;
                TryAabb(
                    position,
                    velocity - brickVelocity,
                    maxTime,
                    brickCenter,
                    brickExtents,
                    brick,
                    false,
                    brickVelocity,
                    ref best);
            }

            return best;
        }

        private static void TryPlane(
            Vector2 position,
            Vector2 velocity,
            float maxTime,
            float plane,
            bool vertical,
            Vector2 normal,
            ref CollisionCandidate best)
        {
            float component = vertical ? velocity.x : velocity.y;
            if (Mathf.Abs(component) < 0.000001f)
            {
                return;
            }

            if (Vector2.Dot(velocity, normal) >= 0f)
            {
                return;
            }

            float origin = vertical ? position.x : position.y;
            float time = (plane - origin) / component;
            if (time < 0f || time > maxTime || time >= best.Time)
            {
                return;
            }

            best = new CollisionCandidate(
                true,
                time,
                normal,
                null,
                false,
                Vector2.zero,
                Vector2.zero);
        }

        private static void TryAabb(
            Vector2 origin,
            Vector2 velocity,
            float maxTime,
            Vector2 center,
            Vector2 extents,
            BrickDuelBrickState brick,
            bool isPaddle,
            Vector2 colliderVelocity,
            ref CollisionCandidate best)
        {
            Vector2 minimum = center - extents;
            Vector2 maximum = center + extents;
            float enterX;
            float exitX;
            float enterY;
            float exitY;

            if (!RaySlab(origin.x, velocity.x, minimum.x, maximum.x, out enterX, out exitX) ||
                !RaySlab(origin.y, velocity.y, minimum.y, maximum.y, out enterY, out exitY))
            {
                return;
            }

            float enter = Mathf.Max(enterX, enterY);
            float exit = Mathf.Min(exitX, exitY);
            if (exit < 0f || enter > exit || enter > maxTime)
            {
                return;
            }

            float hitTime = Mathf.Max(0f, enter);
            if (hitTime >= best.Time)
            {
                return;
            }

            Vector2 normal;
            if (enter < 0f)
            {
                normal = ResolveOverlapNormal(origin, center, extents, velocity);
            }
            else if (enterX > enterY)
            {
                normal = velocity.x > 0f ? Vector2.left : Vector2.right;
            }
            else
            {
                normal = velocity.y > 0f ? Vector2.down : Vector2.up;
            }

            if (Vector2.Dot(velocity, normal) >= 0f)
            {
                return;
            }

            best = new CollisionCandidate(
                true,
                hitTime,
                normal,
                brick,
                isPaddle,
                colliderVelocity,
                center + colliderVelocity * hitTime);
        }

        private static Vector2 ResolveOverlapNormal(
            Vector2 origin,
            Vector2 center,
            Vector2 extents,
            Vector2 relativeVelocity)
        {
            float distanceLeft = Mathf.Abs(origin.x - (center.x - extents.x));
            float distanceRight = Mathf.Abs((center.x + extents.x) - origin.x);
            float distanceBottom = Mathf.Abs(origin.y - (center.y - extents.y));
            float distanceTop = Mathf.Abs((center.y + extents.y) - origin.y);
            float minimum = Mathf.Min(
                Mathf.Min(distanceLeft, distanceRight),
                Mathf.Min(distanceBottom, distanceTop));
            if (minimum == distanceLeft)
            {
                return Vector2.left;
            }
            if (minimum == distanceRight)
            {
                return Vector2.right;
            }
            if (minimum == distanceBottom)
            {
                return Vector2.down;
            }
            if (minimum == distanceTop)
            {
                return Vector2.up;
            }

            return relativeVelocity.sqrMagnitude > 0.000001f
                ? -relativeVelocity.normalized
                : Vector2.up;
        }

        private static bool RaySlab(
            float origin,
            float velocity,
            float minimum,
            float maximum,
            out float enter,
            out float exit)
        {
            if (Mathf.Abs(velocity) < 0.000001f)
            {
                enter = float.NegativeInfinity;
                exit = float.PositiveInfinity;
                return origin >= minimum && origin <= maximum;
            }

            float inverse = 1f / velocity;
            float first = (minimum - origin) * inverse;
            float second = (maximum - origin) * inverse;
            enter = Mathf.Min(first, second);
            exit = Mathf.Max(first, second);
            return true;
        }

        private readonly struct CollisionCandidate
        {
            public CollisionCandidate(
                bool hit,
                float time,
                Vector2 normal,
                BrickDuelBrickState brick,
                bool isPaddle,
                Vector2 colliderVelocity,
                Vector2 colliderCenter)
            {
                Hit = hit;
                Time = time;
                Normal = normal;
                Brick = brick;
                IsPaddle = isPaddle;
                ColliderVelocity = colliderVelocity;
                ColliderCenter = colliderCenter;
            }

            public bool Hit { get; }
            public float Time { get; }
            public Vector2 Normal { get; }
            public BrickDuelBrickState Brick { get; }
            public bool IsPaddle { get; }
            public Vector2 ColliderVelocity { get; }
            public Vector2 ColliderCenter { get; }

            public static CollisionCandidate None(float maxTime)
            {
                return new CollisionCandidate(
                    false,
                    maxTime,
                    Vector2.zero,
                    null,
                    false,
                    Vector2.zero,
                    Vector2.zero);
            }
        }
    }
}
