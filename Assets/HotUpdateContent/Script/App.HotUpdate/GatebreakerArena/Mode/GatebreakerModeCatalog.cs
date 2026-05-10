using System;
using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Core;

namespace App.HotUpdate.GatebreakerArena.Mode
{
    public sealed class GatebreakerModeCatalog
    {
        private readonly Dictionary<string, ModeRuleDefinition> _modes;
        private readonly Dictionary<string, BallRuleDefinition> _balls;
        private readonly Dictionary<string, AiRuleDefinition> _aiRules;
        private readonly Dictionary<string, MapRuleDefinition> _maps;

        public GatebreakerModeCatalog(
            IEnumerable<ModeRuleDefinition> modes,
            IEnumerable<BallRuleDefinition> balls,
            IEnumerable<AiRuleDefinition> aiRules,
            IEnumerable<MapRuleDefinition> maps)
        {
            _modes = IndexBy(modes, item => item.ModeId);
            _balls = IndexBy(balls, item => item.BallTypeId);
            _aiRules = IndexBy(aiRules, item => item.AILevelId);
            _maps = IndexBy(maps, item => item.MapId);
        }

        public static GatebreakerModeCatalog CreateDefault()
        {
            return new GatebreakerModeCatalog(
                new[]
                {
                    CreateMode("PVE_STANDARD", "PVE标准", 150, 1, 4, 6.0f, 1, 2, 1, ScoreRuleType.AddScore, 1.05f, 0.95f),
                    CreateMode("PVP_FFA", "PVP乱斗", 150, 1, 4, 6.0f, 1, 2, 1, ScoreRuleType.AddScore, 1.10f, 0.90f),
                    CreateMode("PVP_TEAM", "PVP组队乱斗", 180, 1, 4, 6.5f, 1, 2, 1, ScoreRuleType.TeamScore, 1.10f, 0.92f),
                },
                new[]
                {
                    new BallRuleDefinition
                    {
                        BallTypeId = "BALL_NORMAL",
                        BallTypeName = "普通球",
                        InitialSpeed = 7.5f,
                        MaxSpeed = 14.0f,
                        PaddleBounceFactor = 1.0f,
                        WallBounceFactor = 1.0f,
                        GoalReboundFactor = 1.0f,
                        SpeedGainOnPaddleHit = 0.15f,
                        MinVerticalVelocity = 2.0f,
                        DangerPromptThreshold = 1.2f,
                        TrailStyle = "Default",
                        ColorTag = "Neutral",
                    },
                },
                new[]
                {
                    new AiRuleDefinition
                    {
                        AILevelId = "AI_NORMAL",
                        AILevelName = "普通",
                        ReactionDelay = 0.18f,
                        PredictError = 0.25f,
                        ServeDecisionInterval = 0.6f,
                        AggressionWeight = 0.55f,
                        DefenseWeight = 0.70f,
                        MultiBallPriority = 0.65f,
                        AimAccuracy = 0.60f,
                        TargetSwitchFrequency = 0.50f,
                    },
                },
                new[]
                {
                    new MapRuleDefinition
                    {
                        MapId = "MAP_ARENA_01",
                        MapName = "标准四边场",
                        SupportedPlayerCount = new[] { 2, 3, 4 },
                        SpawnLayoutType = SpawnLayoutType.FourSide,
                        HasObstacle = false,
                        InitialBallsModifier = 0,
                        MaxBallsModifier = 0,
                        ServeCooldownModifier = 0f,
                        BallSpeedModifier = 0f,
                        GoalSizeModifier = 0f,
                    },
                });
        }

        public ModeRuleDefinition GetMode(string modeId)
        {
            return _modes.TryGetValue(modeId, out ModeRuleDefinition rule)
                ? rule
                : throw new KeyNotFoundException($"Unknown mode rule: {modeId}");
        }

        public BallRuleDefinition GetBall(string ballTypeId)
        {
            return _balls.TryGetValue(ballTypeId, out BallRuleDefinition rule)
                ? rule
                : throw new KeyNotFoundException($"Unknown ball rule: {ballTypeId}");
        }

        public AiRuleDefinition GetAi(string aiLevelId)
        {
            return _aiRules.TryGetValue(aiLevelId, out AiRuleDefinition rule)
                ? rule
                : throw new KeyNotFoundException($"Unknown AI rule: {aiLevelId}");
        }

        public MapRuleDefinition GetMap(string mapId)
        {
            return _maps.TryGetValue(mapId, out MapRuleDefinition rule)
                ? rule
                : throw new KeyNotFoundException($"Unknown map rule: {mapId}");
        }

        public EffectiveMatchRule BuildEffectiveRule(string modeId, string mapId)
        {
            ModeRuleDefinition mode = GetMode(modeId);
            MapRuleDefinition map = GetMap(mapId);
            return new EffectiveMatchRule(
                mode,
                map,
                mode.InitialBallsInMatch + map.InitialBallsModifier,
                mode.MaxBallsInMatch + map.MaxBallsModifier,
                mode.BaseServeCooldown + map.ServeCooldownModifier);
        }

        private static ModeRuleDefinition CreateMode(
            string id,
            string name,
            int duration,
            int initialBalls,
            int maxBalls,
            float cooldown,
            int initialAmmo,
            int maxAmmo,
            int maxOwnedBalls,
            ScoreRuleType scoreRule,
            float finalSpeedScale,
            float finalCooldownScale)
        {
            return new ModeRuleDefinition
            {
                ModeId = id,
                ModeName = name,
                MatchDuration = duration,
                InitialBallsInMatch = initialBalls,
                MaxBallsInMatch = maxBalls,
                BaseServeCooldown = cooldown,
                InitialServeAmmo = initialAmmo,
                MaxServeAmmo = maxAmmo,
                MaxOwnedBallsInField = maxOwnedBalls,
                GoalPauseTime = 0.4f,
                ScoreRuleType = scoreRule,
                EnableOvertime = true,
                OvertimeRuleType = OvertimeRuleType.SuddenDeath,
                OvertimeDuration = 60,
                OvertimeEligibleOnly = true,
                OvertimeWinScore = 1,
                AllowAimServe = false,
                FinalPhaseStartTime = 30,
                FinalPhaseBallSpeedScale = finalSpeedScale,
                FinalPhaseCooldownScale = finalCooldownScale,
            };
        }

        private static Dictionary<string, T> IndexBy<T>(IEnumerable<T> items, Func<T, string> keySelector)
        {
            var result = new Dictionary<string, T>();
            foreach (T item in items)
            {
                string key = keySelector(item);
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException($"{typeof(T).Name} contains empty id.");
                }

                result[key] = item;
            }

            return result;
        }
    }

    public sealed class EffectiveMatchRule
    {
        public EffectiveMatchRule(
            ModeRuleDefinition mode,
            MapRuleDefinition map,
            int initialBallsInMatch,
            int maxBallsInMatch,
            float baseServeCooldown)
        {
            Mode = mode;
            Map = map;
            InitialBallsInMatch = initialBallsInMatch;
            MaxBallsInMatch = maxBallsInMatch;
            BaseServeCooldown = baseServeCooldown;
        }

        public ModeRuleDefinition Mode { get; }
        public MapRuleDefinition Map { get; }
        public int InitialBallsInMatch { get; }
        public int MaxBallsInMatch { get; }
        public float BaseServeCooldown { get; }
    }
}
