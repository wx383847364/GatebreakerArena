using System;
using App.HotUpdate.GatebreakerArena.Core;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public sealed class ArenaGeometry
    {
        public ArenaGeometry(float halfWidth, float halfHeight, float paddleInset, float paddleLength, float paddleThickness, float paddleSpeed)
        {
            HalfWidth = Math.Max(1f, halfWidth);
            HalfHeight = Math.Max(1f, halfHeight);
            PaddleInset = Mathf.Clamp(paddleInset, 0.05f, Math.Min(HalfWidth, HalfHeight) * 0.5f);
            PaddleLength = Math.Max(0.25f, paddleLength);
            PaddleThickness = Math.Max(0.05f, paddleThickness);
            PaddleSpeed = Math.Max(0f, paddleSpeed);
        }

        public float HalfWidth { get; }
        public float HalfHeight { get; }
        public float PaddleInset { get; }
        public float PaddleLength { get; }
        public float PaddleThickness { get; }
        public float PaddleSpeed { get; }

        public static ArenaGeometry CreateDefault()
        {
            return new ArenaGeometry(8f, 5f, 0.55f, 2.8f, 0.28f, 8f);
        }

        public Vector2 GetSideNormal(SpawnLayoutType layoutType, int playerIndex)
        {
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
            return Mathf.Abs(normal.y) > 0.5f ? Vector2.right : Vector2.up;
        }

        public Vector2 GetPaddleCenter(Vector2 normal, float axis)
        {
            float clampedAxis = ClampPaddleAxis(normal, axis);
            if (Mathf.Abs(normal.y) > 0.5f)
            {
                return new Vector2(clampedAxis, normal.y > 0f ? -HalfHeight + PaddleInset : HalfHeight - PaddleInset);
            }

            return new Vector2(normal.x > 0f ? -HalfWidth + PaddleInset : HalfWidth - PaddleInset, clampedAxis);
        }

        public float ClampPaddleAxis(Vector2 normal, float axis)
        {
            float limit = Mathf.Abs(normal.y) > 0.5f
                ? HalfWidth - PaddleLength * 0.5f
                : HalfHeight - PaddleLength * 0.5f;
            return Mathf.Clamp(axis, -Math.Max(0f, limit), Math.Max(0f, limit));
        }

        public bool TryGetGoalOwner(Vector2 position, int playerCount, SpawnLayoutType layoutType, out int playerIndex)
        {
            playerIndex = -1;
            if (playerCount <= 0)
            {
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
    }
}
