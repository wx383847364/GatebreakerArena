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
        private readonly Dictionary<int, PlayerColorRuleDefinition> _playerColors;

        public GatebreakerModeCatalog(
            IEnumerable<ModeRuleDefinition> modes,
            IEnumerable<BallRuleDefinition> balls,
            IEnumerable<AiRuleDefinition> aiRules,
            IEnumerable<MapRuleDefinition> maps,
            IEnumerable<PlayerColorRuleDefinition> playerColors)
        {
            _modes = IndexBy(modes, item => item.ModeId);
            _balls = IndexBy(balls, item => item.BallTypeId);
            _aiRules = IndexBy(aiRules, item => item.AILevelId);
            _maps = IndexBy(maps, item => item.MapId);
            _playerColors = IndexBy(playerColors, item => item.PlayerId);
        }

        public static GatebreakerModeCatalog CreateDefault()
        {
            return new GatebreakerModeCatalog(
                new[]
                {
                    CreateMode("PVE_STANDARD", "PVE标准", 60, 0, 4, 6.0f, 2, 2, 1, ScoreRuleType.AddScore, 1.05f, 0.95f),
                    CreateMode("PVP_FFA", "PVP乱斗", 60, 0, 4, 6.0f, 2, 2, 1, ScoreRuleType.AddScore, 1.10f, 0.90f),
                    CreateMode("PVP_TEAM", "PVP组队乱斗", 60, 0, 4, 6.5f, 2, 2, 1, ScoreRuleType.TeamScore, 1.10f, 0.92f),
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
                        BallContactRadius = 0.08f,
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
                        PaddleMoveSpeed = 3.2f,
                        BallSpeedModifier = 0f,
                        GoalSizeModifier = 0f,
                        ScenePrefabLocation = "Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab",
                        PaddlePrefabLocation = "Assets/HotUpdateContent/Res/prefabs/Baffle.prefab",
                        DefaultPlayerCount = 3,
                        ArenaHalfWidth = 2.81f,
                        ArenaHalfHeight = 2.456f,
                        PaddleInset = 0.18f,
                        PaddleLength = 0.78f,
                        PaddleThickness = 0.05f,
                        GoalHalfLength = 1.06f,
                        GoalTriggerInset = 0.14f,
                        GoalContactLineInset = 0.04f,
                        BoundaryPoints = CreateScene3v3BoundaryPoints(),
                        GoalCenters = CreateScene3v3GoalCenters(),
                        PlayerSideBindings = new[]
                        {
                            CreatePlayerSideBinding(1, "Position01", 5),
                            CreatePlayerSideBinding(2, "Position03", 1),
                            CreatePlayerSideBinding(3, "Position05", 3),
                        },
                    },
                },
                new[]
                {
                    CreatePlayerColor(1, "Red", 1.0f, 0.18f, 0.16f),
                    CreatePlayerColor(2, "Blue", 0.20f, 0.48f, 1.0f),
                    CreatePlayerColor(3, "Green", 0.24f, 0.86f, 0.34f),
                    CreatePlayerColor(4, "Yellow", 1.0f, 0.86f, 0.18f),
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

        public PlayerColorRuleDefinition GetPlayerColor(int playerId)
        {
            return _playerColors.TryGetValue(playerId, out PlayerColorRuleDefinition rule)
                ? rule
                : throw new KeyNotFoundException($"Unknown player color rule: {playerId}");
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
                BallSpeedByTime = CreateDefaultBallSpeedByTime(),
                TuningValues = CreateDefaultTuningValues(),
            };
        }

        private static IReadOnlyDictionary<string, int> CreateDefaultTuningValues()
        {
            return new Dictionary<string, int>
            {
                ["HitOffsetInfluenceValue"] = 90,
                ["PaddleVelocityInfluenceValue"] = 55,
                ["MinimumOutwardShareValue"] = 25,
            };
        }

        private static IReadOnlyList<MapVector2Definition> CreateScene3v3BoundaryPoints()
        {
            return new[]
            {
                CreateMapPoint(1.379f, -2.456f),
                CreateMapPoint(2.809f, 0.021f),
                CreateMapPoint(1.411f, 2.443f),
                CreateMapPoint(-1.416f, 2.443f),
                CreateMapPoint(-2.809f, 0.031f),
                CreateMapPoint(-1.373f, -2.456f),
            };
        }

        private static IReadOnlyList<MapVector2Definition> CreateScene3v3GoalCenters()
        {
            return new[]
            {
                CreateMapPoint(2.086f, -1.231f),
                CreateMapPoint(2.118f, 1.218f),
                CreateMapPoint(0f, 2.443f),
                CreateMapPoint(-2.114f, 1.234f),
                CreateMapPoint(-2.094f, -1.207f),
                CreateMapPoint(0f, -2.456f),
            };
        }

        private static MapVector2Definition CreateMapPoint(float x, float y)
        {
            return new MapVector2Definition
            {
                X = x,
                Y = y,
            };
        }

        private static IReadOnlyList<BallSpeedTimePointDefinition> CreateDefaultBallSpeedByTime()
        {
            return new[]
            {
                CreateBallSpeedTimePoint(15f, 10f),
                CreateBallSpeedTimePoint(30f, 15f),
                CreateBallSpeedTimePoint(45f, 20f),
            };
        }

        private static BallSpeedTimePointDefinition CreateBallSpeedTimePoint(float timeSeconds, float speed)
        {
            return new BallSpeedTimePointDefinition
            {
                TimeSeconds = timeSeconds,
                Speed = speed,
            };
        }

        private static PlayerColorRuleDefinition CreatePlayerColor(
            int playerId,
            string colorName,
            float red,
            float green,
            float blue)
        {
            return new PlayerColorRuleDefinition
            {
                PlayerId = playerId,
                ColorName = colorName,
                Red = red,
                Green = green,
                Blue = blue,
                Alpha = 1f,
            };
        }

        private static MapPlayerSideBindingDefinition CreatePlayerSideBinding(
            int playerId,
            string scenePosition,
            int boundarySegmentIndex)
        {
            return new MapPlayerSideBindingDefinition
            {
                PlayerId = playerId,
                ScenePosition = scenePosition,
                BoundarySegmentIndex = boundarySegmentIndex,
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

        private static Dictionary<int, T> IndexBy<T>(IEnumerable<T> items, Func<T, int> keySelector)
        {
            var result = new Dictionary<int, T>();
            foreach (T item in items)
            {
                int key = keySelector(item);
                if (key <= 0)
                {
                    throw new ArgumentException($"{typeof(T).Name} contains invalid id.");
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
