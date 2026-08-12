using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Core;

namespace App.HotUpdate.GatebreakerArena.Mode
{
    // --- Modifier structs shared by chip types ---
    public sealed class ModeRuleDefinition
    {
        public string ModeId { get; set; }
        public string ModeName { get; set; }
        public int MatchDuration { get; set; }
        public int InitialBallsInMatch { get; set; }
        public int MaxBallsInMatch { get; set; }
        public float BaseServeCooldown { get; set; }
        public int InitialServeAmmo { get; set; }
        public int MaxServeAmmo { get; set; }
        public int MaxOwnedBallsInField { get; set; }
        public float GoalPauseTime { get; set; }
        public ScoreRuleType ScoreRuleType { get; set; }
        public bool EnableOvertime { get; set; }
        public OvertimeRuleType OvertimeRuleType { get; set; }
        public int OvertimeDuration { get; set; }
        public bool OvertimeEligibleOnly { get; set; }
        public int OvertimeWinScore { get; set; }
        public bool AllowAimServe { get; set; }
        public int FinalPhaseStartTime { get; set; }
        public float FinalPhaseBallSpeedScale { get; set; }
        public float FinalPhaseCooldownScale { get; set; }
        public IReadOnlyList<BallSpeedTimePointDefinition> BallSpeedByTime { get; set; }
        public IReadOnlyDictionary<string, int> TuningValues { get; set; }
        public int CountdownSeconds => MatchDuration;
    }

    public sealed class BallSpeedTimePointDefinition
    {
        public float TimeSeconds { get; set; }
        public float Speed { get; set; }
    }

    public sealed class BallRuleDefinition
    {
        public string BallTypeId { get; set; }
        public string BallTypeName { get; set; }
        public float InitialSpeed { get; set; }
        public float MaxSpeed { get; set; }
        public float PaddleBounceFactor { get; set; }
        public float WallBounceFactor { get; set; }
        public float GoalReboundFactor { get; set; }
        public float SpeedGainOnPaddleHit { get; set; }
        public float MinVerticalVelocity { get; set; }
        public float DangerPromptThreshold { get; set; }
        public float BallContactRadius { get; set; }
        public string TrailStyle { get; set; }
        public string ColorTag { get; set; }
        public string PrefabLocation { get; set; }
    }

    public sealed class AiRuleDefinition
    {
        public string AILevelId { get; set; }
        public string AILevelName { get; set; }
        public float ReactionDelay { get; set; }
        public float PredictError { get; set; }
        public float ServeDecisionInterval { get; set; }
        public float AggressionWeight { get; set; }
        public float DefenseWeight { get; set; }
        public float MultiBallPriority { get; set; }
        public float AimAccuracy { get; set; }
        public float TargetSwitchFrequency { get; set; }
    }

    public sealed class MapRuleDefinition
    {
        public string MapId { get; set; }
        public string MapName { get; set; }
        public IReadOnlyList<int> SupportedPlayerCount { get; set; }
        public SpawnLayoutType SpawnLayoutType { get; set; }
        public bool HasObstacle { get; set; }
        public int InitialBallsModifier { get; set; }
        public int MaxBallsModifier { get; set; }
        public float ServeCooldownModifier { get; set; }
        public int? MaxServeAmmo { get; set; }
        public int? MaxOwnedBallsInField { get; set; }
        public float? ServeRechargeSeconds { get; set; }
        public float PaddleMoveSpeed { get; set; }
        public float BallSpeedModifier { get; set; }
        public float GoalSizeModifier { get; set; }
        public string ScenePrefabLocation { get; set; }
        public string PaddlePrefabLocation { get; set; }
        public int DefaultPlayerCount { get; set; }
        public float ArenaHalfWidth { get; set; }
        public float ArenaHalfHeight { get; set; }
        public float PaddleInset { get; set; }
        public float PaddleLength { get; set; }
        public float PaddleThickness { get; set; }
        public float GoalHalfLength { get; set; }
        public float GoalTriggerInset { get; set; }
        public float GoalContactLineInset { get; set; }
        public IReadOnlyList<MapVector2Definition> BoundaryPoints { get; set; }
        public IReadOnlyList<MapVector2Definition> GoalCenters { get; set; }
        public IReadOnlyList<MapPlayerSideBindingDefinition> PlayerSideBindings { get; set; }
        public IReadOnlyList<MapCollisionLayoutDefinition> CollisionLayouts { get; set; }
    }

    public sealed class MapVector2Definition
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public sealed class MapPlayerSideBindingDefinition
    {
        public int PlayerId { get; set; }
        public string ScenePosition { get; set; }
        public int BoundarySegmentIndex { get; set; }
    }

    public sealed class MapCollisionLayoutDefinition
    {
        public int PlayerCount { get; set; }
        public IReadOnlyList<MapBoundarySegmentDefinition> BoundarySegments { get; set; }
        public IReadOnlyList<MapPlayerSideBindingDefinition> PlayerSideBindings { get; set; }
    }

