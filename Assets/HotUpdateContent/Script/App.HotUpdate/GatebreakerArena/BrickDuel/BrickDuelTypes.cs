using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public enum BrickDuelPhase
    {
        Waiting = 0,
        Countdown = 1,
        Playing = 2,
        Result = 3,
    }

    public enum BrickDuelSide
    {
        Bottom = 1,
        Top = 2,
    }

    public enum BrickDuelBrickType
    {
        Green = 1,
        Red = 2,
        Yellow = 3,
        Mystery = 4,
    }

    public enum BrickDuelResult
    {
        None = 0,
        PlayerWin = 1,
        PlayerLose = 2,
        Draw = 3,
    }

    public readonly struct BrickDuelFrameInput
    {
        public BrickDuelFrameInput(float playerMoveAxis)
        {
            PlayerMoveAxis = Mathf.Clamp(playerMoveAxis, -1f, 1f);
        }

        public float PlayerMoveAxis { get; }
    }

    public sealed class BrickDuelPaddleState
    {
        public BrickDuelSide Side { get; internal set; }
        public Vector2 Position { get; internal set; }
        public float MoveAxis { get; internal set; }
    }

    public sealed class BrickDuelBallState
    {
        public BrickDuelSide Side { get; internal set; }
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public bool IsActive { get; internal set; }
        public int ResetFramesRemaining { get; internal set; }
        public int StuckFrames { get; internal set; }
    }

    public sealed class BrickDuelBrickState
    {
        public int BrickId { get; internal set; }
        public BrickDuelSide Side { get; internal set; }
        public BrickDuelBrickType InitialType { get; internal set; }
        public int Health { get; internal set; }
        public Vector2 Position { get; internal set; }

        public BrickDuelBrickType VisualType
        {
            get
            {
                if (InitialType == BrickDuelBrickType.Mystery)
                {
                    return BrickDuelBrickType.Mystery;
                }

                if (Health >= 3)
                {
                    return BrickDuelBrickType.Yellow;
                }

                return Health == 2 ? BrickDuelBrickType.Red : BrickDuelBrickType.Green;
            }
        }
    }

    public sealed class BrickDuelFrameEvents
    {
        private readonly List<int> _destroyedBrickIds = new List<int>();
        private readonly List<int> _mysteryDestroyedBrickIds = new List<int>();

        public int BottomCoreDamage { get; internal set; }
        public int TopCoreDamage { get; internal set; }
        public bool PressureLevelChanged { get; internal set; }
        public bool BottomBallReset { get; internal set; }
        public bool TopBallReset { get; internal set; }
        public IReadOnlyList<int> DestroyedBrickIds => _destroyedBrickIds;
        public IReadOnlyList<int> MysteryDestroyedBrickIds => _mysteryDestroyedBrickIds;

        internal void Clear()
        {
            BottomCoreDamage = 0;
            TopCoreDamage = 0;
            PressureLevelChanged = false;
            BottomBallReset = false;
            TopBallReset = false;
            _destroyedBrickIds.Clear();
            _mysteryDestroyedBrickIds.Clear();
        }

        internal void AddDestroyed(BrickDuelBrickState brick)
        {
            _destroyedBrickIds.Add(brick.BrickId);
            if (brick.InitialType == BrickDuelBrickType.Mystery)
            {
                _mysteryDestroyedBrickIds.Add(brick.BrickId);
            }
        }
    }

    public sealed class BrickDuelSnapshot
    {
        public BrickDuelPhase Phase { get; set; }
        public BrickDuelResult Result { get; set; }
        public bool IsPaused { get; set; }
        public int SimulationFrame { get; set; }
        public int CountdownFramesRemaining { get; set; }
        public int ElapsedFrames { get; set; }
        public int BottomCoreHealth { get; set; }
        public int TopCoreHealth { get; set; }
        public int PressureLevel { get; set; }
        public float PressureMultiplier { get; set; }
        public int FramesUntilPressureIncrease { get; set; }
        public float BottomDangerDistance { get; set; }
        public float TopDangerDistance { get; set; }
        public BrickDuelPaddleState BottomPaddle { get; set; }
        public BrickDuelPaddleState TopPaddle { get; set; }
        public BrickDuelBallState BottomBall { get; set; }
        public BrickDuelBallState TopBall { get; set; }
        public IReadOnlyList<BrickDuelBrickState> Bricks { get; set; }
    }
}
