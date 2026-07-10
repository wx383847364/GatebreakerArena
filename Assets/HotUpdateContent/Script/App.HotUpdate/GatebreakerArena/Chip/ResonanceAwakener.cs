using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.Chip
{
    /// <summary>Derives the three pre-match awakenings and hero path levels without runtime side effects.</summary>
    public static class ResonanceAwakener
    {
        public const int AwakeningChipCount = 3;
        public const uint SeedSalt = 0x9E3779B9u;

        public static AwakeningResult Awaken(
            GatebreakerModeCatalog catalog,
            int matchSeed,
            int playerId,
            string heroId,
            IEnumerable<string> deckChipIds)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (playerId <= 0)
            {
                return AwakeningResult.Fail("PlayerId must be positive.");
            }

            if (string.IsNullOrEmpty(heroId) || !catalog.AllHeroes.ContainsKey(heroId))
            {
                return AwakeningResult.Fail($"'{heroId}' is not a V1 hero.");
            }

            DeckValidationResult validation = DeckValidator.Validate(catalog, deckChipIds);
            if (!validation.IsValid)
            {
                return AwakeningResult.Fail(validation.Message);
            }

            var remaining = new List<string>(validation.CanonicalDeckChipIds);
            var random = new GatebreakerDeterministicPrng(unchecked((uint)matchSeed) ^ unchecked((uint)playerId) ^ SeedSalt);
            var active = new List<string>(AwakeningChipCount);
            for (int i = 0; i < AwakeningChipCount; i++)
            {
                int index = random.NextInt(remaining.Count);
                active.Add(remaining[index]);
                remaining.RemoveAt(index);
            }

            active.Sort(StringComparer.Ordinal);
            IReadOnlyList<HeroPathRuntimeState> pathStates = CalculatePathStates(catalog, heroId, active);
            return AwakeningResult.Success(validation.CanonicalDeckChipIds, active, pathStates);
        }

        public static IReadOnlyList<HeroPathRuntimeState> CalculatePathStates(
            GatebreakerModeCatalog catalog,
            string heroId,
            IEnumerable<string> activeChipIds)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            HeroDefinition hero = catalog.GetHero(heroId);
            var categoryCounts = new Dictionary<ChipCategory, int>();
            foreach (string chipId in activeChipIds ?? Enumerable.Empty<string>())
            {
                UniversalChipDefinition chip = catalog.GetUniversalChip(chipId);
                categoryCounts.TryGetValue(chip.Category, out int count);
                categoryCounts[chip.Category] = count + 1;
            }

            var states = new List<HeroPathRuntimeState>();
            foreach (string pathId in hero.PathIds ?? Array.Empty<string>())
            {
                HeroPathDefinition path = catalog.GetHeroPath(pathId);
                int matchingChips = 0;
                foreach (ChipCategory category in path.ResonanceCategories ?? Array.Empty<ChipCategory>())
                {
                    categoryCounts.TryGetValue(category, out int count);
                    matchingChips += count;
                }

                states.Add(new HeroPathRuntimeState
                {
                    PathId = path.PathId,
                    Level = Math.Min(2, matchingChips),
                });
            }

            return states.OrderBy(state => state.PathId, StringComparer.Ordinal).ToArray();
        }
    }

    public sealed class AwakeningResult
    {
        private AwakeningResult(bool isValid, string error, IReadOnlyList<string> canonicalDeckChipIds, IReadOnlyList<string> activeChipIds, IReadOnlyList<HeroPathRuntimeState> pathStates)
        {
            IsValid = isValid;
            Error = error;
            CanonicalDeckChipIds = canonicalDeckChipIds ?? Array.Empty<string>();
            ActiveChipIds = activeChipIds ?? Array.Empty<string>();
            PathStates = pathStates ?? Array.Empty<HeroPathRuntimeState>();
        }

        public bool IsValid { get; }
        public string Error { get; }
        public IReadOnlyList<string> CanonicalDeckChipIds { get; }
        public IReadOnlyList<string> ActiveChipIds { get; }
        public IReadOnlyList<HeroPathRuntimeState> PathStates { get; }

        public static AwakeningResult Success(IReadOnlyList<string> canonicalDeckChipIds, IReadOnlyList<string> activeChipIds, IReadOnlyList<HeroPathRuntimeState> pathStates)
        {
            return new AwakeningResult(true, string.Empty, canonicalDeckChipIds, activeChipIds, pathStates);
        }

        public static AwakeningResult Fail(string error)
        {
            return new AwakeningResult(false, error, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<HeroPathRuntimeState>());
        }
    }
}
