using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.Chip
{
    /// <summary>
    /// Converts configured Lv1 chip modifiers into a clamped, read-only-friendly rule snapshot.
    /// The match runtime owns applying this snapshot to balls, paddles, serves, and goals.
    /// </summary>
    public static class ChipRuleInjector
    {
        public static ChipRuleSnapshot Inject(
            GatebreakerModeCatalog catalog,
            string heroId,
            IEnumerable<HeroPathRuntimeState> pathStates,
            IEnumerable<string> activeChipIds,
            ChipRuleBaseValues baseValues)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (baseValues == null)
            {
                throw new ArgumentNullException(nameof(baseValues));
            }

            var snapshot = new ChipRuleSnapshot(baseValues);
            Dictionary<string, int> pathLevels = (pathStates ?? Enumerable.Empty<HeroPathRuntimeState>())
                .Where(state => state != null && !string.IsNullOrEmpty(state.PathId))
                .GroupBy(state => state.PathId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Max(state => state.Level), StringComparer.Ordinal);

            foreach (string chipId in (activeChipIds ?? Enumerable.Empty<string>()).OrderBy(id => id, StringComparer.Ordinal))
            {
                if (!DeckValidator.IsV1UniversalChipId(chipId) || !catalog.AllUniversalChips.ContainsKey(chipId))
                {
                    throw new ArgumentException($"'{chipId}' is not a configured V1 universal chip.", nameof(activeChipIds));
                }

                UniversalChipDefinition chip = catalog.GetUniversalChip(chipId);
                ApplyAll(snapshot, chip.Modifiers);
                foreach (UniversalChipConditionalModifierDefinition modifier in chip.ConditionalModifiers ?? Array.Empty<UniversalChipConditionalModifierDefinition>())
                {
                    if (MatchesCondition(modifier, heroId, pathLevels))
                    {
                        Apply(snapshot, modifier.ModifierType, modifier.Op, modifier.Value);
                    }
                }
            }

            snapshot.ClampToRedlines();
            return snapshot;
        }

        private static void ApplyAll(ChipRuleSnapshot snapshot, IEnumerable<UniversalChipModifierDefinition> modifiers)
        {
            foreach (UniversalChipModifierDefinition modifier in modifiers ?? Enumerable.Empty<UniversalChipModifierDefinition>())
            {
                Apply(snapshot, modifier.ModifierType, modifier.Op, modifier.ValueLv1);
            }
        }

        private static bool MatchesCondition(
            UniversalChipConditionalModifierDefinition modifier,
            string heroId,
            IReadOnlyDictionary<string, int> pathLevels)
        {
            if (modifier == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(modifier.HeroId) && !string.Equals(modifier.HeroId, heroId, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.IsNullOrEmpty(modifier.PathId))
            {
                return modifier.MinimumPathLevel <= 0;
            }

            return pathLevels.TryGetValue(modifier.PathId, out int level) && level >= modifier.MinimumPathLevel;
        }

        private static void Apply(ChipRuleSnapshot snapshot, string modifierType, ModifierOp op, float value)
        {
            switch (modifierType)
            {
                case "BallSpeedMultiplier": snapshot.BallSpeedMultiplier = ApplyFloat(snapshot.BallSpeedMultiplier, op, value); break;
                case "BallMaxSpeedMultiplier": snapshot.BallMaxSpeedMultiplier = ApplyFloat(snapshot.BallMaxSpeedMultiplier, op, value); break;
                case "PaddleLengthMultiplier": snapshot.PaddleLengthMultiplier = ApplyFloat(snapshot.PaddleLengthMultiplier, op, value); break;
                case "PaddleMoveSpeedMultiplier": snapshot.PaddleMoveSpeedMultiplier = ApplyFloat(snapshot.PaddleMoveSpeedMultiplier, op, value); break;
                case "GoalHalfLengthMultiplier": snapshot.GoalHalfLengthMultiplier = ApplyFloat(snapshot.GoalHalfLengthMultiplier, op, value); break;
                case "ServeInitialSpeedMultiplier": snapshot.ServeInitialSpeedMultiplier = ApplyFloat(snapshot.ServeInitialSpeedMultiplier, op, value); break;
                case "ServeCooldownMultiplier": snapshot.ServeCooldownMultiplier = ApplyFloat(snapshot.ServeCooldownMultiplier, op, value); break;
                case "PaddleBounceSpeedMultiplier": snapshot.PaddleBounceSpeedMultiplier = ApplyFloat(snapshot.PaddleBounceSpeedMultiplier, op, value); break;
                case "EnemyWallBounceSpeedMultiplier": snapshot.EnemyWallBounceSpeedMultiplier = ApplyFloat(snapshot.EnemyWallBounceSpeedMultiplier, op, value); break;
                case "EnemyPaddleMoveSpeedMultiplier": snapshot.EnemyPaddleMoveSpeedMultiplier = ApplyFloat(snapshot.EnemyPaddleMoveSpeedMultiplier, op, value); break;
                case "WallBounceDeflectionDegrees": snapshot.WallBounceDeflectionDegrees = ApplyFloat(snapshot.WallBounceDeflectionDegrees, op, value); break;
                case "RicochetSpeedMultiplier": snapshot.RicochetSpeedMultiplier = ApplyFloat(snapshot.RicochetSpeedMultiplier, op, value); break;
                case "EnemyPaddleSlowDurationSeconds": snapshot.EnemyPaddleSlowDurationSeconds = ApplyFloat(snapshot.EnemyPaddleSlowDurationSeconds, op, value); break;
                case "ServeAmmoCapacity": snapshot.MaxServeAmmo = ApplyInt(snapshot.MaxServeAmmo, op, value); break;
                case "MaxBallsInMatch": snapshot.MaxBallsInMatch = ApplyInt(snapshot.MaxBallsInMatch, op, value); break;
                case "RicochetRequiredCollisionCount": snapshot.RicochetRequiredCollisionCount = ApplyInt(snapshot.RicochetRequiredCollisionCount, op, value); break;
                default: throw new ArgumentOutOfRangeException(nameof(modifierType), modifierType, "Unsupported V1 chip modifier type.");
            }
        }

        private static float ApplyFloat(float current, ModifierOp op, float value)
        {
            switch (op)
            {
                case ModifierOp.Add: return current + value;
                case ModifierOp.Multiply: return current * value;
                case ModifierOp.Override: return value;
                case ModifierOp.Flag: return value > 0f ? 1f : 0f;
                default: throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }
        }

        private static int ApplyInt(int current, ModifierOp op, float value)
        {
            switch (op)
            {
                case ModifierOp.Add: return current + (int)value;
                case ModifierOp.Multiply: return (int)(current * value);
                case ModifierOp.Override: return (int)value;
                case ModifierOp.Flag: return value > 0f ? 1 : 0;
                default: throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }
        }
    }

    public sealed class ChipRuleBaseValues
    {
        public int MaxBallsInMatch { get; set; }
        public int MaxServeAmmo { get; set; }
    }

    public sealed class ChipRuleSnapshot
    {
        internal ChipRuleSnapshot(ChipRuleBaseValues baseValues)
        {
            MaxBallsInMatch = baseValues.MaxBallsInMatch;
            MaxServeAmmo = baseValues.MaxServeAmmo;
        }

        public float BallSpeedMultiplier { get; internal set; } = 1f;
        public float BallMaxSpeedMultiplier { get; internal set; } = 1f;
        public float PaddleLengthMultiplier { get; internal set; } = 1f;
        public float PaddleMoveSpeedMultiplier { get; internal set; } = 1f;
        public float GoalHalfLengthMultiplier { get; internal set; } = 1f;
        public float ServeInitialSpeedMultiplier { get; internal set; } = 1f;
        public float ServeCooldownMultiplier { get; internal set; } = 1f;
        public float PaddleBounceSpeedMultiplier { get; internal set; } = 1f;
        public float EnemyWallBounceSpeedMultiplier { get; internal set; } = 1f;
        public float EnemyPaddleMoveSpeedMultiplier { get; internal set; } = 1f;
        public float WallBounceDeflectionDegrees { get; internal set; }
        public float RicochetSpeedMultiplier { get; internal set; } = 1f;
        public float EnemyPaddleSlowDurationSeconds { get; internal set; }
        public int MaxBallsInMatch { get; internal set; }
        public int MaxServeAmmo { get; internal set; }
        public int RicochetRequiredCollisionCount { get; internal set; }

        internal void ClampToRedlines()
        {
            BallSpeedMultiplier = Clamp(BallSpeedMultiplier, 0.1f, 3f);
            BallMaxSpeedMultiplier = Clamp(BallMaxSpeedMultiplier, 0.1f, 3f);
            PaddleLengthMultiplier = Clamp(PaddleLengthMultiplier, 0.1f, 1.8f);
            PaddleMoveSpeedMultiplier = Clamp(PaddleMoveSpeedMultiplier, 0.1f, 3f);
            GoalHalfLengthMultiplier = Clamp(GoalHalfLengthMultiplier, 0.1f, 3f);
            ServeInitialSpeedMultiplier = Clamp(ServeInitialSpeedMultiplier, 0.1f, 3f);
            ServeCooldownMultiplier = Clamp(ServeCooldownMultiplier, 1f / 3f, 3f);
            PaddleBounceSpeedMultiplier = Clamp(PaddleBounceSpeedMultiplier, 0.1f, 3f);
            EnemyWallBounceSpeedMultiplier = Clamp(EnemyWallBounceSpeedMultiplier, 0.1f, 3f);
            EnemyPaddleMoveSpeedMultiplier = Clamp(EnemyPaddleMoveSpeedMultiplier, 0.1f, 3f);
            WallBounceDeflectionDegrees = Clamp(WallBounceDeflectionDegrees, -15f, 15f);
            RicochetSpeedMultiplier = Clamp(RicochetSpeedMultiplier, 0.1f, 3f);
            EnemyPaddleSlowDurationSeconds = Clamp(EnemyPaddleSlowDurationSeconds, 0f, 3f);
            MaxBallsInMatch = Math.Max(0, Math.Min(25, MaxBallsInMatch));
            MaxServeAmmo = Math.Max(0, MaxServeAmmo);
            RicochetRequiredCollisionCount = Math.Max(0, RicochetRequiredCollisionCount);
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
