using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Core;

namespace App.HotUpdate.GatebreakerArena.Mode
{
    public sealed class ModeRuleDefinition
    {
        public string ModeId { get; set; }
        public string ModeName { get; set; }
        public int Time { get; set; }
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
        public int CountdownSeconds => Time > 0 ? Time : MatchDuration;
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
        public float BallSpeedModifier { get; set; }
        public float GoalSizeModifier { get; set; }
        public string ScenePrefabLocation { get; set; }
        public string PaddlePrefabLocation { get; set; }
    }
}
