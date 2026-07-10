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
        public const string ThornGuardianId = "HERO_THORN_GUARDIAN";
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
            HeroCombatState combatState)
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
            runtimeState.AbilityCooldownRemainingFrames = 0;
            combatState.HeroId = runtimeState.HeroId;
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

            if (string.Equals(hero.HeroId, ThornGuardianId, StringComparison.Ordinal))
            {
                effects.OwnPaddleLengthMultiplier = GetThornLengthMultiplier(paths, combatState);
            }
            else if (string.Equals(hero.HeroId, RadiantPaladinId, StringComparison.Ordinal))
            {
                if (combatState.DivineShieldRemainingFrames > 0)
                {
                    effects.OwnGoalImmuneFrames = combatState.DivineShieldRemainingFrames;
                    if (paths.HolyLightLevel >= 2)
                    {
                        effects.OwnPaddleBounceSpeedMultiplier = 1.5f;
                    }

                    if (paths.BrillianceLevel >= 2)
                    {
                        effects.OwnPaddleMoveSpeedMultiplier = 1.5f;
                    }
                }

                if (paths.BrillianceLevel >= 2)
                {
                    effects.OwnBallSpeedMultiplier *= 1f + combatState.RadianceStacks * 0.05f;
                    if (combatState.TeamBallSpeedBoostRemainingFrames > 0)
                    {
                        effects.OwnBallSpeedMultiplier *= 1.5f;
                    }
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

            if (string.Equals(hero.HeroId, FrostQueenId, StringComparison.Ordinal) && paths.IceCrystalLevel < 1)
            {
                combatState.FrostDecayFrameProgress++;
                if (combatState.FrostDecayFrameProgress >= framesPerSecond)
                {
                    combatState.FrostDecayFrameProgress = 0;
                    foreach (HeroFrostStackState frost in combatState.FrostByOpponent)
                    {
                        frost.Amount = Math.Max(0, frost.Amount - FrostDecayPerSecond);
                    }
                }
            }

            if (string.Equals(hero.HeroId, ThornGuardianId, StringComparison.Ordinal) &&
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

            if (string.Equals(hero.HeroId, ThornGuardianId, StringComparison.Ordinal))
            {
                return input.HasEnemyBallInOwnDangerZone;
            }

            return string.Equals(hero.HeroId, RadiantPaladinId, StringComparison.Ordinal) &&
                   (input.HasEnemyBallInOwnDangerZone || combatState.RadianceStacks >= 6);
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
            if (combatState == null || ballId == 0 || combatState.IceCrystalBallSpeedStacks == null)
            {
                return;
            }

            combatState.IceCrystalBallSpeedStacks.RemoveAll(item => item == null || item.BallId == ballId);
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
                HeroFrostStackState frost = GetOrCreateFrost(state, runtimeEvent.OtherPlayerId);
                int increment = paths.ExtremeColdLevel >= 1 ? FrostM1PerHit : FrostBasePerHit;
                if (state.BlizzardRemainingFrames > 0)
                {
                    increment = (int)Math.Ceiling(increment * 1.5f);
                }

                frost.Amount += increment;
                if (frost.Amount >= FrostThreshold)
                {
                    frost.Amount = 0;
                    result.Effects.TargetPaddleFreezeFrames = SecondsToFrames(GetFrostFreezeSeconds(paths), framesPerSecond);
                    if (paths.ExtremeColdLevel >= 2)
                    {
                        result.Effects.TargetPaddleFreezeFrames = SecondsToFrames(1.2f, framesPerSecond);
                        result.Effects.TargetAllBallsFreezeFrames = SecondsToFrames(0.75f, framesPerSecond);
                    }
                }

                if (paths.IceCrystalLevel >= 2 && runtimeEvent.BallId != 0)
                {
                    HeroBallSpeedStackState ballState = GetOrCreateBallSpeedStack(state, runtimeEvent.BallId);
                    ballState.Stacks = Math.Min(IceCrystalMaxSpeedStacks, ballState.Stacks + 1);
                    result.Effects.OwnBallSpeedMultiplier = 1.15f;
                    result.Effects.RedirectBounceTowardsNearestEnemyGoal = true;
                    result.Effects.BounceRedirectMaxDegrees = 8f;
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
            if (string.Equals(hero.HeroId, ThornGuardianId, StringComparison.Ordinal) && state.ThornArmorRemainingFrames > 0)
            {
                result.Effects.OwnPaddleBounceSpeedMultiplier = paths.ThornsLevel >= 2
                    ? 2f
                    : paths.ThornsLevel >= 1 ? 1.5f : 1.3f;
                result.Effects.RedirectBounceTowardsNearestEnemyGoal = paths.ThornsLevel >= 2;
                return result;
            }

            if (!string.Equals(hero.HeroId, RadiantPaladinId, StringComparison.Ordinal))
            {
                return result;
            }

            state.RadianceStacks++;
            int threshold = paths.HolyLightLevel >= 1 || paths.BrillianceLevel >= 1 ? 6 : 8;
            if (state.RadianceStacks < threshold)
            {
                return result;
            }

            state.RadianceStacks = 0;
            result.Effects.OwnPaddleBounceSpeedMultiplier = paths.HolyLightLevel >= 2
                ? 2f
                : paths.HolyLightLevel >= 1 ? 1.6f : 1.3f;
            result.Effects.RedirectBounceTowardsNearestEnemyGoal = true;
            if (paths.BrillianceLevel >= 2)
            {
                state.TeamBallSpeedBoostRemainingFrames = SecondsToFrames(3f, framesPerSecond);
                result.Effects.OwnTeamBallSpeedBoostFrames = state.TeamBallSpeedBoostRemainingFrames;
                result.Effects.OwnTeamBallSpeedBoostMultiplierPercent = 50;
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
            if (!string.Equals(hero.HeroId, ThornGuardianId, StringComparison.Ordinal))
            {
                return result;
            }

            int maximum = GetThornGrowthMaximum(paths);
            state.ThornGrowthStacks = Math.Min(maximum, state.ThornGrowthStacks + 1);
            result.Effects.OwnPaddleLengthMultiplier = GetThornLengthMultiplier(paths, state);
            if (paths.ThornsLevel >= 1)
            {
                result.Effects.TargetServeAmmoDelta = -1;
            }

            if (paths.ThornsLevel >= 2)
            {
                result.Effects.TargetPaddleSlowFrames = SecondsToFrames(3f, framesPerSecond);
                result.Effects.TargetPaddleMoveSpeedMultiplier = 0.8f;
            }

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
            else if (string.Equals(hero.HeroId, ThornGuardianId, StringComparison.Ordinal))
            {
                state.ThornArmorRemainingFrames = SecondsToFrames(4f, framesPerSecond);
                state.ThornArmorGrowthFrameProgress = 0;
            }
            else if (string.Equals(hero.HeroId, RadiantPaladinId, StringComparison.Ordinal))
            {
                float duration = paths.HolyLightLevel >= 2 ? 3f : paths.HolyLightLevel >= 1 ? 2f : 1.5f;
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
                int level = runtimePath != null ? Math.Min(2, Math.Max(0, runtimePath.Level)) : 0;
                if (IsCategoryPair(path.ResonanceCategories, ChipCategory.Strike, ChipCategory.Guard))
                {
                    levels.SetStrikeGuard(level);
                }
                else if (IsCategoryPair(path.ResonanceCategories, ChipCategory.Guard, ChipCategory.Flow))
                {
                    levels.SetGuardFlow(level);
                }
                else if (IsCategoryPair(path.ResonanceCategories, ChipCategory.Strike, ChipCategory.Flow))
                {
                    levels.SetStrikeFlow(level);
                }
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

        private static HeroFrostStackState GetOrCreateFrost(HeroCombatState state, int opponentPlayerId)
        {
            HeroFrostStackState frost = state.FrostByOpponent.FirstOrDefault(item => item.OpponentPlayerId == opponentPlayerId);
            if (frost != null)
            {
                return frost;
            }

            frost = new HeroFrostStackState { OpponentPlayerId = opponentPlayerId };
            state.FrostByOpponent.Add(frost);
            state.FrostByOpponent.Sort((left, right) => left.OpponentPlayerId.CompareTo(right.OpponentPlayerId));
            return frost;
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

            public void SetStrikeGuard(int level)
            {
                ExtremeColdLevel = level;
                ThornsLevel = level;
                HolyLightLevel = level;
            }

            public void SetGuardFlow(int level)
            {
                IceCrystalLevel = level;
                GrowthLevel = level;
            }

            public void SetStrikeFlow(int level)
            {
                BrillianceLevel = level;
            }
        }
    }
}
