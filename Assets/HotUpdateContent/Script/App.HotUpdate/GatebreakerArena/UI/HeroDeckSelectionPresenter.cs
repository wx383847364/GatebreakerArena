using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Core;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.UI
{
    // Pure selection state for UI bindings. This class deliberately validates only
    // catalog identity and deck-shape rules; it does not awaken chips or calculate paths.
    public sealed class HeroDeckSelectionPresenter
    {
        public const int MaxDeckSize = 8;
        public const int MaxChipCountPerCategory = 3;

        public const string FrostQueenHeroId = "HERO_FROST_QUEEN";
        public const string ThornGuardianHeroId = "HERO_THORN_GUARDIAN";
        public const string RadiantPaladinHeroId = "HERO_RADIANT_PALADIN";

        private static readonly string[] V1HeroIds =
        {
            FrostQueenHeroId,
            ThornGuardianHeroId,
            RadiantPaladinHeroId,
        };

        private static readonly HashSet<string> V1ChipIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "STRIKE_POWER",
            "STRIKE_SERVE",
            "STRIKE_OVERCHARGE",
            "GUARD_LENGTH",
            "GUARD_GOAL",
            "GUARD_BOUNCE",
            "FLOW_SPEED",
            "FLOW_AMMO",
            "FLOW_CAPACITY",
            "CHAOS_SPIN",
            "CHAOS_RICOCHET",
            "CHAOS_DISRUPT",
        };

        private readonly GatebreakerModeCatalog _catalog;
        private readonly List<string> _selectedDeckChipIds = new List<string>();
        private readonly IReadOnlyList<HeroDeckHeroOption> _availableHeroes;
        private readonly IReadOnlyList<HeroDeckChipOption> _availableChips;

        public HeroDeckSelectionPresenter(GatebreakerModeCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _availableHeroes = BuildAvailableHeroes(catalog);
            _availableChips = BuildAvailableChips(catalog);
        }

        public IReadOnlyList<HeroDeckHeroOption> AvailableHeroes => _availableHeroes;
        public IReadOnlyList<HeroDeckChipOption> AvailableChips => _availableChips;
        public IReadOnlyList<string> SelectedDeckChipIds => _selectedDeckChipIds;
        public string SelectedHeroId { get; private set; } = string.Empty;

        public bool TrySelectHero(string heroId, out HeroDeckSelectionValidation validation)
        {
            if (string.IsNullOrEmpty(heroId) || !AvailableHeroes.Any(option => option.HeroId == heroId))
            {
                validation = HeroDeckSelectionValidation.Fail(
                    HeroDeckSelectionFailure.UnknownHero,
                    "The selected hero is not available in the V1 catalog.");
                return false;
            }

            SelectedHeroId = heroId;
            validation = HeroDeckSelectionValidation.Success();
            return true;
        }

        public bool TryAddChip(string chipId, out HeroDeckSelectionValidation validation)
        {
            if (!TryGetAvailableChip(chipId, out HeroDeckChipOption chip))
            {
                validation = HeroDeckSelectionValidation.Fail(
                    HeroDeckSelectionFailure.UnknownChip,
                    "The selected chip is not available in the V1 catalog.");
                return false;
            }

            if (_selectedDeckChipIds.Contains(chipId))
            {
                validation = HeroDeckSelectionValidation.Fail(
                    HeroDeckSelectionFailure.DuplicateChip,
                    "A deck cannot contain the same chip more than once.");
                return false;
            }

            if (_selectedDeckChipIds.Count >= MaxDeckSize)
            {
                validation = HeroDeckSelectionValidation.Fail(
                    HeroDeckSelectionFailure.DeckFull,
                    "A V1 deck can contain at most eight chips.");
                return false;
            }

            int categoryCount = _selectedDeckChipIds
                .Select(id => GetAvailableChip(id))
                .Count(item => item.Category == chip.Category);
            if (categoryCount >= MaxChipCountPerCategory)
            {
                validation = HeroDeckSelectionValidation.Fail(
                    HeroDeckSelectionFailure.CategoryLimitReached,
                    "A V1 deck can contain at most three chips from a category.");
                return false;
            }

            _selectedDeckChipIds.Add(chipId);
            validation = HeroDeckSelectionValidation.Success();
            return true;
        }

        public bool RemoveChip(string chipId)
        {
            return _selectedDeckChipIds.Remove(chipId);
        }

        public void ClearDeck()
        {
            _selectedDeckChipIds.Clear();
        }

        public bool TryCreatePlayerSlot(
            int slotIndex,
            int sideOrder,
            int playerId,
            bool isAi,
            out GatebreakerMatchPlayerSlot playerSlot,
            out HeroDeckSelectionValidation validation)
        {
            if (string.IsNullOrEmpty(SelectedHeroId))
            {
                playerSlot = null;
                validation = HeroDeckSelectionValidation.Fail(
                    HeroDeckSelectionFailure.HeroNotSelected,
                    "Select a V1 hero before creating the match slot.");
                return false;
            }

            if (playerId <= 0)
            {
                playerSlot = null;
                validation = HeroDeckSelectionValidation.Fail(
                    HeroDeckSelectionFailure.InvalidPlayerId,
                    "The match player id must be positive.");
                return false;
            }

            playerSlot = new GatebreakerMatchPlayerSlot
            {
                SlotIndex = slotIndex,
                SideOrder = sideOrder,
                PlayerId = playerId,
                IsAi = isAi,
                HeroId = SelectedHeroId,
                DeckChipIds = _selectedDeckChipIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            };
            validation = HeroDeckSelectionValidation.Success();
            return true;
        }

        private bool TryGetAvailableChip(string chipId, out HeroDeckChipOption chip)
        {
            chip = AvailableChips.FirstOrDefault(option => option.ChipId == chipId);
            return chip != null;
        }

        private HeroDeckChipOption GetAvailableChip(string chipId)
        {
            TryGetAvailableChip(chipId, out HeroDeckChipOption chip);
            return chip;
        }

        private static IReadOnlyList<HeroDeckHeroOption> BuildAvailableHeroes(GatebreakerModeCatalog catalog)
        {
            var heroes = new List<HeroDeckHeroOption>(V1HeroIds.Length);
            for (int i = 0; i < V1HeroIds.Length; i++)
            {
                string heroId = V1HeroIds[i];
                if (!catalog.AllHeroes.TryGetValue(heroId, out HeroDefinition hero) || hero == null)
                {
                    continue;
                }

                heroes.Add(new HeroDeckHeroOption(hero.HeroId, hero.DisplayName, hero.Description));
            }

            return heroes;
        }

        private static IReadOnlyList<HeroDeckChipOption> BuildAvailableChips(GatebreakerModeCatalog catalog)
        {
            return catalog.AllUniversalChips.Values
                .Where(chip => chip != null && V1ChipIds.Contains(chip.ChipId))
                .OrderBy(chip => chip.ChipId, StringComparer.Ordinal)
                .Select(chip => new HeroDeckChipOption(chip.ChipId, chip.DisplayName, chip.Category, chip.Description))
                .ToArray();
        }
    }

    public sealed class HeroDeckHeroOption
    {
        public HeroDeckHeroOption(string heroId, string displayName, string description)
        {
            HeroId = heroId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string HeroId { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }

    public sealed class HeroDeckChipOption
    {
        public HeroDeckChipOption(string chipId, string displayName, ChipCategory category, string description)
        {
            ChipId = chipId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category;
            Description = description ?? string.Empty;
        }

        public string ChipId { get; }
        public string DisplayName { get; }
        public ChipCategory Category { get; }
        public string Description { get; }
    }

    public enum HeroDeckSelectionFailure
    {
        None = 0,
        UnknownHero = 1,
        UnknownChip = 2,
        DuplicateChip = 3,
        DeckFull = 4,
        CategoryLimitReached = 5,
        HeroNotSelected = 6,
        InvalidPlayerId = 7,
    }

    public sealed class HeroDeckSelectionValidation
    {
        private HeroDeckSelectionValidation(HeroDeckSelectionFailure failure, string message)
        {
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public HeroDeckSelectionFailure Failure { get; }
        public string Message { get; }
        public bool IsValid => Failure == HeroDeckSelectionFailure.None;

        public static HeroDeckSelectionValidation Success()
        {
            return new HeroDeckSelectionValidation(HeroDeckSelectionFailure.None, string.Empty);
        }

        public static HeroDeckSelectionValidation Fail(HeroDeckSelectionFailure failure, string message)
        {
            return new HeroDeckSelectionValidation(failure, message);
        }
    }
}
