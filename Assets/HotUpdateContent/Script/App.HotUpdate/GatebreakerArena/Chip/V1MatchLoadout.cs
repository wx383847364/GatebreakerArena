using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.Chip
{
    /// <summary>Immutable, ordered V1 match contract. Order is gameplay data, never sort it.</summary>
    [Serializable]
    public sealed class V1MatchLoadout
    {
        public const int UniversalChipCount = 5;
        public const int OpeningChipCount = 2;
        public const int ScheduledChipCount = 3;
        private readonly string[] _openingUniversalChipIds;
        private readonly string[] _scheduledUniversalChipIds;

        public V1MatchLoadout(string heroId, string pathId, string signatureChipId,
            IEnumerable<string> openingUniversalChipIds, IEnumerable<string> scheduledUniversalChipIds)
        {
            HeroId = heroId ?? string.Empty;
            PathId = pathId ?? string.Empty;
            SignatureChipId = signatureChipId ?? string.Empty;
            _openingUniversalChipIds = (openingUniversalChipIds ?? Enumerable.Empty<string>()).ToArray();
            _scheduledUniversalChipIds = (scheduledUniversalChipIds ?? Enumerable.Empty<string>()).ToArray();
        }

        public string HeroId { get; }
        public string PathId { get; }
        public string SignatureChipId { get; }
        public IReadOnlyList<string> OpeningUniversalChipIds => Array.AsReadOnly(_openingUniversalChipIds);
        public IReadOnlyList<string> ScheduledUniversalChipIds => Array.AsReadOnly(_scheduledUniversalChipIds);

        public IReadOnlyList<string> OrderedUniversalChipIds => Array.AsReadOnly(_openingUniversalChipIds.Concat(_scheduledUniversalChipIds).ToArray());
        public V1MatchLoadout Clone() => new V1MatchLoadout(HeroId, PathId, SignatureChipId, _openingUniversalChipIds, _scheduledUniversalChipIds);
    }

    public static class V1MatchLoadoutValidator
    {
        private static readonly HashSet<string> V1HeroIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "HERO_FROST_QUEEN", "HERO_MECH_ENGINEER", "HERO_RADIANT_PALADIN",
        };
        public static LoadoutValidationResult Validate(GatebreakerModeCatalog catalog, V1MatchLoadout loadout)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (loadout == null) return LoadoutValidationResult.Fail("Loadout is required.");
            if (!V1HeroIds.Contains(loadout.HeroId) || !catalog.AllHeroes.TryGetValue(loadout.HeroId, out HeroDefinition hero)) return LoadoutValidationResult.Fail("Unknown V1 hero.");
            if (!catalog.AllHeroPaths.TryGetValue(loadout.PathId ?? string.Empty, out HeroPathDefinition path) || path.HeroId != hero.HeroId) return LoadoutValidationResult.Fail("Path does not belong to hero.");
            if (!catalog.AllSignatureChips.TryGetValue(loadout.SignatureChipId ?? string.Empty, out SignatureChipDefinition signature) || signature.HeroId != hero.HeroId || signature.PathId != path.PathId || signature.ResonanceValue != 3) return LoadoutValidationResult.Fail("Signature chip does not belong to hero path.");
            string[] opening = (loadout.OpeningUniversalChipIds ?? Array.Empty<string>()).ToArray();
            string[] scheduled = (loadout.ScheduledUniversalChipIds ?? Array.Empty<string>()).ToArray();
            if (opening.Length != V1MatchLoadout.OpeningChipCount || scheduled.Length != V1MatchLoadout.ScheduledChipCount) return LoadoutValidationResult.Fail("V1 requires two opening and three scheduled universal chips.");
            string[] chips = opening.Concat(scheduled).ToArray();
            if (chips.Length != V1MatchLoadout.UniversalChipCount || chips.Distinct(StringComparer.Ordinal).Count() != chips.Length) return LoadoutValidationResult.Fail("Universal chips must be five unique entries.");
            var categoryCounts = new Dictionary<ChipCategory, int>();
            foreach (string chipId in chips)
            {
                if (!DeckValidator.IsV1UniversalChipId(chipId) || !catalog.AllUniversalChips.TryGetValue(chipId, out UniversalChipDefinition chip)) return LoadoutValidationResult.Fail("Unknown or unavailable universal chip.");
                categoryCounts.TryGetValue(chip.Category, out int count);
                if (++count > 3) return LoadoutValidationResult.Fail("At most three universal chips per category.");
                categoryCounts[chip.Category] = count;
            }
            if (!opening.Select(catalog.GetUniversalChip).Any(chip => path.ResonanceCategories.Contains(chip.Category))) return LoadoutValidationResult.Fail("At least one opening chip must match the path.");
            return LoadoutValidationResult.Success();
        }
    }

    public sealed class LoadoutValidationResult
    {
        private LoadoutValidationResult(bool isValid, string error) { IsValid = isValid; Error = error; }
        public bool IsValid { get; }
        public string Error { get; }
        public static LoadoutValidationResult Success() => new LoadoutValidationResult(true, string.Empty);
        public static LoadoutValidationResult Fail(string error) => new LoadoutValidationResult(false, error);
    }

    public static class V1ActivationSchedule
    {
        public const int FirstScheduledFrame = 450;
        public const int SecondScheduledFrame = 900;
        public const int FinalScheduledFrame = 1350;
        public static IReadOnlyList<string> ResolveActive(V1MatchLoadout loadout, int playingFrame)
        {
            if (loadout == null) return Array.Empty<string>();
            var result = new List<string>(1 + V1MatchLoadout.UniversalChipCount) { loadout.SignatureChipId };
            result.AddRange(loadout.OpeningUniversalChipIds ?? Array.Empty<string>());
            int extraCount = playingFrame >= FinalScheduledFrame ? 3 : playingFrame >= SecondScheduledFrame ? 2 : playingFrame >= FirstScheduledFrame ? 1 : 0;
            result.AddRange((loadout.ScheduledUniversalChipIds ?? Array.Empty<string>()).Take(extraCount));
            return result;
        }
        public static int ResolveMilestone(GatebreakerModeCatalog catalog, V1MatchLoadout loadout, int playingFrame)
        {
            if (catalog == null || loadout == null || !catalog.AllHeroPaths.TryGetValue(loadout.PathId, out HeroPathDefinition path)) return 0;
            int matching = ResolveActive(loadout, playingFrame).Where(id => catalog.AllUniversalChips.ContainsKey(id)).Select(catalog.GetUniversalChip).Count(chip => path.ResonanceCategories.Contains(chip.Category));
            if (playingFrame >= FinalScheduledFrame && matching >= 4) return 3;
            if (matching >= 2) return 2;
            return matching >= 1 ? 1 : 0;
        }
    }

    public static class V1ContractHash
    {
        public const int RulesSchemaVersion = 2;
        public static string ComputeLoadout(V1MatchLoadout loadout)
        {
            if (loadout == null) return string.Empty;
            uint hash = 2166136261u;
            Add(ref hash, loadout.HeroId); Add(ref hash, loadout.PathId); Add(ref hash, loadout.SignatureChipId);
            foreach (string id in loadout.OpeningUniversalChipIds) Add(ref hash, id);
            Add(ref hash, "|");
            foreach (string id in loadout.ScheduledUniversalChipIds) Add(ref hash, id);
            return hash.ToString("X8");
        }
        public static string ComputeCatalog(GatebreakerModeCatalog catalog)
        {
            if (catalog == null) return string.Empty;
            uint hash = 2166136261u;
            Add(ref hash, RulesSchemaVersion.ToString());
            foreach (string id in catalog.AllHeroes.Keys.OrderBy(id => id, StringComparer.Ordinal)) Add(ref hash, id);
            foreach (string id in catalog.AllHeroPaths.Keys.OrderBy(id => id, StringComparer.Ordinal)) Add(ref hash, id);
            foreach (string id in catalog.AllUniversalChips.Keys.OrderBy(id => id, StringComparer.Ordinal)) Add(ref hash, id);
            foreach (SignatureChipDefinition chip in catalog.AllSignatureChips.Values.OrderBy(item => item.ChipId, StringComparer.Ordinal))
            {
                Add(ref hash, chip.ChipId); Add(ref hash, chip.HeroId); Add(ref hash, chip.PathId); Add(ref hash, chip.VariantKind);
                foreach (KeyValuePair<string, float> parameter in (chip.Parameters ?? new Dictionary<string, float>()).OrderBy(item => item.Key, StringComparer.Ordinal))
                { Add(ref hash, parameter.Key); Add(ref hash, parameter.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); }
            }
            return hash.ToString("X8");
        }
        private static void Add(ref uint hash, string value)
        {
            foreach (char ch in value ?? string.Empty) { hash ^= ch; hash *= 16777619u; }
            hash ^= 0xFF; hash *= 16777619u;
        }
    }
}
