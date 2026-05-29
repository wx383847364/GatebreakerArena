using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class ArenaGeometry
    {
        private const string Scene3v3PrefabName = "Scene3v3";

        public ArenaGeometry(
            float halfWidth,
            float halfHeight,
            float paddleInset,
            float paddleLength,
            float paddleThickness,
            float paddleSpeed,
            IReadOnlyList<ArenaBoundarySegment> boundarySegments = null)
        {
            HalfWidth = Math.Max(1f, halfWidth);
            HalfHeight = Math.Max(1f, halfHeight);
            PaddleInset = Mathf.Clamp(paddleInset, 0.05f, Math.Min(HalfWidth, HalfHeight) * 0.5f);
            PaddleLength = Math.Max(0.25f, paddleLength);
            PaddleThickness = Math.Max(0.05f, paddleThickness);
            PaddleSpeed = Math.Max(0f, paddleSpeed);
            BoundarySegments = boundarySegments != null
                ? new List<ArenaBoundarySegment>(boundarySegments)
                : Array.Empty<ArenaBoundarySegment>();
        }

        public float HalfWidth { get; }
        public float HalfHeight { get; }
        public float PaddleInset { get; }
        public float PaddleLength { get; }
        public float PaddleThickness { get; }
        public float PaddleSpeed { get; }
        public IReadOnlyList<ArenaBoundarySegment> BoundarySegments { get; }
        public bool HasCustomBoundary => BoundarySegments.Count > 0;

        public static ArenaGeometry CreateDefault()
        {
            return new ArenaGeometry(8f, 5f, 0.55f, 2.8f, 0.28f, 8f);
        }

        public static ArenaGeometry CreateForMap(
            MapRuleDefinition map,
            IReadOnlyList<int> activePlayerIds = null)
        {
            if (map != null &&
                !string.IsNullOrEmpty(map.ScenePrefabLocation) &&
                map.ScenePrefabLocation.IndexOf(Scene3v3PrefabName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CreateScene3v3(map, activePlayerIds);
            }

            return CreateDefault();
        }

        public static ArenaGeometry CreateScene3v3(
            MapRuleDefinition map = null,
            IReadOnlyList<int> activePlayerIds = null)
        {
            const float scene3v3GoalHalfLength = 1.06f;
            const float scene3v3GoalTriggerInset = 0.14f;
            const float scene3v3PaddleLength = 0.78f;
            const float scene3v3PaddleThickness = 0.05f;
            // Derived from Scene3v3 Position01..06 edge-center transforms and rotations.
            var points = new[]
            {
                new Vector2(1.379f, -2.456f),
                new Vector2(2.809f, 0.021f),
                new Vector2(1.411f, 2.443f),
                new Vector2(-1.416f, 2.443f),
                new Vector2(-2.809f, 0.031f),
                new Vector2(-1.373f, -2.456f),
            };
            // Only active child objects named "net" score: Position01, Position03, Position05.
            int[] goalOwners = CreateScene3v3GoalOwners(map, activePlayerIds);
            var goalCenters = new[]
            {
                new Vector2(2.086f, -1.231f),
                new Vector2(2.118f, 1.218f),
                new Vector2(0f, 2.443f),
                new Vector2(-2.114f, 1.234f),
                new Vector2(-2.094f, -1.207f),
                new Vector2(0f, -2.456f),
            };
            return new ArenaGeometry(
                2.81f,
                2.456f,
                0.18f,
                scene3v3PaddleLength,
                scene3v3PaddleThickness,
                3.2f,
                CreateBoundarySegments(points, goalOwners, goalCenters, scene3v3GoalHalfLength, scene3v3GoalTriggerInset));
        }

        private static int[] CreateScene3v3GoalOwners(
            MapRuleDefinition map,
            IReadOnlyList<int> activePlayerIds)
        {
            var goalOwners = new[] { -1, -1, -1, -1, -1, -1 };
            IReadOnlyList<MapPlayerSideBindingDefinition> bindings =
                map?.PlayerSideBindings != null && map.PlayerSideBindings.Count > 0
                    ? map.PlayerSideBindings
                    : CreateDefaultScene3v3PlayerSideBindings();

            for (int i = 0; i < bindings.Count; i++)
            {
                MapPlayerSideBindingDefinition binding = bindings[i];
                if (binding == null ||
                    binding.BoundarySegmentIndex < 0 ||
                    binding.BoundarySegmentIndex >= goalOwners.Length)
                {
                    continue;
                }

                int playerIndex = ResolveActivePlayerIndex(binding.PlayerId, activePlayerIds);
                if (playerIndex < 0)
                {
                    continue;
                }

                goalOwners[binding.BoundarySegmentIndex] = playerIndex;
            }

            return goalOwners;
        }

        private static IReadOnlyList<MapPlayerSideBindingDefinition> CreateDefaultScene3v3PlayerSideBindings()
        {
            return new[]
            {
                new MapPlayerSideBindingDefinition
                {
                    PlayerId = 1,
                    ScenePosition = "Position01",
                    BoundarySegmentIndex = 5,
                },
                new MapPlayerSideBindingDefinition
                {
                    PlayerId = 2,
                    ScenePosition = "Position03",
                    BoundarySegmentIndex = 1,
                },
                new MapPlayerSideBindingDefinition
                {
                    PlayerId = 3,
                    ScenePosition = "Position05",
                    BoundarySegmentIndex = 3,
                },
            };
        }

        private static int ResolveActivePlayerIndex(int playerId, IReadOnlyList<int> activePlayerIds)
        {
            if (playerId <= 0)
            {
                return -1;
            }

            if (activePlayerIds == null || activePlayerIds.Count == 0)
            {
                return playerId - 1;
            }

            for (int i = 0; i < activePlayerIds.Count; i++)
            {
                if (activePlayerIds[i] == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        public ArenaGeometry WithPaddleLength(float paddleLength)
        {
            return new ArenaGeometry(
                HalfWidth,
                HalfHeight,
                PaddleInset,
                paddleLength,
                PaddleThickness,
                PaddleSpeed,
                BoundarySegments);
        }

        public Vector2 GetSideNormal(SpawnLayoutType layoutType, int playerIndex)
        {
            if (TryGetGoalSegmentForPlayer(playerIndex, out ArenaBoundarySegment segment))
            {
                return segment.InwardNormal;
            }

            switch (layoutType)
            {
                case SpawnLayoutType.DualFront:
                    return playerIndex % 2 == 0 ? Vector2.up : Vector2.down;
                case SpawnLayoutType.Ring:
                case SpawnLayoutType.FourSide:
                default:
                    switch (playerIndex % 4)
                    {
                        case 0:
                            return Vector2.up;
                        case 1:
                            return Vector2.down;
                        case 2:
                            return Vector2.left;
                        default:
                            return Vector2.right;
                    }
            }
        }

        public Vector2 GetSideTangent(Vector2 normal)
        {
            if (TryGetGoalSegmentForNormal(normal, out ArenaBoundarySegment segment))
            {
                return segment.Tangent;
            }

            return Mathf.Abs(normal.y) > 0.5f ? Vector2.right : Vector2.up;
        }

        public Vector2 GetPaddleCenter(Vector2 normal, float axis)
        {
            float clampedAxis = ClampPaddleAxis(normal, axis);
            if (TryGetGoalSegmentForNormal(normal, out ArenaBoundarySegment segment))
            {
                return segment.GoalCenter + segment.InwardNormal * PaddleInset + segment.Tangent * clampedAxis;
            }

            if (Mathf.Abs(normal.y) > 0.5f)
            {
                return new Vector2(clampedAxis, normal.y > 0f ? -HalfHeight + PaddleInset : HalfHeight - PaddleInset);
            }

            return new Vector2(normal.x > 0f ? -HalfWidth + PaddleInset : HalfWidth - PaddleInset, clampedAxis);
        }

        public float ClampPaddleAxis(Vector2 normal, float axis)
        {
            if (TryGetGoalSegmentForNormal(normal, out ArenaBoundarySegment segment))
            {
                float segmentLimit = segment.GoalHalfLength - PaddleLength * 0.5f;
                return Mathf.Clamp(axis, -Math.Max(0f, segmentLimit), Math.Max(0f, segmentLimit));
            }

            float limit = Mathf.Abs(normal.y) > 0.5f
                ? HalfWidth - PaddleLength * 0.5f
                : HalfHeight - PaddleLength * 0.5f;
            return Mathf.Clamp(axis, -Math.Max(0f, limit), Math.Max(0f, limit));
        }

        public Vector2 GetZoneCenter(Vector2 normal)
        {
            if (TryGetGoalSegmentForNormal(normal, out ArenaBoundarySegment segment))
            {
                return segment.GoalCenter;
            }

            if (Mathf.Abs(normal.y) > 0.5f)
            {
                return new Vector2(0f, normal.y > 0f ? -HalfHeight : HalfHeight);
            }

            return new Vector2(normal.x > 0f ? -HalfWidth : HalfWidth, 0f);
        }

        public bool TryGetGoalOwner(Vector2 position, int playerCount, SpawnLayoutType layoutType, out int playerIndex)
        {
            playerIndex = -1;
            if (playerCount <= 0)
            {
                return false;
            }

            if (HasCustomBoundary)
            {
                for (int i = 0; i < BoundarySegments.Count; i++)
                {
                    ArenaBoundarySegment segment = BoundarySegments[i];
                    if (segment.GoalPlayerIndex < 0 || segment.GoalPlayerIndex >= playerCount)
                    {
                        continue;
                    }

                    if (!segment.ContainsGoalPoint(position))
                    {
                        continue;
                    }

                    if (segment.IsPastGoalLine(position))
                    {
                        playerIndex = segment.GoalPlayerIndex;
                        return true;
                    }
                }

                return false;
            }

            if (position.y < -HalfHeight)
            {
                playerIndex = 0;
                return playerIndex < playerCount;
            }

            if (position.y > HalfHeight)
            {
                playerIndex = layoutType == SpawnLayoutType.DualFront ? 1 : 1;
                return playerIndex < playerCount;
            }

            if (layoutType != SpawnLayoutType.DualFront && position.x > HalfWidth)
            {
                playerIndex = 2;
                return playerIndex < playerCount;
            }

            if (layoutType != SpawnLayoutType.DualFront && position.x < -HalfWidth)
            {
                playerIndex = 3;
                return playerIndex < playerCount;
            }

            return false;
        }

        public bool Contains(Vector2 position)
        {
            if (!HasCustomBoundary)
            {
                return position.x >= -HalfWidth &&
                       position.x <= HalfWidth &&
                       position.y >= -HalfHeight &&
                       position.y <= HalfHeight;
            }

            for (int i = 0; i < BoundarySegments.Count; i++)
            {
                ArenaBoundarySegment segment = BoundarySegments[i];
                if (Vector2.Dot(position - segment.Start, segment.InwardNormal) < -0.001f)
                {
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<ArenaBoundarySegment> CreateBoundarySegments(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<int> goalOwners,
            IReadOnlyList<Vector2> goalCenters,
            float goalHalfLength,
            float goalTriggerInset)
        {
            var segments = new List<ArenaBoundarySegment>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[(i + 1) % points.Count];
                Vector2 edge = end - start;
                Vector2 inwardNormal = new Vector2(-edge.y, edge.x).normalized;
                int goalOwner = i < goalOwners.Count ? goalOwners[i] : -1;
                Vector2 goalCenter = i < goalCenters.Count ? goalCenters[i] : (start + end) * 0.5f;
                segments.Add(new ArenaBoundarySegment(start, end, inwardNormal, goalOwner, goalCenter, goalHalfLength, goalTriggerInset));
            }

            return segments;
        }

        public bool TryGetGoalSegmentForPlayer(int playerIndex, out ArenaBoundarySegment segment)
        {
            if (HasCustomBoundary)
            {
                for (int i = 0; i < BoundarySegments.Count; i++)
                {
                    ArenaBoundarySegment candidate = BoundarySegments[i];
                    if (candidate.GoalPlayerIndex == playerIndex)
                    {
                        segment = candidate;
                        return true;
                    }
                }
            }

            segment = null;
            return false;
        }

        private bool TryGetGoalSegmentForNormal(Vector2 normal, out ArenaBoundarySegment segment)
        {
            if (HasCustomBoundary)
            {
                Vector2 normalized = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.zero;
                for (int i = 0; i < BoundarySegments.Count; i++)
                {
                    ArenaBoundarySegment candidate = BoundarySegments[i];
                    if (candidate.GoalPlayerIndex >= 0 && Vector2.Dot(candidate.InwardNormal, normalized) > 0.999f)
                    {
                        segment = candidate;
                        return true;
                    }
                }
            }

            segment = null;
            return false;
        }
    }

    public sealed class ArenaBoundarySegment
    {
        public ArenaBoundarySegment(
            Vector2 start,
            Vector2 end,
            Vector2 inwardNormal,
            int goalPlayerIndex,
            Vector2 goalCenter,
            float goalHalfLength,
            float goalTriggerInset)
        {
            Start = start;
            End = end;
            InwardNormal = inwardNormal.sqrMagnitude > 0.0001f ? inwardNormal.normalized : Vector2.up;
            GoalPlayerIndex = goalPlayerIndex;
            GoalCenter = goalCenter;
            GoalHalfLength = Math.Max(0f, goalHalfLength);
            GoalTriggerInset = Math.Max(0f, goalTriggerInset);
        }

        public Vector2 Start { get; }
        public Vector2 End { get; }
        public Vector2 InwardNormal { get; }
        public Vector2 Tangent => (End - Start).sqrMagnitude > 0.0001f ? (End - Start).normalized : Vector2.right;
        public int GoalPlayerIndex { get; }
        public Vector2 GoalCenter { get; }
        public float GoalHalfLength { get; }
        public float GoalTriggerInset { get; }

        public bool ContainsGoalPoint(Vector2 point)
        {
            if (GoalPlayerIndex < 0 || GoalHalfLength <= 0f)
            {
                return false;
            }

            Vector2 edge = End - Start;
            if (edge.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector2 tangent = edge.normalized;
            return Math.Abs(Vector2.Dot(point - GoalCenter, tangent)) <= GoalHalfLength;
        }

        public bool IsPastGoalLine(Vector2 point)
        {
            return GoalPlayerIndex >= 0 &&
                   ContainsGoalPoint(point) &&
                   Vector2.Dot(point - GoalCenter, InwardNormal) <= GoalTriggerInset;
        }

        public Vector2 GoalTriggerStart => GoalCenter - Tangent * GoalHalfLength + InwardNormal * GoalTriggerInset;
        public Vector2 GoalTriggerEnd => GoalCenter + Tangent * GoalHalfLength + InwardNormal * GoalTriggerInset;
    }
}
