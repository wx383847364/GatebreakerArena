using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Mode;

namespace App.HotUpdate.GatebreakerArena.Hero
{
    /// <summary>
    /// Per-player, deterministic hero state. The match owner persists this alongside
    /// <see cref="HeroRuntimeState"/> and includes it in its checksum.
    /// </summary>
    [Serializable]
    public sealed class HeroCombatState
    {
        public string HeroId { get; set; } = string.Empty;
        public int RadianceStacks { get; set; }
        public int ThornGrowthStacks { get; set; }
        public int ThornArmorRemainingFrames { get; set; }
        public int ThornArmorGrowthFrameProgress { get; set; }
        public int DivineShieldRemainingFrames { get; set; }
        public int BlizzardRemainingFrames { get; set; }
        public int TeamBallSpeedBoostRemainingFrames { get; set; }
        public int FrostDecayFrameProgress { get; set; }
        public List<HeroFrostStackState> FrostByOpponent { get; set; } = new List<HeroFrostStackState>();
        public List<HeroBallSpeedStackState> IceCrystalBallSpeedStacks { get; set; } = new List<HeroBallSpeedStackState>();
    }

    [Serializable]
    public sealed class HeroFrostStackState
    {
        public int OpponentPlayerId { get; set; }
        public int Amount { get; set; }
    }

    [Serializable]
    public sealed class HeroBallSpeedStackState
    {
        public int BallId { get; set; }
        public int Stacks { get; set; }
    }

    public enum HeroRuntimeEventType
    {
        OpponentPaddleHit,
        OwnPaddleHit,
        ConcededGoal,
        AbilityPressed,
    }

    /// <summary>
    /// A match event already resolved by the simulation. BallId is required only for
    /// effects that are intentionally tracked per ball (Ice Crystal).
    /// </summary>
    public readonly struct HeroRuntimeEvent
    {
        public HeroRuntimeEvent(HeroRuntimeEventType eventType, int otherPlayerId = 0, int ballId = 0)
        {
            EventType = eventType;
            OtherPlayerId = otherPlayerId;
            BallId = ballId;
        }

        public HeroRuntimeEventType EventType { get; }
        public int OtherPlayerId { get; }
        public int BallId { get; }
    }

    /// <summary>
    /// Event-local and persistent modifiers. Consumers compose multipliers and apply
    /// durations to their corresponding ball/paddle/serve/goal states.
    /// </summary>
    public sealed class HeroEffectBundle
    {
        public float OwnBallSpeedMultiplier { get; set; } = 1f;
        public float OwnPaddleLengthMultiplier { get; set; } = 1f;
        public float OwnPaddleMoveSpeedMultiplier { get; set; } = 1f;
        public float OwnServeInitialSpeedMultiplier { get; set; } = 1f;
        public float OwnPaddleBounceSpeedMultiplier { get; set; } = 1f;
        public bool RedirectBounceTowardsNearestEnemyGoal { get; set; }
        public float BounceRedirectMaxDegrees { get; set; }
        public int OwnGoalImmuneFrames { get; set; }
        public int TargetPaddleFreezeFrames { get; set; }
        public int TargetPaddleSlowFrames { get; set; }
        public float TargetPaddleMoveSpeedMultiplier { get; set; } = 1f;
        public int TargetAllBallsFreezeFrames { get; set; }
        public int TargetServeAmmoDelta { get; set; }
        public int OwnTeamBallSpeedBoostFrames { get; set; }
        public int OwnTeamBallSpeedBoostMultiplierPercent { get; set; }

        public static HeroEffectBundle None => new HeroEffectBundle();
    }

    public sealed class HeroRuntimeEventResult
    {
        public HeroEffectBundle Effects { get; set; } = HeroEffectBundle.None;
        public bool AbilityActivated { get; set; }
    }

    public readonly struct HeroAiAbilityDecisionInput
    {
        public HeroAiAbilityDecisionInput(int highestOpponentFrost, bool hasEnemyBallInOwnDangerZone)
        {
            HighestOpponentFrost = highestOpponentFrost;
            HasEnemyBallInOwnDangerZone = hasEnemyBallInOwnDangerZone;
        }

        public int HighestOpponentFrost { get; }
        public bool HasEnemyBallInOwnDangerZone { get; }
    }
}
