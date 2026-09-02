using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Phase;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public sealed class BrickDuelRuntime
    {
        private readonly struct LogicalRow
        {
            public LogicalRow(BrickDuelBrickType[] types, string[] itemIds)
            {
                Types = types;
                ItemIds = itemIds;
            }

            public BrickDuelBrickType[] Types { get; }
            public string[] ItemIds { get; }
        }

        private readonly BrickDuelRuleDefinition _rule;
        private readonly BrickDuelCollisionSolver _collisionSolver;
        private readonly BrickDuelTacticalAiController _aiController;
        private readonly List<BrickDuelBrickState> _bricks = new List<BrickDuelBrickState>();
        private readonly List<BrickDuelBallState> _splitBalls = new List<BrickDuelBallState>();
        private readonly List<BrickDuelItemCapsuleState> _capsules = new List<BrickDuelItemCapsuleState>();
        private readonly Dictionary<int, LogicalRow> _logicalRows = new Dictionary<int, LogicalRow>();
        private readonly HashSet<int> _bottomHitBrickIds = new HashSet<int>();
        private readonly HashSet<int> _topHitBrickIds = new HashSet<int>();
        private readonly HashSet<int> _splitHitBrickIds = new HashSet<int>();
        private readonly HashSet<int> _bottomIgnoredBrickIds = new HashSet<int>();
        private readonly HashSet<int> _topIgnoredBrickIds = new HashSet<int>();
        private readonly Dictionary<int, HashSet<int>> _splitIgnoredBrickIds =
            new Dictionary<int, HashSet<int>>();
        private readonly BrickDuelSideItemEffects _bottomEffects = new BrickDuelSideItemEffects();
        private readonly BrickDuelSideItemEffects _topEffects = new BrickDuelSideItemEffects();
        private readonly GatebreakerModeCatalog _catalog;
        private readonly BrickDuelPhaseSideRuntime _bottomPhase;
        private readonly BrickDuelPhaseSideRuntime _topPhase;
        private readonly PhaseCurveDefinition _phaseCurve;
        private GatebreakerDeterministicPrng _rowRandom;
        private BrickDuelItemDropBag _itemBag;
        private int _nextBrickId;
        private int _nextBallId;
        private int _nextCapsuleId;
        private int _nextLogicalRowId;
        private int _bottomNextRowId;
        private int _topNextRowId;
        private float _bottomRowTravelSinceSpawn;
        private float _topRowTravelSinceSpawn;
        private int _bottomBreakCounter;
        private int _topBreakCounter;
        private int _bottomPendingUpgradeFrames;
        private int _topPendingUpgradeFrames;
        private int _bottomPendingRowUpgrades;
        private int _topPendingRowUpgrades;

        public BrickDuelRuntime(
            BrickDuelRuleDefinition rule,
            BrickDuelAiRuleDefinition aiRule)
            : this(rule, aiRule, null, null, null)
        {
        }

        public BrickDuelRuntime(
            BrickDuelRuleDefinition rule,
            BrickDuelAiRuleDefinition aiRule,
            GatebreakerModeCatalog catalog,
            PhaseMatchLoadout bottomLoadout,
            PhaseMatchLoadout topLoadout)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            if (aiRule == null)
            {
                throw new ArgumentNullException(nameof(aiRule));
            }

            _collisionSolver = new BrickDuelCollisionSolver();
            _catalog = catalog;
            _phaseCurve = catalog?.AllPhaseCurves.Values.FirstOrDefault();
            if (catalog != null && bottomLoadout != null && topLoadout != null)
            {
                _bottomPhase = new BrickDuelPhaseSideRuntime(catalog, bottomLoadout, rule.SimulationFps);
                _topPhase = new BrickDuelPhaseSideRuntime(catalog, topLoadout, rule.SimulationFps);
            }
            _aiController = new BrickDuelTacticalAiController(
                rule,
                aiRule,
                BrickDuelSide.Top);
            BottomPaddle = new BrickDuelPaddleState { Side = BrickDuelSide.Bottom };
            TopPaddle = new BrickDuelPaddleState { Side = BrickDuelSide.Top };
            BottomBall = new BrickDuelBallState
            {
                BallId = 1,
                Side = BrickDuelSide.Bottom,
                IsSplit = false,
            };
            TopBall = new BrickDuelBallState
            {
                BallId = 2,
                Side = BrickDuelSide.Top,
                IsSplit = false,
            };
            LastFrameEvents = new BrickDuelFrameEvents();
            ResetToWaiting();
        }

        public BrickDuelRuleDefinition Rule => _rule;
        public BrickDuelPhase Phase { get; private set; }
        public BrickDuelResult Result { get; private set; }
        public bool IsPaused { get; private set; }
        public int SimulationFrame { get; private set; }
        public int CountdownFramesRemaining { get; private set; }
        public int ElapsedFrames { get; private set; }
        public int BottomCoreHealth { get; private set; }
        public int TopCoreHealth { get; private set; }
        public BrickDuelPaddleState BottomPaddle { get; }
        public BrickDuelPaddleState TopPaddle { get; }
        public BrickDuelBallState BottomBall { get; }
        public BrickDuelBallState TopBall { get; }
        public BrickDuelSideItemEffects BottomEffects => _bottomEffects;
        public BrickDuelSideItemEffects TopEffects => _topEffects;
        public IReadOnlyList<BrickDuelBrickState> Bricks => _bricks;
        public IReadOnlyList<BrickDuelBallState> SplitBalls => _splitBalls;
        public IReadOnlyList<BrickDuelItemCapsuleState> Capsules => _capsules;
        public BrickDuelFrameEvents LastFrameEvents { get; }
        public bool HasPhaseGrowth => _bottomPhase != null && _topPhase != null;
        public BrickDuelPhaseSideState BottomPhaseState => _bottomPhase?.State;
        public BrickDuelPhaseSideState TopPhaseState => _topPhase?.State;
        public float FrameDelta => 1f / Mathf.Max(1, _rule.SimulationFps);
        public int PressureLevel =>
            Mathf.FloorToInt(ElapsedFrames / (float)PressureIntervalFrames);
        public float PressureMultiplier =>
            1f + _rule.PressureIncrement * Mathf.Max(0, PressureLevel);
        public int PressureIntervalFrames =>
            Mathf.Max(1, Mathf.RoundToInt(_rule.PressureIntervalSeconds * _rule.SimulationFps));
        public int FramesUntilPressureIncrease
        {
            get
            {
                int remainder = ElapsedFrames % PressureIntervalFrames;
                return remainder == 0 && ElapsedFrames > 0
                    ? PressureIntervalFrames
                    : PressureIntervalFrames - remainder;
            }
        }

        public float BottomDangerDistance => GetDangerDistance(BrickDuelSide.Bottom);
        public float TopDangerDistance => GetDangerDistance(BrickDuelSide.Top);
        public float BottomPaddleHalfWidth => GetPaddleHalfWidth(_bottomEffects);
        public float TopPaddleHalfWidth => GetPaddleHalfWidth(_topEffects);
        public float BottomBallRadius => GetBallRadius(_bottomEffects);
        public float TopBallRadius => GetBallRadius(_topEffects);
        public float BottomTideSpeedMultiplier => GetTideSpeedMultiplier(_bottomEffects);
        public float TopTideSpeedMultiplier => GetTideSpeedMultiplier(_topEffects);
        public float BottomBallSpeedMultiplier => GetBallSpeedMultiplier(_bottomEffects);
        public float TopBallSpeedMultiplier => GetBallSpeedMultiplier(_topEffects);

        public void BeginCountdown()
        {
            SimulationFrame = 0;
            ElapsedFrames = 0;
            Result = BrickDuelResult.None;
            IsPaused = false;
            BottomCoreHealth = _rule.InitialCoreHealth;
            TopCoreHealth = _rule.InitialCoreHealth;
            CountdownFramesRemaining = _rule.CountdownSeconds * _rule.SimulationFps;
            _nextBrickId = 1;
            _nextBallId = 3;
            _nextCapsuleId = 1;
            _nextLogicalRowId = 0;
            _bottomNextRowId = 0;
            _topNextRowId = 0;
            _bottomRowTravelSinceSpawn = 0f;
            _topRowTravelSinceSpawn = 0f;
            _bottomBreakCounter = 0;
            _topBreakCounter = 0;
            _bottomPendingUpgradeFrames = 0;
            _topPendingUpgradeFrames = 0;
            _bottomPendingRowUpgrades = 0;
            _topPendingRowUpgrades = 0;
            _rowRandom = new GatebreakerDeterministicPrng(unchecked((uint)_rule.RandomSeed));
            _itemBag = new BrickDuelItemDropBag(
                BrickDuelItemDropBag.ResolveDefinitions(_rule.ItemDrops),
                unchecked((uint)_rule.RandomSeed) ^ 0x17E401u);
            _aiController.Reset();
            _bricks.Clear();
            _splitBalls.Clear();
            _capsules.Clear();
            _logicalRows.Clear();
            _bottomIgnoredBrickIds.Clear();
            _topIgnoredBrickIds.Clear();
            _splitIgnoredBrickIds.Clear();
            _bottomEffects.Clear();
            _topEffects.Clear();
            _bottomPhase?.Reset();
            _topPhase?.Reset();
            SpawnInitialRows();
            ResetPaddles();
            PositionBallForServe(BottomBall, BottomBallRadius);
            PositionBallForServe(TopBall, TopBallRadius);
            BottomBall.IsActive = false;
            TopBall.IsActive = false;
            LastFrameEvents.Clear();
            Phase = BrickDuelPhase.Countdown;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = Phase == BrickDuelPhase.Playing && paused;
        }

        /// <summary>
        /// Configures the aggregate duration correction applied when the speed-ball item is picked up.
        /// The resolved duration is (base seconds + additive seconds) * multiplier, then clamped.
        /// Callers are responsible for synchronizing modifier changes in networked matches.
        /// </summary>
        public void ConfigureSpeedBallDurationModifier(
            BrickDuelSide side,
            float additiveSeconds,
            float multiplier)
        {
            BrickDuelSideItemEffects effects = side == BrickDuelSide.Bottom
                ? _bottomEffects
                : _topEffects;
            float safeAdditive = float.IsNaN(additiveSeconds) || float.IsInfinity(additiveSeconds)
                ? 0f
                : additiveSeconds;
            float safeMultiplier = float.IsNaN(multiplier) || float.IsInfinity(multiplier)
                ? 1f
                : multiplier;
            effects.SpeedBallDurationAddSeconds = Mathf.Clamp(
                safeAdditive,
                -GetSpeedBallBaseDurationSeconds() +
                BrickDuelItemConstants.SpeedBallDurationSecondsMin,
                BrickDuelItemConstants.SpeedBallDurationSecondsMax);
            effects.SpeedBallDurationMultiplier = Mathf.Clamp(
                safeMultiplier,
                BrickDuelItemConstants.SpeedBallDurationMultiplierMin,
                BrickDuelItemConstants.SpeedBallDurationMultiplierMax);
        }

        public float GetResolvedSpeedBallDurationSeconds(BrickDuelSide side)
        {
            BrickDuelSideItemEffects effects = side == BrickDuelSide.Bottom
                ? _bottomEffects
                : _topEffects;
            float resolved =
                (GetSpeedBallBaseDurationSeconds() +
                 effects.SpeedBallDurationAddSeconds) *
                Mathf.Clamp(
                    effects.SpeedBallDurationMultiplier,
                    BrickDuelItemConstants.SpeedBallDurationMultiplierMin,
                    BrickDuelItemConstants.SpeedBallDurationMultiplierMax);
            return Mathf.Clamp(
                resolved,
                BrickDuelItemConstants.SpeedBallDurationSecondsMin,
                BrickDuelItemConstants.SpeedBallDurationSecondsMax);
        }

        public void ResetToWaiting()
        {
            Phase = BrickDuelPhase.Waiting;
            Result = BrickDuelResult.None;
            IsPaused = false;
            SimulationFrame = 0;
            ElapsedFrames = 0;
            CountdownFramesRemaining = 0;
            BottomCoreHealth = _rule.InitialCoreHealth;
            TopCoreHealth = _rule.InitialCoreHealth;
            _bricks.Clear();
            _splitBalls.Clear();
            _capsules.Clear();
            _logicalRows.Clear();
            _bottomIgnoredBrickIds.Clear();
            _topIgnoredBrickIds.Clear();
            _splitIgnoredBrickIds.Clear();
            _bottomEffects.Clear();
            _topEffects.Clear();
            _bottomPhase?.Reset();
            _topPhase?.Reset();
            ResetPaddles();
            PositionBallForServe(BottomBall, _rule.BallRadius);
            PositionBallForServe(TopBall, _rule.BallRadius);
            BottomBall.IsActive = false;
            TopBall.IsActive = false;
            LastFrameEvents.Clear();
        }

        public void StepFrame(BrickDuelFrameInput input)
        {
            LastFrameEvents.Clear();
            if (Phase == BrickDuelPhase.Waiting || Phase == BrickDuelPhase.Result || IsPaused)
            {
                return;
            }

            SimulationFrame++;
            if (Phase == BrickDuelPhase.Countdown)
            {
                CountdownFramesRemaining = Mathf.Max(0, CountdownFramesRemaining - 1);
                if (CountdownFramesRemaining > 0)
                {
                    return;
                }

                Phase = BrickDuelPhase.Playing;
                ActivateBall(BottomBall, BottomBallRadius);
                ActivateBall(TopBall, TopBallRadius);
            }

            int previousPressureLevel = PressureLevel;
            ElapsedFrames++;
            LastFrameEvents.PressureLevelChanged = PressureLevel != previousPressureLevel;

            Vector2 bottomPaddleStart = BottomPaddle.Position;
            Vector2 topPaddleStart = TopPaddle.Position;
            _bottomPhase?.Tick(SimulationFrame);
            _topPhase?.Tick(SimulationFrame);
            MovePaddle(BottomPaddle, input.BottomMoveAxis, BottomPaddleHalfWidth);
            float topMoveAxis = input.UseTopAi
                ? _aiController.Step(
                    TopBall,
                    _splitBalls,
                    TopPaddle,
                    _bricks,
                    _capsules,
                    _rule.BaseTideSpeed * PressureMultiplier * TopTideSpeedMultiplier,
                    TopPaddleHalfWidth,
                    TopBallRadius)
                : input.TopMoveAxis;
            MovePaddle(TopPaddle, topMoveAxis, TopPaddleHalfWidth);

            if (input.BottomAbilityPressed) ApplyPhaseAbility(BrickDuelSide.Bottom);
            bool topAiAbility = input.UseTopAi && _topPhase != null &&
                                _topPhase.State.AbilityAvailable;
            if (input.TopAbilityPressed || topAiAbility) ApplyPhaseAbility(BrickDuelSide.Top);

            ResolveItemCapsulePickupsAndMisses();

            float bottomTideSpeed = _rule.BaseTideSpeed * PressureMultiplier * BottomTideSpeedMultiplier;
            float topTideSpeed = _rule.BaseTideSpeed * PressureMultiplier * TopTideSpeedMultiplier;
            Vector2 bottomPaddleVelocity =
                (BottomPaddle.Position - bottomPaddleStart) / FrameDelta;
            Vector2 topPaddleVelocity =
                (TopPaddle.Position - topPaddleStart) / FrameDelta;
            _bottomHitBrickIds.Clear();
            _topHitBrickIds.Clear();
            int bottomPierceCharges = _bottomPhase?.State.TotalPierceCharges ?? _bottomEffects.PhaseDrillCharges;
            int topPierceCharges = _topPhase?.State.TotalPierceCharges ?? _topEffects.PhaseDrillCharges;
            if (_bottomPhase == null && !_bottomEffects.HasPhaseDrill)
            {
                bottomPierceCharges = 0;
            }

            if (_topPhase == null && !_topEffects.HasPhaseDrill)
            {
                topPierceCharges = 0;
            }

            int bottomSplits = 0;
            int topSplits = 0;
            var bottomTelemetry = new BrickDuelCollisionFrameTelemetry(
                _bottomPhase == null
                    ? null
                    : (Action<BrickDuelCollisionEvent>)(collisionEvent =>
                    {
                        if (collisionEvent.Pierced) _bottomPhase.ConsumePierce(1);
                        bottomSplits += _bottomPhase.OnMainBallCollisionEvent(collisionEvent);
                    }));
            var topTelemetry = new BrickDuelCollisionFrameTelemetry(
                _topPhase == null
                    ? null
                    : (Action<BrickDuelCollisionEvent>)(collisionEvent =>
                    {
                        if (collisionEvent.Pierced) _topPhase.ConsumePierce(1);
                        topSplits += _topPhase.OnMainBallCollisionEvent(collisionEvent);
                    }));
            StepBall(
                BottomBall,
                BottomPaddle,
                bottomPaddleStart,
                bottomPaddleVelocity,
                bottomTideSpeed,
                BottomPaddleHalfWidth,
                BottomBallRadius,
                ref bottomPierceCharges,
                _bottomIgnoredBrickIds,
                _bottomHitBrickIds,
                bottomTelemetry);
            StepBall(
                TopBall,
                TopPaddle,
                topPaddleStart,
                topPaddleVelocity,
                topTideSpeed,
                TopPaddleHalfWidth,
                TopBallRadius,
                ref topPierceCharges,
                _topIgnoredBrickIds,
                _topHitBrickIds,
                topTelemetry);
            if (_bottomPhase == null)
                _bottomEffects.PhaseDrillCharges = bottomPierceCharges;
            if (_topPhase == null)
                _topEffects.PhaseDrillCharges = topPierceCharges;
            if (_bottomEffects.PhaseDrillCharges <= 0)
            {
                _bottomEffects.PhaseDrillFramesRemaining = 0;
            }

            if (_topEffects.PhaseDrillCharges <= 0)
            {
                _topEffects.PhaseDrillFramesRemaining = 0;
            }

            ApplyBrickHits(BrickDuelSide.Bottom, _bottomHitBrickIds);
            ApplyBrickHits(BrickDuelSide.Top, _topHitBrickIds);
            if (bottomSplits > 0) SpawnSplitBallsFromSide(BrickDuelSide.Bottom, bottomSplits, true);
            if (topSplits > 0) SpawnSplitBallsFromSide(BrickDuelSide.Top, topSplits, true);
            StepSplitBalls(
                bottomPaddleStart,
                topPaddleStart,
                bottomPaddleVelocity,
                topPaddleVelocity,
                bottomTideSpeed,
                topTideSpeed);

            AdvanceBrickTide(BrickDuelSide.Bottom, bottomTideSpeed);
            AdvanceBrickTide(BrickDuelSide.Top, topTideSpeed);
            AdvanceItemCapsules();
            ResolveCoreDamage();
            TickItemEffects();
            TickBreakCounterUpgrades();
            ResolveResult();
        }

        public BrickDuelSnapshot CreateSnapshot()
        {
            return new BrickDuelSnapshot
            {
                Phase = Phase,
                Result = Result,
                IsPaused = IsPaused,
                SimulationFrame = SimulationFrame,
                CountdownFramesRemaining = CountdownFramesRemaining,
                ElapsedFrames = ElapsedFrames,
                BottomCoreHealth = BottomCoreHealth,
                TopCoreHealth = TopCoreHealth,
                PressureLevel = PressureLevel,
                PressureMultiplier = PressureMultiplier,
                FramesUntilPressureIncrease = FramesUntilPressureIncrease,
                BottomDangerDistance = BottomDangerDistance,
                TopDangerDistance = TopDangerDistance,
                BottomPaddleHalfWidth = BottomPaddleHalfWidth,
                TopPaddleHalfWidth = TopPaddleHalfWidth,
                BottomBallRadius = BottomBallRadius,
                TopBallRadius = TopBallRadius,
                BottomPaddle = ClonePaddle(BottomPaddle),
                TopPaddle = ClonePaddle(TopPaddle),
                BottomBall = CloneBall(BottomBall),
                TopBall = CloneBall(TopBall),
                BottomEffects = _bottomEffects.Clone(),
                TopEffects = _topEffects.Clone(),
                BottomPhaseState = _bottomPhase?.State.Clone(),
                TopPhaseState = _topPhase?.State.Clone(),
                Bricks = _bricks
                    .OrderBy(brick => brick.BrickId)
                    .Select(CloneBrick)
                    .ToArray(),
                Capsules = _capsules
                    .OrderBy(capsule => capsule.CapsuleId)
                    .Select(CloneCapsule)
                    .ToArray(),
                SplitBalls = _splitBalls
                    .OrderBy(ball => ball.BallId)
                    .Select(CloneBall)
                    .ToArray(),
            };
        }

        public ulong GetChecksum()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            Hash(ref hash, (int)Phase, prime);
            Hash(ref hash, (int)Result, prime);
            Hash(ref hash, IsPaused ? 1 : 0, prime);
            Hash(ref hash, SimulationFrame, prime);
            Hash(ref hash, CountdownFramesRemaining, prime);
            Hash(ref hash, ElapsedFrames, prime);
            Hash(ref hash, BottomCoreHealth, prime);
            Hash(ref hash, TopCoreHealth, prime);
            Hash(ref hash, _nextBrickId, prime);
            Hash(ref hash, _nextBallId, prime);
            Hash(ref hash, _nextCapsuleId, prime);
            Hash(ref hash, _nextLogicalRowId, prime);
            Hash(ref hash, _bottomNextRowId, prime);
            Hash(ref hash, _topNextRowId, prime);
            Hash(ref hash, Quantize(_bottomRowTravelSinceSpawn), prime);
            Hash(ref hash, Quantize(_topRowTravelSinceSpawn), prime);
            Hash(ref hash, _bottomBreakCounter, prime);
            Hash(ref hash, _topBreakCounter, prime);
            Hash(ref hash, _bottomPendingUpgradeFrames, prime);
            Hash(ref hash, _topPendingUpgradeFrames, prime);
            Hash(ref hash, _bottomPendingRowUpgrades, prime);
            Hash(ref hash, _topPendingRowUpgrades, prime);
            Hash(ref hash, unchecked((int)_rowRandom.State), prime);
            Hash(ref hash, unchecked((int)(_itemBag?.RandomState ?? 0u)), prime);
            if (_itemBag == null)
            {
                Hash(ref hash, 0, prime);
            }
            else
            {
                Hash(ref hash, 1, prime);
                IReadOnlyList<string> remainingItems = _itemBag.RemainingItemIds;
                Hash(ref hash, remainingItems.Count, prime);
                for (int i = 0; i < remainingItems.Count; i++)
                {
                    HashString(ref hash, remainingItems[i], prime);
                }
            }
            HashIntSet(ref hash, _bottomIgnoredBrickIds, prime);
            HashIntSet(ref hash, _topIgnoredBrickIds, prime);
            BrickDuelBallState[] orderedSplitBalls = _splitBalls
                .OrderBy(item => item.BallId)
                .ToArray();
            Hash(ref hash, orderedSplitBalls.Length, prime);
            for (int i = 0; i < orderedSplitBalls.Length; i++)
            {
                BrickDuelBallState splitBall = orderedSplitBalls[i];
                Hash(ref hash, splitBall.BallId, prime);
                _splitIgnoredBrickIds.TryGetValue(
                    splitBall.BallId,
                    out HashSet<int> ignoredBrickIds);
                HashIntSet(ref hash, ignoredBrickIds, prime);
            }
            Hash(ref hash, _logicalRows.Count, prime);
            foreach (KeyValuePair<int, LogicalRow> pair in _logicalRows.OrderBy(item => item.Key))
            {
                Hash(ref hash, pair.Key, prime);
                BrickDuelBrickType[] types = pair.Value.Types ?? Array.Empty<BrickDuelBrickType>();
                string[] itemIds = pair.Value.ItemIds ?? Array.Empty<string>();
                Hash(ref hash, types.Length, prime);
                for (int i = 0; i < types.Length; i++)
                {
                    Hash(ref hash, (int)types[i], prime);
                }
                Hash(ref hash, itemIds.Length, prime);
                for (int i = 0; i < itemIds.Length; i++)
                {
                    HashString(ref hash, itemIds[i], prime);
                }
            }
            Hash(ref hash, _aiController.FramesUntilDecision, prime);
            Hash(ref hash, _aiController.CurrentTargetBrickId, prime);
            Hash(ref hash, _aiController.CurrentTargetTier, prime);
            Hash(ref hash, _aiController.PlannedBallId, prime);
            Hash(ref hash, (int)_aiController.CurrentBehavior, prime);
            Hash(ref hash, Quantize(_aiController.TargetX), prime);
            Hash(ref hash, _aiController.PlannedWallBounces, prime);
            HashBall(ref hash, BottomBall, prime);
            HashBall(ref hash, TopBall, prime);
            foreach (BrickDuelBallState splitBall in _splitBalls.OrderBy(item => item.BallId))
            {
                HashBall(ref hash, splitBall, prime);
            }
            Hash(ref hash, Quantize(BottomPaddle.Position.x), prime);
            Hash(ref hash, Quantize(TopPaddle.Position.x), prime);
            HashEffects(ref hash, _bottomEffects, prime);
            HashEffects(ref hash, _topEffects, prime);
            HashPhaseState(ref hash, _bottomPhase?.State, prime);
            HashPhaseState(ref hash, _topPhase?.State, prime);
            foreach (BrickDuelBrickState brick in _bricks.OrderBy(item => item.BrickId))
            {
                Hash(ref hash, brick.BrickId, prime);
                Hash(ref hash, (int)brick.Side, prime);
                Hash(ref hash, (int)brick.InitialType, prime);
                Hash(ref hash, brick.Health, prime);
                Hash(ref hash, brick.ColumnId, prime);
                Hash(ref hash, brick.LogicalRowId, prime);
                Hash(ref hash, Quantize(brick.Position.x), prime);
                Hash(ref hash, Quantize(brick.Position.y), prime);
                HashString(ref hash, brick.ItemId, prime);
            }

            foreach (BrickDuelItemCapsuleState capsule in _capsules.OrderBy(item => item.CapsuleId))
            {
                Hash(ref hash, capsule.CapsuleId, prime);
                Hash(ref hash, (int)capsule.Side, prime);
                Hash(ref hash, capsule.SpawnFrame, prime);
                Hash(ref hash, Quantize(capsule.Position.x), prime);
                Hash(ref hash, Quantize(capsule.Position.y), prime);
                HashString(ref hash, capsule.ItemId, prime);
            }

            return hash;
        }

        private void ResetPaddles()
        {
            BottomPaddle.Position = new Vector2(0f, -_rule.PaddleSpawnY);
            BottomPaddle.MoveAxis = 0f;
            TopPaddle.Position = new Vector2(0f, _rule.PaddleSpawnY);
            TopPaddle.MoveAxis = 0f;
        }

        private void MovePaddle(BrickDuelPaddleState paddle, float moveAxis, float paddleHalfWidth)
        {
            paddle.MoveAxis = Mathf.Clamp(moveAxis, -1f, 1f);
            float limit = Mathf.Max(0f, _rule.ArenaHalfWidth - paddleHalfWidth);
            float nextX = paddle.Position.x + paddle.MoveAxis * _rule.PaddleMoveSpeed * FrameDelta;
            paddle.Position = new Vector2(Mathf.Clamp(nextX, -limit, limit), paddle.Position.y);
        }

        private void StepBall(
            BrickDuelBallState ball,
            BrickDuelPaddleState paddle,
            Vector2 paddleStartPosition,
            Vector2 paddleVelocity,
            float tideSpeed,
            float paddleHalfWidth,
            float ballRadius,
            ref int pierceCharges,
            ISet<int> ignoredBrickIds,
            ISet<int> hitBrickIds,
            BrickDuelCollisionFrameTelemetry telemetry)
        {
            if (!ball.IsActive)
            {
                if (ball.ResetFramesRemaining > 0)
                {
                    ball.ResetFramesRemaining--;
                }

                if (ball.ResetFramesRemaining <= 0)
                {
                    ActivateBall(ball, ballRadius);
                }
                return;
            }

            BrickDuelCollisionSolver.RefreshIgnoredBrickContacts(
                ball,
                _bricks,
                _rule,
                ballRadius,
                ignoredBrickIds);

            Vector2 previous = ball.Position;
            _collisionSolver.StepBall(
                ball,
                paddle,
                paddleStartPosition,
                paddleVelocity,
                _bricks,
                _rule,
                FrameDelta,
                tideSpeed,
                paddleHalfWidth,
                ballRadius,
                ref pierceCharges,
                ignoredBrickIds,
                hitBrickIds,
                GetBallSpeed(ball.Side),
                GetAimedReboundTarget(ball.Side),
                telemetry,
                GetRefractMirrorBarrier(ball.Side),
                GetBallSpeed(ball.Side, forPaddleBounce: true),
                () => GetBallSpeed(ball.Side, forPaddleBounce: true));
            Vector2 displacement = ball.Position - previous;
            if (displacement.sqrMagnitude <
                _rule.StuckMovementEpsilon * _rule.StuckMovementEpsilon * FrameDelta * FrameDelta)
            {
                ball.StuckFrames++;
            }
            else
            {
                ball.StuckFrames = 0;
            }

            int stuckFrameLimit = Mathf.Max(
                1,
                Mathf.RoundToInt(_rule.StuckTimeoutSeconds * _rule.SimulationFps));
            if (ball.StuckFrames >= stuckFrameLimit)
            {
                BeginBallReset(ball, ballRadius);
            }
        }

        private void StepSplitBalls(
            Vector2 bottomPaddleStart,
            Vector2 topPaddleStart,
            Vector2 bottomPaddleVelocity,
            Vector2 topPaddleVelocity,
            float bottomTideSpeed,
            float topTideSpeed)
        {
            if (_splitBalls.Count == 0)
            {
                return;
            }

            int stuckFrameLimit = Mathf.Max(
                1,
                Mathf.RoundToInt(_rule.StuckTimeoutSeconds * _rule.SimulationFps));
            var expiredBallIds = new List<int>();
            for (int i = 0; i < _splitBalls.Count; i++)
            {
                BrickDuelBallState ball = _splitBalls[i];
                if (!ball.IsActive)
                {
                    expiredBallIds.Add(ball.BallId);
                    continue;
                }

                if (!_splitIgnoredBrickIds.TryGetValue(ball.BallId, out HashSet<int> ignoredBrickIds))
                {
                    ignoredBrickIds = new HashSet<int>();
                    _splitIgnoredBrickIds[ball.BallId] = ignoredBrickIds;
                }

                _splitHitBrickIds.Clear();
                int pierceCharges = 0;
                bool isBottom = ball.Side == BrickDuelSide.Bottom;
                float ballRadius = isBottom ? BottomBallRadius : TopBallRadius;
                Vector2 previous = ball.Position;
                BrickDuelCollisionSolver.RefreshIgnoredBrickContacts(
                    ball,
                    _bricks,
                    _rule,
                    ballRadius,
                    ignoredBrickIds);
                _collisionSolver.StepBall(
                    ball,
                    isBottom ? BottomPaddle : TopPaddle,
                    isBottom ? bottomPaddleStart : topPaddleStart,
                    isBottom ? bottomPaddleVelocity : topPaddleVelocity,
                    _bricks,
                    _rule,
                    FrameDelta,
                    isBottom ? bottomTideSpeed : topTideSpeed,
                    isBottom ? BottomPaddleHalfWidth : TopPaddleHalfWidth,
                    ballRadius,
                    ref pierceCharges,
                    ignoredBrickIds,
                    _splitHitBrickIds,
                    GetBallSpeed(ball.Side),
                    GetAimedReboundTarget(ball.Side),
                    horizontalBarrier: GetRefractMirrorBarrier(ball.Side));

                Vector2 displacement = ball.Position - previous;
                if (displacement.sqrMagnitude <
                    _rule.StuckMovementEpsilon * _rule.StuckMovementEpsilon * FrameDelta * FrameDelta)
                {
                    ball.StuckFrames++;
                }
                else
                {
                    ball.StuckFrames = 0;
                }

                if (_splitHitBrickIds.Count > 0)
                {
                    ApplyBrickHits(ball.Side, _splitHitBrickIds);
                }

                ball.RemainingLifetimeFrames--;
                if (ball.RemainingLifetimeFrames <= 0 || ball.StuckFrames >= stuckFrameLimit)
                {
                    expiredBallIds.Add(ball.BallId);
                }
            }

            for (int i = 0; i < expiredBallIds.Count; i++)
            {
                RemoveSplitBall(expiredBallIds[i]);
            }
        }

        private void SpawnSplitBallsFromSide(
            BrickDuelSide side,
            int requestedCount = 1,
            bool mainBallOnly = true)
        {
            var sources = new List<(BrickDuelBallState Ball, float Radius)>();
            BrickDuelBallState mother = side == BrickDuelSide.Bottom ? BottomBall : TopBall;
            if (mother.IsActive)
            {
                sources.Add((
                    mother,
                    side == BrickDuelSide.Bottom ? BottomBallRadius : TopBallRadius));
            }

            for (int i = 0; !mainBallOnly && i < _splitBalls.Count; i++)
            {
                BrickDuelBallState existing = _splitBalls[i];
                if (existing.Side == side && existing.IsActive)
                {
                    sources.Add((existing, side == BrickDuelSide.Bottom
                        ? BottomBallRadius
                        : TopBallRadius));
                }
            }

            float newRadius = side == BrickDuelSide.Bottom
                ? BottomBallRadius
                : TopBallRadius;
            float angleRadians =
                BrickDuelItemConstants.SplitBallSpawnAngleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRadians);
            float sin = Mathf.Sin(angleRadians);
            int spawned = 0;
            int spawnTarget = mainBallOnly ? requestedCount : Math.Min(requestedCount, sources.Count);
            while (spawned < spawnTarget && sources.Count > 0)
            {
                int sourceIndex = mainBallOnly ? 0 : spawned;
                BrickDuelBallState source = sources[sourceIndex].Ball;
                float sourceRadius = sources[sourceIndex].Radius;
                Vector2 sourceDirection = source.Velocity.sqrMagnitude > 0.0001f
                    ? source.Velocity.normalized
                    : new Vector2(0f, side == BrickDuelSide.Bottom ? 1f : -1f);
                float directionSign = mainBallOnly && (spawned & 1) == 1 ? -1f : 1f;
                float spawnSin = sin * directionSign;
                Vector2 spawnDirection = new Vector2(
                    sourceDirection.x * cos - sourceDirection.y * spawnSin,
                    sourceDirection.x * spawnSin + sourceDirection.y * cos);
                if (spawnDirection.sqrMagnitude <= 0.0001f)
                {
                    spawnDirection = sourceDirection;
                }
                else
                {
                    spawnDirection = spawnDirection.normalized;
                }

                float separation =
                    sourceRadius +
                    newRadius +
                    BrickDuelItemConstants.SplitBallSpawnSeparation;
                var splitBall = new BrickDuelBallState
                {
                    BallId = _nextBallId++,
                    Side = side,
                    Position = source.Position + spawnDirection * separation,
                    Velocity = spawnDirection * GetBallSpeed(side),
                    IsActive = true,
                    IsSplit = true,
                    RemainingLifetimeFrames = GetSplitBallLifetimeFrames(side),
                };
                BrickDuelCollisionSolver.SeparateBallFromBricksAndWalls(
                    splitBall,
                    _bricks,
                    _rule,
                    newRadius,
                    GetBallSpeed(side));
                _splitBalls.Add(splitBall);
                _splitIgnoredBrickIds[splitBall.BallId] = new HashSet<int>();
                spawned++;
            }
        }

        private void RemoveSplitBall(int ballId)
        {
            for (int i = 0; i < _splitBalls.Count; i++)
            {
                if (_splitBalls[i].BallId != ballId)
                {
                    continue;
                }

                _splitBalls.RemoveAt(i);
                _splitIgnoredBrickIds.Remove(ballId);
                return;
            }
        }

        private int GetSplitBallLifetimeFrames(BrickDuelSide side)
        {
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            float seconds = phase != null
                ? phase.GetSplitLifetimeSeconds(GetPhaseItem(BrickDuelItemIds.Split))
                : BrickDuelItemConstants.SplitBallLifetimeSeconds;
            return Mathf.Max(
                1,
                Mathf.RoundToInt(seconds * _rule.SimulationFps));
        }

        private void BeginBallReset(BrickDuelBallState ball, float ballRadius)
        {
            PositionBallForServe(ball, ballRadius);
            ball.IsActive = false;
            ball.ResetFramesRemaining = Mathf.Max(
                1,
                Mathf.RoundToInt(_rule.BallResetSeconds * _rule.SimulationFps));
            ball.StuckFrames = 0;
            if (ball.Side == BrickDuelSide.Bottom)
            {
                _bottomIgnoredBrickIds.Clear();
                LastFrameEvents.BottomBallReset = true;
            }
            else
            {
                _topIgnoredBrickIds.Clear();
                LastFrameEvents.TopBallReset = true;
            }
        }

        private void PositionBallForServe(BrickDuelBallState ball, float ballRadius)
        {
            float direction = ball.Side == BrickDuelSide.Bottom ? 1f : -1f;
            float paddleY = ball.Side == BrickDuelSide.Bottom
                ? -_rule.PaddleSpawnY
                : _rule.PaddleSpawnY;
            float offset = _rule.PaddleHalfHeight + ballRadius + 0.02f;
            ball.Position = new Vector2(0f, paddleY + direction * offset);
            ball.Velocity = new Vector2(0f, direction * GetBallSpeed(ball.Side));
            ball.ResetFramesRemaining = 0;
            ball.StuckFrames = 0;
        }

        private void ActivateBall(BrickDuelBallState ball, float ballRadius)
        {
            PositionBallForServe(ball, ballRadius);
            ball.IsActive = true;
        }

        private void AdvanceBrickTide(BrickDuelSide side, float tideSpeed)
        {
            float distance = tideSpeed * FrameDelta;
            for (int i = 0; i < _bricks.Count; i++)
            {
                BrickDuelBrickState brick = _bricks[i];
                if (brick.Side != side)
                {
                    continue;
                }

                float direction = side == BrickDuelSide.Bottom ? -1f : 1f;
                brick.Position += new Vector2(0f, direction * distance);
            }

            if (side == BrickDuelSide.Bottom)
            {
                _bottomRowTravelSinceSpawn += distance;
                while (_bottomRowTravelSinceSpawn + 0.000001f >= _rule.BrickHeight)
                {
                    _bottomRowTravelSinceSpawn -= _rule.BrickHeight;
                    SpawnRowForSide(BrickDuelSide.Bottom, _bottomNextRowId++, 0);
                }
            }
            else
            {
                _topRowTravelSinceSpawn += distance;
                while (_topRowTravelSinceSpawn + 0.000001f >= _rule.BrickHeight)
                {
                    _topRowTravelSinceSpawn -= _rule.BrickHeight;
                    SpawnRowForSide(BrickDuelSide.Top, _topNextRowId++, 0);
                }
            }
        }

        private void AdvanceItemCapsules()
        {
            float dropSpeed = _rule.BallSpeed * BrickDuelItemConstants.ItemDropSpeedFactor;
            float distance = dropSpeed * FrameDelta;
            for (int i = 0; i < _capsules.Count; i++)
            {
                BrickDuelItemCapsuleState capsule = _capsules[i];
                float direction = capsule.Side == BrickDuelSide.Bottom ? -1f : 1f;
                capsule.Position += new Vector2(0f, direction * distance);
            }
        }

        private void ResolveItemCapsulePickupsAndMisses()
        {
            if (_capsules.Count == 0)
            {
                return;
            }

            var collected = new List<BrickDuelItemCapsuleState>();
            var missed = new List<int>();
            for (int i = 0; i < _capsules.Count; i++)
            {
                BrickDuelItemCapsuleState capsule = _capsules[i];
                BrickDuelPaddleState paddle = capsule.Side == BrickDuelSide.Bottom
                    ? BottomPaddle
                    : TopPaddle;
                float paddleHalfWidth = capsule.Side == BrickDuelSide.Bottom
                    ? BottomPaddleHalfWidth
                    : TopPaddleHalfWidth;
                BrickDuelSideItemEffects sideEffects = capsule.Side == BrickDuelSide.Bottom
                    ? _bottomEffects
                    : _topEffects;
                if (sideEffects.MagnetPullRemaining > 0)
                {
                    sideEffects.MagnetPullRemaining--;
                    collected.Add(capsule);
                    continue;
                }
                if (OverlapsPaddle(capsule, paddle, paddleHalfWidth))
                {
                    collected.Add(capsule);
                    continue;
                }

                bool crossedCore = capsule.Side == BrickDuelSide.Bottom
                    ? capsule.Position.y - BrickDuelItemConstants.CapsuleHalfHeight <= -_rule.CoreLineY
                    : capsule.Position.y + BrickDuelItemConstants.CapsuleHalfHeight >= _rule.CoreLineY;
                if (crossedCore)
                {
                    missed.Add(capsule.CapsuleId);
                }
            }

            for (int i = 0; i < collected.Count; i++)
            {
                BrickDuelItemCapsuleState capsule = collected[i];
                ApplyItemPickup(capsule);
                LastFrameEvents.AddCollected(capsule);
                RemoveCapsule(capsule.CapsuleId);
            }

            for (int i = 0; i < missed.Count; i++)
            {
                int capsuleId = missed[i];
                LastFrameEvents.AddMissed(capsuleId);
                RemoveCapsule(capsuleId);
            }
        }

        private bool OverlapsPaddle(
            BrickDuelItemCapsuleState capsule,
            BrickDuelPaddleState paddle,
            float paddleHalfWidth)
        {
            float dx = Mathf.Abs(capsule.Position.x - paddle.Position.x);
            float dy = Mathf.Abs(capsule.Position.y - paddle.Position.y);
            return dx <= paddleHalfWidth + BrickDuelItemConstants.CapsuleHalfWidth &&
                   dy <= _rule.PaddleHalfHeight + BrickDuelItemConstants.CapsuleHalfHeight;
        }

        private void ApplyItemPickup(BrickDuelItemCapsuleState capsule)
        {
            BrickDuelSideItemEffects effects = capsule.Side == BrickDuelSide.Bottom
                ? _bottomEffects
                : _topEffects;
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(capsule.Side);
            phase?.OnItemCollected();
            bool hadLargeBall = effects.HasLargeBall;
            switch (capsule.ItemId)
            {
                case BrickDuelItemIds.Wide:
                case BrickDuelItemIds.WidePaddle:
                    effects.WidePaddleFramesRemaining = SecondsToFrames(
                        GetPhaseRawFloat(capsule.Side, BrickDuelItemIds.Wide, "DurationSeconds", GetWidePaddleDurationSeconds()));
                    ClampPaddleInsideArena(
                        capsule.Side == BrickDuelSide.Bottom ? BottomPaddle : TopPaddle,
                        GetPaddleHalfWidth(effects));
                    break;
                case BrickDuelItemIds.Large:
                case BrickDuelItemIds.LargeBall:
                    effects.LargeBallFramesRemaining = SecondsToFrames(
                        GetPhaseRawFloat(capsule.Side, BrickDuelItemIds.Large, "DurationSeconds", BrickDuelItemConstants.LargeBallDurationSeconds));
                    if (!hadLargeBall)
                    {
                        SeparateSideBallsFromBricksAndWalls(
                            capsule.Side,
                            GetBallRadius(effects));
                    }
                    break;
                case BrickDuelItemIds.Pierce:
                    int phaseGrant = GetPhaseInt(capsule.Side, BrickDuelItemIds.Pierce, "PierceCharges", 2);
                    if (phase != null)
                        phase.AddItemPierce(phaseGrant);
                    else
                        effects.PhaseDrillCharges = Mathf.Max(effects.PhaseDrillCharges, phaseGrant);
                    effects.PhaseDrillFramesRemaining = int.MaxValue;
                    break;
                case BrickDuelItemIds.PhaseDrill:
                    effects.PhaseDrillCharges = Mathf.Min(
                        BrickDuelItemConstants.PhaseDrillMaxCharges,
                        effects.PhaseDrillCharges + BrickDuelItemConstants.PhaseDrillGrantCharges);
                    effects.PhaseDrillFramesRemaining = SecondsToFrames(
                        BrickDuelItemConstants.PhaseDrillDurationSeconds);
                    break;
                case BrickDuelItemIds.Split:
                    SpawnSplitBallsFromSide(
                        capsule.Side,
                        GetPhaseInt(capsule.Side, BrickDuelItemIds.Split, "TempBallCount", 1),
                        true);
                    break;
                case BrickDuelItemIds.SplitBall:
                    SpawnSplitBallsFromSide(capsule.Side);
                    break;
                case BrickDuelItemIds.Speed:
                case BrickDuelItemIds.SpeedBall:
                    effects.SpeedBallFramesRemaining = SecondsToFrames(
                        GetPhaseRawFloat(capsule.Side, BrickDuelItemIds.Speed, "DurationSeconds", GetResolvedSpeedBallDurationSeconds(capsule.Side)));
                    NormalizeBallSpeeds(capsule.Side, GetBallSpeed(capsule.Side));
                    break;
                case BrickDuelItemIds.Aimed:
                case BrickDuelItemIds.AimedRebound:
                    effects.AimedReboundFramesRemaining = SecondsToFrames(
                        GetPhaseRawFloat(capsule.Side, BrickDuelItemIds.Aimed, "DurationSeconds", GetAimedReboundDurationSeconds()));
                    break;
                case BrickDuelItemIds.Damp:
                case BrickDuelItemIds.DampingPulse:
                    effects.DampingFramesRemaining = SecondsToFrames(
                        GetPhaseRawFloat(capsule.Side, BrickDuelItemIds.Damp, "DurationSeconds", BrickDuelItemConstants.DampingDurationSeconds));
                    break;
                case BrickDuelItemIds.Magnet:
                    effects.MagnetPullRemaining += GetPhaseInt(capsule.Side, BrickDuelItemIds.Magnet, "PullNextItemCount", 1);
                    break;
                case BrickDuelItemIds.Buffer:
                case BrickDuelItemIds.CoreBuffer:
                    effects.HasCoreBuffer = true;
                    effects.CoreBufferFramesRemaining = SecondsToFrames(
                        GetPhaseRawFloat(capsule.Side, BrickDuelItemIds.Buffer, "DurationSeconds", BrickDuelItemConstants.CoreBufferDurationSeconds));
                    break;
            }
        }

        private void TickItemEffects()
        {
            TickSideEffects(_bottomEffects, BrickDuelSide.Bottom);
            TickSideEffects(_topEffects, BrickDuelSide.Top);
        }

        private void TickSideEffects(BrickDuelSideItemEffects effects, BrickDuelSide side)
        {
            bool hadWide = effects.HasWidePaddle;
            bool hadLarge = effects.HasLargeBall;
            bool hadSpeedBall = effects.HasSpeedBall;
            if (effects.WidePaddleFramesRemaining > 0)
            {
                effects.WidePaddleFramesRemaining--;
            }

            if (effects.LargeBallFramesRemaining > 0)
            {
                effects.LargeBallFramesRemaining--;
            }

            if (effects.PhaseDrillFramesRemaining > 0)
            {
                effects.PhaseDrillFramesRemaining--;
                if (effects.PhaseDrillFramesRemaining <= 0)
                {
                    effects.PhaseDrillCharges = 0;
                }
            }

            if (effects.SpeedBallFramesRemaining > 0)
            {
                effects.SpeedBallFramesRemaining--;
            }

            if (effects.AimedReboundFramesRemaining > 0)
            {
                effects.AimedReboundFramesRemaining--;
            }

            if (effects.DampingFramesRemaining > 0)
            {
                effects.DampingFramesRemaining--;
            }

            if (effects.CoreBufferFramesRemaining > 0)
            {
                effects.CoreBufferFramesRemaining--;
                if (effects.CoreBufferFramesRemaining <= 0)
                {
                    effects.HasCoreBuffer = false;
                }
            }

            if (hadWide && !effects.HasWidePaddle)
            {
                BrickDuelPaddleState paddle = side == BrickDuelSide.Bottom ? BottomPaddle : TopPaddle;
                ClampPaddleInsideArena(paddle, GetPaddleHalfWidth(effects));
            }

            if (hadLarge && !effects.HasLargeBall)
            {
                SeparateSideBallsFromBricksAndWalls(side, GetBallRadius(effects));
            }

            if (hadSpeedBall && !effects.HasSpeedBall)
            {
                NormalizeBallSpeeds(side, GetBallSpeed(side));
            }
        }

        private void ResolveCoreDamage()
        {
            var bottomHits = new List<BrickDuelBrickState>();
            var topHits = new List<BrickDuelBrickState>();
            float halfHeight = _rule.BrickHeight * 0.5f;
            for (int i = _bricks.Count - 1; i >= 0; i--)
            {
                BrickDuelBrickState brick = _bricks[i];
                bool reachedCore = brick.Side == BrickDuelSide.Bottom
                    ? brick.Position.y - halfHeight <= -_rule.CoreLineY
                    : brick.Position.y + halfHeight >= _rule.CoreLineY;
                if (!reachedCore)
                {
                    continue;
                }

                if (brick.Side == BrickDuelSide.Bottom)
                {
                    bottomHits.Add(brick);
                }
                else
                {
                    topHits.Add(brick);
                }
                _bricks.RemoveAt(i);
            }

            ApplyCoreHits(BrickDuelSide.Bottom, bottomHits, _bottomEffects);
            ApplyCoreHits(BrickDuelSide.Top, topHits, _topEffects);
        }

        private void ApplyCoreHits(
            BrickDuelSide side,
            List<BrickDuelBrickState> hits,
            BrickDuelSideItemEffects effects)
        {
            if (hits.Count == 0)
            {
                return;
            }

            hits.Sort((left, right) =>
            {
                int columnCompare = left.ColumnId.CompareTo(right.ColumnId);
                return columnCompare != 0
                    ? columnCompare
                    : left.BrickId.CompareTo(right.BrickId);
            });

            int damage = 0;
            int absorbed = 0;
            for (int i = 0; i < hits.Count; i++)
            {
                int hitDamage = _rule.BrickCoreDamage;
                if (effects.HasCoreBuffer && hitDamage > 0)
                {
                    int blocked = Mathf.Min(hitDamage, BrickDuelItemConstants.CoreBufferMaxLayers);
                    hitDamage -= blocked;
                    absorbed += blocked;
                    effects.HasCoreBuffer = false;
                    effects.CoreBufferFramesRemaining = 0;
                }

                damage += hitDamage;
            }

            if (side == BrickDuelSide.Bottom)
            {
                if (damage > 0)
                {
                    BottomCoreHealth = Mathf.Max(0, BottomCoreHealth - damage);
                }

                LastFrameEvents.BottomCoreDamage = damage;
                LastFrameEvents.BottomCoreDamageAbsorbed = absorbed;
            }
            else
            {
                if (damage > 0)
                {
                    TopCoreHealth = Mathf.Max(0, TopCoreHealth - damage);
                }

                LastFrameEvents.TopCoreDamage = damage;
                LastFrameEvents.TopCoreDamageAbsorbed = absorbed;
            }
        }

        private void ResolveResult()
        {
            if (BottomCoreHealth > 0 && TopCoreHealth > 0)
            {
                return;
            }

            if (BottomCoreHealth <= 0 && TopCoreHealth <= 0)
            {
                Result = BrickDuelResult.Draw;
            }
            else if (TopCoreHealth <= 0)
            {
                Result = BrickDuelResult.PlayerWin;
            }
            else
            {
                Result = BrickDuelResult.PlayerLose;
            }

            Phase = BrickDuelPhase.Result;
            IsPaused = false;
            BottomBall.IsActive = false;
            TopBall.IsActive = false;
            _splitBalls.Clear();
            _splitIgnoredBrickIds.Clear();
            _capsules.Clear();
            _bottomEffects.Clear();
            _topEffects.Clear();
        }

        private void ApplyBrickHits(BrickDuelSide side, ISet<int> brickIds)
        {
            if (brickIds.Count == 0)
            {
                return;
            }

            for (int i = _bricks.Count - 1; i >= 0; i--)
            {
                BrickDuelBrickState brick = _bricks[i];
                if (brick.Side != side || !brickIds.Contains(brick.BrickId))
                {
                    continue;
                }

                brick.Health = Mathf.Max(0, brick.Health - 1);
                if (brick.Health == 0)
                {
                    LastFrameEvents.AddDestroyed(brick);
                    GetPhaseRuntime(side)?.OnBrickDestroyed(brick.InitialType);
                    RegisterBreakCounter(side);
                    if (brick.InitialType == BrickDuelBrickType.Mystery &&
                        !string.IsNullOrEmpty(brick.ItemId))
                    {
                        SpawnItemCapsule(brick);
                    }

                    _bricks.RemoveAt(i);
                }
            }
        }

        private void SpawnItemCapsule(BrickDuelBrickState brick)
        {
            EnforceCapsuleCap(brick.Side);
            var capsule = new BrickDuelItemCapsuleState
            {
                CapsuleId = _nextCapsuleId++,
                Side = brick.Side,
                ItemId = brick.ItemId,
                Position = brick.Position,
                SpawnFrame = SimulationFrame,
            };
            _capsules.Add(capsule);
            LastFrameEvents.AddSpawnedCapsule(capsule);
        }

        private void EnforceCapsuleCap(BrickDuelSide side)
        {
            int count = 0;
            int oldestId = int.MaxValue;
            int oldestIndex = -1;
            for (int i = 0; i < _capsules.Count; i++)
            {
                BrickDuelItemCapsuleState capsule = _capsules[i];
                if (capsule.Side != side)
                {
                    continue;
                }

                count++;
                if (capsule.CapsuleId < oldestId)
                {
                    oldestId = capsule.CapsuleId;
                    oldestIndex = i;
                }
            }

            if (count < BrickDuelItemConstants.MaxCapsulesPerSide || oldestIndex < 0)
            {
                return;
            }

            LastFrameEvents.AddExpired(_capsules[oldestIndex].CapsuleId);
            _capsules.RemoveAt(oldestIndex);
        }

        private void RemoveCapsule(int capsuleId)
        {
            for (int i = 0; i < _capsules.Count; i++)
            {
                if (_capsules[i].CapsuleId == capsuleId)
                {
                    _capsules.RemoveAt(i);
                    return;
                }
            }
        }

        private void SpawnInitialRows()
        {
            for (int rowIndex = 0; rowIndex < _rule.InitialRows; rowIndex++)
            {
                int logicalRowId = _nextLogicalRowId++;
                LogicalRow row = CreateLogicalRow();
                _logicalRows[logicalRowId] = row;
                SpawnRowForSide(BrickDuelSide.Bottom, logicalRowId, rowIndex, reuseExisting: true);
                SpawnRowForSide(BrickDuelSide.Top, logicalRowId, rowIndex, reuseExisting: true);
                _bottomNextRowId = Mathf.Max(_bottomNextRowId, logicalRowId + 1);
                _topNextRowId = Mathf.Max(_topNextRowId, logicalRowId + 1);
            }
        }

        private void SpawnRowForSide(
            BrickDuelSide side,
            int logicalRowId,
            int visualRowIndex,
            bool reuseExisting = false)
        {
            LogicalRow row;
            if (reuseExisting)
            {
                row = _logicalRows[logicalRowId];
            }
            else if (!_logicalRows.TryGetValue(logicalRowId, out row))
            {
                row = CreateLogicalRow();
                _logicalRows[logicalRowId] = row;
                _nextLogicalRowId = Mathf.Max(_nextLogicalRowId, logicalRowId + 1);
            }

            row = ApplyPendingRowUpgrade(side, row);

            float startX = -(_rule.Columns - 1) * _rule.BrickWidth * 0.5f;
            float y = (visualRowIndex + 0.5f) * _rule.BrickHeight;
            for (int column = 0; column < _rule.Columns; column++)
            {
                BrickDuelBrickType type = row.Types[column];
                string itemId = row.ItemIds[column];
                float x = startX + column * _rule.BrickWidth;
                float signedY = side == BrickDuelSide.Bottom ? -y : y;
                AddBrick(side, type, new Vector2(x, signedY), column, logicalRowId, itemId);
            }
        }

        private LogicalRow CreateLogicalRow()
        {
            BrickDuelBrickType[] types = GenerateWeightedRow();
            var itemIds = new string[_rule.Columns];
            for (int i = 0; i < types.Length; i++)
            {
                itemIds[i] = types[i] == BrickDuelBrickType.Mystery
                    ? _itemBag.NextItemId()
                    : null;
            }

            return new LogicalRow(types, itemIds);
        }

        private BrickDuelCompositionStageDefinition ResolveCompositionWeights(float elapsedSeconds)
        {
            if (_phaseCurve?.Stages == null || _phaseCurve.Stages.Count == 0)
                return _rule.ResolveBrickCompositionWeights(elapsedSeconds);
            PhaseCurveStageDefinition stage = _phaseCurve.Stages
                .Where(item => item != null && item.TimeStart <= elapsedSeconds)
                .OrderBy(item => item.TimeStart)
                .LastOrDefault() ?? _phaseCurve.Stages[0];
            return new BrickDuelCompositionStageDefinition
            {
                GreenWeight = stage.GreenWeight,
                RedWeight = stage.RedWeight,
                YellowWeight = stage.YellowWeight,
                MysteryWeight = stage.MysteryWeight,
            };
        }

        private void RegisterBreakCounter(BrickDuelSide scoringSide)
        {
            int threshold = Math.Max(1, _phaseCurve?.BreakCounterThreshold ?? int.MaxValue);
            if (threshold == int.MaxValue) return;
            if (scoringSide == BrickDuelSide.Bottom)
            {
                _bottomBreakCounter++;
                if (_bottomBreakCounter < threshold) return;
                _bottomBreakCounter -= threshold;
                _topPendingRowUpgrades++;
                _topPendingUpgradeFrames = Math.Max(
                    _topPendingUpgradeFrames,
                    SecondsToFrames(_phaseCurve.BreakCounterWarnSeconds));
                LastFrameEvents.BottomBreakCounterTriggered = true;
            }
            else
            {
                _topBreakCounter++;
                if (_topBreakCounter < threshold) return;
                _topBreakCounter -= threshold;
                _bottomPendingRowUpgrades++;
                _bottomPendingUpgradeFrames = Math.Max(
                    _bottomPendingUpgradeFrames,
                    SecondsToFrames(_phaseCurve.BreakCounterWarnSeconds));
                LastFrameEvents.TopBreakCounterTriggered = true;
            }
        }

        private void TickBreakCounterUpgrades()
        {
            if (_bottomPendingUpgradeFrames > 0) _bottomPendingUpgradeFrames--;
            if (_topPendingUpgradeFrames > 0) _topPendingUpgradeFrames--;
        }

        private LogicalRow ApplyPendingRowUpgrade(BrickDuelSide side, LogicalRow row)
        {
            int pending = side == BrickDuelSide.Bottom
                ? _bottomPendingRowUpgrades
                : _topPendingRowUpgrades;
            int warningFrames = side == BrickDuelSide.Bottom
                ? _bottomPendingUpgradeFrames
                : _topPendingUpgradeFrames;
            if (pending <= 0 || warningFrames > 0) return row;

            BrickDuelBrickType[] types = (BrickDuelBrickType[])row.Types.Clone();
            bool upgraded = false;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == BrickDuelBrickType.Green)
                {
                    types[i] = BrickDuelBrickType.Red;
                    upgraded = true;
                    break;
                }
                if (types[i] == BrickDuelBrickType.Red)
                {
                    types[i] = BrickDuelBrickType.Yellow;
                    upgraded = true;
                    break;
                }
            }
            if (!upgraded) return row;
            if (side == BrickDuelSide.Bottom) _bottomPendingRowUpgrades--;
            else _topPendingRowUpgrades--;
            return new LogicalRow(types, (string[])row.ItemIds.Clone());
        }

        private BrickDuelBrickType[] GenerateWeightedRow()
        {
            float elapsedSeconds = ElapsedFrames / (float)Mathf.Max(1, _rule.SimulationFps);
            BrickDuelCompositionStageDefinition weights = ResolveCompositionWeights(elapsedSeconds);
            var row = new BrickDuelBrickType[_rule.Columns];
            for (int i = 0; i < row.Length; i++)
            {
                float value = (_rowRandom.NextUInt() & 0x00FFFFFFu) / 16777216f;
                if (value < weights.GreenWeight)
                {
                    row[i] = BrickDuelBrickType.Green;
                }
                else if (value < weights.GreenWeight + weights.RedWeight)
                {
                    row[i] = BrickDuelBrickType.Red;
                }
                else if (value < weights.GreenWeight + weights.RedWeight + weights.YellowWeight)
                {
                    row[i] = BrickDuelBrickType.Yellow;
                }
                else
                {
                    row[i] = BrickDuelBrickType.Mystery;
                }
            }
            return row;
        }

        private void AddBrick(
            BrickDuelSide side,
            BrickDuelBrickType type,
            Vector2 position,
            int columnId,
            int logicalRowId,
            string itemId)
        {
            _bricks.Add(new BrickDuelBrickState
            {
                BrickId = _nextBrickId++,
                Side = side,
                InitialType = type,
                Health = GetInitialHealth(type),
                Position = position,
                ColumnId = columnId,
                LogicalRowId = logicalRowId,
                ItemId = itemId,
            });
        }

        private int GetInitialHealth(BrickDuelBrickType type)
        {
            switch (type)
            {
                case BrickDuelBrickType.Green:
                    return _rule.GreenHealth;
                case BrickDuelBrickType.Red:
                    return _rule.RedHealth;
                case BrickDuelBrickType.Yellow:
                    return _rule.YellowHealth;
                case BrickDuelBrickType.Mystery:
                    return _rule.MysteryHealth;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private float GetDangerDistance(BrickDuelSide side)
        {
            float nearest = float.PositiveInfinity;
            float halfHeight = _rule.BrickHeight * 0.5f;
            for (int i = 0; i < _bricks.Count; i++)
            {
                BrickDuelBrickState brick = _bricks[i];
                if (brick.Side != side)
                {
                    continue;
                }

                float distance = side == BrickDuelSide.Bottom
                    ? brick.Position.y - halfHeight + _rule.CoreLineY
                    : _rule.CoreLineY - brick.Position.y - halfHeight;
                nearest = Mathf.Min(nearest, Mathf.Max(0f, distance));
            }

            return float.IsPositiveInfinity(nearest) ? _rule.CoreLineY : nearest;
        }

        private float GetPaddleHalfWidth(BrickDuelSideItemEffects effects)
        {
            BrickDuelSide side = ReferenceEquals(effects, _bottomEffects)
                ? BrickDuelSide.Bottom
                : BrickDuelSide.Top;
            float multiplier = effects.HasWidePaddle
                ? GetPhaseEffectMultiplier(side, BrickDuelItemIds.Wide, "PaddleLengthMultiplier", GetWidePaddleConfiguredMultiplier(), false)
                : 1f;
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            if (phase != null)
                multiplier *= phase.GetHeroPaddleMultiplier(_splitBalls.Count(ball => ball.Side == side && ball.IsActive));
            multiplier = Mathf.Clamp(
                multiplier,
                BrickDuelItemConstants.PaddleWidthMultiplierMin,
                BrickDuelItemConstants.PaddleWidthMultiplierMax);
            return _rule.PaddleHalfWidth * multiplier;
        }

        private float GetBallRadius(BrickDuelSideItemEffects effects)
        {
            BrickDuelSide side = ReferenceEquals(effects, _bottomEffects)
                ? BrickDuelSide.Bottom
                : BrickDuelSide.Top;
            float multiplier = effects.HasLargeBall
                ? GetPhaseEffectMultiplier(side, BrickDuelItemIds.Large, "BallRadiusMultiplier", BrickDuelItemConstants.LargeBallRadiusMultiplier, false)
                : 1f;
            multiplier = Mathf.Clamp(
                multiplier,
                BrickDuelItemConstants.BallRadiusMultiplierMin,
                BrickDuelItemConstants.BallRadiusMultiplierMax);
            return _rule.BallRadius * multiplier;
        }

        private float GetTideSpeedMultiplier(BrickDuelSideItemEffects effects)
        {
            BrickDuelSide side = ReferenceEquals(effects, _bottomEffects)
                ? BrickDuelSide.Bottom
                : BrickDuelSide.Top;
            float multiplier = effects.HasDamping
                ? GetPhaseEffectMultiplier(side, BrickDuelItemIds.Damp, "TideSpeedMultiplier", 0.8f, true)
                : 1f;
            return Mathf.Clamp(
                multiplier,
                BrickDuelItemConstants.TideSpeedMultiplierMin,
                BrickDuelItemConstants.TideSpeedMultiplierMax);
        }

        private float GetBallSpeedMultiplier(
            BrickDuelSideItemEffects effects,
            bool forPaddleBounce = false)
        {
            BrickDuelSide side = ReferenceEquals(effects, _bottomEffects)
                ? BrickDuelSide.Bottom
                : BrickDuelSide.Top;
            float multiplier = effects.HasSpeedBall
                ? GetPhaseEffectMultiplier(side, BrickDuelItemIds.Speed, "BallSpeedMultiplier", GetSpeedBallConfiguredMultiplier(), false)
                : 1f;
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            if (phase != null)
                multiplier *= phase.GetHeroBallSpeedMultiplier(forPaddleBounce);
            return Mathf.Clamp(
                multiplier,
                BrickDuelItemConstants.BallSpeedMultiplierMin,
                BrickDuelItemConstants.BallSpeedMultiplierMax);
        }

        private float GetBallSpeed(
            BrickDuelSide side,
            bool forPaddleBounce = false)
        {
            BrickDuelSideItemEffects effects = side == BrickDuelSide.Bottom
                ? _bottomEffects
                : _topEffects;
            float speed = _rule.BallSpeed * GetBallSpeedMultiplier(effects, forPaddleBounce);
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            return phase == null ? speed : Mathf.Min(speed, phase.GetAbsoluteBallSpeedCap());
        }

        private float GetSpeedBallBaseDurationSeconds()
        {
            BrickDuelItemDropDefinition definition = GetItemDropDefinition(BrickDuelItemIds.SpeedBall);
            return definition != null && definition.EffectDurationSeconds > 0f
                ? definition.EffectDurationSeconds
                : BrickDuelItemConstants.SpeedBallBaseDurationSeconds;
        }

        private float GetWidePaddleDurationSeconds()
        {
            BrickDuelItemDropDefinition definition = GetItemDropDefinition(BrickDuelItemIds.WidePaddle);
            return definition != null && definition.EffectDurationSeconds > 0f
                ? definition.EffectDurationSeconds
                : BrickDuelItemConstants.WidePaddleDurationSeconds;
        }

        private float GetWidePaddleConfiguredMultiplier()
        {
            BrickDuelItemDropDefinition definition = GetItemDropDefinition(BrickDuelItemIds.WidePaddle);
            return definition != null && definition.EffectMagnitude > 0f
                ? definition.EffectMagnitude
                : BrickDuelItemConstants.WidePaddleWidthMultiplier;
        }

        private float GetSpeedBallConfiguredMultiplier()
        {
            BrickDuelItemDropDefinition definition = GetItemDropDefinition(BrickDuelItemIds.SpeedBall);
            return definition != null && definition.EffectMagnitude > 0f
                ? definition.EffectMagnitude
                : BrickDuelItemConstants.SpeedBallSpeedMultiplier;
        }

        private float GetAimedReboundDurationSeconds()
        {
            BrickDuelItemDropDefinition definition = GetItemDropDefinition(
                BrickDuelItemIds.AimedRebound);
            return definition != null && definition.EffectDurationSeconds > 0f
                ? definition.EffectDurationSeconds
                : BrickDuelItemConstants.AimedReboundDurationSeconds;
        }

        private Vector2? GetAimedReboundTarget(BrickDuelSide side)
        {
            BrickDuelSideItemEffects effects = side == BrickDuelSide.Bottom
                ? _bottomEffects
                : _topEffects;
            if (!effects.HasAimedRebound)
            {
                return null;
            }

            BrickDuelBrickState target = null;
            for (int i = 0; i < _bricks.Count; i++)
            {
                BrickDuelBrickState candidate = _bricks[i];
                if (candidate.Side != side || candidate.Health <= 0)
                {
                    continue;
                }

                if (target == null || IsBetterAimedReboundTarget(candidate, target, side))
                {
                    target = candidate;
                }
            }

            return target?.Position;
        }

        private static bool IsBetterAimedReboundTarget(
            BrickDuelBrickState candidate,
            BrickDuelBrickState current,
            BrickDuelSide side)
        {
            const float positionEpsilon = 0.0001f;
            float deltaY = candidate.Position.y - current.Position.y;
            if (Mathf.Abs(deltaY) > positionEpsilon)
            {
                return side == BrickDuelSide.Bottom ? deltaY < 0f : deltaY > 0f;
            }

            if (candidate.ColumnId != current.ColumnId)
            {
                return candidate.ColumnId < current.ColumnId;
            }

            return candidate.BrickId < current.BrickId;
        }

        private BrickDuelItemDropDefinition GetItemDropDefinition(string itemId)
        {
            if (_rule.ItemDrops == null)
            {
                return null;
            }

            for (int i = 0; i < _rule.ItemDrops.Count; i++)
            {
                BrickDuelItemDropDefinition definition = _rule.ItemDrops[i];
                if (definition != null && string.Equals(
                        definition.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private BrickDuelPhaseSideRuntime GetPhaseRuntime(BrickDuelSide side) =>
            side == BrickDuelSide.Bottom ? _bottomPhase : _topPhase;

        private PhaseItemDefinition GetPhaseItem(string itemId)
        {
            if (_catalog == null || string.IsNullOrEmpty(itemId)) return null;
            return _catalog.AllPhaseItems.TryGetValue(itemId, out PhaseItemDefinition item)
                ? item
                : null;
        }

        private float GetPhaseRawFloat(
            BrickDuelSide side,
            string itemId,
            string effectKey,
            float fallback)
        {
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            PhaseItemDefinition item = GetPhaseItem(itemId);
            if (phase == null || item?.Effect == null ||
                !item.Effect.TryGetValue(effectKey, out object raw) || raw == null)
                return fallback;
            float value;
            try { value = Convert.ToSingle(raw); }
            catch { value = fallback; }
            return value;
        }

        private float GetPhaseEffectMultiplier(
            BrickDuelSide side,
            string itemId,
            string effectKey,
            float fallback,
            bool inverse)
        {
            float configured = GetPhaseRawFloat(side, itemId, effectKey, fallback);
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            if (phase == null) return configured;
            float factor = 1f + phase.Modifiers.GetPercent(itemId) / 100f;
            float magnitude = inverse ? 1f - configured : configured - 1f;
            return inverse ? 1f - magnitude * factor : 1f + magnitude * factor;
        }

        private int GetPhaseInt(
            BrickDuelSide side,
            string itemId,
            string effectKey,
            int fallback)
        {
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            PhaseItemDefinition item = GetPhaseItem(itemId);
            if (phase == null || item?.Effect == null ||
                !item.Effect.TryGetValue(effectKey, out object raw) || raw == null)
                return fallback;
            int value;
            try { value = Convert.ToInt32(raw); }
            catch { value = fallback; }
            return phase.Modifiers.ApplyStep(item, value);
        }

        private void ApplyPhaseAbility(BrickDuelSide side)
        {
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            if (phase == null) return;
            switch (phase.TryActivateAbility())
            {
                case PhaseAbilityActivation.MirageTide:
                    SpawnSplitBallsFromSide(side, 2, false);
                    break;
                case PhaseAbilityActivation.PulseBurst:
                case PhaseAbilityActivation.RiftPierce:
                    NormalizeBallSpeeds(side, GetBallSpeed(side));
                    break;
            }
        }

        private BrickDuelHorizontalBarrier? GetRefractMirrorBarrier(BrickDuelSide side)
        {
            BrickDuelPhaseSideRuntime phase = GetPhaseRuntime(side);
            if (phase == null || phase.State.MirrorFrames <= 0)
            {
                return null;
            }

            float direction = side == BrickDuelSide.Bottom ? 1f : -1f;
            float mirrorY = -direction * _rule.CoreLineY * phase.GetMirrorHalfFieldRatio();
            float halfWidth = _rule.ArenaHalfWidth * phase.GetMirrorWidthRatio();
            return new BrickDuelHorizontalBarrier(
                mirrorY,
                halfWidth,
                new Vector2(0f, direction));
        }

        private void NormalizeBallSpeeds(BrickDuelSide side, float speed)
        {
            BrickDuelBallState mother = side == BrickDuelSide.Bottom ? BottomBall : TopBall;
            NormalizeBallSpeed(mother, speed);
            for (int i = 0; i < _splitBalls.Count; i++)
            {
                BrickDuelBallState split = _splitBalls[i];
                if (split.Side == side)
                {
                    NormalizeBallSpeed(split, speed);
                }
            }
        }

        private void SeparateSideBallsFromBricksAndWalls(
            BrickDuelSide side,
            float ballRadius)
        {
            float speed = GetBallSpeed(side);
            BrickDuelBallState mother = side == BrickDuelSide.Bottom ? BottomBall : TopBall;
            BrickDuelCollisionSolver.SeparateBallFromBricksAndWalls(
                mother,
                _bricks,
                _rule,
                ballRadius,
                speed);
            for (int i = 0; i < _splitBalls.Count; i++)
            {
                BrickDuelBallState split = _splitBalls[i];
                if (split.Side != side) continue;
                BrickDuelCollisionSolver.SeparateBallFromBricksAndWalls(
                    split,
                    _bricks,
                    _rule,
                    ballRadius,
                    speed);
            }
        }

        private static void NormalizeBallSpeed(BrickDuelBallState ball, float speed)
        {
            if (ball != null && ball.Velocity.sqrMagnitude > 0.0001f)
            {
                ball.Velocity = ball.Velocity.normalized * speed;
            }
        }

        private void ClampPaddleInsideArena(BrickDuelPaddleState paddle, float paddleHalfWidth)
        {
            float limit = Mathf.Max(0f, _rule.ArenaHalfWidth - paddleHalfWidth);
            paddle.Position = new Vector2(
                Mathf.Clamp(paddle.Position.x, -limit, limit),
                paddle.Position.y);
        }

        private int SecondsToFrames(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds * _rule.SimulationFps));
        }

        private static BrickDuelPaddleState ClonePaddle(BrickDuelPaddleState source)
        {
            return new BrickDuelPaddleState
            {
                Side = source.Side,
                Position = source.Position,
                MoveAxis = source.MoveAxis,
            };
        }

        private static BrickDuelBallState CloneBall(BrickDuelBallState source)
        {
            return new BrickDuelBallState
            {
                BallId = source.BallId,
                Side = source.Side,
                Position = source.Position,
                Velocity = source.Velocity,
                IsActive = source.IsActive,
                IsSplit = source.IsSplit,
                RemainingLifetimeFrames = source.RemainingLifetimeFrames,
                ResetFramesRemaining = source.ResetFramesRemaining,
                StuckFrames = source.StuckFrames,
            };
        }

        private static BrickDuelBrickState CloneBrick(BrickDuelBrickState source)
        {
            return new BrickDuelBrickState
            {
                BrickId = source.BrickId,
                Side = source.Side,
                InitialType = source.InitialType,
                Health = source.Health,
                Position = source.Position,
                ColumnId = source.ColumnId,
                LogicalRowId = source.LogicalRowId,
                ItemId = source.ItemId,
            };
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

        private static void HashBall(ref ulong hash, BrickDuelBallState ball, ulong prime)
        {
            Hash(ref hash, ball.BallId, prime);
            Hash(ref hash, (int)ball.Side, prime);
            Hash(ref hash, Quantize(ball.Position.x), prime);
            Hash(ref hash, Quantize(ball.Position.y), prime);
            Hash(ref hash, Quantize(ball.Velocity.x), prime);
            Hash(ref hash, Quantize(ball.Velocity.y), prime);
            Hash(ref hash, ball.IsActive ? 1 : 0, prime);
            Hash(ref hash, ball.IsSplit ? 1 : 0, prime);
            Hash(ref hash, ball.RemainingLifetimeFrames, prime);
            Hash(ref hash, ball.ResetFramesRemaining, prime);
            Hash(ref hash, ball.StuckFrames, prime);
        }

        private static void HashEffects(
            ref ulong hash,
            BrickDuelSideItemEffects effects,
            ulong prime)
        {
            Hash(ref hash, effects.WidePaddleFramesRemaining, prime);
            Hash(ref hash, effects.LargeBallFramesRemaining, prime);
            Hash(ref hash, effects.PhaseDrillFramesRemaining, prime);
            Hash(ref hash, effects.PhaseDrillCharges, prime);
            Hash(ref hash, effects.SpeedBallFramesRemaining, prime);
            Hash(ref hash, Quantize(effects.SpeedBallDurationAddSeconds), prime);
            Hash(ref hash, Quantize(effects.SpeedBallDurationMultiplier), prime);
            Hash(ref hash, effects.AimedReboundFramesRemaining, prime);
            Hash(ref hash, effects.DampingFramesRemaining, prime);
            Hash(ref hash, effects.CoreBufferFramesRemaining, prime);
            Hash(ref hash, effects.HasCoreBuffer ? 1 : 0, prime);
            Hash(ref hash, effects.MagnetPullRemaining, prime);
        }

        private static void HashPhaseState(
            ref ulong hash,
            BrickDuelPhaseSideState state,
            ulong prime)
        {
            if (state == null)
            {
                Hash(ref hash, 0, prime);
                return;
            }

            Hash(ref hash, 1, prime);
            HashString(ref hash, state.HeroId, prime);
            HashString(ref hash, state.LoadoutHash, prime);
            Hash(ref hash, state.PhaseLevel, prime);
            Hash(ref hash, Quantize(state.Phi), prime);
            Hash(ref hash, Quantize(state.PhiGainedThisSecond), prime);
            Hash(ref hash, state.PhiSecondWindow, prime);
            Hash(ref hash, state.AbilityCooldownFrames, prime);
            Hash(ref hash, state.AbilityActiveFrames, prime);
            Hash(ref hash, state.Combo, prime);
            Hash(ref hash, state.Tempo, prime);
            Hash(ref hash, state.TempoIdleFrames, prime);
            Hash(ref hash, state.RiftCharge, prime);
            Hash(ref hash, state.HeroPierceCharges, prime);
            Hash(ref hash, state.ItemPierceCharges, prime);
            Hash(ref hash, state.PulseHitCounter, prime);
            Hash(ref hash, state.RefractMarkFrames, prime);
            Hash(ref hash, state.RefractSpeedStacks, prime);
            Hash(ref hash, state.RefractPendingSpeedStacks, prime);
            Hash(ref hash, state.RefractBoostFrames, prime);
            Hash(ref hash, state.RefractCycles, prime);
            Hash(ref hash, state.MirrorFrames, prime);
            Hash(ref hash, state.RiftEmpowered ? 1 : 0, prime);
        }

        private static void HashString(ref ulong hash, string value, ulong prime)
        {
            if (string.IsNullOrEmpty(value))
            {
                Hash(ref hash, 0, prime);
                return;
            }

            Hash(ref hash, value.Length, prime);
            for (int i = 0; i < value.Length; i++)
            {
                Hash(ref hash, value[i], prime);
            }
        }

        private static void HashIntSet(
            ref ulong hash,
            IEnumerable<int> values,
            ulong prime)
        {
            int[] ordered = values == null
                ? Array.Empty<int>()
                : values.OrderBy(value => value).ToArray();
            Hash(ref hash, ordered.Length, prime);
            for (int i = 0; i < ordered.Length; i++)
            {
                Hash(ref hash, ordered[i], prime);
            }
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 10000f);
        }

        private static void Hash(ref ulong hash, int value, ulong prime)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= prime;
            }
        }
    }
}
