using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class ArenaGeometry
    {
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
            if (map == null)
            {
                return CreateDefault();
            }

            int playerCount = ResolveActivePlayerCount(map, activePlayerIds);
            ValidateConfiguredMap(map, playerCount);
            return CreateConfiguredMap(map, activePlayerIds, playerCount);
        }

        private static void ValidateConfiguredMap(MapRuleDefinition map, int playerCount)
        {
            MapCollisionLayoutDefinition collisionLayout = FindCollisionLayout(map, playerCount);
            bool hasTargetCollisionLayout = collisionLayout != null;
            if (hasTargetCollisionLayout)
            {
                ValidateCollisionLayout(map, collisionLayout, playerCount);
            }

            if (!hasTargetCollisionLayout && (map.BoundaryPoints == null || map.BoundaryPoints.Count < 3))
            {
                throw new InvalidOperationException($"Map '{map.MapId}' must configure at least 3 BoundaryPoints in JSON.");
            }

            if (!hasTargetCollisionLayout && (map.GoalCenters == null || map.GoalCenters.Count == 0))
            {
                throw new InvalidOperationException($"Map '{map.MapId}' must configure GoalCenters in JSON.");
            }

            if (map.ArenaHalfWidth <= 0f ||
                map.ArenaHalfHeight <= 0f ||
                map.PaddleInset <= 0f ||
                map.PaddleLength <= 0f ||
                map.PaddleThickness <= 0f ||
                map.PaddleMoveSpeed <= 0f ||
                map.GoalHalfLength <= 0f ||
                map.GoalTriggerInset < 0f ||
                map.GoalContactLineInset < 0f)
            {
                throw new InvalidOperationException($"Map '{map.MapId}' must configure valid gameplay geometry values in JSON.");
            }
        }

        private static ArenaGeometry CreateConfiguredMap(
            MapRuleDefinition map,
            IReadOnlyList<int> activePlayerIds,
            int playerCount)
        {
            MapCollisionLayoutDefinition collisionLayout = FindCollisionLayout(map, playerCount);
            if (collisionLayout != null)
            {
                return CreateConfiguredCollisionLayoutMap(map, collisionLayout, activePlayerIds);
            }

            IReadOnlyList<Vector2> points = ToVector2List(map.BoundaryPoints);
            IReadOnlyList<Vector2> goalCenters = map.GoalCenters != null && map.GoalCenters.Count > 0
                ? ToVector2List(map.GoalCenters)
                : Array.Empty<Vector2>();
            IReadOnlyList<MapPlayerSideBindingDefinition> sideBindings =
                CreateScenePlayerSideBindings(playerCount, map.PlayerSideBindings);
            int[] goalOwners = CreateGoalOwners(sideBindings, activePlayerIds, points.Count);
            float halfWidth = map.ArenaHalfWidth <= 0f
                ? CalculateHalfExtent(points, true)
                : map.ArenaHalfWidth;
            float halfHeight = map.ArenaHalfHeight <= 0f
                ? CalculateHalfExtent(points, false)
                : map.ArenaHalfHeight;
            return new ArenaGeometry(
                halfWidth,
                halfHeight,
                map.PaddleInset,
                map.PaddleLength,
                map.PaddleThickness,
                map.PaddleMoveSpeed,
                CreateBoundarySegments(points, goalOwners, goalCenters, map.GoalHalfLength, map.GoalTriggerInset, map.GoalContactLineInset));
        }

        private static MapCollisionLayoutDefinition FindCollisionLayout(MapRuleDefinition map, int playerCount)
        {
            if (map?.CollisionLayouts == null || map.CollisionLayouts.Count <= 0)
            {
                return null;
            }

            return map.CollisionLayouts.FirstOrDefault(layout =>
                layout != null &&
                layout.PlayerCount == playerCount &&
                layout.BoundarySegments != null &&
                layout.BoundarySegments.Count >= 3);
        }

        private static void ValidateCollisionLayout(
            MapRuleDefinition map,
            MapCollisionLayoutDefinition layout,
            int playerCount)
        {
            if (layout.PlayerSideBindings == null || layout.PlayerSideBindings.Count < playerCount)
            {
                throw new InvalidOperationException(
                    $"Map '{map.MapId}' CollisionLayout playerCount={playerCount} must configure PlayerSideBindings for every active player.");
            }

            var playerIds = new HashSet<int>();
            var segmentIndexes = new HashSet<int>();
            for (int i = 0; i < layout.PlayerSideBindings.Count; i++)
            {
                MapPlayerSideBindingDefinition binding = layout.PlayerSideBindings[i];
                if (binding == null || binding.PlayerId <= 0)
                {
                    throw new InvalidOperationException(
                        $"Map '{map.MapId}' CollisionLayout playerCount={playerCount} has invalid PlayerSideBindings[{i}].");
                }

                if (binding.BoundarySegmentIndex < 0 || binding.BoundarySegmentIndex >= layout.BoundarySegments.Count)
                {
                    throw new InvalidOperationException(
                        $"Map '{map.MapId}' CollisionLayout playerCount={playerCount} binding for player {binding.PlayerId} references invalid boundary segment {binding.BoundarySegmentIndex}.");
                }

                if (!playerIds.Add(binding.PlayerId))
                {
                    throw new InvalidOperationException(
                        $"Map '{map.MapId}' CollisionLayout playerCount={playerCount} contains duplicate PlayerId {binding.PlayerId}.");
                }

                if (!segmentIndexes.Add(binding.BoundarySegmentIndex))
                {
                    throw new InvalidOperationException(
                        $"Map '{map.MapId}' CollisionLayout playerCount={playerCount} contains duplicate BoundarySegmentIndex {binding.BoundarySegmentIndex}.");
                }
            }
        }

        private static ArenaGeometry CreateConfiguredCollisionLayoutMap(
            MapRuleDefinition map,
            MapCollisionLayoutDefinition layout,
            IReadOnlyList<int> activePlayerIds)
        {
            IReadOnlyList<MapPlayerSideBindingDefinition> sideBindings = layout.PlayerSideBindings;
            int[] goalOwners = CreateGoalOwners(sideBindings, activePlayerIds, layout.BoundarySegments.Count);
            var segments = new List<ArenaBoundarySegment>(layout.BoundarySegments.Count);
            for (int i = 0; i < layout.BoundarySegments.Count; i++)
            {
                MapBoundarySegmentDefinition segment = layout.BoundarySegments[i];
                Vector2 start = ToVector2(segment?.Start);
                Vector2 end = ToVector2(segment?.End);
                Vector2 edge = end - start;
                Vector2 inwardNormal = new Vector2(-edge.y, edge.x).normalized;
                int goalOwner = i < goalOwners.Length ? goalOwners[i] : -1;
                Vector2 goalCenter = segment?.GoalCenter != null
                    ? ToVector2(segment.GoalCenter)
                    : (start + end) * 0.5f;
                float goalHalfLength = goalOwner >= 0 && segment != null && segment.GoalHalfLength > 0f
                    ? segment.GoalHalfLength
                    : 0f;
                float goalTriggerInset = goalOwner >= 0 && segment != null
                    ? Math.Max(0f, segment.GoalTriggerInset)
                    : 0f;
                float goalContactLineInset = goalOwner >= 0 ? map.GoalContactLineInset : 0f;
                segments.Add(new ArenaBoundarySegment(
                    start,
                    end,
                    inwardNormal,
                    goalOwner,
                    goalCenter,
                    goalHalfLength,
                    goalTriggerInset,
                    goalContactLineInset));
            }

            float halfWidth = CalculateHalfExtentFromSegments(segments, true);
            float halfHeight = CalculateHalfExtentFromSegments(segments, false);
            return new ArenaGeometry(
                halfWidth,
                halfHeight,
                map.PaddleInset,
                map.PaddleLength,
                map.PaddleThickness,
                map.PaddleMoveSpeed,
                segments);
        }

        public static ArenaGeometry CreateScene3v3(
            MapRuleDefinition map = null,
            IReadOnlyList<int> activePlayerIds = null)
        {
            const float scene3v3GoalHalfLength = 1.037f;
            const float scene3v3GoalTriggerInset = 0.069f;
            const float scene3v3GoalContactLineInset = 0.04f;
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
            int playerCount = ResolveActivePlayerCount(map, activePlayerIds);
            int[] goalOwners = CreateGoalOwners(
                CreateScenePlayerSideBindings(playerCount, map?.PlayerSideBindings),
                activePlayerIds,
                points.Length);
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
                ResolvePaddleMoveSpeed(map, 3.2f),
                CreateBoundarySegments(points, goalOwners, goalCenters, scene3v3GoalHalfLength, scene3v3GoalTriggerInset, scene3v3GoalContactLineInset));
        }

        private static float ResolvePaddleMoveSpeed(MapRuleDefinition map, float defaultSpeed)
        {
            return map != null && map.PaddleMoveSpeed > 0f
                ? map.PaddleMoveSpeed
                : defaultSpeed;
        }

        private static int ResolveActivePlayerCount(
            MapRuleDefinition map,
            IReadOnlyList<int> activePlayerIds)
        {
            if (activePlayerIds != null && activePlayerIds.Count > 0)
            {
                return activePlayerIds.Count;
            }

            return map != null && map.DefaultPlayerCount > 0 ? map.DefaultPlayerCount : 3;
        }

        private static int[] CreateGoalOwners(
            IReadOnlyList<MapPlayerSideBindingDefinition> bindings,
            IReadOnlyList<int> activePlayerIds,
            int segmentCount)
        {
            int safeSegmentCount = Math.Max(0, segmentCount);
            var goalOwners = new int[safeSegmentCount];
            for (int i = 0; i < goalOwners.Length; i++)
            {
                goalOwners[i] = -1;
            }

            IReadOnlyList<MapPlayerSideBindingDefinition> resolvedBindings =
                bindings != null && bindings.Count > 0
                    ? bindings
                    : CreateScenePlayerSideBindings(3, null);

            for (int i = 0; i < resolvedBindings.Count; i++)
            {
                MapPlayerSideBindingDefinition binding = resolvedBindings[i];
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

        private static IReadOnlyList<Vector2> ToVector2List(IReadOnlyList<MapVector2Definition> points)
        {
            var result = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                MapVector2Definition point = points[i];
                result.Add(point != null ? new Vector2(point.X, point.Y) : Vector2.zero);
            }

            return result;
        }

        private static Vector2 ToVector2(MapVector2Definition point)
        {
            return point != null ? new Vector2(point.X, point.Y) : Vector2.zero;
        }

        private static float CalculateHalfExtent(IReadOnlyList<Vector2> points, bool xAxis)
        {
            float extent = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                extent = Math.Max(extent, Math.Abs(xAxis ? points[i].x : points[i].y));
            }

            return extent;
        }

        private static float CalculateHalfExtentFromSegments(IReadOnlyList<ArenaBoundarySegment> segments, bool xAxis)
        {
            float extent = 0f;
            if (segments == null)
            {
                return extent;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                ArenaBoundarySegment segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                extent = Math.Max(extent, Math.Abs(xAxis ? segment.Start.x : segment.Start.y));
                extent = Math.Max(extent, Math.Abs(xAxis ? segment.End.x : segment.End.y));
            }

            return extent;
        }

        public static IReadOnlyList<MapPlayerSideBindingDefinition> CreateScenePlayerSideBindings(
            int playerCount,
            IReadOnlyList<MapPlayerSideBindingDefinition> configuredBindings = null)
        {
            if (playerCount <= 2)
            {
                return new[]
                {
                    CreatePlayerSideBinding(1, "Position01", 5),
                    CreatePlayerSideBinding(2, "Position04", 2),
                };
            }

            if (playerCount >= 4)
            {
                return new[]
                {
                    CreatePlayerSideBinding(1, "Position01", 7),
                    CreatePlayerSideBinding(2, "Position03", 1),
                    CreatePlayerSideBinding(3, "Position05", 3),
                    CreatePlayerSideBinding(4, "Position07", 5),
                };
            }

            return configuredBindings != null && configuredBindings.Count > 0
                ? configuredBindings
                : new[]
                {
                    CreatePlayerSideBinding(1, "Position01", 5),
                    CreatePlayerSideBinding(2, "Position03", 1),
                    CreatePlayerSideBinding(3, "Position05", 3),
                };
        }

        private static MapPlayerSideBindingDefinition CreatePlayerSideBinding(
            int playerId,
            string scenePosition,
            int boundarySegmentIndex)
        {
            return new MapPlayerSideBindingDefinition
            {
                PlayerId = playerId,
                ScenePosition = scenePosition,
                BoundarySegmentIndex = boundarySegmentIndex,
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

        public ArenaGeometry WithGoalBandDimensions(float goalHalfLength, float goalTriggerInset)
        {
            if (!HasCustomBoundary)
            {
                return this;
            }

            float safeGoalHalfLength = Math.Max(0.01f, goalHalfLength);
            float safeGoalTriggerInset = Math.Max(0f, goalTriggerInset);
            var segments = new List<ArenaBoundarySegment>(BoundarySegments.Count);
            for (int i = 0; i < BoundarySegments.Count; i++)
            {
                ArenaBoundarySegment segment = BoundarySegments[i];
                segments.Add(segment != null && segment.GoalPlayerIndex >= 0
                    ? segment.WithGoalBandDimensions(safeGoalHalfLength, safeGoalTriggerInset)
                    : segment);
            }

            return new ArenaGeometry(
                HalfWidth,
                HalfHeight,
                PaddleInset,
                PaddleLength,
                PaddleThickness,
                PaddleSpeed,
                segments);
        }

        public ArenaGeometry WithGoalBandDimensions(IReadOnlyDictionary<int, ArenaGoalBandDimensions> dimensionsBySegmentIndex)
        {
            if (!HasCustomBoundary || dimensionsBySegmentIndex == null || dimensionsBySegmentIndex.Count == 0)
            {
                return this;
            }

            var segments = new List<ArenaBoundarySegment>(BoundarySegments.Count);
            for (int i = 0; i < BoundarySegments.Count; i++)
            {
                ArenaBoundarySegment segment = BoundarySegments[i];
                if (segment != null &&
                    segment.GoalPlayerIndex >= 0 &&
                    dimensionsBySegmentIndex.TryGetValue(i, out ArenaGoalBandDimensions dimensions))
                {
                    segments.Add(segment.WithGoalBandDimensions(
                        Math.Max(0.01f, dimensions.GoalHalfLength),
                        Math.Max(0f, dimensions.GoalTriggerInset)));
                }
                else
                {
                    segments.Add(segment);
                }
            }

            return new ArenaGeometry(
                HalfWidth,
                HalfHeight,
                PaddleInset,
                PaddleLength,
                PaddleThickness,
                PaddleSpeed,
                segments);
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

        public bool TryGetGoalOwner(
            Vector2 position,
            int playerCount,
            SpawnLayoutType layoutType,
            out int playerIndex,
            float goalContactRadius = 0f)
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

                    if (segment.IsPastGoalLine(position, goalContactRadius))
                    {
                        playerIndex = segment.GoalPlayerIndex;
                        return true;
                    }
                }

                return false;
            }

            float contactRadius = Math.Max(0f, goalContactRadius);
            if (position.y <= -HalfHeight + contactRadius)
            {
                playerIndex = 0;
                return playerIndex < playerCount;
            }

            if (position.y >= HalfHeight - contactRadius)
            {
                playerIndex = layoutType == SpawnLayoutType.DualFront ? 1 : 1;
                return playerIndex < playerCount;
            }

            if (layoutType != SpawnLayoutType.DualFront && position.x >= HalfWidth - contactRadius)
            {
                playerIndex = 2;
                return playerIndex < playerCount;
            }

            if (layoutType != SpawnLayoutType.DualFront && position.x <= -HalfWidth + contactRadius)
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
            float goalTriggerInset,
            float goalContactLineInset = 0f)
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
                float activeGoalContactLineInset = goalOwner >= 0 ? goalContactLineInset : 0f;
                segments.Add(new ArenaBoundarySegment(start, end, inwardNormal, goalOwner, goalCenter, goalHalfLength, goalTriggerInset, activeGoalContactLineInset));
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
            float goalTriggerInset,
            float goalContactLineInset = 0f)
        {
            Start = start;
            End = end;
            InwardNormal = inwardNormal.sqrMagnitude > 0.0001f ? inwardNormal.normalized : Vector2.up;
            GoalPlayerIndex = goalPlayerIndex;
            GoalCenter = goalCenter;
            GoalHalfLength = Math.Max(0f, goalHalfLength);
            GoalTriggerInset = Math.Max(0f, goalTriggerInset);
            GoalContactLineInset = Math.Max(0f, goalContactLineInset);
        }

        public Vector2 Start { get; }
        public Vector2 End { get; }
        public Vector2 InwardNormal { get; }
        public Vector2 Tangent => (End - Start).sqrMagnitude > 0.0001f ? (End - Start).normalized : Vector2.right;
        public int GoalPlayerIndex { get; }
        public Vector2 GoalCenter { get; }
        public float GoalHalfLength { get; }
        public float GoalTriggerInset { get; }
        public float GoalContactLineInset { get; }
        public Vector2 GoalContactCenter => GoalCenter + InwardNormal * GoalContactLineInset;

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

        public bool IsPastGoalLine(Vector2 point, float goalContactRadius = 0f)
        {
            return GoalPlayerIndex >= 0 &&
                   ContainsGoalPoint(point) &&
                   Vector2.Dot(point - GoalContactCenter, InwardNormal) <= Math.Max(0f, goalContactRadius);
        }

        public Vector2 GoalOuterStart => GoalCenter - Tangent * GoalHalfLength;
        public Vector2 GoalOuterEnd => GoalCenter + Tangent * GoalHalfLength;
        public Vector2 GoalContactOuterStart => GoalContactCenter - Tangent * GoalHalfLength;
        public Vector2 GoalContactOuterEnd => GoalContactCenter + Tangent * GoalHalfLength;
        public Vector2 GetGoalContactStart(float goalContactRadius) => GoalContactOuterStart + InwardNormal * Math.Max(0f, goalContactRadius);
        public Vector2 GetGoalContactEnd(float goalContactRadius) => GoalContactOuterEnd + InwardNormal * Math.Max(0f, goalContactRadius);
        public Vector2 GoalTriggerStart => GoalCenter - Tangent * GoalHalfLength + InwardNormal * GoalTriggerInset;
        public Vector2 GoalTriggerEnd => GoalCenter + Tangent * GoalHalfLength + InwardNormal * GoalTriggerInset;

        public ArenaBoundarySegment WithGoalBandDimensions(float goalHalfLength, float goalTriggerInset)
        {
            return new ArenaBoundarySegment(
                Start,
                End,
                InwardNormal,
                GoalPlayerIndex,
                GoalCenter,
                goalHalfLength,
                goalTriggerInset,
                GoalContactLineInset);
        }
    }

    public readonly struct ArenaGoalBandDimensions
    {
        public ArenaGoalBandDimensions(float goalHalfLength, float goalTriggerInset)
        {
            GoalHalfLength = goalHalfLength;
            GoalTriggerInset = goalTriggerInset;
        }

        public float GoalHalfLength { get; }
        public float GoalTriggerInset { get; }
    }
}
