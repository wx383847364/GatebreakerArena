using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.BrickDuel;
using App.HotUpdate.GatebreakerArena.Mode;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Phase
{
    public enum PhaseSettlementStatus
    {
        Completed = 0,
        AlreadySettled = 1,
        NotEligible = 2,
        PersistenceFailed = 3,
    }

    public readonly struct PhaseSettlementResult
    {
        public PhaseSettlementResult(PhaseSettlementStatus status, int reward)
        {
            Status = status;
            Reward = reward;
        }

        public PhaseSettlementStatus Status { get; }
        public int Reward { get; }
        public bool IsFinal => Status == PhaseSettlementStatus.Completed ||
                               Status == PhaseSettlementStatus.AlreadySettled ||
                               Status == PhaseSettlementStatus.NotEligible;
    }

    [Serializable]
    public sealed class PhasePlayerProfile
    {
        public int SchemaVersion = 1;
        public int Currency = 15;
        public string LastSelectedHeroId = "HERO_MIRAGE";
        public List<string> UnlockedTechIds = new List<string>();
        public List<PhaseSavedLoadout> Loadouts = new List<PhaseSavedLoadout>();
        public List<string> SettledMatchIds = new List<string>();

        public PhasePlayerProfile Clone() => JsonUtility.FromJson<PhasePlayerProfile>(JsonUtility.ToJson(this));
        public HashSet<string> CreateUnlockedSet() => new HashSet<string>(UnlockedTechIds ?? new List<string>(), StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class PhaseSavedLoadout
    {
        public string HeroId = string.Empty;
        public List<string> TechIds = new List<string>();
        public PhaseMatchLoadout ToLoadout() => new PhaseMatchLoadout(HeroId, TechIds);
    }

    public sealed class PhaseProfileService
    {
        public const string PersistenceKey = "gatebreaker.phase_profile.v1";
        private readonly IPersistence _persistence;
        private readonly GatebreakerModeCatalog _catalog;
        private readonly PhaseMetaDefinition _meta;
        private readonly IAppLogger _logger;
        private readonly SemaphoreSlim _transactionGate = new SemaphoreSlim(1, 1);

        public PhaseProfileService(IPersistence persistence, GatebreakerModeCatalog catalog, IAppLogger logger = null)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _meta = catalog.AllPhaseMetas.Values.FirstOrDefault() ?? throw new InvalidOperationException("Phase meta is missing.");
            _logger = logger;
            Current = CreateDefault();
        }

        public PhasePlayerProfile Current { get; private set; }

        public async Task<PhasePlayerProfile> LoadAsync()
        {
            await _transactionGate.WaitAsync();
            try
            {
                PhasePlayerProfile loaded;
                try
                {
                    byte[] bytes = await _persistence.LoadAsync(PersistenceKey);
                    if (bytes == null || bytes.Length == 0)
                    {
                        loaded = CreateDefault();
                    }
                    else
                    {
                        loaded = JsonUtility.FromJson<PhasePlayerProfile>(Encoding.UTF8.GetString(bytes));
                        if (loaded == null || loaded.SchemaVersion != _meta.ProfileSchemaVersion)
                            throw new FormatException("Unsupported phase profile schema.");
                        Normalize(loaded);
                    }
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning("Phase profile load failed; using a new profile. {0}", exception);
                    loaded = CreateDefault();
                }

                Current = loaded;
                return Current.Clone();
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        public PhaseMatchLoadout GetLoadout(string heroId)
        {
            PhaseSavedLoadout saved = Current.Loadouts?.FirstOrDefault(entry => entry != null && entry.HeroId == heroId);
            if (saved != null)
            {
                PhaseMatchLoadout candidate = saved.ToLoadout();
                if (PhaseLoadoutValidator.Validate(_catalog, candidate, Current.CreateUnlockedSet()).IsValid) return candidate;
            }
            return PhaseMatchLoadout.CreateDefault(_catalog, heroId);
        }

        public async Task<bool> SaveLoadoutAsync(PhaseMatchLoadout loadout)
        {
            await _transactionGate.WaitAsync();
            try
            {
                PhaseLoadoutValidation validation = PhaseLoadoutValidator.Validate(
                    _catalog,
                    loadout,
                    Current.CreateUnlockedSet());
                if (!validation.IsValid) return false;

                PhasePlayerProfile candidate = Current.Clone();
                if (candidate.Loadouts == null) candidate.Loadouts = new List<PhaseSavedLoadout>();
                candidate.Loadouts.RemoveAll(entry => entry == null || entry.HeroId == loadout.HeroId);
                candidate.Loadouts.Add(new PhaseSavedLoadout
                {
                    HeroId = loadout.HeroId,
                    TechIds = loadout.TechIds.ToList(),
                });
                candidate.LastSelectedHeroId = loadout.HeroId;
                if (!await PersistAsync(candidate)) return false;
                Current = candidate;
                return true;
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        public async Task<bool> UnlockAsync(string techId)
        {
            await _transactionGate.WaitAsync();
            try
            {
                if (!_catalog.AllPhaseTechs.TryGetValue(techId ?? string.Empty, out PhaseTechDefinition tech) ||
                    tech.Kind == "Default" || Current.UnlockedTechIds.Contains(techId) ||
                    Current.Currency < tech.CostCurrency)
                    return false;

                PhasePlayerProfile candidate = Current.Clone();
                if (candidate.UnlockedTechIds == null) candidate.UnlockedTechIds = new List<string>();
                candidate.Currency -= tech.CostCurrency;
                candidate.UnlockedTechIds.Add(techId);
                if (!await PersistAsync(candidate)) return false;
                Current = candidate;
                return true;
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        public async Task<int> SettleAsync(string matchId, BrickDuelResult localResult, bool completedNormally)
        {
            PhaseSettlementResult result = await SettleWithResultAsync(matchId, localResult, completedNormally);
            return result.Status == PhaseSettlementStatus.Completed ? result.Reward : 0;
        }

        public async Task<PhaseSettlementResult> SettleWithResultAsync(
            string matchId,
            BrickDuelResult localResult,
            bool completedNormally)
        {
            await _transactionGate.WaitAsync();
            try
            {
                if (!completedNormally || string.IsNullOrWhiteSpace(matchId))
                    return new PhaseSettlementResult(PhaseSettlementStatus.NotEligible, 0);
                if (Current.SettledMatchIds.Contains(matchId))
                    return new PhaseSettlementResult(PhaseSettlementStatus.AlreadySettled, 0);
                int reward = localResult == BrickDuelResult.PlayerWin ? _meta.CurrencyWin :
                    localResult == BrickDuelResult.Draw ? _meta.CurrencyDraw :
                    localResult == BrickDuelResult.PlayerLose ? _meta.CurrencyLoss : 0;
                if (reward <= 0)
                    return new PhaseSettlementResult(PhaseSettlementStatus.NotEligible, 0);

                PhasePlayerProfile candidate = Current.Clone();
                if (candidate.SettledMatchIds == null) candidate.SettledMatchIds = new List<string>();
                candidate.Currency += reward;
                candidate.SettledMatchIds.Add(matchId);
                int capacity = Math.Max(1, _meta.SettlementHistoryCapacity);
                while (candidate.SettledMatchIds.Count > capacity) candidate.SettledMatchIds.RemoveAt(0);
                if (!await PersistAsync(candidate))
                    return new PhaseSettlementResult(PhaseSettlementStatus.PersistenceFailed, 0);
                Current = candidate;
                return new PhaseSettlementResult(PhaseSettlementStatus.Completed, reward);
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        private PhasePlayerProfile CreateDefault()
        {
            var profile = new PhasePlayerProfile
            {
                SchemaVersion = _meta.ProfileSchemaVersion,
                Currency = _meta.StartingCurrency,
                LastSelectedHeroId = "HERO_MIRAGE",
            };
            foreach (string heroId in _catalog.AllPhaseHeroes.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                PhaseMatchLoadout loadout = PhaseMatchLoadout.CreateDefault(_catalog, heroId);
                profile.Loadouts.Add(new PhaseSavedLoadout { HeroId = heroId, TechIds = loadout.TechIds.ToList() });
            }
            return profile;
        }

        private void Normalize(PhasePlayerProfile profile)
        {
            profile.Currency = Math.Max(0, profile.Currency);
            profile.UnlockedTechIds = (profile.UnlockedTechIds ?? new List<string>()).Where(id => _catalog.AllPhaseTechs.ContainsKey(id)).Distinct().ToList();
            profile.Loadouts = profile.Loadouts ?? new List<PhaseSavedLoadout>();
            var settledMatchIds = (profile.SettledMatchIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            var historyCapacity = Math.Max(1, _meta.SettlementHistoryCapacity);
            profile.SettledMatchIds = settledMatchIds.Count <= historyCapacity
                ? settledMatchIds
                : settledMatchIds.Skip(settledMatchIds.Count - historyCapacity).ToList();
        }

        private async Task<bool> PersistAsync(PhasePlayerProfile candidate)
        {
            try
            {
                return await _persistence.SaveAsync(
                    PersistenceKey,
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(candidate)));
            }
            catch (Exception exception)
            {
                _logger?.LogWarning("Phase profile persistence failed. {0}", exception);
                return false;
            }
        }
    }
}