    public sealed class MapBoundarySegmentDefinition
    {
        public string ScenePosition { get; set; }
        public MapVector2Definition Start { get; set; }
        public MapVector2Definition End { get; set; }
        public MapVector2Definition GoalCenter { get; set; }
        public float GoalHalfLength { get; set; }
        public float GoalTriggerInset { get; set; }
    }

    public sealed class PlayerColorRuleDefinition
    {
        public int PlayerId { get; set; }
        public string ColorName { get; set; }
        public float Red { get; set; }
        public float Green { get; set; }
        public float Blue { get; set; }
        public float Alpha { get; set; }
    }

    /// <summary>
    /// Local endless 1v1 brick-tide rules. This definition stays in HotUpdate because
    /// it is gameplay data rather than a stable host/network contract.
    /// </summary>
    public sealed class BrickDuelRuleDefinition
    {
        public string RuleId { get; set; }
        public int SimulationFps { get; set; }
        public int CountdownSeconds { get; set; }
        public int InitialCoreHealth { get; set; }
        public int InitialRows { get; set; }
        public int Columns { get; set; }
        public float ArenaHalfWidth { get; set; }
        public float CoreLineY { get; set; }
        public float PaddleSpawnY { get; set; }
        public float PaddleHalfWidth { get; set; }
        public float PaddleHalfHeight { get; set; }
        public float PaddleMoveSpeed { get; set; }
        public float BrickWidth { get; set; }
        public float BrickHeight { get; set; }
        public float BallRadius { get; set; }
        public float BallSpeed { get; set; }
        public float BaseTideSpeed { get; set; }
        public float BallResetSeconds { get; set; }
        public float StuckTimeoutSeconds { get; set; }
        public float StuckMovementEpsilon { get; set; }
        public float PressureIntervalSeconds { get; set; }
        public float PressureIncrement { get; set; }
        public float DangerDistance { get; set; }
        public int GreenHealth { get; set; }
        public int RedHealth { get; set; }
        public int YellowHealth { get; set; }
        public int MysteryHealth { get; set; }
        public int BrickCoreDamage { get; set; }
        public float GreenWeight { get; set; }
        public float RedWeight { get; set; }
        public float YellowWeight { get; set; }
        public float MysteryWeight { get; set; }
        public float BrickCompositionIntervalSeconds { get; set; }
        public IReadOnlyList<BrickDuelCompositionStageDefinition> BrickCompositionStages { get; set; }
        public int RandomSeed { get; set; }
        public string BrickDuelAiRuleId { get; set; }
        public IReadOnlyList<string> InitialRowPatterns { get; set; }
        public string ScenePrefabLocation { get; set; }
        public string PaddlePrefabLocation { get; set; }
        public string PlayerBallPrefabLocation { get; set; }
        public string AiBallPrefabLocation { get; set; }
        public string GreenBrickPrefabLocation { get; set; }
        public string RedBrickPrefabLocation { get; set; }
        public string YellowBrickPrefabLocation { get; set; }
        public string MysteryBrickPrefabLocation { get; set; }
        public IReadOnlyList<BrickDuelItemDropDefinition> ItemDrops { get; set; }

        public int ResolveBrickCompositionStageIndex(float elapsedSeconds)
        {
            float interval = BrickCompositionIntervalSeconds > 0.0001f
                ? BrickCompositionIntervalSeconds
                : 30f;
            int maxIndex = BrickCompositionStages != null && BrickCompositionStages.Count > 0
                ? BrickCompositionStages.Count - 1
                : 0;
            if (elapsedSeconds < 0f)
            {
                elapsedSeconds = 0f;
            }

            int stage = (int)(elapsedSeconds / interval);
            return stage > maxIndex ? maxIndex : stage;
        }

        public BrickDuelCompositionStageDefinition ResolveBrickCompositionWeights(float elapsedSeconds)
        {
            if (BrickCompositionStages == null || BrickCompositionStages.Count == 0)
            {
                return new BrickDuelCompositionStageDefinition
                {
                    GreenWeight = GreenWeight,
                    RedWeight = RedWeight,
                    YellowWeight = YellowWeight,
                    MysteryWeight = MysteryWeight,
                };
            }

            return BrickCompositionStages[ResolveBrickCompositionStageIndex(elapsedSeconds)];
        }
    }

    public sealed class BrickDuelAiRuleDefinition
    {
        public string RuleId { get; set; }
        public int DecisionIntervalFrames { get; set; }
        public float EmergencyDistance { get; set; }
        public float MoveDeadZone { get; set; }
    }

    public sealed class BrickDuelCompositionStageDefinition
    {
        public float GreenWeight { get; set; }
        public float RedWeight { get; set; }
        public float YellowWeight { get; set; }
        public float MysteryWeight { get; set; }
    }

