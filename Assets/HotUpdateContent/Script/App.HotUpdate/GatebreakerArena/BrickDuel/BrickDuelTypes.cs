using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Phase;
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
            : this(playerMoveAxis, 0f, false, false, true)
        {
        }

        public BrickDuelFrameInput(
            float bottomMoveAxis,
            float topMoveAxis,
            bool bottomAbilityPressed = false,
            bool topAbilityPressed = false,
            bool useTopAi = false)
        {
            BottomMoveAxis = Mathf.Clamp(bottomMoveAxis, -1f, 1f);
            TopMoveAxis = Mathf.Clamp(topMoveAxis, -1f, 1f);
            BottomAbilityPressed = bottomAbilityPressed;
            TopAbilityPressed = topAbilityPressed;
            UseTopAi = useTopAi;
        }

        public float BottomMoveAxis { get; }
        public float TopMoveAxis { get; }
        public bool BottomAbilityPressed { get; }
        public bool TopAbilityPressed { get; }
        public bool UseTopAi { get; }
        public float PlayerMoveAxis => BottomMoveAxis;
    }

    public sealed class BrickDuelCollisionFrameTelemetry
    {
        private readonly List<BrickDuelCollisionEvent> _events =
            new List<BrickDuelCollisionEvent>();
        private readonly Action<BrickDuelCollisionEvent> _eventConsumer;

        public BrickDuelCollisionFrameTelemetry()
        {
        }

        internal BrickDuelCollisionFrameTelemetry(
            Action<BrickDuelCollisionEvent> eventConsumer)
        {
            _eventConsumer = eventConsumer;
        }

        public int PaddleBounceCount { get; internal set; }
        public int OwnOuterWallBounceCount { get; internal set; }
        public int BrickHitCount { get; internal set; }
        public int PiercedBrickHitCount { get; internal set; }
        public float MaximumPaddleRedirectDegrees { get; internal set; }
        public IReadOnlyList<BrickDuelCollisionEvent> Events => _events;

        internal void RecordBrickHit(int brickId, bool pierced)
        {
            BrickHitCount++;
            if (pierced) PiercedBrickHitCount++;
            Record(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.BrickHit,
                brickId,
                pierced,
                0f));
        }

        internal void RecordPaddleBounce(float redirectDegrees)
        {
            PaddleBounceCount++;
            MaximumPaddleRedirectDegrees = Mathf.Max(
                MaximumPaddleRedirectDegrees,
                redirectDegrees);
            Record(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.PaddleBounce,
                0,
                false,
                redirectDegrees));
        }

        internal void RecordOwnOuterWallBounce()
        {
            OwnOuterWallBounceCount++;
            Record(new BrickDuelCollisionEvent(
                BrickDuelCollisionEventType.OwnOuterWallBounce,
                0,
                false,
                0f));
        }

        private void Record(BrickDuelCollisionEvent collisionEvent)
        {
            _events.Add(collisionEvent);
            _eventConsumer?.Invoke(collisionEvent);
        }
    }

    public enum BrickDuelCollisionEventType
    {
        BrickHit = 1,
        PaddleBounce = 2,
        OwnOuterWallBounce = 3,
    }

    public readonly struct BrickDuelCollisionEvent
    {
        public BrickDuelCollisionEvent(
            BrickDuelCollisionEventType eventType,
            int brickId = 0,
            bool pierced = false,
            float paddleRedirectDegrees = 0f)
        {
            EventType = eventType;
            BrickId = brickId;
            Pierced = pierced;
            PaddleRedirectDegrees = paddleRedirectDegrees;
        }

        public BrickDuelCollisionEventType EventType { get; }
        public int BrickId { get; }
        public bool Pierced { get; }
        public float PaddleRedirectDegrees { get; }
    }

    public readonly struct BrickDuelHorizontalBarrier
    {
        public BrickDuelHorizontalBarrier(float y, float halfWidth, Vector2 normal)
        {
            Y = y;
            HalfWidth = Mathf.Max(0f, halfWidth);
            Normal = normal;
        }

        public float Y { get; }
        public float HalfWidth { get; }
        public Vector2 Normal { get; }
        public bool IsValid =>
            HalfWidth > 0f &&
            Mathf.Abs(Normal.x) <= 0.0001f &&
            Mathf.Abs(Mathf.Abs(Normal.y) - 1f) <= 0.0001f;
    }

    public sealed class BrickDuelPaddleState
    {
        public BrickDuelSide Side { get; internal set; }
        public Vector2 Position { get; internal set; }
        public float MoveAxis { get; internal set; }
    }

    public sealed class BrickDuelBallState
    {
        public int BallId { get; internal set; }
        public BrickDuelSide Side { get; internal set; }
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public bool IsActive { get; internal set; }
        public bool IsSplit { get; internal set; }
        public int RemainingLifetimeFrames { get; internal set; }
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
        public int ColumnId { get; internal set; }
        public int LogicalRowId { get; internal set; }
        public string ItemId { get; internal set; }

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

    public sealed class BrickDuelItemCapsuleState
    {
        public int CapsuleId { get; internal set; }
        public BrickDuelSide Side { get; internal set; }
        public string ItemId { get; internal set; }
        public Vector2 Position { get; internal set; }
        public int SpawnFrame { get; internal set; }
    }

    public sealed class BrickDuelSideItemEffects
    {
        public int WidePaddleFramesRemaining { get; internal set; }
        public int LargeBallFramesRemaining { get; internal set; }
        public int PhaseDrillFramesRemaining { get; internal set; }
        public int PhaseDrillCharges { get; internal set; }
        public int SpeedBallFramesRemaining { get; internal set; }
        public float SpeedBallDurationAddSeconds { get; internal set; }
        public float SpeedBallDurationMultiplier { get; internal set; } = 1f;
        public int AimedReboundFramesRemaining { get; internal set; }
        public int DampingFramesRemaining { get; internal set; }
        public int CoreBufferFramesRemaining { get; internal set; }
        public bool HasCoreBuffer { get; internal set; }
        public int MagnetPullRemaining { get; internal set; }

        public bool HasWidePaddle => WidePaddleFramesRemaining > 0;
        public bool HasLargeBall => LargeBallFramesRemaining > 0;
        public bool HasPhaseDrill => PhaseDrillCharges > 0 && PhaseDrillFramesRemaining > 0;
        public bool HasSpeedBall => SpeedBallFramesRemaining > 0;
        public bool HasAimedRebound => AimedReboundFramesRemaining > 0;
        public bool HasDamping => DampingFramesRemaining > 0;

        internal void Clear()
        {
            WidePaddleFramesRemaining = 0;
            LargeBallFramesRemaining = 0;
            PhaseDrillFramesRemaining = 0;
            PhaseDrillCharges = 0;
            SpeedBallFramesRemaining = 0;
            AimedReboundFramesRemaining = 0;
            DampingFramesRemaining = 0;
            CoreBufferFramesRemaining = 0;
            HasCoreBuffer = false;
            MagnetPullRemaining = 0;
        }

        internal BrickDuelSideItemEffects Clone()
        {
            return new BrickDuelSideItemEffects
            {
                WidePaddleFramesRemaining = WidePaddleFramesRemaining,
                LargeBallFramesRemaining = LargeBallFramesRemaining,
                PhaseDrillFramesRemaining = PhaseDrillFramesRemaining,
                PhaseDrillCharges = PhaseDrillCharges,
                SpeedBallFramesRemaining = SpeedBallFramesRemaining,
                SpeedBallDurationAddSeconds = SpeedBallDurationAddSeconds,
                SpeedBallDurationMultiplier = SpeedBallDurationMultiplier,
                AimedReboundFramesRemaining = AimedReboundFramesRemaining,
                DampingFramesRemaining = DampingFramesRemaining,
                CoreBufferFramesRemaining = CoreBufferFramesRemaining,
                HasCoreBuffer = HasCoreBuffer,
                MagnetPullRemaining = MagnetPullRemaining,
            };
        }
    }

    public sealed class BrickDuelFrameEvents
    {
        private readonly List<int> _destroyedBrickIds = new List<int>();
        private readonly List<int> _mysteryDestroyedBrickIds = new List<int>();
        private readonly List<BrickDuelItemCapsuleState> _spawnedCapsules =
            new List<BrickDuelItemCapsuleState>();
        private readonly List<int> _collectedCapsuleIds = new List<int>();
        private readonly List<int> _missedCapsuleIds = new List<int>();
        private readonly List<int> _expiredCapsuleIds = new List<int>();
        private readonly List<string> _bottomCollectedItemIds = new List<string>();
        private readonly List<string> _topCollectedItemIds = new List<string>();

        public int BottomCoreDamage { get; internal set; }
        public int TopCoreDamage { get; internal set; }
        public int BottomCoreDamageAbsorbed { get; internal set; }
        public int TopCoreDamageAbsorbed { get; internal set; }
        public bool PressureLevelChanged { get; internal set; }
        public bool BottomBallReset { get; internal set; }
        public bool TopBallReset { get; internal set; }
        public bool BottomBreakCounterTriggered { get; internal set; }
        public bool TopBreakCounterTriggered { get; internal set; }
        public IReadOnlyList<int> DestroyedBrickIds => _destroyedBrickIds;
        public IReadOnlyList<int> MysteryDestroyedBrickIds => _mysteryDestroyedBrickIds;
        public IReadOnlyList<BrickDuelItemCapsuleState> SpawnedCapsules => _spawnedCapsules;
        public IReadOnlyList<int> CollectedCapsuleIds => _collectedCapsuleIds;
        public IReadOnlyList<int> MissedCapsuleIds => _missedCapsuleIds;
        public IReadOnlyList<int> ExpiredCapsuleIds => _expiredCapsuleIds;
        public IReadOnlyList<string> BottomCollectedItemIds => _bottomCollectedItemIds;
        public IReadOnlyList<string> TopCollectedItemIds => _topCollectedItemIds;

        internal void Clear()
        {
            BottomCoreDamage = 0;
            TopCoreDamage = 0;
            BottomCoreDamageAbsorbed = 0;
            TopCoreDamageAbsorbed = 0;
            PressureLevelChanged = false;
            BottomBallReset = false;
            TopBallReset = false;
            BottomBreakCounterTriggered = false;
            TopBreakCounterTriggered = false;
            _destroyedBrickIds.Clear();
            _mysteryDestroyedBrickIds.Clear();
            _spawnedCapsules.Clear();
            _collectedCapsuleIds.Clear();
            _missedCapsuleIds.Clear();
            _expiredCapsuleIds.Clear();
            _bottomCollectedItemIds.Clear();
            _topCollectedItemIds.Clear();
        }

        internal void AddDestroyed(BrickDuelBrickState brick)
        {
            _destroyedBrickIds.Add(brick.BrickId);
            if (brick.InitialType == BrickDuelBrickType.Mystery)
            {
                _mysteryDestroyedBrickIds.Add(brick.BrickId);
            }
        }

        internal void AddSpawnedCapsule(BrickDuelItemCapsuleState capsule)
        {
            _spawnedCapsules.Add(CloneCapsule(capsule));
        }

        internal void AddCollected(BrickDuelItemCapsuleState capsule)
        {
            _collectedCapsuleIds.Add(capsule.CapsuleId);
            if (capsule.Side == BrickDuelSide.Bottom)
            {
                _bottomCollectedItemIds.Add(capsule.ItemId);
            }
            else
            {
                _topCollectedItemIds.Add(capsule.ItemId);
            }
        }

        internal void AddMissed(int capsuleId)
        {
            _missedCapsuleIds.Add(capsuleId);
        }

        internal void AddExpired(int capsuleId)
        {
            _expiredCapsuleIds.Add(capsuleId);
        }

        private static BrickDuelItemCapsuleState CloneCapsule(BrickDuelItemCapsuleState source)
        {
            return new BrickDuelItemCapsuleState
            {
                CapsuleId = source.CapsuleId,
                Side = source.Side,
                ItemId = source.ItemId,
                Position = source.Position,
                SpawnFrame = source.SpawnFrame,
            };
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
        public float BottomPaddleHalfWidth { get; set; }
        public float TopPaddleHalfWidth { get; set; }
        public float BottomBallRadius { get; set; }
        public float TopBallRadius { get; set; }
        public BrickDuelPaddleState BottomPaddle { get; set; }
        public BrickDuelPaddleState TopPaddle { get; set; }
        public BrickDuelBallState BottomBall { get; set; }
        public BrickDuelBallState TopBall { get; set; }
        public BrickDuelSideItemEffects BottomEffects { get; set; }
        public BrickDuelSideItemEffects TopEffects { get; set; }
        public BrickDuelPhaseSideState BottomPhaseState { get; set; }
        public BrickDuelPhaseSideState TopPhaseState { get; set; }
        public IReadOnlyList<BrickDuelBrickState> Bricks { get; set; }
        public IReadOnlyList<BrickDuelItemCapsuleState> Capsules { get; set; }
        public IReadOnlyList<BrickDuelBallState> SplitBalls { get; set; }
    }
}
