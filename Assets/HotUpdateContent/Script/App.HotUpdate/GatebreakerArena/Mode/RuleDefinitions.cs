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
        public string LinkedQuantumEvent { get; set; }
        public string IconPath { get; set; }
    }

    public sealed class SignatureChipDefinition
    {
        public string ChipId { get; set; }
        public string DisplayName { get; set; }
        public string HeroId { get; set; }
        public string PathId { get; set; }
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
}
