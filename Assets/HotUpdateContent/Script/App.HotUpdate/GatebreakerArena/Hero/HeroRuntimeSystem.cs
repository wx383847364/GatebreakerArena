using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.Hero
{
    /// <summary>
    /// Pure deterministic V1 hero rules. This class does not own match objects or
    /// Unity objects; the match runtime routes resolved events into it and applies
    /// the returned modifiers to balls, paddles, serves, and goals.
    /// </summary>
    public sealed class HeroRuntimeSystem
    {
        public const string FrostQueenId = "HERO_FROST_QUEEN";
        public const string EngineerId = "HERO_MECH_ENGINEER";
        public const string RadiantPaladinId = "HERO_RADIANT_PALADIN";
        public const int DefaultFramesPerSecond = 30;

        private const int FrostThreshold = 100;
        private const int FrostBasePerHit = 5;
        private const int FrostM1PerHit = 10;
        private const int FrostDecayPerSecond = 5;
        private const int IceCrystalMaxSpeedStacks = 3;

        /// <summary>
        /// Resolves V1 M1/M2 milestones from the activated chip categories and writes
        /// only the existing shared HeroRuntimeState fields.
        /// </summary>
        public void Initialize(
            HeroDefinition hero,
            IReadOnlyList<HeroPathDefinition> heroPaths,
            IReadOnlyList<UniversalChipDefinition> activeChips,
            HeroRuntimeState runtimeState,
            HeroCombatState combatState,
            bool resetTransientState = true,
            SignatureChipDefinition signatureChip = null)
        {
            if (hero == null)
            {
                throw new ArgumentNullException(nameof(hero));
            }

            if (runtimeState == null)
            {
                throw new ArgumentNullException(nameof(runtimeState));
            }

            if (combatState == null)
            {
                throw new ArgumentNullException(nameof(combatState));
            }

            HeroPathDefinition[] ownedPaths = (heroPaths ?? Array.Empty<HeroPathDefinition>())
                .Where(path => path != null && string.Equals(path.HeroId, hero.HeroId, StringComparison.Ordinal))
                .OrderBy(path => path.PathId ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
            UniversalChipDefinition[] chips = (activeChips ?? Array.Empty<UniversalChipDefinition>())
                .Where(chip => chip != null)
                .OrderBy(chip => chip.ChipId ?? string.Empty, StringComparer.Ordinal)
                .ToArray();

            runtimeState.HeroId = hero.HeroId ?? string.Empty;
            runtimeState.ActiveChipIds = chips.Select(chip => chip.ChipId ?? string.Empty).ToArray();
            runtimeState.PathStates = CalculatePathStates(ownedPaths, chips);
            if (resetTransientState)
            {
                runtimeState.AbilityCooldownRemainingFrames = 0;
            }
            combatState.HeroId = runtimeState.HeroId;
            if (signatureChip != null)
            {
                combatState.SignatureParameters = (signatureChip.Parameters ?? new Dictionary<string, float>())
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new HeroParameterState { Key = item.Key, Value = item.Value }).ToList();
            }
        }

        public IReadOnlyList<HeroPathRuntimeState> CalculatePathStates(
            IReadOnlyList<HeroPathDefinition> heroPaths,
            IReadOnlyList<UniversalChipDefinition> activeChips)
        {
            UniversalChipDefinition[] chips = (activeChips ?? Array.Empty<UniversalChipDefinition>())
                .Where(chip => chip != null)
                .ToArray();
            return (heroPaths ?? Array.Empty<HeroPathDefinition>())
                .Where(path => path != null)
                .OrderBy(path => path.PathId ?? string.Empty, StringComparer.Ordinal)
                .Select(path => new HeroPathRuntimeState
                {
                    PathId = path.PathId ?? string.Empty,
                    Level = Math.Min(2, chips.Count(chip => ContainsCategory(path.ResonanceCategories, chip.Category))),
                })
                .ToArray();
        }

        public HeroRuntimeEventResult HandleEvent(
            HeroDefinition hero,
            IReadOnlyList<HeroPathDefinition> heroPaths,
            HeroRuntimeState runtimeState,
            HeroCombatState combatState,
            HeroRuntimeEvent runtimeEvent,
            int framesPerSecond = DefaultFramesPerSecond)
        {
            ValidateContext(hero, runtimeState, combatState, framesPerSecond);
            HeroPathLevels paths = ResolvePathLevels(hero, heroPaths, runtimeState);
            combatState.SimulationFrame++;
            switch (runtimeEvent.EventType)
            {
                case HeroRuntimeEventType.OpponentPaddleHit:
                    return HandleOpponentPaddleHit(hero, paths, combatState, runtimeEvent, framesPerSecond);
                case HeroRuntimeEventType.OwnPaddleHit:
                    return HandleOwnPaddleHit(hero, paths, combatState, runtimeEvent, framesPerSecond);
                case HeroRuntimeEventType.ConcededGoal:
                    return HandleConcededGoal(hero, paths, combatState, framesPerSecond);
                case HeroRuntimeEventType.AbilityPressed:
                    return TryActivateAbility(hero, paths, runtimeState, combatState, framesPerSecond);
                default:
                    throw new ArgumentOutOfRangeException(nameof(runtimeEvent));
            }
        }

        /// <summary>Returns effects that must be sampled before simulation each frame.</summary>
        public HeroEffectBundle GetPersistentEffects(
            HeroDefinition hero,
            IReadOnlyList<HeroPathDefinition> heroPaths,
            HeroRuntimeState runtimeState,
            HeroCombatState combatState,
            int framesPerSecond = DefaultFramesPerSecond)
        {
            ValidateContext(hero, runtimeState, combatState, framesPerSecond);
            HeroPathLevels paths = ResolvePathLevels(hero, heroPaths, runtimeState);
            var effects = new HeroEffectBundle();

            if (string.Equals(hero.HeroId, RadiantPaladinId, StringComparison.Ordinal))
            {
                if (combatState.DivineShieldRemainingFrames > 0)
                {
                    effects.OwnGoalImmuneFrames = combatState.DivineShieldRemainingFrames;
                }
                if (paths.GlowLevel >= 1 && combatState.GlowStacks > 0)
                {
                    float[] boosts = { 0f, 0.08f, 0.14f, 0.18f };
                    effects.OwnBallSpeedMultiplier = Math.Min(GetParameter(combatState, "GlowCap", 1.4f), 1f + boosts[Math.Min(3, combatState.GlowStacks)]);
                }
            }

            return effects;
        }

        /// <summary>Advances explicit frame state; the caller invokes this once per simulation frame.</summary>
        public void Tick(
            HeroDefinition hero,
            IReadOnlyList<HeroPathDefinition> heroPaths,
            HeroRuntimeState runtimeState,
            HeroCombatState combatState,
            int framesPerSecond = DefaultFramesPerSecond)
        {
            ValidateContext(hero, runtimeState, combatState, framesPerSecond);
            HeroPathLevels paths = ResolvePathLevels(hero, heroPaths, runtimeState);
            runtimeState.AbilityCooldownRemainingFrames = Math.Max(0, runtimeState.AbilityCooldownRemainingFrames - 1);
            combatState.ThornArmorRemainingFrames = Math.Max(0, combatState.ThornArmorRemainingFrames - 1);
            combatState.DivineShieldRemainingFrames = Math.Max(0, combatState.DivineShieldRemainingFrames - 1);
            combatState.BlizzardRemainingFrames = Math.Max(0, combatState.BlizzardRemainingFrames - 1);
            combatState.TeamBallSpeedBoostRemainingFrames = Math.Max(0, combatState.TeamBallSpeedBoostRemainingFrames - 1);
            combatState.ArcPulseCooldownRemainingFrames = Math.Max(0, combatState.ArcPulseCooldownRemainingFrames - 1);
            combatState.GlowDecayRemainingFrames = Math.Max(0, combatState.GlowDecayRemainingFrames - 1);
            if (combatState.GlowDecayRemainingFrames == 0) combatState.GlowStacks = 0;
            foreach (HeroFreezeImmunityState immunity in combatState.FreezeImmunityByOpponent)
                immunity.RemainingFrames = Math.Max(0, immunity.RemainingFrames - 1);
            foreach (HeroBarrierState barrier in combatState.Barriers)
            {
                barrier.RemainingFrames = Math.Max(0, barrier.RemainingFrames - 1);
                barrier.DisabledRemainingFrames = Math.Max(0, barrier.DisabledRemainingFrames - 1);
                barrier.HitWindowRemainingFrames = Math.Max(0, barrier.HitWindowRemainingFrames - 1);
                if (barrier.HitWindowRemainingFrames == 0) barrier.HitsInWindow = 0;
            }
            combatState.Barriers.RemoveAll(barrier => barrier == null || barrier.RemainingFrames <= 0);

            if (string.Equals(hero.HeroId, FrostQueenId, StringComparison.Ordinal))
            {
                combatState.FrostDecayFrameProgress++;
                if (combatState.FrostDecayFrameProgress >= framesPerSecond)
                {
                    combatState.FrostDecayFrameProgress = 0;
                    int decay = (int)GetParameter(combatState, "FrostDecay", FrostDecayPerSecond);
                    foreach (HeroBallFrostState frost in combatState.FrostByBall)
                    {
                        frost.Amount = Math.Max(0, frost.Amount - decay);
                    }
                }
            }

            if (string.Equals(hero.HeroId, EngineerId, StringComparison.Ordinal) &&
                paths.GrowthLevel >= 2 && combatState.ThornArmorRemainingFrames > 0)
            {
                combatState.ThornArmorGrowthFrameProgress++;
                if (combatState.ThornArmorGrowthFrameProgress >= framesPerSecond)
                {
                    combatState.ThornArmorGrowthFrameProgress = 0;
                    int maximum = GetThornGrowthMaximum(paths);
                    combatState.ThornGrowthStacks = Math.Min(maximum, combatState.ThornGrowthStacks + 1);
                }
            }
            else if (combatState.ThornArmorRemainingFrames == 0)
            {
                combatState.ThornArmorGrowthFrameProgress = 0;
            }
        }

        public bool ShouldAiUseAbility(
            HeroDefinition hero,
            HeroRuntimeState runtimeState,
            HeroCombatState combatState,
            HeroAiAbilityDecisionInput input)
        {
            if (hero == null || runtimeState == null || combatState == null || runtimeState.AbilityCooldownRemainingFrames > 0)
            {
                return false;
            }

            if (string.Equals(hero.HeroId, FrostQueenId, StringComparison.Ordinal))
            {
                return input.HighestOpponentFrost >= 50;
            }

            if (string.Equals(hero.HeroId, EngineerId, StringComparison.Ordinal))
            {
                return input.HasEnemyBallInOwnDangerZone;
            }

            return string.Equals(hero.HeroId, RadiantPaladinId, StringComparison.Ordinal) &&
                   (input.HasEnemyBallInOwnDangerZone || combatState.ChargeStacks >= 5);
        }

        /// <summary>
        /// Returns the accumulated Ice Crystal multiplier for one live ball. The match
        /// owner calls <see cref="RemoveBallState"/> when that ball is destroyed.
        /// </summary>
        public float GetIceCrystalBallSpeedMultiplier(HeroCombatState combatState, int ballId)
        {
            if (combatState == null || ballId == 0)
            {
                return 1f;
            }

            HeroBallSpeedStackState state = (combatState.IceCrystalBallSpeedStacks ?? new List<HeroBallSpeedStackState>())
                .FirstOrDefault(item => item != null && item.BallId == ballId);
            return state == null ? 1f : 1f + Math.Min(IceCrystalMaxSpeedStacks, state.Stacks) * 0.15f;
        }

        /// <summary>Removes per-ball state after a ball leaves the deterministic simulation.</summary>
        public void RemoveBallState(HeroCombatState combatState, int ballId)
        {
            if (combatState == null || ballId == 0)
            {
                return;
            }
            combatState.IceCrystalBallSpeedStacks?.RemoveAll(item => item == null || item.BallId == ballId);
            combatState.FrostByBall?.RemoveAll(item => item == null || item.BallId == ballId);
        }

        private static HeroRuntimeEventResult HandleOpponentPaddleHit(
            HeroDefinition hero,
            HeroPathLevels paths,
            HeroCombatState state,
            HeroRuntimeEvent runtimeEvent,
            int framesPerSecond)
        {
            var result = new HeroRuntimeEventResult();
            if (string.Equals(hero.HeroId, FrostQueenId, StringComparison.Ordinal))
            {
                HeroBallFrostState frost = GetOrCreateBallFrost(state, runtimeEvent.BallId);
                int increment = (int)GetParameter(state, "FrostPerHit", paths.ExtremeColdLevel >= 1 || paths.IceCrystalLevel >= 1 ? 15 : FrostBasePerHit);
                if (state.BlizzardRemainingFrames > 0)
                {
                    increment = (int)Math.Ceiling(increment * 1.5f);
                }

                frost.Amount += increment;
                if (frost.Amount >= FrostThreshold)
                {
                    frost.Amount = 0;
                    if (paths.IceCrystalLevel >= 1 && runtimeEvent.BallId != 0)
                    {
                        HeroBallSpeedStackState ballState = GetOrCreateBallSpeedStack(state, runtimeEvent.BallId);
                        ballState.Stacks = Math.Min(2, ballState.Stacks + 1);
                        result.Effects.OwnBallSpeedMultiplier = GetParameter(state, "CrystalSpeed", 1.25f);
                        result.Effects.RedirectBounceTowardsNearestEnemyGoal = paths.IceCrystalLevel >= 2;
                    }
                    else
                    {
                        HeroFreezeImmunityState immunity = GetOrCreateFreezeImmunity(state, runtimeEvent.OtherPlayerId);
                        if (immunity.RemainingFrames > 0)
                        {
                            result.Effects.TargetPaddleSlowFrames = SecondsToFrames(1f, framesPerSecond);
                            result.Effects.TargetPaddleMoveSpeedMultiplier = 0.8f;
                        }
                        else
                        {
                            float freezeSeconds = GetParameter(state, "FreezeSeconds", 1.5f);
                            result.Effects.TargetPaddleFreezeFrames = SecondsToFrames(freezeSeconds, framesPerSecond);
                            immunity.RemainingFrames = SecondsToFrames(6f, framesPerSecond);
                            if (paths.ExtremeColdLevel >= 2)
                            {
                                result.Effects.TargetPaddleSlowFrames = SecondsToFrames(1.5f, framesPerSecond);
                                result.Effects.TargetPaddleMoveSpeedMultiplier = GetParameter(state, "FreezeSlowMultiplier", 0.8f);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static HeroRuntimeEventResult HandleOwnPaddleHit(
            HeroDefinition hero,
            HeroPathLevels paths,
            HeroCombatState state,
            HeroRuntimeEvent runtimeEvent,
            int framesPerSecond)
        {
            var result = new HeroRuntimeEventResult();
            if (string.Equals(hero.HeroId, EngineerId, StringComparison.Ordinal))
            {
                if (paths.TurretLevel >= 1)
                {
                    result.Effects.OwnPaddleBounceSpeedMultiplier = GetParameter(state, "BounceSpeed", 1.2f);
                    result.Effects.RedirectBounceTowardsNearestEnemyGoal = paths.TurretLevel >= 2;
                    result.Effects.BounceRedirectMaxDegrees = GetParameter(state, "RedirectDegrees", 12f);
                }
                return result;
            }

            if (!string.Equals(hero.HeroId, RadiantPaladinId, StringComparison.Ordinal))
            {
                return result;
            }

            if (paths.GlowLevel >= 1)
            {
                state.GlowStacks = Math.Min(3, state.GlowStacks + 1);
                state.GlowDecayRemainingFrames = SecondsToFrames(GetParameter(state, "GlowSeconds", 5f), framesPerSecond);
                result.Effects.OwnPaddleBounceSpeedMultiplier = 1f + state.GlowStacks * 0.06f;
                return result;
            }
            if (paths.ChargeLevel >= 1)
            {
                state.ChargeStacks++;
                int threshold = (int)GetParameter(state, "ChargeThreshold", 6f);
                if (state.ChargeStacks >= threshold)
                {
                    state.ChargeStacks = 0;
                    result.Effects.OwnPaddleBounceSpeedMultiplier = GetParameter(state, "BurstSpeed", 1.45f);
                    result.Effects.OwnGoalImmuneFrames = SecondsToFrames(GetParameter(state, "ShieldSeconds", 0.5f), framesPerSecond);
                    if (paths.ChargeLevel >= 2)
                    {
                        result.Effects.TemporaryCloneCount = paths.ChargeLevel >= 3 ? 2 : 1;
                        result.Effects.TemporaryCloneDurationFrames = SecondsToFrames(3f, framesPerSecond);
                    }
                }
            }

            return result;
        }

        private static HeroRuntimeEventResult HandleConcededGoal(
            HeroDefinition hero,
            HeroPathLevels paths,
            HeroCombatState state,
            int framesPerSecond)
        {
            var result = new HeroRuntimeEventResult();
            return result;
        }

        private static HeroRuntimeEventResult TryActivateAbility(
            HeroDefinition hero,
            HeroPathLevels paths,
            HeroRuntimeState runtimeState,
            HeroCombatState state,
            int framesPerSecond)
        {
            var result = new HeroRuntimeEventResult();
            if (runtimeState.AbilityCooldownRemainingFrames > 0)
            {
                return result;
            }

            runtimeState.AbilityCooldownRemainingFrames = SecondsToFrames(hero.ActiveAbilityCooldownSeconds, framesPerSecond);
            result.AbilityActivated = true;
            if (string.Equals(hero.HeroId, FrostQueenId, StringComparison.Ordinal))
            {
                state.BlizzardRemainingFrames = SecondsToFrames(paths.ExtremeColdLevel >= 1 ? 5f : 4f, framesPerSecond);
            }
            else if (string.Equals(hero.HeroId, EngineerId, StringComparison.Ordinal))
            {
                result.Effects.SpawnBarrier = true;
                result.Effects.BarrierDurationFrames = SecondsToFrames(GetParameter(state, "BarrierSeconds", 8f), framesPerSecond);
                result.Effects.BarrierLength = GetParameter(state, "BarrierLength", 1.5f);
            }
            else if (string.Equals(hero.HeroId, RadiantPaladinId, StringComparison.Ordinal))
            {
                float duration = paths.ChargeLevel >= 1 ? GetParameter(state, "ShieldSeconds", 0.5f) : 1.5f;
                state.DivineShieldRemainingFrames = SecondsToFrames(duration, framesPerSecond);
                result.Effects.OwnGoalImmuneFrames = state.DivineShieldRemainingFrames;
            }

            return result;
        }

        private static HeroPathLevels ResolvePathLevels(
            HeroDefinition hero,
            IReadOnlyList<HeroPathDefinition> heroPaths,
            HeroRuntimeState runtimeState)
        {
            var levels = new HeroPathLevels();
            foreach (HeroPathDefinition path in heroPaths ?? Array.Empty<HeroPathDefinition>())
            {
                if (path == null || !string.Equals(path.HeroId, hero.HeroId, StringComparison.Ordinal))
                {
                    continue;
                }

                HeroPathRuntimeState runtimePath = (runtimeState.PathStates ?? Array.Empty<HeroPathRuntimeState>())
                    .FirstOrDefault(item => item != null && string.Equals(item.PathId, path.PathId, StringComparison.Ordinal));
                int level = runtimePath != null ? Math.Min(3, Math.Max(0, runtimePath.Level)) : 0;
                levels.Set(path.PathId, level);
            }

            return levels;
        }

        private static void ValidateContext(HeroDefinition hero, HeroRuntimeState runtimeState, HeroCombatState combatState, int framesPerSecond)
        {
            if (hero == null)
            {
                throw new ArgumentNullException(nameof(hero));
            }

            if (runtimeState == null)
            {
                throw new ArgumentNullException(nameof(runtimeState));
            }

            if (combatState == null)
            {
                throw new ArgumentNullException(nameof(combatState));
            }

            if (framesPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            }
        }

        private static float GetFrostFreezeSeconds(HeroPathLevels paths)
        {
            if (paths.ExtremeColdLevel >= 1)
            {
                return 0.9f;
            }

            return paths.IceCrystalLevel >= 1 ? 0.78f : 0.6f;
        }

        private static int GetThornGrowthMaximum(HeroPathLevels paths)
        {
            return paths.GrowthLevel >= 1 ? 5 : 3;
        }

        private static float GetThornLengthMultiplier(HeroPathLevels paths, HeroCombatState state)
        {
            float perStack = paths.ThornsLevel >= 1 || paths.GrowthLevel >= 1 ? 0.05f : 0.03f;
            return Math.Min(1.8f, 1f + state.ThornGrowthStacks * perStack);
        }

        private static HeroBallFrostState GetOrCreateBallFrost(HeroCombatState state, int ballId)
        {
            HeroBallFrostState frost = state.FrostByBall.FirstOrDefault(item => item.BallId == ballId);
            if (frost != null)
            {
                return frost;
            }

            frost = new HeroBallFrostState { BallId = ballId };
            state.FrostByBall.Add(frost);
            state.FrostByBall.Sort((left, right) => left.BallId.CompareTo(right.BallId));
            return frost;
        }

        private static HeroFreezeImmunityState GetOrCreateFreezeImmunity(HeroCombatState state, int opponentPlayerId)
        {
            HeroFreezeImmunityState item = state.FreezeImmunityByOpponent.FirstOrDefault(value => value.OpponentPlayerId == opponentPlayerId);
            if (item != null) return item;
            item = new HeroFreezeImmunityState { OpponentPlayerId = opponentPlayerId };
            state.FreezeImmunityByOpponent.Add(item);
            state.FreezeImmunityByOpponent.Sort((left, right) => left.OpponentPlayerId.CompareTo(right.OpponentPlayerId));
            return item;
        }

        private static float GetParameter(HeroCombatState state, string key, float fallback)
        {
            HeroParameterState parameter = (state.SignatureParameters ?? new List<HeroParameterState>())
                .FirstOrDefault(item => item != null && string.Equals(item.Key, key, StringComparison.Ordinal));
            return parameter != null ? parameter.Value : fallback;
        }

        private static HeroBallSpeedStackState GetOrCreateBallSpeedStack(HeroCombatState state, int ballId)
        {
            HeroBallSpeedStackState ball = state.IceCrystalBallSpeedStacks.FirstOrDefault(item => item.BallId == ballId);
            if (ball != null)
            {
                return ball;
            }

            ball = new HeroBallSpeedStackState { BallId = ballId };
            state.IceCrystalBallSpeedStacks.Add(ball);
            state.IceCrystalBallSpeedStacks.Sort((left, right) => left.BallId.CompareTo(right.BallId));
            return ball;
        }

        private static bool ContainsCategory(IReadOnlyList<ChipCategory> categories, ChipCategory category)
        {
            return categories != null && categories.Contains(category);
        }

        private static bool IsCategoryPair(IReadOnlyList<ChipCategory> categories, ChipCategory first, ChipCategory second)
        {
            return categories != null && categories.Count == 2 && categories.Contains(first) && categories.Contains(second);
        }

        private static int SecondsToFrames(float seconds, int framesPerSecond)
        {
            return Math.Max(0, (int)Math.Ceiling(seconds * framesPerSecond));
        }

        private sealed class HeroPathLevels
        {
            public int ExtremeColdLevel { get; private set; }
            public int IceCrystalLevel { get; private set; }
            public int ThornsLevel { get; private set; }
            public int GrowthLevel { get; private set; }
            public int HolyLightLevel { get; private set; }
            public int BrillianceLevel { get; private set; }
            public int FortressLevel { get; private set; }
            public int TurretLevel { get; private set; }
            public int ChargeLevel { get; private set; }
            public int GlowLevel { get; private set; }

            public void Set(string pathId, int level)
            {
                switch (pathId)
                {
                    case "PATH_FROST_EXTREME": ExtremeColdLevel = level; break;
                    case "PATH_FROST_CRYSTAL": IceCrystalLevel = level; break;
                    case "PATH_MECH_FORTRESS": FortressLevel = level; break;
                    case "PATH_MECH_TURRET": TurretLevel = level; break;
                    case "PATH_RADIANT_CHARGE": ChargeLevel = level; break;
                    case "PATH_RADIANT_GLOW": GlowLevel = level; break;
                }
            }
        }
    }
}
