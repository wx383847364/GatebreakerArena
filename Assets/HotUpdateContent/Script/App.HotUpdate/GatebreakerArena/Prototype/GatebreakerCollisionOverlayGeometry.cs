using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Paddle;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Prototype
{
    public enum GatebreakerCollisionOverlayLineKind
    {
        Wall,
        GoalTrigger,
        GoalBand,
        PaddleContact,
    }

    public readonly struct GatebreakerCollisionOverlayLine
    {
        public GatebreakerCollisionOverlayLine(
            GatebreakerCollisionOverlayLineKind kind,
            Vector2 start,
            Vector2 end,
            int goalPlayerIndex)
        {
            Kind = kind;
            Start = start;
            End = end;
            GoalPlayerIndex = goalPlayerIndex;
        }

        public GatebreakerCollisionOverlayLineKind Kind { get; }
        public Vector2 Start { get; }
        public Vector2 End { get; }
        public int GoalPlayerIndex { get; }
    }

    public static class GatebreakerCollisionOverlayGeometry
    {
        private const float SegmentEpsilon = 0.001f;

        public static IReadOnlyList<GatebreakerCollisionOverlayLine> BuildLines(
            ArenaGeometry arena,
            int activePlayerCount)
        {
            return BuildArenaLines(arena, activePlayerCount, true);
        }

        public static IReadOnlyList<GatebreakerCollisionOverlayLine> BuildLines(
            ArenaGeometry arena,
            IReadOnlyList<PlayerRuntimeState> players)
        {
            int activePlayerCount = players != null ? players.Count : 0;
            List<GatebreakerCollisionOverlayLine> lines = BuildArenaLines(arena, activePlayerCount, false);
            AddRuntimePaddleContactLines(players, lines);
            return lines;
        }

        private static List<GatebreakerCollisionOverlayLine> BuildArenaLines(
            ArenaGeometry arena,
            int activePlayerCount,
            bool includeStaticPaddleContact)
        {
            if (arena == null || !arena.HasCustomBoundary)
            {
                return new List<GatebreakerCollisionOverlayLine>();
            }

            var lines = new List<GatebreakerCollisionOverlayLine>(arena.BoundarySegments.Count * 5);
            for (int i = 0; i < arena.BoundarySegments.Count; i++)
            {
                ArenaBoundarySegment segment = arena.BoundarySegments[i];
                if (IsActiveGoalSegment(segment, activePlayerCount))
                {
                    AddActiveGoalLines(arena, segment, lines, includeStaticPaddleContact);
                }
                else
                {
                    AddLine(lines, GatebreakerCollisionOverlayLineKind.Wall, segment.Start, segment.End, -1);
                }
            }

            return lines;
        }

        private static bool IsActiveGoalSegment(ArenaBoundarySegment segment, int activePlayerCount)
        {
            return segment != null &&
                   segment.GoalPlayerIndex >= 0 &&
                   segment.GoalPlayerIndex < activePlayerCount &&
                   segment.GoalHalfLength > SegmentEpsilon;
        }

        private static void AddActiveGoalLines(
            ArenaGeometry arena,
            ArenaBoundarySegment segment,
            List<GatebreakerCollisionOverlayLine> lines,
            bool includeStaticPaddleContact)
        {
            Vector2 edge = segment.End - segment.Start;
            float edgeLength = edge.magnitude;
            if (edgeLength <= SegmentEpsilon)
            {
                return;
            }

            Vector2 tangent = edge / edgeLength;
            float goalCenterDistance = Vector2.Dot(segment.GoalCenter - segment.Start, tangent);
            float goalStartDistance = Mathf.Clamp(goalCenterDistance - segment.GoalHalfLength, 0f, edgeLength);
            float goalEndDistance = Mathf.Clamp(goalCenterDistance + segment.GoalHalfLength, 0f, edgeLength);

            AddWallSpan(lines, segment, tangent, 0f, goalStartDistance);
            AddWallSpan(lines, segment, tangent, goalEndDistance, edgeLength);

            Vector2 goalOuterStart = segment.Start + tangent * goalStartDistance;
            Vector2 goalOuterEnd = segment.Start + tangent * goalEndDistance;
            Vector2 goalTriggerStart = goalOuterStart + segment.InwardNormal * segment.GoalTriggerInset;
            Vector2 goalTriggerEnd = goalOuterEnd + segment.InwardNormal * segment.GoalTriggerInset;
            AddLine(lines, GatebreakerCollisionOverlayLineKind.GoalTrigger, goalTriggerStart, goalTriggerEnd, segment.GoalPlayerIndex);
            AddLine(lines, GatebreakerCollisionOverlayLineKind.GoalBand, goalOuterStart, goalOuterEnd, segment.GoalPlayerIndex);
            AddLine(lines, GatebreakerCollisionOverlayLineKind.GoalBand, goalOuterStart, goalTriggerStart, segment.GoalPlayerIndex);
            AddLine(lines, GatebreakerCollisionOverlayLineKind.GoalBand, goalOuterEnd, goalTriggerEnd, segment.GoalPlayerIndex);
            if (!includeStaticPaddleContact)
            {
                return;
            }

            Vector2 paddleCenter = segment.GoalCenter + segment.InwardNormal * arena.PaddleInset;
            Vector2 paddleContactCenter = paddleCenter + segment.InwardNormal * arena.PaddleThickness;
            AddLine(
                lines,
                GatebreakerCollisionOverlayLineKind.PaddleContact,
                paddleContactCenter - tangent * (arena.PaddleLength * 0.5f),
                paddleContactCenter + tangent * (arena.PaddleLength * 0.5f),
                segment.GoalPlayerIndex);
        }

        private static void AddRuntimePaddleContactLines(
            IReadOnlyList<PlayerRuntimeState> players,
            List<GatebreakerCollisionOverlayLine> lines)
        {
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                PlayerRuntimeState player = players[i];
                PaddleRuntimeState paddle = player?.Paddle;
                if (player == null || player.IsDisabled || paddle == null)
                {
                    continue;
                }

                Vector2 tangent = paddle.Tangent.sqrMagnitude > SegmentEpsilon * SegmentEpsilon
                    ? paddle.Tangent.normalized
                    : Vector2.right;
                Vector2 contactCenter = paddle.Position + paddle.Normal * paddle.Thickness;
                AddLine(
                    lines,
                    GatebreakerCollisionOverlayLineKind.PaddleContact,
                    contactCenter - tangent * (paddle.Length * 0.5f),
                    contactCenter + tangent * (paddle.Length * 0.5f),
                    i);
            }
        }

        private static void AddWallSpan(
            List<GatebreakerCollisionOverlayLine> lines,
            ArenaBoundarySegment segment,
            Vector2 tangent,
            float startDistance,
            float endDistance)
        {
            if (endDistance - startDistance <= SegmentEpsilon)
            {
                return;
            }

            Vector2 start = segment.Start + tangent * startDistance;
            Vector2 end = segment.Start + tangent * endDistance;
            AddLine(lines, GatebreakerCollisionOverlayLineKind.Wall, start, end, -1);
        }

        private static void AddLine(
            List<GatebreakerCollisionOverlayLine> lines,
            GatebreakerCollisionOverlayLineKind kind,
            Vector2 start,
            Vector2 end,
            int goalPlayerIndex)
        {
            if ((end - start).sqrMagnitude <= SegmentEpsilon * SegmentEpsilon)
            {
                return;
            }

            lines.Add(new GatebreakerCollisionOverlayLine(kind, start, end, goalPlayerIndex));
        }
    }
}
