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

    public readonly struct BrickDuelWallOverlayBounds
    {
        public BrickDuelWallOverlayBounds(
            float minimumX,
            float maximumX,
            float minimumY,
            float maximumY)
        {
            MinimumX = minimumX;
            MaximumX = maximumX;
            MinimumY = minimumY;
            MaximumY = maximumY;
        }

        public float MinimumX { get; }
        public float MaximumX { get; }
        public float MinimumY { get; }
        public float MaximumY { get; }

        public bool IsValid =>
            MaximumX > MinimumX + 0.0001f &&
            MaximumY > MinimumY + 0.0001f;
    }

    public static class BrickDuelCollisionOverlayGeometry
    {
        private const float SegmentEpsilon = 0.0001f;
        private const string BottomWallName = "Position01";
        private const string RightWallName = "Position02";
        private const string LeftWallName = "Position03";
        private const string TopWallName = "Position04";

        public static IReadOnlyList<BrickDuelCollisionOverlayLine> BuildLines(
            BrickDuelRuleDefinition rule,
            BrickDuelSnapshot snapshot,
            BrickDuelWallOverlayBounds? wallBounds = null)
        {
            if (rule == null || snapshot == null)
            {
                return new List<BrickDuelCollisionOverlayLine>();
            }

            int brickCount = snapshot.Bricks != null ? snapshot.Bricks.Count : 0;
            var lines = new List<BrickDuelCollisionOverlayLine>(16 + brickCount * 4);
            float minimumX = wallBounds.HasValue ? wallBounds.Value.MinimumX : -rule.ArenaHalfWidth;
            float maximumX = wallBounds.HasValue ? wallBounds.Value.MaximumX : rule.ArenaHalfWidth;
            float minimumY = wallBounds.HasValue ? wallBounds.Value.MinimumY : -rule.CoreLineY;
            float maximumY = wallBounds.HasValue ? wallBounds.Value.MaximumY : rule.CoreLineY;
            AddRectangle(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(minimumX, minimumY),
                new Vector2(maximumX, 0f));
            AddRectangle(
                lines,
                BrickDuelCollisionOverlayLineKind.Wall,
                new Vector2(minimumX, 0f),
                new Vector2(maximumX, maximumY));

            Vector2 bottomPaddleExtents = new Vector2(
                snapshot.BottomPaddleHalfWidth > 0.0001f
                    ? snapshot.BottomPaddleHalfWidth
                    : rule.PaddleHalfWidth,
                rule.PaddleHalfHeight);
            Vector2 topPaddleExtents = new Vector2(
                snapshot.TopPaddleHalfWidth > 0.0001f
                    ? snapshot.TopPaddleHalfWidth
                    : rule.PaddleHalfWidth,
                rule.PaddleHalfHeight);
            AddAabb(
                lines,
                BrickDuelCollisionOverlayLineKind.Paddle,
                snapshot.BottomPaddle?.Position,
                bottomPaddleExtents);
            AddAabb(
                lines,
                BrickDuelCollisionOverlayLineKind.Paddle,
                snapshot.TopPaddle?.Position,
                topPaddleExtents);

            Vector2 brickExtents = new Vector2(
                rule.BrickWidth * 0.5f,
                rule.BrickHeight * 0.5f);
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

        public static bool TryResolveWallInnerBounds(
            Transform sceneRoot,
            out BrickDuelWallOverlayBounds bounds)
        {
            bounds = default;
            if (sceneRoot == null)
            {
                return false;
            }

            if (!TryGetWallRendererBounds(sceneRoot, LeftWallName, out Bounds left) ||
                !TryGetWallRendererBounds(sceneRoot, RightWallName, out Bounds right) ||
                !TryGetWallRendererBounds(sceneRoot, BottomWallName, out Bounds bottom) ||
                !TryGetWallRendererBounds(sceneRoot, TopWallName, out Bounds top))
            {
                return false;
            }

            // Position01~04 内侧：左墙 max.x、右墙 min.x、底墙 max.y、顶墙 min.y
            float halfWidth = Mathf.Min(-left.max.x, right.min.x);
            float halfHeight = Mathf.Min(-bottom.max.y, top.min.y);
            bounds = new BrickDuelWallOverlayBounds(
                -halfWidth,
                halfWidth,
                -halfHeight,
                halfHeight);
            return bounds.IsValid;
        }

        public static bool TryApplyWallInnerBoundsToRule(
            BrickDuelRuleDefinition rule,
            BrickDuelWallOverlayBounds bounds)
        {
            if (rule == null || !bounds.IsValid)
            {
                return false;
            }

            float halfWidth = Mathf.Min(-bounds.MinimumX, bounds.MaximumX);
            float halfHeight = Mathf.Min(-bounds.MinimumY, bounds.MaximumY);
            if (halfWidth <= rule.BallRadius || halfHeight <= rule.BallRadius)
            {
                return false;
            }

            rule.ArenaHalfWidth = halfWidth;
            rule.CoreLineY = halfHeight;
            if (rule.PaddleSpawnY >= rule.CoreLineY)
            {
                rule.PaddleSpawnY = Mathf.Max(
                    rule.PaddleHalfHeight + rule.BallRadius + 0.02f,
                    rule.CoreLineY - 0.2f);
            }

            return true;
        }

        private static bool TryGetWallRendererBounds(
            Transform sceneRoot,
            string wallName,
            out Bounds bounds)
        {
            bounds = default;
            Transform wall = sceneRoot.Find(wallName);
            if (wall == null)
            {
                return false;
            }

            SpriteRenderer renderer = wall.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || !renderer.enabled)
            {
                return false;
            }

            bounds = renderer.bounds;
            return true;
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
