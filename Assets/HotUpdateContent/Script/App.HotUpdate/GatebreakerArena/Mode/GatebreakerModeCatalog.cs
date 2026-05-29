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
                    CreateMode("PVE_STANDARD", "PVE标准", 60, 1, 4, 6.0f, 1, 2, 1, ScoreRuleType.AddScore, 1.05f, 0.95f),
                    CreateMode("PVP_FFA", "PVP乱斗", 60, 1, 4, 6.0f, 1, 2, 1, ScoreRuleType.AddScore, 1.10f, 0.90f),
                    CreateMode("PVP_TEAM", "PVP组队乱斗", 60, 1, 4, 6.5f, 1, 2, 1, ScoreRuleType.TeamScore, 1.10f, 0.92f),
                },
                new[]
                {
                    new BallRuleDefinition
                    {
                        BallTypeId = "BALL_NORMAL",
                        BallTypeName = "普通球",
                        InitialSpeed = 5.25f,
                        MaxSpeed = 9.8f,
                        PaddleBounceFactor = 1.0f,
                        WallBounceFactor = 1.0f,
                        GoalReboundFactor = 1.0f,
                        SpeedGainOnPaddleHit = 0.15f,
                        MinVerticalVelocity = 2.0f,
                        DangerPromptThreshold = 1.2f,
                        TrailStyle = "Default",
                        ColorTag = "Neutral",
                        PrefabLocation = "Assets/HotUpdateContent/Res/prefabs/Ball01.prefab",
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
                        MaxBallsModifier = 16,
                        ServeCooldownModifier = 0f,
                        MaxServeAmmo = 5,
                        MaxOwnedBallsInField = 5,
                        ServeRechargeSeconds = 5f,
                        BallSpeedModifier = 0f,
                        GoalSizeModifier = 0f,
                        ScenePrefabLocation = "Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab",
                        PaddlePrefabLocation = "Assets/HotUpdateContent/Res/prefabs/Baffle.prefab",
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
            float serveRechargeSeconds = map.ServeRechargeSeconds
                ?? mode.BaseServeCooldown + map.ServeCooldownModifier;
            return new EffectiveMatchRule(
                mode,
                map,
                mode.InitialBallsInMatch + map.InitialBallsModifier,
                mode.MaxBallsInMatch + map.MaxBallsModifier,
                mode.InitialServeAmmo,
                map.MaxServeAmmo ?? mode.MaxServeAmmo,
                map.MaxOwnedBallsInField ?? mode.MaxOwnedBallsInField,
                Math.Max(0f, serveRechargeSeconds));
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
                Time = duration,
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
                AllowAimServe = true,
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
            int initialServeAmmo,
            int maxServeAmmo,
            int maxOwnedBallsInField,
            float serveRechargeSeconds)
        {
            Mode = mode;
            Map = map;
            InitialBallsInMatch = initialBallsInMatch;
            MaxBallsInMatch = maxBallsInMatch;
            InitialServeAmmo = initialServeAmmo;
            MaxServeAmmo = maxServeAmmo;
            MaxOwnedBallsInField = maxOwnedBallsInField;
            ServeRechargeSeconds = serveRechargeSeconds;
        }

        public ModeRuleDefinition Mode { get; }
        public MapRuleDefinition Map { get; }
        public int InitialBallsInMatch { get; }
        public int MaxBallsInMatch { get; }
        public int InitialServeAmmo { get; }
        public int MaxServeAmmo { get; }
        public int MaxOwnedBallsInField { get; }
        public float ServeRechargeSeconds { get; }
        public float BaseServeCooldown => ServeRechargeSeconds;
    }
}
