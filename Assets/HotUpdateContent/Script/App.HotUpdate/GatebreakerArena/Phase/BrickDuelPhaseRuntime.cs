using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using App.HotUpdate.GatebreakerArena.BrickDuel;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Phase
{
    public enum PhaseAbilityActivation
    {
        None = 0,
        MirageTide = 1,
        PulseBurst = 2,
        RiftPierce = 3,
        RefractMirror = 4,
    }

    [Serializable]
    public sealed class BrickDuelPhaseSideState
    {
        public string HeroId { get; internal set; }
        public string LoadoutHash { get; internal set; }
        public int PhaseLevel { get; internal set; }
        public float Phi { get; internal set; }
        public float PhiGainedThisSecond { get; internal set; }
        public int PhiSecondWindow { get; internal set; }
        public int AbilityCooldownFrames { get; internal set; }
        public int AbilityActiveFrames { get; internal set; }
        public int Combo { get; internal set; }
        public int Tempo { get; internal set; }
        public int TempoIdleFrames { get; internal set; }
        public int RiftCharge { get; internal set; }
        public int HeroPierceCharges { get; internal set; }
        public int ItemPierceCharges { get; internal set; }
        public int PulseHitCounter { get; internal set; }
        public int RefractMarkFrames { get; internal set; }
        public int RefractSpeedStacks { get; internal set; }
        public int RefractPendingSpeedStacks { get; internal set; }
        public int RefractBoostFrames { get; internal set; }
        public bool RefractBoostPending => RefractPendingSpeedStacks > 0;
        public int RefractCycles { get; internal set; }
        public int MirrorFrames { get; internal set; }
        public bool RiftEmpowered { get; internal set; }

        public int TotalPierceCharges => HeroPierceCharges + ItemPierceCharges;
        public bool AbilityUnlocked => PhaseLevel >= 3;
        public bool AbilityAvailable => AbilityUnlocked && AbilityCooldownFrames <= 0;

        public BrickDuelPhaseSideState Clone() => (BrickDuelPhaseSideState)MemberwiseClone();
    }

    /// <summary>
    /// Deterministic per-side phase-growth rules. This class owns gameplay state only;
    /// presentation reads the immutable snapshot exposed by BrickDuelRuntime.
    /// </summary>
    public sealed class BrickDuelPhaseSideRuntime
    {
        private readonly PhaseHeroDefinition _hero;
        private readonly PhaseMetaDefinition _meta;
        private readonly int _fps;

        public BrickDuelPhaseSideRuntime(
            GatebreakerModeCatalog catalog,
            PhaseMatchLoadout loadout,
            int simulationFps)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            PhaseLoadoutValidation validation = PhaseLoadoutValidator.Validate(
                catalog,
                loadout,
                new HashSet<string>(loadout?.TechIds ?? Array.Empty<string>(), StringComparer.Ordinal));
            if (!validation.IsValid) throw new ArgumentException(validation.Error, nameof(loadout));
            _hero = catalog.GetPhaseHero(loadout.HeroId);
            _meta = catalog.AllPhaseMetas.Values.FirstOrDefault()
                ?? throw new InvalidOperationException("DT_PhaseMeta is required for phase matches.");
            Modifiers = PhaseTechModifierSet.Compile(catalog, loadout);
            _fps = Math.Max(1, simulationFps);
            State = new BrickDuelPhaseSideState
            {
                HeroId = loadout.HeroId,
                LoadoutHash = loadout.LoadoutHash,
            };
            Reset();
        }

        public BrickDuelPhaseSideState State { get; }
        public PhaseTechModifierSet Modifiers { get; }
        public bool IsMirage => State.HeroId == "HERO_MIRAGE";
        public bool IsPulse => State.HeroId == "HERO_PULSE";
        public bool IsRift => State.HeroId == "HERO_RIFT";
        public bool IsRefract => State.HeroId == "HERO_REFRACT";

        public void Reset()
        {
            State.PhaseLevel = 1;
            State.Phi = 0f;
            State.PhiGainedThisSecond = 0f;
            State.PhiSecondWindow = -1;
            State.AbilityCooldownFrames = 0;
            State.AbilityActiveFrames = 0;
            State.Combo = 0;
            State.Tempo = 0;
            State.TempoIdleFrames = 0;
            State.RiftCharge = 0;
            State.HeroPierceCharges = 0;
            State.ItemPierceCharges = 0;
            State.PulseHitCounter = 0;
            State.RefractMarkFrames = 0;
            State.RefractSpeedStacks = 0;
            State.RefractPendingSpeedStacks = 0;
            State.RefractBoostFrames = 0;
            State.RefractCycles = 0;
            State.MirrorFrames = 0;
            State.RiftEmpowered = false;
        }

        public void Tick(int simulationFrame)
        {
            int window = simulationFrame / _fps;
            if (window != State.PhiSecondWindow)
            {
                State.PhiSecondWindow = window;
                State.PhiGainedThisSecond = 0f;
            }

            State.AbilityCooldownFrames = Decrement(State.AbilityCooldownFrames);
            State.AbilityActiveFrames = Decrement(State.AbilityActiveFrames);
            State.RefractMarkFrames = Decrement(State.RefractMarkFrames);
            State.RefractBoostFrames = Decrement(State.RefractBoostFrames);
            State.MirrorFrames = Decrement(State.MirrorFrames);

            if (!IsPulse) return;
            State.TempoIdleFrames++;
            int decayFrames = SecondsToFrames(Tune("TempoDecaySeconds", 3f));
            if (State.TempoIdleFrames >= decayFrames)
            {
                if (State.AbilityActiveFrames <= 0 && State.Tempo > 0) State.Tempo--;
                State.TempoIdleFrames = 0;
            }
        }

        public void OnItemCollected() => AddPhi("PickupItem");

        public void OnBrickDestroyed(BrickDuelBrickType type)
        {
            switch (type)
            {
                case BrickDuelBrickType.Mystery: AddPhi("BreakMysteryBrick"); break;
                case BrickDuelBrickType.Red: AddPhi("BreakRedBrick"); break;
                case BrickDuelBrickType.Yellow: AddPhi("BreakYellowBrick"); break;
                default: AddPhi("BreakGreenBrick"); break;
            }
        }

        public int OnMainBallCollision(BrickDuelCollisionFrameTelemetry telemetry)
        {
            if (telemetry == null) return 0;
            int requestedSplitBalls = 0;
            if (telemetry.Events.Count > 0)
            {
                for (int i = 0; i < telemetry.Events.Count; i++)
                    requestedSplitBalls += OnMainBallCollisionEvent(telemetry.Events[i]);
                return requestedSplitBalls;
            }

            // Compatibility path for callers that still construct aggregate telemetry directly.
            for (int i = 0; i < telemetry.OwnOuterWallBounceCount; i++)
            {
                requestedSplitBalls += OnMainBallCollisionEvent(
                    new BrickDuelCollisionEvent(
                        BrickDuelCollisionEventType.OwnOuterWallBounce));
            }

            for (int i = 0; i < telemetry.PaddleBounceCount; i++)
            {
                requestedSplitBalls += OnMainBallCollisionEvent(
                    new BrickDuelCollisionEvent(
                        BrickDuelCollisionEventType.PaddleBounce,
                        paddleRedirectDegrees: telemetry.MaximumPaddleRedirectDegrees));
            }

            for (int i = 0; i < telemetry.BrickHitCount; i++)
            {
                requestedSplitBalls += OnMainBallCollisionEvent(
                    new BrickDuelCollisionEvent(
                        BrickDuelCollisionEventType.BrickHit,
                        pierced: i < telemetry.PiercedBrickHitCount));
            }

            return requestedSplitBalls;
        }

        public int OnMainBallCollisionEvent(BrickDuelCollisionEvent collisionEvent)
        {
            switch (collisionEvent.EventType)
            {
                case BrickDuelCollisionEventType.OwnOuterWallBounce:
                    State.Combo = 0;
                    State.RiftCharge = 0;
                    State.RefractSpeedStacks = 0;
                    State.RefractMarkFrames = 0;
                    State.RefractPendingSpeedStacks = 0;
                    State.RefractBoostFrames = 0;
                    return 0;

                case BrickDuelCollisionEventType.PaddleBounce:
                    if (IsPulse &&
                        collisionEvent.PaddleRedirectDegrees + 0.0001f >=
                        Tune("RedirectMinDegrees", 10f))
                    {
                        State.Tempo = Math.Min(GetTempoCap(), State.Tempo + 1);
                        State.TempoIdleFrames = 0;
                    }

                    if (IsRefract && State.RefractPendingSpeedStacks > 0)
                    {
                        State.RefractSpeedStacks = State.RefractPendingSpeedStacks;
                        State.RefractPendingSpeedStacks = 0;
                        State.RefractBoostFrames = SecondsToFrames(
                            Tune("BounceBoostSeconds", 3f));
                    }

                    if (IsRefract)
                        State.RefractMarkFrames = SecondsToFrames(GetRefractMarkSeconds());
                    return 0;

                case BrickDuelCollisionEventType.BrickHit:
                    return ResolveMainBallBrickHit(collisionEvent.Pierced);

                default:
                    return 0;
            }
        }

        private int ResolveMainBallBrickHit(bool pierced)
        {
            int requestedSplitBalls = 0;
            if (IsMirage)
            {
                State.Combo++;
                int threshold = Mathf.RoundToInt(Tune(
                    State.PhaseLevel >= 5 ? "P5ComboThreshold" :
                    State.PhaseLevel >= 2 ? "P2ComboThreshold" : "P1ComboThreshold",
                    State.PhaseLevel >= 5 ? 4f : State.PhaseLevel >= 2 ? 6f : 8f));
                if (State.Combo >= threshold)
                {
                    State.Combo = 0;
                    requestedSplitBalls = Mathf.RoundToInt(Tune(
                        State.PhaseLevel >= 4 ? "P4SplitCount" : "P1SplitCount",
                        State.PhaseLevel >= 4 ? 2f : 1f));
                }
            }
            else if (IsRift)
            {
                State.RiftCharge++;
                if (pierced && State.PhaseLevel >= 5) State.RiftCharge++;
                ResolveRiftCharge();
            }
            else if (IsPulse && State.PhaseLevel >= 5 && State.Tempo > 4)
            {
                State.PulseHitCounter++;
                int threshold = Mathf.RoundToInt(Tune("P5HitThreshold", 3f));
                if (State.PulseHitCounter >= threshold)
                {
                    State.PulseHitCounter = 0;
                    GrantHeroPierce(1, Mathf.RoundToInt(Tune("P5PierceCap", 1f)));
                }
            }

            if (IsRefract && State.RefractMarkFrames > 0)
            {
                State.RefractMarkFrames = 0;
                State.RefractCycles++;
                State.RefractPendingSpeedStacks = State.PhaseLevel >= 4
                    ? Math.Min(
                        Mathf.RoundToInt(Tune("P4MaxStacks", 3f)),
                        Math.Max(State.RefractSpeedStacks, State.RefractPendingSpeedStacks) + 1)
                    : 1;
                ReduceRefractCooldownIfReady();
            }

            return requestedSplitBalls;
        }

        public PhaseAbilityActivation TryActivateAbility()
        {
            if (!State.AbilityAvailable) return PhaseAbilityActivation.None;
            PhaseHeroActiveAbilityDefinition ability = _hero.PhaseLevels?
                .FirstOrDefault(level => level.PhaseLevel == "P3")?.ActiveAbility;
            if (ability == null) return PhaseAbilityActivation.None;
            State.AbilityCooldownFrames = SecondsToFrames(ability.CooldownSeconds);
            State.AbilityActiveFrames = SecondsToFrames(ability.DurationSeconds);

            if (IsMirage) return PhaseAbilityActivation.MirageTide;
            if (IsPulse)
            {
                State.Tempo = GetTempoCap();
                return PhaseAbilityActivation.PulseBurst;
            }
            if (IsRift)
            {
                SetMinimumTotalPierce(Mathf.RoundToInt(Tune("P3MinimumPierce", 4f)));
                State.RiftEmpowered = true;
                return PhaseAbilityActivation.RiftPierce;
            }
            if (IsRefract)
            {
                State.MirrorFrames = SecondsToFrames(ability.DurationSeconds);
                return PhaseAbilityActivation.RefractMirror;
            }
            return PhaseAbilityActivation.None;
        }

        public void AddItemPierce(int grant)
        {
            int targetTotal = Math.Max(State.TotalPierceCharges, Math.Max(0, grant));
            State.ItemPierceCharges = Math.Max(0, targetTotal - State.HeroPierceCharges);
        }

        public void ConsumePierce(int consumed)
        {
            int remaining = Math.Max(0, consumed);
            int fromItem = Math.Min(State.ItemPierceCharges, remaining);
            State.ItemPierceCharges -= fromItem;
            remaining -= fromItem;
            State.HeroPierceCharges = Math.Max(0, State.HeroPierceCharges - remaining);
            if (State.TotalPierceCharges <= 0) State.RiftEmpowered = false;
        }

        public float GetHeroBallSpeedMultiplier(bool forPaddleBounce = false)
        {
            if (IsPulse)
            {
                float perTempo = Tune(State.PhaseLevel >= 4 ? "P4SpeedPercentPerTempo" : "P1SpeedPercentPerTempo", State.PhaseLevel >= 4 ? 5f : 3f);
                float cap = Tune(State.PhaseLevel >= 4 ? "P4SpeedPercentCap" : "P1SpeedPercentCap", State.PhaseLevel >= 4 ? 20f : 12f);
                return 1f + Math.Min(cap, State.Tempo * perTempo) / 100f;
            }
            if (IsRift && State.TotalPierceCharges > 0 && State.RiftEmpowered)
                return 1f + Tune("P3SpeedPercent", 12f) / 100f;
            int refractStacks = forPaddleBounce && State.RefractPendingSpeedStacks > 0
                ? State.RefractPendingSpeedStacks
                : State.RefractSpeedStacks;
            bool hasRefractBoost = forPaddleBounce
                ? State.RefractPendingSpeedStacks > 0 || State.RefractBoostFrames > 0
                : State.RefractBoostFrames > 0;
            if (IsRefract && hasRefractBoost)
            {
                float percent = State.PhaseLevel >= 4
                    ? refractStacks * Tune("P4StackSpeedPercent", 6f)
                    : Tune(State.PhaseLevel >= 2 ? "P2BounceSpeedPercent" : "P1BounceSpeedPercent", State.PhaseLevel >= 2 ? 12f : 8f);
                return 1f + percent / 100f;
            }
            return 1f;
        }

        public float GetAbsoluteBallSpeedCap()
        {
            return IsPulse && State.PhaseLevel >= 3 && State.AbilityActiveFrames > 0
                ? Tune("P3AbsoluteSpeedCap", 6f)
                : float.PositiveInfinity;
        }

        public float GetMirrorHalfFieldRatio() =>
            Mathf.Clamp01(Tune("MirrorHalfFieldRatio", 0.55f));

        public float GetMirrorWidthRatio() =>
            Mathf.Clamp01(Tune("MirrorWidthRatio", 0.6f));

        public float GetHeroPaddleMultiplier(int activeSplitBallCount)
        {
            if (!IsMirage || State.PhaseLevel < 5) return 1f;
            float percent = Math.Min(
                Tune("P5PaddlePercentCap", 12f),
                Math.Max(0, activeSplitBallCount) * Tune("P5PaddlePercentPerExtraBall", 4f));
            return 1f + percent / 100f;
        }

        public float GetSplitLifetimeSeconds(PhaseItemDefinition splitItem)
        {
            float seconds = Tune("SplitLifetimeSeconds", 6f);
            return Mathf.Clamp(
                Modifiers.ApplyPercentValue(splitItem?.ItemId, seconds),
                0.1f,
                60f);
        }

        private void AddPhi(string source)
        {
            PhaseHeroPhiSourceDefinition row = _hero.PhiSources?.FirstOrDefault(item => item.Source == source);
            float requested = Math.Max(0f, row?.Phi ?? 0f);
            float room = Math.Max(0f, _meta.PhiPerSecondCap - State.PhiGainedThisSecond);
            float accepted = Math.Min(requested, room);
            if (accepted <= 0f || State.PhaseLevel >= 5) return;
            State.Phi += accepted;
            State.PhiGainedThisSecond += accepted;
            int nextCost = GetNextPhaseCost();
            if (nextCost > 0 && State.Phi + 0.0001f >= nextCost)
            {
                State.PhaseLevel++;
                State.Phi = 0f;
            }
        }

        private int GetNextPhaseCost()
        {
            string next = "P" + (State.PhaseLevel + 1).ToString(CultureInfo.InvariantCulture);
            return _hero.PhaseLevels?.FirstOrDefault(level => level.PhaseLevel == next)?.PhiToReach ?? 0;
        }

        private void ResolveRiftCharge()
        {
            int threshold = Mathf.RoundToInt(Tune(State.PhaseLevel >= 5 ? "P5ChargeThreshold" : "P1ChargeThreshold", State.PhaseLevel >= 5 ? 4f : 5f));
            if (State.RiftCharge < threshold) return;
            State.RiftCharge -= threshold;
            int grant = Mathf.RoundToInt(Tune(State.PhaseLevel >= 4 ? "P4PierceGrant" : "P1PierceGrant", State.PhaseLevel >= 4 ? 2f : 1f));
            int cap = Mathf.RoundToInt(Tune(State.PhaseLevel >= 2 ? "P2PierceCap" : "P1PierceCap", State.PhaseLevel >= 2 ? 3f : 2f));
            GrantHeroPierce(grant, cap);
        }

        private void SetMinimumTotalPierce(int minimum)
        {
            if (State.TotalPierceCharges >= minimum) return;
            SetHeroPierceCharges(Math.Max(State.HeroPierceCharges, minimum));
        }

        private void GrantHeroPierce(int grant, int cap)
        {
            int target = Math.Min(Math.Max(0, cap), State.HeroPierceCharges + Math.Max(0, grant));
            SetHeroPierceCharges(target);
        }

        private void SetHeroPierceCharges(int targetHeroCharges)
        {
            int targetTotal = Math.Max(State.TotalPierceCharges, Math.Max(0, targetHeroCharges));
            State.HeroPierceCharges = Math.Max(0, targetHeroCharges);
            State.ItemPierceCharges = Math.Max(0, targetTotal - State.HeroPierceCharges);
        }

        private void ReduceRefractCooldownIfReady()
        {
            if (State.PhaseLevel < 5) return;
            int cycles = Mathf.RoundToInt(Tune("P5CyclesPerCooldownReduction", 2f));
            if (cycles <= 0 || State.RefractCycles % cycles != 0) return;
            int reduction = SecondsToFrames(Tune("P5CooldownReductionSeconds", 3f));
            int minimum = SecondsToFrames(Tune("P5MinimumCooldownSeconds", 4f));
            if (State.AbilityCooldownFrames > minimum)
            {
                State.AbilityCooldownFrames = Math.Max(
                    minimum,
                    State.AbilityCooldownFrames - reduction);
            }
        }

        private int GetTempoCap() => Mathf.RoundToInt(Tune(State.PhaseLevel >= 2 ? "P2TempoCap" : "P1TempoCap", State.PhaseLevel >= 2 ? 5f : 4f));
        private float GetRefractMarkSeconds() => Tune(State.PhaseLevel >= 4 ? "P4MarkSeconds" : State.PhaseLevel >= 2 ? "P2MarkSeconds" : "P1MarkSeconds", State.PhaseLevel >= 4 ? 4f : State.PhaseLevel >= 2 ? 3f : 2f);
        private int SecondsToFrames(float seconds) => Math.Max(0, Mathf.RoundToInt(Math.Max(0f, seconds) * _fps));

        private float Tune(string key, float fallback)
        {
            if (_hero.RuntimeTuning == null || !_hero.RuntimeTuning.TryGetValue(key, out object raw) || raw == null)
                return fallback;
            try { return Convert.ToSingle(raw, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static int Decrement(int value) => value > 0 ? value - 1 : 0;
    }
}