    public sealed class BrickDuelItemDropDefinition
    {
        public string DropTableId { get; set; }
        public int SortOrder { get; set; }
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public float DropWeight { get; set; }
        public int BagCopies { get; set; }
        public bool Enabled { get; set; }
        public string IconLocation { get; set; }
        public string PrefabLocation { get; set; }
        public float EffectDurationSeconds { get; set; }
        public float EffectMagnitude { get; set; }
        public string DurationModifierKey { get; set; }
    }

    // --- Chip modifier structs ---

    public sealed class UniversalChipModifierDefinition
    {
        public string ModifierType { get; set; }
        public ModifierOp Op { get; set; }
        public float ValueLv1 { get; set; }
        public float ValueLv2 { get; set; }
        public float ValueLv3 { get; set; }
    }

    public sealed class SignatureChipModifierDefinition
    {
        public string ModifierType { get; set; }
        public ModifierOp Op { get; set; }
        public float Value { get; set; }
    }

    // --- Chip Definition classes ---

    public sealed class UniversalChipDefinition
    {
        public string ChipId { get; set; }
        public string DisplayName { get; set; }
        public ChipCategory Category { get; set; }
        public ChipRarity Rarity { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<UniversalChipModifierDefinition> Modifiers { get; set; }
        public IReadOnlyList<UniversalChipConditionalModifierDefinition> ConditionalModifiers { get; set; }
        public string LinkedQuantumEvent { get; set; }
        public string IconPath { get; set; }
    }

    public sealed class SignatureChipDefinition
    {
        public string ChipId { get; set; }
        public string DisplayName { get; set; }
        public string HeroId { get; set; }
        public string PathId { get; set; }
        // V1 uses equal-strength side variants rather than the legacy grade/upgrade tree.
        public string VariantKind { get; set; }
        public IReadOnlyDictionary<string, float> Parameters { get; set; }
        public SignatureGrade Grade { get; set; }
        public int ResonanceValue { get; set; }
        public string Description { get; set; }
        public string EffectDesc { get; set; }
        public IReadOnlyList<SignatureChipModifierDefinition> GradeModifiers { get; set; }
        public string QualitativeEffectId { get; set; }
        public string UpgradesTo { get; set; }
        public int UpgradeCost { get; set; }
        public string IconPath { get; set; }
    }

    // V1 hero contracts. These are data-only definitions; gameplay application belongs
    // to the Hero/Chip systems and must not be added to the catalog or UI layer.
    public sealed class HeroDefinition
    {
        public string HeroId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string ActiveAbilityId { get; set; }
        public float ActiveAbilityCooldownSeconds { get; set; }
        public IReadOnlyList<string> PathIds { get; set; }
    }

    public sealed class HeroPathDefinition
    {
        public string PathId { get; set; }
        public string HeroId { get; set; }
        public string DisplayName { get; set; }
        public IReadOnlyList<ChipCategory> ResonanceCategories { get; set; }
        public IReadOnlyList<HeroPathEffectDefinition> MilestoneEffects { get; set; }
    }

    public sealed class HeroPathEffectDefinition
    {
        public int PathLevel { get; set; }
        public string EffectId { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<UniversalChipModifierDefinition> Modifiers { get; set; }
    }

    public sealed class UniversalChipConditionalModifierDefinition
    {
        public string HeroId { get; set; }
        public string PathId { get; set; }
        public int MinimumPathLevel { get; set; }
        public string ModifierType { get; set; }
        public ModifierOp Op { get; set; }
        public float Value { get; set; }
    }

    public enum HeroTemporaryStatusType
    {
        None = 0,
        Frozen = 1,
        Slowed = 2,
        Shielded = 3,
        Armored = 4,
        SpeedBoosted = 5,
    }

    public sealed class HeroPathRuntimeState
    {
        public string PathId { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    public sealed class HeroTemporaryStatusState
    {
        public HeroTemporaryStatusType StatusType { get; set; }
        public int RemainingFrames { get; set; }
        public float Magnitude { get; set; }
    }

    public sealed class HeroRuntimeState
    {
        public string HeroId { get; set; } = string.Empty;
        public string PathId { get; set; } = string.Empty;
        public string SignatureChipId { get; set; } = string.Empty;
        public IReadOnlyList<string> OpeningUniversalChipIds { get; set; } = new string[0];
        public IReadOnlyList<string> ScheduledUniversalChipIds { get; set; } = new string[0];
        public IReadOnlyList<string> DeckChipIds { get; set; } = new string[0];
        public IReadOnlyList<string> ActiveChipIds { get; set; } = new string[0];
        public int PlayingFrame { get; set; }
        public IReadOnlyList<HeroPathRuntimeState> PathStates { get; set; } = new HeroPathRuntimeState[0];
        public int AbilityCooldownRemainingFrames { get; set; }
        public IReadOnlyList<HeroTemporaryStatusState> TemporaryStatuses { get; set; } = new HeroTemporaryStatusState[0];
    }
}
