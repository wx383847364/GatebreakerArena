using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Phase
{
    public sealed class PhaseTechModifierSet
    {
        private readonly Dictionary<string, int> _percentByItem = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _stepByItem = new Dictionary<string, int>(StringComparer.Ordinal);

        public static PhaseTechModifierSet Compile(GatebreakerModeCatalog catalog, PhaseMatchLoadout loadout)
        {
            PhaseLoadoutValidation validation = PhaseLoadoutValidator.Validate(catalog, loadout,
                new HashSet<string>(loadout?.TechIds ?? Array.Empty<string>(), StringComparer.Ordinal));
            if (!validation.IsValid) throw new InvalidOperationException(validation.Error);
            var result = new PhaseTechModifierSet();
            foreach (string id in loadout.TechIds)
            {
                PhaseTechDefinition tech = catalog.GetPhaseTech(id);
                foreach (PhaseTechEffectDefinition effect in tech.Effects ?? Array.Empty<PhaseTechEffectDefinition>())
                {
                    int sign = string.Equals(effect.Op, "Weaken", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
                    result.Add(result._percentByItem, effect.ItemId, sign * Math.Abs(effect.MagnitudePercent));
                    if (effect.MagnitudeStep != 0)
                        result.Add(result._stepByItem, effect.ItemId, sign * Math.Abs(effect.MagnitudeStep));
                }
            }
            return result;
        }

        public int GetPercent(string itemId) => Get(_percentByItem, itemId);
        public int GetStep(string itemId) => Get(_stepByItem, itemId);

        public float ApplyPercent(PhaseItemDefinition item, float baseValue)
        {
            if (item == null) return baseValue;
            float value = baseValue * (1f + GetPercent(item.ItemId) / 100f);
            return Mathf.Clamp(value, item.MinValue, item.MaxValue);
        }

        public float ApplyPercentValue(string itemId, float baseValue) =>
            baseValue * (1f + GetPercent(itemId) / 100f);

        public int ApplyStep(PhaseItemDefinition item, int baseValue)
        {
            if (item == null) return baseValue;
            return Mathf.RoundToInt(Mathf.Clamp(baseValue + GetStep(item.ItemId), item.MinValue, item.MaxValue));
        }

        private static int Get(Dictionary<string, int> source, string key) =>
            key != null && source.TryGetValue(key, out int value) ? value : 0;

        private void Add(Dictionary<string, int> target, string key, int value)
        {
            if (string.IsNullOrEmpty(key) || value == 0) return;
            target[key] = Get(target, key) + value;
        }
    }
}
