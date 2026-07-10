using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.Chip
{
    /// <summary>Validates the immutable V1 deck contract before a match starts.</summary>
    public static class DeckValidator
    {
        public const int MinimumDeckSize = 3;
        public const int MaximumDeckSize = 8;
        public const int MaximumChipsPerCategory = 3;

        private static readonly HashSet<string> V1ChipIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "STRIKE_POWER", "STRIKE_SERVE", "STRIKE_OVERCHARGE",
            "GUARD_LENGTH", "GUARD_GOAL", "GUARD_BOUNCE",
            "FLOW_SPEED", "FLOW_AMMO", "FLOW_CAPACITY",
            "CHAOS_SPIN", "CHAOS_RICOCHET", "CHAOS_DISRUPT",
        };

        public static bool IsV1UniversalChipId(string chipId)
        {
            return !string.IsNullOrEmpty(chipId) && V1ChipIds.Contains(chipId);
        }

        public static DeckValidationResult Validate(GatebreakerModeCatalog catalog, IEnumerable<string> deckChipIds)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            string[] deck = (deckChipIds ?? Enumerable.Empty<string>()).ToArray();
            if (deck.Length < MinimumDeckSize)
            {
                return DeckValidationResult.Fail(DeckValidationFailure.TooFewChips, "A V1 deck must contain at least three chips.");
            }

            if (deck.Length > MaximumDeckSize)
            {
                return DeckValidationResult.Fail(DeckValidationFailure.TooManyChips, "A V1 deck can contain at most eight chips.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var categoryCounts = new Dictionary<ChipCategory, int>();
            foreach (string chipId in deck)
            {
                if (!IsV1UniversalChipId(chipId) || !catalog.AllUniversalChips.ContainsKey(chipId))
                {
                    return DeckValidationResult.Fail(DeckValidationFailure.UnknownOrUnavailableChip, $"'{chipId}' is not a legal V1 universal chip.");
                }

                if (!seen.Add(chipId))
                {
                    return DeckValidationResult.Fail(DeckValidationFailure.DuplicateChip, $"'{chipId}' occurs more than once.");
                }

                ChipCategory category = catalog.GetUniversalChip(chipId).Category;
                categoryCounts.TryGetValue(category, out int count);
                count++;
                if (count > MaximumChipsPerCategory)
                {
                    return DeckValidationResult.Fail(DeckValidationFailure.CategoryLimitExceeded, $"A V1 deck can contain at most three {category} chips.");
                }

                categoryCounts[category] = count;
            }

            return DeckValidationResult.Success(deck.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
    }

    public enum DeckValidationFailure
    {
        None = 0,
        TooFewChips = 1,
        TooManyChips = 2,
        DuplicateChip = 3,
        UnknownOrUnavailableChip = 4,
        CategoryLimitExceeded = 5,
    }

    public sealed class DeckValidationResult
    {
        private DeckValidationResult(bool isValid, DeckValidationFailure failure, string message, IReadOnlyList<string> canonicalDeckChipIds)
        {
            IsValid = isValid;
            Failure = failure;
            Message = message;
            CanonicalDeckChipIds = canonicalDeckChipIds ?? Array.Empty<string>();
        }

        public bool IsValid { get; }
        public DeckValidationFailure Failure { get; }
        public string Message { get; }
        public IReadOnlyList<string> CanonicalDeckChipIds { get; }

        public static DeckValidationResult Success(IReadOnlyList<string> canonicalDeckChipIds)
        {
            return new DeckValidationResult(true, DeckValidationFailure.None, string.Empty, canonicalDeckChipIds);
        }

        public static DeckValidationResult Fail(DeckValidationFailure failure, string message)
        {
            return new DeckValidationResult(false, failure, message, Array.Empty<string>());
        }
    }
}
