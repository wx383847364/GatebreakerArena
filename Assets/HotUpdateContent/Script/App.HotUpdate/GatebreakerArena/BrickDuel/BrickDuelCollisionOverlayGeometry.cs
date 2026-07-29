using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public enum BrickDuelCollisionOverlayLineKind
    {
        Wall,
        Paddle,
        Brick,
    }

    public readonly struct BrickDuelCollisionOverlayLine
    {
        public BrickDuelCollisionOverlayLine(
            BrickDuelCollisionOverlayLineKind kind,
            Vector2 start,
            Vector2 end)
        {
            Kind = kind;
            Start = start;
            End = end;
        }

        public BrickDuelCollisionOverlayLineKind Kind { get; }
        public Vector2 Start { get; }
        public Vector2 End { get; }
    }

    public static class BrickDuelCollisionOverlayGeometry
    {
        private const float SegmentEpsilon = 0.0001f;

        public static IReadOnlyList<BrickDuelCollisionOverlayLine> BuildLines(
            BrickDuelRuleDefinition rule,
            BrickDuelSnapshot snapshot)
        {
            if (rule == null || snapshot == null)
            {
                return new List<BrickDuelCollisionOverlayLine>();
            }

            int brickCount = snapshot.Bricks != null ? snapshot.Bricks.Count : 0;
            var lines = new List<BrickDuelCollisionOverlayLine>(10 + brickCount * 4);
            float minimumX = -rule.ArenaHalfWidth + rule.BallRadius;
            float maximumX = rule.ArenaHalfWidth - rule.BallRadius;
            AddRectangle(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(minimumX, -rule.CoreLineY + rule.BallRadius),
                new Vector2(maximumX, -rule.BallRadius));
            AddRectangle(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(minimumX, rule.BallRadius),
                new Vector2(maximumX, rule.CoreLineY - rule.BallRadius));

            float paddleContactHalfWidth = rule.PaddleHalfWidth + rule.BallRadius;
            float paddleContactDistance = rule.PaddleHalfHeight + rule.BallRadius;
            AddPaddleFace(
                lines,
                snapshot.BottomPaddle?.Position,
                Vector2.up,
                paddleContactHalfWidth,
                paddleContactDistance);
            AddPaddleFace(
                lines,
                snapshot.TopPaddle?.Position,
                Vector2.down,
                paddleContactHalfWidth,
                paddleContactDistance);

            Vector2 brickExtents = new Vector2(
                rule.BrickWidth * 0.5f + rule.BallRadius,
                rule.BrickHeight * 0.5f + rule.BallRadius);
            if (snapshot.Bricks != null)
            {
                for (int i = 0; i < snapshot.Bricks.Count; i++)
                {
                    BrickDuelBrickState brick = snapshot.Bricks[i];
                    if (brick == null || brick.Health <= 0)
                    {
                        continue;
                    }

                    AddAabb(
                        lines,
                        BrickDuelCollisionOverlayLineKind.Brick,
                        brick.Position,
                        brickExtents);
                }
            }

            return lines;
        }

        private static void AddPaddleFace(
            List<BrickDuelCollisionOverlayLine> lines,
            Vector2? center,
            Vector2 normal,
            float contactHalfWidth,
            float contactDistance)
        {
            if (!center.HasValue)
            {
                return;
            }

            Vector2 contactCenter = center.Value + normal * contactDistance;
            AddLine(
                lines,
                BrickDuelCollisionOverlayLineKind.Paddle,
                contactCenter + Vector2.left * contactHalfWidth,
                contactCenter + Vector2.right * contactHalfWidth);
        }

        private static void AddAabb(
            List<BrickDuelCollisionOverlayLine> lines,
            BrickDuelCollisionOverlayLineKind kind,
            Vector2? center,
            Vector2 extents)
        {
            if (!center.HasValue)
            {
                return;
            }

            AddRectangle(lines, kind, center.Value - extents, center.Value + extents);
        }

        private static void AddRectangle(
            List<BrickDuelCollisionOverlayLine> lines,
            BrickDuelCollisionOverlayLineKind kind,
            Vector2 minimum,
            Vector2 maximum)
        {
            AddLine(lines, kind, new Vector2(minimum.x, minimum.y), new Vector2(maximum.x, minimum.y));
            AddLine(lines, kind, new Vector2(maximum.x, minimum.y), new Vector2(maximum.x, maximum.y));
            AddLine(lines, kind, new Vector2(maximum.x, maximum.y), new Vector2(minimum.x, maximum.y));
            AddLine(lines, kind, new Vector2(minimum.x, maximum.y), new Vector2(minimum.x, minimum.y));
        }

        private static void AddLine(
            List<BrickDuelCollisionOverlayLine> lines,
            BrickDuelCollisionOverlayLineKind kind,
            Vector2 start,
            Vector2 end)
        {
            if ((end - start).sqrMagnitude <= SegmentEpsilon * SegmentEpsilon)
            {
                return;
            }

            lines.Add(new BrickDuelCollisionOverlayLine(kind, start, end));
        }
    }
}
