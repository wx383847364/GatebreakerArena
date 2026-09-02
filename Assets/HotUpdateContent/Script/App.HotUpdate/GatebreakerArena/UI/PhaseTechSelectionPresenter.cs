using System;
using System.Collections.Generic;
using System.Linq;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Phase;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class PhaseTechSelectionPresenter
    {
        private readonly GatebreakerModeCatalog _catalog;
        private readonly string[] _selectedTechIds = new string[5];

        public PhaseTechSelectionPresenter(GatebreakerModeCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            AvailableHeroes = catalog.AllPhaseHeroes.Values
                .OrderBy(hero => hero.HeroId, StringComparer.Ordinal).ToArray();
            if (AvailableHeroes.Count == 0) throw new InvalidOperationException("Phase heroes are missing.");
            SelectHero(AvailableHeroes[0].HeroId);
        }

        public IReadOnlyList<PhaseHeroDefinition> AvailableHeroes { get; }
        public string SelectedHeroId { get; private set; }

        public IReadOnlyList<PhaseTechDefinition> GetOptions(int phaseIndex)
        {
            string phase = "P" + (phaseIndex + 1);
            return _catalog.AllPhaseTechs.Values
                .Where(tech => tech.HeroId == SelectedHeroId && tech.SlotPhase == phase)
                .OrderBy(tech => tech.Kind == "Default" ? 0 : 1)
                .ThenBy(tech => tech.TechId, StringComparer.Ordinal)
                .ToArray();
        }

        public void SelectHero(string heroId)
        {
            if (!_catalog.AllPhaseHeroes.ContainsKey(heroId)) throw new ArgumentException("Unknown phase hero.", nameof(heroId));
            SelectedHeroId = heroId;
            PhaseMatchLoadout defaults = PhaseMatchLoadout.CreateDefault(_catalog, heroId);
            for (int i = 0; i < _selectedTechIds.Length; i++) _selectedTechIds[i] = defaults.TechIds[i];
        }

        public bool TryApplyLoadout(
            PhaseMatchLoadout loadout,
            ISet<string> unlockedTechIds,
            out string error)
        {
            PhaseLoadoutValidation validation = PhaseLoadoutValidator.Validate(
                _catalog,
                loadout,
                unlockedTechIds);
            if (!validation.IsValid)
            {
                error = validation.Error;
                return false;
            }

            SelectedHeroId = loadout.HeroId;
            for (int i = 0; i < _selectedTechIds.Length; i++)
                _selectedTechIds[i] = loadout.TechIds[i];
            error = string.Empty;
            return true;
        }

        public bool TrySelectTech(int phaseIndex, int optionIndex, ISet<string> unlockedTechIds, out string error)
        {
            IReadOnlyList<PhaseTechDefinition> options = GetOptions(phaseIndex);
            if (phaseIndex < 0 || phaseIndex >= 5 || optionIndex < 0 || optionIndex >= options.Count)
            {
                error = "相位科技槽位无效。";
                return false;
            }
            PhaseTechDefinition tech = options[optionIndex];
            if (tech.Kind != "Default" && (unlockedTechIds == null || !unlockedTechIds.Contains(tech.TechId)))
            {
                error = $"{tech.DisplayName} 尚未解锁（需要 {tech.CostCurrency} 相位币）。";
                return false;
            }
            _selectedTechIds[phaseIndex] = tech.TechId;
            error = string.Empty;
            return true;
        }

        public PhaseMatchLoadout Build(ISet<string> unlockedTechIds = null)
        {
            var loadout = new PhaseMatchLoadout(SelectedHeroId, _selectedTechIds);
            PhaseLoadoutValidation validation = PhaseLoadoutValidator.Validate(_catalog, loadout, unlockedTechIds);
            if (!validation.IsValid) throw new InvalidOperationException(validation.Error);
            return loadout;
        }

        public int GetSelectedOptionIndex(int phaseIndex)
        {
            IReadOnlyList<PhaseTechDefinition> options = GetOptions(phaseIndex);
            for (int i = 0; i < options.Count; i++) if (options[i].TechId == _selectedTechIds[phaseIndex]) return i;
            return 0;
        }
    }
}
