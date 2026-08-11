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
            float paddleHalfWidth,
            float ballRadius,
            ref int pierceCharges,
            ISet<int> ignoredBrickIds,
            ISet<int> hitBrickIds,
            float targetBallSpeed = -1f)
        {
            if (ball == null || !ball.IsActive || deltaTime <= 0f)
            {
                return;
            }

            float resolvedBallSpeed = targetBallSpeed > 0f ? targetBallSpeed : rule.BallSpeed;
            if (ball.Velocity.sqrMagnitude > 0.0001f)
            {
                ball.Velocity = ball.Velocity.normalized * resolvedBallSpeed;
            }

            RecoverBallInsideArena(ball, rule, ballRadius, resolvedBallSpeed);
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
                    tideSpeed,
                    paddleHalfWidth,
                    ballRadius,
                    ignoredBrickIds);
                if (!candidate.Hit)
                {
                    ball.Position += ball.Velocity * remaining;
                    break;
                }

                ball.Position += ball.Velocity * candidate.Time;
                remaining -= candidate.Time;
                elapsed += candidate.Time;

                bool pierceBrick = false;
                if (candidate.Brick != null)
                {
                    hitBrickIds.Add(candidate.Brick.BrickId);
                    ignoredBrickIds?.Add(candidate.Brick.BrickId);
                    if (pierceCharges > 0)
                    {
                        pierceBrick = true;
                        pierceCharges--;
                    }
                }

                if (pierceBrick)
                {
                    // Keep travel direction; only separate enough to leave the contact surface.
                    ball.Position += candidate.Normal * SeparationEpsilon;
                    float separationTime =
                        SeparationEpsilon / Mathf.Max(ball.Velocity.magnitude, 0.001f);
                    remaining = Mathf.Max(0f, remaining - separationTime);
                    elapsed = Mathf.Min(deltaTime, elapsed + separationTime);
                    continue;
                }

                Vector2 relativeVelocity = ball.Velocity - candidate.ColliderVelocity;
                Vector2 reflected =
                    Vector2.Reflect(relativeVelocity, candidate.Normal) +
                    candidate.ColliderVelocity;
                if (candidate.IsPaddle)
                {
                    float hitOffset = (ball.Position.x - candidate.ColliderCenter.x) /
                                      Mathf.Max(0.001f, paddleHalfWidth);
                    float tangentShare = Mathf.Clamp(hitOffset, -1f, 1f) * 0.72f +
                                         paddle.MoveAxis * 0.18f;
                    float outwardY = ball.Side == BrickDuelSide.Bottom ? 1f : -1f;
                    float verticalShare = Mathf.Sqrt(Mathf.Max(0.16f, 1f - tangentShare * tangentShare));
                    reflected = new Vector2(tangentShare, outwardY * verticalShare);
                }

                Vector2 direction = reflected.sqrMagnitude > 0.0001f
                    ? reflected.normalized
                    : candidate.Normal;
                ball.Velocity = direction * resolvedBallSpeed;
                ball.Position += candidate.Normal *
                                 (candidate.SeparationDistance + SeparationEpsilon);
                float bounceSeparationTime =
                    SeparationEpsilon / Mathf.Max(relativeVelocity.magnitude, 0.001f);
                remaining = Mathf.Max(0f, remaining - bounceSeparationTime);
                elapsed = Mathf.Min(deltaTime, elapsed + bounceSeparationTime);
            }

            RecoverBallInsideArena(ball, rule, ballRadius, resolvedBallSpeed);
        }

        public static void RefreshIgnoredBrickContacts(
            BrickDuelBallState ball,
            IList<BrickDuelBrickState> bricks,
            BrickDuelRuleDefinition rule,
            float ballRadius,
            ISet<int> ignoredBrickIds)
        {
            if (ball == null || ignoredBrickIds == null || ignoredBrickIds.Count == 0)
            {
                return;
            }

            Vector2 extents = new Vector2(
                rule.BrickWidth * 0.5f + ballRadius,
                rule.BrickHeight * 0.5f + ballRadius);
            var stale = new List<int>();
            foreach (int brickId in ignoredBrickIds)
            {
                BrickDuelBrickState brick = null;
                for (int i = 0; i < bricks.Count; i++)
                {
                    if (bricks[i].BrickId == brickId)
                    {
                        brick = bricks[i];
                        break;
                    }
                }

                if (brick == null || brick.Side != ball.Side || brick.Health <= 0)
                {
                    stale.Add(brickId);
                    continue;
                }

                Vector2 delta = ball.Position - brick.Position;
                if (Mathf.Abs(delta.x) > extents.x || Mathf.Abs(delta.y) > extents.y)
                {
                    stale.Add(brickId);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                ignoredBrickIds.Remove(stale[i]);
            }
        }

        public static void SeparateBallFromBricksAndWalls(
            BrickDuelBallState ball,
            IList<BrickDuelBrickState> bricks,
            BrickDuelRuleDefinition rule,
            float ballRadius,
            float targetBallSpeed = -1f)
        {
            if (ball == null)
            {
                return;
            }

            float resolvedBallSpeed = targetBallSpeed > 0f ? targetBallSpeed : rule.BallSpeed;
            RecoverBallInsideArena(ball, rule, ballRadius, resolvedBallSpeed);
            Vector2 extents = new Vector2(
                rule.BrickWidth * 0.5f + ballRadius,
                rule.BrickHeight * 0.5f + ballRadius);
            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;
                for (int i = 0; i < bricks.Count; i++)
                {
                    BrickDuelBrickState brick = bricks[i];
                    if (brick.Side != ball.Side || brick.Health <= 0)
                    {
                        continue;
                    }

                    Vector2 delta = ball.Position - brick.Position;
                    float overlapX = extents.x - Mathf.Abs(delta.x);
                    float overlapY = extents.y - Mathf.Abs(delta.y);
                    if (overlapX <= 0f || overlapY <= 0f)
                    {
                        continue;
                    }

                    if (overlapX < overlapY)
                    {
                        float sign = delta.x >= 0f ? 1f : -1f;
                        ball.Position += new Vector2(sign * (overlapX + SeparationEpsilon), 0f);
                    }
                    else
                    {
                        float sign = delta.y >= 0f ? 1f : -1f;
                        ball.Position += new Vector2(0f, sign * (overlapY + SeparationEpsilon));
                    }

                    moved = true;
                }

                RecoverBallInsideArena(ball, rule, ballRadius, resolvedBallSpeed);
                if (!moved)
                {
                    break;
                }
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
            float tideSpeed,
            float paddleHalfWidth,
            float ballRadius,
            ISet<int> ignoredBrickIds)
        {
            CollisionCandidate best = CollisionCandidate.None(maxTime);
            Vector2 velocity = ball.Velocity;
            Vector2 position = ball.Position;

            TryPlane(
                position,
                velocity,
                maxTime,
                -rule.ArenaHalfWidth + ballRadius,
                true,
                Vector2.right,
                ref best);
            TryPlane(
                position,
                velocity,
                maxTime,
                rule.ArenaHalfWidth - ballRadius,
                true,
                Vector2.left,
                ref best);

            float centerBoundary = ball.Side == BrickDuelSide.Bottom
                ? -ballRadius
                : ballRadius;
            TryPlane(
                position,
                velocity,
                maxTime,
                centerBoundary,
                false,
                ball.Side == BrickDuelSide.Bottom ? Vector2.down : Vector2.up,
                ref best);

            float outerBoundary = ball.Side == BrickDuelSide.Bottom
                ? -rule.CoreLineY + ballRadius
                : rule.CoreLineY - ballRadius;
            TryPlane(
                position,
                velocity,
                maxTime,
                outerBoundary,
                false,
                ball.Side == BrickDuelSide.Bottom ? Vector2.up : Vector2.down,
                ref best);

            Vector2 paddleCenter = paddleStartPosition + paddleVelocity * elapsed;
            TryPaddleFace(
                position,
                velocity,
                maxTime,
                paddleCenter,
                paddleVelocity,
                ball.Side,
                paddleHalfWidth + ballRadius,
                rule.PaddleHalfHeight + ballRadius,
                ref best);

            Vector2 brickExtents = new Vector2(
                rule.BrickWidth * 0.5f + ballRadius,
                rule.BrickHeight * 0.5f + ballRadius);
            for (int i = 0; i < bricks.Count; i++)
            {
                BrickDuelBrickState brick = bricks[i];
                if (brick.Side != ball.Side || brick.Health <= 0)
                {
                    continue;
                }

                if (ignoredBrickIds != null && ignoredBrickIds.Contains(brick.BrickId))
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

        private static void TryPaddleFace(
            Vector2 origin,
            Vector2 velocity,
            float maxTime,
            Vector2 paddleCenter,
            Vector2 paddleVelocity,
            BrickDuelSide side,
            float contactHalfWidth,
            float contactDistance,
            ref CollisionCandidate best)
        {
            Vector2 normal = side == BrickDuelSide.Bottom
                ? Vector2.up
                : Vector2.down;
            Vector2 relativeVelocity = velocity - paddleVelocity;
            float normalVelocity = Vector2.Dot(relativeVelocity, normal);
            if (normalVelocity >= -0.000001f)
            {
                return;
            }

            float normalDistance = Vector2.Dot(origin - paddleCenter, normal);
            if (normalDistance < 0f)
            {
                return;
            }

            float hitTime = 0f;
            float separationDistance = 0f;
            if (normalDistance > contactDistance)
            {
                hitTime = (contactDistance - normalDistance) / normalVelocity;
                if (hitTime < 0f || hitTime > maxTime)
                {
                    return;
                }
            }
            else
            {
                separationDistance = contactDistance - normalDistance;
            }

            if (hitTime >= best.Time)
            {
                return;
            }

            Vector2 paddleAtHit = paddleCenter + paddleVelocity * hitTime;
            Vector2 ballAtHit = origin + velocity * hitTime;
            if (Mathf.Abs(ballAtHit.x - paddleAtHit.x) > contactHalfWidth)
            {
                return;
            }

            best = new CollisionCandidate(
                true,
                hitTime,
                normal,
                null,
                true,
                paddleVelocity,
                paddleAtHit,
                separationDistance);
        }

        private static void RecoverBallInsideArena(
            BrickDuelBallState ball,
            BrickDuelRuleDefinition rule,
            float ballRadius,
            float ballSpeed)
        {
            float minimumX = -rule.ArenaHalfWidth + ballRadius;
            float maximumX = rule.ArenaHalfWidth - ballRadius;
            float minimumY = ball.Side == BrickDuelSide.Bottom
                ? -rule.CoreLineY + ballRadius
                : ballRadius;
            float maximumY = ball.Side == BrickDuelSide.Bottom
                ? -ballRadius
                : rule.CoreLineY - ballRadius;
            Vector2 position = ball.Position;
            Vector2 velocity = ball.Velocity;
            bool corrected = false;

            if (position.x < minimumX)
            {
                position.x = minimumX;
                if (velocity.x < 0f)
                {
                    velocity.x = -velocity.x;
                }
                corrected = true;
            }
            else if (position.x > maximumX)
            {
                position.x = maximumX;
                if (velocity.x > 0f)
                {
                    velocity.x = -velocity.x;
                }
                corrected = true;
            }

            if (position.y < minimumY)
            {
                position.y = minimumY;
                if (velocity.y < 0f)
                {
                    velocity.y = -velocity.y;
                }
                corrected = true;
            }
            else if (position.y > maximumY)
            {
                position.y = maximumY;
                if (velocity.y > 0f)
                {
                    velocity.y = -velocity.y;
                }
                corrected = true;
            }

            if (!corrected)
            {
                return;
            }

            ball.Position = position;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                ball.Velocity = velocity.normalized * ballSpeed;
            }
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
                Vector2 colliderCenter,
                float separationDistance = 0f)
            {
                Hit = hit;
                Time = time;
                Normal = normal;
                Brick = brick;
                IsPaddle = isPaddle;
                ColliderVelocity = colliderVelocity;
                ColliderCenter = colliderCenter;
                SeparationDistance = separationDistance;
            }

            public bool Hit { get; }
            public float Time { get; }
            public Vector2 Normal { get; }
            public BrickDuelBrickState Brick { get; }
            public bool IsPaddle { get; }
            public Vector2 ColliderVelocity { get; }
            public Vector2 ColliderCenter { get; }
            public float SeparationDistance { get; }

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
