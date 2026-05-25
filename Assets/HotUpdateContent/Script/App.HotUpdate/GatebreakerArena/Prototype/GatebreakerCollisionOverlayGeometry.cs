using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Match;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Prototype
{
    public enum GatebreakerCollisionOverlayLineKind
    {
        Wall,
        GoalTrigger,
        GoalBand,
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
            if (arena == null || !arena.HasCustomBoundary)
            {
                return Array.Empty<GatebreakerCollisionOverlayLine>();
            }

            var lines = new List<GatebreakerCollisionOverlayLine>(arena.BoundarySegments.Count * 4);
            for (int i = 0; i < arena.BoundarySegments.Count; i++)
            {
                ArenaBoundarySegment segment = arena.BoundarySegments[i];
                if (IsActiveGoalSegment(segment, activePlayerCount))
                {
                    AddActiveGoalLines(segment, lines);
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
            ArenaBoundarySegment segment,
            List<GatebreakerCollisionOverlayLine> lines)
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
