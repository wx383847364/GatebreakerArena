using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.Phase
{
    [Serializable]
    public sealed class PhaseMatchLoadout
    {
        public PhaseMatchLoadout(string heroId, IEnumerable<string> techIds)
        {
            HeroId = heroId ?? string.Empty;
            TechIds = (techIds ?? Array.Empty<string>()).ToArray();
        }

        public string HeroId { get; }
        public IReadOnlyList<string> TechIds { get; }
        public string LoadoutHash => ComputeHash(HeroId, TechIds);

        public PhaseMatchLoadout Clone() => new PhaseMatchLoadout(HeroId, TechIds);

        public static PhaseMatchLoadout CreateDefault(GatebreakerModeCatalog catalog, string heroId)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            string[] ids = catalog.AllPhaseTechs.Values
                .Where(tech => tech.HeroId == heroId && tech.Kind == "Default")
                .OrderBy(tech => PhaseLoadoutValidator.PhaseIndex(tech.SlotPhase))
                .Select(tech => tech.TechId)
                .ToArray();
            var result = new PhaseMatchLoadout(heroId, ids);
            PhaseLoadoutValidation validation = PhaseLoadoutValidator.Validate(catalog, result);
            if (!validation.IsValid) throw new InvalidOperationException(validation.Error);
            return result;
        }

        public static string ComputeHash(string heroId, IEnumerable<string> techIds)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            string value = (heroId ?? string.Empty) + "|" + string.Join("|", techIds ?? Array.Empty<string>());
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++) hash = (hash ^ bytes[i]) * prime;
            return hash.ToString("X8");
        }
    }

    public sealed class PhaseLoadoutValidation
    {
        private PhaseLoadoutValidation(string error) { Error = error ?? string.Empty; }
        public string Error { get; }
        public bool IsValid => Error.Length == 0;
        public static PhaseLoadoutValidation Success() => new PhaseLoadoutValidation(string.Empty);
        public static PhaseLoadoutValidation Fail(string error) => new PhaseLoadoutValidation(error);
    }

    public static class PhaseLoadoutValidator
    {
        private static readonly string[] Phases = { "P1", "P2", "P3", "P4", "P5" };

        public static PhaseLoadoutValidation Validate(
            GatebreakerModeCatalog catalog,
            PhaseMatchLoadout loadout,
            ISet<string> unlockedTechIds = null)
        {
            if (catalog == null) return PhaseLoadoutValidation.Fail("Phase catalog is required.");
            if (loadout == null || !catalog.AllPhaseHeroes.ContainsKey(loadout.HeroId))
                return PhaseLoadoutValidation.Fail("Unknown phase hero.");
            if (loadout.TechIds == null || loadout.TechIds.Count != 5)
                return PhaseLoadoutValidation.Fail("Exactly five phase techs are required.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var seenPhases = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < loadout.TechIds.Count; i++)
            {
                string id = loadout.TechIds[i] ?? string.Empty;
                if (!seen.Add(id) || !catalog.AllPhaseTechs.TryGetValue(id, out PhaseTechDefinition tech))
                    return PhaseLoadoutValidation.Fail("Unknown or duplicate phase tech: " + id);
                if (tech.HeroId != loadout.HeroId || !seenPhases.Add(tech.SlotPhase))
                    return PhaseLoadoutValidation.Fail("Tech does not match hero or slot: " + id);
                if (PhaseIndex(tech.SlotPhase) != i)
                    return PhaseLoadoutValidation.Fail("Tech ids must be ordered P1 through P5.");
                if (tech.Kind != "Default" && (unlockedTechIds == null || !unlockedTechIds.Contains(id)))
                    return PhaseLoadoutValidation.Fail("Tech is locked: " + id);
                if (tech.NetOffset != 30)
                    return PhaseLoadoutValidation.Fail("Tech NetOffset must equal 30: " + id);
            }

            return seenPhases.SetEquals(Phases)
                ? PhaseLoadoutValidation.Success()
                : PhaseLoadoutValidation.Fail("One tech from every phase is required.");
        }

        public static int PhaseIndex(string phase)
        {
            for (int i = 0; i < Phases.Length; i++) if (Phases[i] == phase) return i;
            return int.MaxValue;
        }
    }

    /// <summary>
    /// Hashes every phase-growth value that can affect a deterministic match.  The LAN room
    /// uses this in addition to the legacy V1 catalog hash so equal protocol versions cannot
    /// silently connect with different v0.3 tuning data.
    /// </summary>
    public static class PhaseMatchContractHash
    {
        public static string ComputeCatalog(GatebreakerModeCatalog catalog, string legacyCatalogHash)
        {
            if (catalog == null) return string.Empty;
            uint hash = 2166136261u;
            Add(ref hash, legacyCatalogHash);
            if (catalog.TryGetBrickDuelRule("BRICK_DUEL_V0", out BrickDuelRuleDefinition duel))
            {
                Add(ref hash, duel.RuleId); Add(ref hash, duel.SimulationFps.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, duel.CountdownSeconds.ToString(CultureInfo.InvariantCulture)); Add(ref hash, duel.InitialCoreHealth.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, duel.InitialRows.ToString(CultureInfo.InvariantCulture)); Add(ref hash, duel.Columns.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, Float(duel.ArenaHalfWidth)); Add(ref hash, Float(duel.CoreLineY)); Add(ref hash, Float(duel.PaddleSpawnY));
                Add(ref hash, Float(duel.PaddleHalfWidth)); Add(ref hash, Float(duel.PaddleHalfHeight)); Add(ref hash, Float(duel.PaddleMoveSpeed));
                Add(ref hash, Float(duel.BrickWidth)); Add(ref hash, Float(duel.BrickHeight)); Add(ref hash, Float(duel.BallRadius));
                Add(ref hash, Float(duel.BallSpeed)); Add(ref hash, Float(duel.BaseTideSpeed)); Add(ref hash, Float(duel.BallResetSeconds));
                Add(ref hash, Float(duel.StuckTimeoutSeconds)); Add(ref hash, Float(duel.StuckMovementEpsilon));
                Add(ref hash, Float(duel.PressureIntervalSeconds)); Add(ref hash, Float(duel.PressureIncrement)); Add(ref hash, Float(duel.DangerDistance));
                Add(ref hash, duel.GreenHealth.ToString(CultureInfo.InvariantCulture)); Add(ref hash, duel.RedHealth.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, duel.YellowHealth.ToString(CultureInfo.InvariantCulture)); Add(ref hash, duel.MysteryHealth.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, duel.BrickCoreDamage.ToString(CultureInfo.InvariantCulture)); Add(ref hash, duel.RandomSeed.ToString(CultureInfo.InvariantCulture));
                foreach (string pattern in duel.InitialRowPatterns ?? Array.Empty<string>()) Add(ref hash, pattern);
                foreach (BrickDuelItemDropDefinition drop in (duel.ItemDrops ?? Array.Empty<BrickDuelItemDropDefinition>()).OrderBy(item => item.SortOrder))
                {
                    Add(ref hash, drop.DropTableId); Add(ref hash, drop.ItemId); Add(ref hash, drop.SortOrder.ToString(CultureInfo.InvariantCulture));
                    Add(ref hash, Float(drop.DropWeight)); Add(ref hash, drop.BagCopies.ToString(CultureInfo.InvariantCulture));
                    Add(ref hash, drop.Enabled ? "1" : "0"); Add(ref hash, Float(drop.EffectDurationSeconds));
                    Add(ref hash, Float(drop.EffectMagnitude)); Add(ref hash, drop.DurationModifierKey);
                }
                if (catalog.TryGetBrickDuelAiRule(duel.BrickDuelAiRuleId, out BrickDuelAiRuleDefinition ai))
                {
                    Add(ref hash, ai.RuleId); Add(ref hash, ai.DecisionIntervalFrames.ToString(CultureInfo.InvariantCulture));
                    Add(ref hash, Float(ai.EmergencyDistance)); Add(ref hash, Float(ai.MoveDeadZone));
                }
            }
            foreach (PhaseHeroDefinition hero in catalog.AllPhaseHeroes.Values.OrderBy(item => item.HeroId, StringComparer.Ordinal))
            {
                Add(ref hash, hero.HeroId); Add(ref hash, hero.Dimension); Add(ref hash, hero.CoreResource);
                foreach (KeyValuePair<string, object> tuning in (hero.RuntimeTuning ?? new Dictionary<string, object>()).OrderBy(item => item.Key, StringComparer.Ordinal))
                { Add(ref hash, tuning.Key); Add(ref hash, Convert.ToString(tuning.Value, CultureInfo.InvariantCulture)); }
                foreach (PhaseHeroLevelDefinition level in hero.PhaseLevels ?? Array.Empty<PhaseHeroLevelDefinition>())
                {
                    Add(ref hash, level.PhaseLevel); Add(ref hash, level.PhiToReach.ToString(CultureInfo.InvariantCulture));
                    Add(ref hash, Float(level.ActiveAbility?.CooldownSeconds ?? 0f));
                    Add(ref hash, Float(level.ActiveAbility?.DurationSeconds ?? 0f));
                }
                foreach (PhaseHeroPhiSourceDefinition source in hero.PhiSources ?? Array.Empty<PhaseHeroPhiSourceDefinition>())
                { Add(ref hash, source.Source); Add(ref hash, Float(source.Phi)); }
            }
            foreach (PhaseTechDefinition tech in catalog.AllPhaseTechs.Values.OrderBy(item => item.TechId, StringComparer.Ordinal))
            {
                Add(ref hash, tech.TechId); Add(ref hash, tech.HeroId); Add(ref hash, tech.SlotPhase);
                Add(ref hash, tech.Kind); Add(ref hash, tech.CostCurrency.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, tech.NetOffset.ToString(CultureInfo.InvariantCulture)); Add(ref hash, tech.MechanicEffect);
                foreach (PhaseTechEffectDefinition effect in tech.Effects ?? Array.Empty<PhaseTechEffectDefinition>())
                {
                    Add(ref hash, effect.ItemId); Add(ref hash, effect.Op);
                    Add(ref hash, effect.MagnitudePercent.ToString(CultureInfo.InvariantCulture));
                    Add(ref hash, effect.MagnitudeStep.ToString(CultureInfo.InvariantCulture));
                    Add(ref hash, Float(effect.BaseValue)); Add(ref hash, Float(effect.ModifiedValue));
                }
            }
            foreach (PhaseItemDefinition item in catalog.AllPhaseItems.Values.OrderBy(value => value.ItemId, StringComparer.Ordinal))
            {
                Add(ref hash, item.ItemId); Add(ref hash, Float(item.BaseDropWeight));
                Add(ref hash, Float(item.MinValue)); Add(ref hash, Float(item.MaxValue));
                foreach (KeyValuePair<string, object> effect in (item.Effect ?? new Dictionary<string, object>()).OrderBy(value => value.Key, StringComparer.Ordinal))
                { Add(ref hash, effect.Key); Add(ref hash, Convert.ToString(effect.Value, CultureInfo.InvariantCulture)); }
            }
            foreach (PhaseCurveDefinition curve in catalog.AllPhaseCurves.Values.OrderBy(item => item.RuleId, StringComparer.Ordinal))
            {
                Add(ref hash, curve.RuleId); Add(ref hash, Float(curve.CompositionIntervalSeconds));
                Add(ref hash, curve.BreakCounterThreshold.ToString(CultureInfo.InvariantCulture)); Add(ref hash, Float(curve.BreakCounterWarnSeconds));
                foreach (PhaseCurveStageDefinition stage in curve.Stages ?? Array.Empty<PhaseCurveStageDefinition>())
                {
                    Add(ref hash, stage.Stage); Add(ref hash, stage.TimeStart.ToString(CultureInfo.InvariantCulture));
                    Add(ref hash, Float(stage.GreenWeight)); Add(ref hash, Float(stage.YellowWeight));
                    Add(ref hash, Float(stage.RedWeight)); Add(ref hash, Float(stage.MysteryWeight));
                }
            }
            foreach (PhaseMetaDefinition meta in catalog.AllPhaseMetas.Values.OrderBy(item => item.MetaId, StringComparer.Ordinal))
            {
                Add(ref hash, meta.MetaId); Add(ref hash, meta.CurrencyWin.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, meta.CurrencyLoss.ToString(CultureInfo.InvariantCulture)); Add(ref hash, meta.CurrencyDraw.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, meta.StartingCurrency.ToString(CultureInfo.InvariantCulture)); Add(ref hash, meta.PhiPerSecondCap.ToString(CultureInfo.InvariantCulture));
                Add(ref hash, meta.ProfileSchemaVersion.ToString(CultureInfo.InvariantCulture));
            }
            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static void Add(ref uint hash, string value)
        {
            foreach (char ch in value ?? string.Empty) { hash ^= ch; hash *= 16777619u; }
            hash ^= 0xFF; hash *= 16777619u;
        }
    }
}
