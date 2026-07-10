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
        private readonly Dictionary<string, UniversalChipDefinition> _universalChips;
        private readonly Dictionary<string, SignatureChipDefinition> _signatureChips;
        private readonly Dictionary<string, HeroDefinition> _heroes;
        private readonly Dictionary<string, HeroPathDefinition> _heroPaths;

        public GatebreakerModeCatalog(
            IEnumerable<ModeRuleDefinition> modes,
            IEnumerable<BallRuleDefinition> balls,
            IEnumerable<AiRuleDefinition> aiRules,
            IEnumerable<MapRuleDefinition> maps,
            IEnumerable<PlayerColorRuleDefinition> playerColors,
            IEnumerable<UniversalChipDefinition> universalChips,
            IEnumerable<SignatureChipDefinition> signatureChips)
            : this(modes, balls, aiRules, maps, playerColors, universalChips, signatureChips,
                Array.Empty<HeroDefinition>(), Array.Empty<HeroPathDefinition>())
        {
        }

        public GatebreakerModeCatalog(
            IEnumerable<ModeRuleDefinition> modes,
            IEnumerable<BallRuleDefinition> balls,
            IEnumerable<AiRuleDefinition> aiRules,
            IEnumerable<MapRuleDefinition> maps,
            IEnumerable<PlayerColorRuleDefinition> playerColors,
            IEnumerable<UniversalChipDefinition> universalChips,
            IEnumerable<SignatureChipDefinition> signatureChips,
            IEnumerable<HeroDefinition> heroes,
            IEnumerable<HeroPathDefinition> heroPaths)
        {
            _modes = IndexBy(modes, item => item.ModeId);
            _balls = IndexBy(balls, item => item.BallTypeId);
            _aiRules = IndexBy(aiRules, item => item.AILevelId);
            _maps = IndexBy(maps, item => item.MapId);
            _playerColors = IndexBy(playerColors, item => item.PlayerId);
            _universalChips = IndexBy(universalChips, item => item.ChipId);
            _signatureChips = IndexBy(signatureChips, item => item.ChipId);
            _heroes = IndexBy(heroes ?? Array.Empty<HeroDefinition>(), item => item.HeroId);
            _heroPaths = IndexBy(heroPaths ?? Array.Empty<HeroPathDefinition>(), item => item.PathId);
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
                        PaddleMoveSpeed = 8f,
                        BallSpeedModifier = 0f,
                        GoalSizeModifier = 0f,
                        ScenePrefabLocation = "Assets/HotUpdateContent/Res/prefabs/Scene3P.prefab",
                        PaddlePrefabLocation = "Assets/HotUpdateContent/Res/prefabs/Baffle.prefab",
                        DefaultPlayerCount = 3,
                        ArenaHalfWidth = 2.81f,
                        ArenaHalfHeight = 2.456f,
                        PaddleInset = 0.18f,
                        PaddleLength = 0.78f,
                        PaddleThickness = 0.05f,
                        GoalHalfLength = 1.037f,
                        GoalTriggerInset = 0.069f,
                        GoalContactLineInset = 0.04f,
                        BoundaryPoints = CreateScene3v3BoundaryPoints(),
                        GoalCenters = CreateScene3v3GoalCenters(),
                        PlayerSideBindings = new[]
                        {
                            CreatePlayerSideBinding(1, "Position01", 5),
                            CreatePlayerSideBinding(2, "Position03", 1),
                            CreatePlayerSideBinding(3, "Position05", 3),
                        },
                        CollisionLayouts = CreateDefaultCollisionLayouts(),
                    },
                },
                new[]
                {
                    CreatePlayerColor(1, "Red", 1.0f, 0.18f, 0.16f),
                    CreatePlayerColor(2, "Blue", 0.20f, 0.48f, 1.0f),
                    CreatePlayerColor(3, "Green", 0.24f, 0.86f, 0.34f),
                    CreatePlayerColor(4, "Yellow", 1.0f, 0.86f, 0.18f),
                },
                CreateDefaultUniversalChips(),
                Array.Empty<SignatureChipDefinition>(),
                CreateDefaultHeroes(),
                CreateDefaultHeroPaths());
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

        public UniversalChipDefinition GetUniversalChip(string chipId)
        {
            return _universalChips.TryGetValue(chipId, out UniversalChipDefinition chip)
                ? chip
                : throw new KeyNotFoundException($"Unknown universal chip: {chipId}");
        }

        public SignatureChipDefinition GetSignatureChip(string chipId)
        {
            return _signatureChips.TryGetValue(chipId, out SignatureChipDefinition chip)
                ? chip
                : throw new KeyNotFoundException($"Unknown signature chip: {chipId}");
        }

        public HeroDefinition GetHero(string heroId)
        {
            return _heroes.TryGetValue(heroId, out HeroDefinition hero)
                ? hero
                : throw new KeyNotFoundException($"Unknown hero rule: {heroId}");
        }

        public HeroPathDefinition GetHeroPath(string pathId)
        {
            return _heroPaths.TryGetValue(pathId, out HeroPathDefinition path)
                ? path
                : throw new KeyNotFoundException($"Unknown hero path rule: {pathId}");
        }

        public IReadOnlyDictionary<string, UniversalChipDefinition> AllUniversalChips => _universalChips;
        public IReadOnlyDictionary<string, SignatureChipDefinition> AllSignatureChips => _signatureChips;
        public IReadOnlyDictionary<string, HeroDefinition> AllHeroes => _heroes;
        public IReadOnlyDictionary<string, HeroPathDefinition> AllHeroPaths => _heroPaths;

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

        private static IReadOnlyList<MapCollisionLayoutDefinition> CreateDefaultCollisionLayouts()
        {
            return new[]
            {
                new MapCollisionLayoutDefinition
                {
                    PlayerCount = 2,
                    BoundarySegments = new[]
                    {
                        CreateBoundarySegment("Position01", -1.3729f, -2.456f, 1.3787f, -2.456f, 0f, -2.456f, 1.0586f, 0.069f),
                        CreateBoundarySegment("Position02", 1.3787f, -2.456f, 2.809f, 0.0212f),
                        CreateBoundarySegment("Position03", 2.809f, 0.0212f, 1.4107f, 2.443f),
                        CreateBoundarySegment("Position04", 1.4107f, 2.443f, -1.416f, 2.443f, 0f, 2.443f, 1.0586f, 0.069f),
                        CreateBoundarySegment("Position05", -1.416f, 2.443f, -2.8087f, 0.0308f),
                        CreateBoundarySegment("Position06", -2.8087f, 0.0308f, -1.3729f, -2.456f),
                    },
                    PlayerSideBindings = new[]
                    {
                        CreatePlayerSideBinding(1, "Position01", 0),
                        CreatePlayerSideBinding(2, "Position04", 3),
                    },
                },
                new MapCollisionLayoutDefinition
                {
                    PlayerCount = 3,
                    BoundarySegments = new[]
                    {
                        CreateBoundarySegment("Position01", -1.3729f, -2.456f, 1.3787f, -2.456f, 0f, -2.456f, 1.0586f, 0.069f),
                        CreateBoundarySegment("Position02", 1.3787f, -2.456f, 2.809f, 0.0212f),
                        CreateBoundarySegment("Position03", 2.809f, 0.0212f, 1.4107f, 2.443f, 2.118f, 1.218f, 1.0586f, 0.069f),
                        CreateBoundarySegment("Position04", 1.4107f, 2.443f, -1.416f, 2.443f),
                        CreateBoundarySegment("Position05", -1.416f, 2.443f, -2.8087f, 0.0308f, -2.114f, 1.234f, 1.0586f, 0.069f),
                        CreateBoundarySegment("Position06", -2.8087f, 0.0308f, -1.3729f, -2.456f),
                    },
                    PlayerSideBindings = new[]
                    {
                        CreatePlayerSideBinding(1, "Position01", 0),
                        CreatePlayerSideBinding(2, "Position03", 2),
                        CreatePlayerSideBinding(3, "Position05", 4),
                    },
                },
                new MapCollisionLayoutDefinition
                {
                    PlayerCount = 4,
                    BoundarySegments = new[]
                    {
                        CreateBoundarySegment("Position01", -1.184f, -2.736f, 1.164f, -2.736f, 0f, -2.736f, 0.8998f, 0.069f),
                        CreateBoundarySegment("Position02", 1.164f, -2.736f, 2.763f, -1.137f),
                        CreateBoundarySegment("Position03", 2.763f, -1.137f, 2.763f, 1.157f, 2.763f, 0.023f, 0.8998f, 0.069f),
                        CreateBoundarySegment("Position04", 2.763f, 1.157f, 1.15f, 2.77f),
                        CreateBoundarySegment("Position05", 1.15f, 2.77f, -1.15f, 2.77f, 0.02f, 2.77f, 0.8998f, 0.069f),
                        CreateBoundarySegment("Position06", -1.15f, 2.77f, -2.75f, 1.17f),
                        CreateBoundarySegment("Position07", -2.75f, 1.17f, -2.75f, -1.17f, -2.75f, 0.02f, 0.8998f, 0.069f),
                        CreateBoundarySegment("Position08", -2.75f, -1.17f, -1.184f, -2.736f),
                    },
                    PlayerSideBindings = new[]
                    {
                        CreatePlayerSideBinding(1, "Position01", 0),
                        CreatePlayerSideBinding(2, "Position03", 2),
                        CreatePlayerSideBinding(3, "Position05", 4),
                        CreatePlayerSideBinding(4, "Position07", 6),
                    },
                },
            };
        }

        private static MapBoundarySegmentDefinition CreateBoundarySegment(
            string scenePosition,
            float startX,
            float startY,
            float endX,
            float endY)
        {
            return new MapBoundarySegmentDefinition
            {
                ScenePosition = scenePosition,
                Start = CreateMapPoint(startX, startY),
                End = CreateMapPoint(endX, endY),
            };
        }

        private static MapBoundarySegmentDefinition CreateBoundarySegment(
            string scenePosition,
            float startX,
            float startY,
            float endX,
            float endY,
            float goalX,
            float goalY,
            float goalHalfLength,
            float goalTriggerInset)
        {
            MapBoundarySegmentDefinition segment = CreateBoundarySegment(scenePosition, startX, startY, endX, endY);
            segment.GoalCenter = CreateMapPoint(goalX, goalY);
            segment.GoalHalfLength = goalHalfLength;
            segment.GoalTriggerInset = goalTriggerInset;
            return segment;
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

        private static UniversalChipDefinition CreateUniversalChip(
            string chipId,
            string displayName,
            ChipCategory category,
            ChipRarity rarity,
            string description)
        {
            return new UniversalChipDefinition
            {
                ChipId = chipId,
                DisplayName = displayName,
                Category = category,
                Rarity = rarity,
                Description = description,
                Modifiers = Array.Empty<UniversalChipModifierDefinition>(),
                LinkedQuantumEvent = string.Empty,
                IconPath = string.Empty,
            };
        }

        private static IReadOnlyList<UniversalChipDefinition> CreateDefaultUniversalChips()
        {
            return new[]
            {
                CreateUniversalChip("STRIKE_POWER", "蓄能击", ChipCategory.Strike, ChipRarity.Common, "挡板反弹球速提高。"),
                CreateUniversalChip("STRIKE_SERVE", "重发球", ChipCategory.Strike, ChipRarity.Common, "发球初速提高。"),
                CreateUniversalChip("STRIKE_OVERCHARGE", "过载", ChipCategory.Strike, ChipRarity.Common, "提高球速上限。"),
                CreateUniversalChip("GUARD_LENGTH", "长板", ChipCategory.Guard, ChipRarity.Common, "挡板长度提高。"),
                CreateUniversalChip("GUARD_GOAL", "收缩门", ChipCategory.Guard, ChipRarity.Common, "球门缩小。"),
                CreateUniversalChip("GUARD_BOUNCE", "弹性墙", ChipCategory.Guard, ChipRarity.Common, "敌球反弹减速。"),
                CreateUniversalChip("FLOW_SPEED", "疾驰", ChipCategory.Flow, ChipRarity.Common, "挡板移速提高。"),
                CreateUniversalChip("FLOW_AMMO", "快装填", ChipCategory.Flow, ChipRarity.Common, "发球冷却缩短。"),
                CreateUniversalChip("FLOW_CAPACITY", "弹药库", ChipCategory.Flow, ChipRarity.Common, "最大弹药提高。"),
                CreateUniversalChip("CHAOS_SPIN", "旋球", ChipCategory.Chaos, ChipRarity.Common, "墙弹确定性偏转。"),
                CreateUniversalChip("CHAOS_RICOCHET", "连锁弹射", ChipCategory.Chaos, ChipRarity.Common, "固定碰撞计数强化。"),
                CreateUniversalChip("CHAOS_DISRUPT", "扰乱", ChipCategory.Chaos, ChipRarity.Common, "命中敌方挡板后减速。"),
            };
        }

        private static IReadOnlyList<HeroDefinition> CreateDefaultHeroes()
        {
            return new[]
            {
                new HeroDefinition { HeroId = "HERO_FROST_QUEEN", DisplayName = "冰雪女王", ActiveAbilityId = "ABILITY_FROST_BLIZZARD", ActiveAbilityCooldownSeconds = 12f, PathIds = new[] { "PATH_FROST_EXTREME", "PATH_FROST_CRYSTAL" } },
                new HeroDefinition { HeroId = "HERO_THORN_GUARDIAN", DisplayName = "荆棘守护者", ActiveAbilityId = "ABILITY_THORN_ARMOR", ActiveAbilityCooldownSeconds = 12f, PathIds = new[] { "PATH_THORN_BRISTLE", "PATH_THORN_GROWTH" } },
                new HeroDefinition { HeroId = "HERO_RADIANT_PALADIN", DisplayName = "辉光圣骑", ActiveAbilityId = "ABILITY_RADIANT_SHIELD", ActiveAbilityCooldownSeconds = 12f, PathIds = new[] { "PATH_RADIANT_HOLY_LIGHT", "PATH_RADIANT_RAY" } },
            };
        }

        private static IReadOnlyList<HeroPathDefinition> CreateDefaultHeroPaths()
        {
            return new[]
            {
                CreateHeroPath("PATH_FROST_EXTREME", "HERO_FROST_QUEEN", "极寒", ChipCategory.Strike, ChipCategory.Guard),
                CreateHeroPath("PATH_FROST_CRYSTAL", "HERO_FROST_QUEEN", "冰晶", ChipCategory.Guard, ChipCategory.Flow),
                CreateHeroPath("PATH_THORN_BRISTLE", "HERO_THORN_GUARDIAN", "荆棘", ChipCategory.Strike, ChipCategory.Guard),
                CreateHeroPath("PATH_THORN_GROWTH", "HERO_THORN_GUARDIAN", "生长", ChipCategory.Guard, ChipCategory.Flow),
                CreateHeroPath("PATH_RADIANT_HOLY_LIGHT", "HERO_RADIANT_PALADIN", "圣光", ChipCategory.Strike, ChipCategory.Guard),
                CreateHeroPath("PATH_RADIANT_RAY", "HERO_RADIANT_PALADIN", "光芒", ChipCategory.Strike, ChipCategory.Flow),
            };
        }

        private static HeroPathDefinition CreateHeroPath(string pathId, string heroId, string displayName, ChipCategory first, ChipCategory second)
        {
            return new HeroPathDefinition
            {
                PathId = pathId,
                HeroId = heroId,
                DisplayName = displayName,
                ResonanceCategories = new[] { first, second },
                MilestoneEffects = Array.Empty<HeroPathEffectDefinition>(),
            };
        }

        private static SignatureChipDefinition CreateSignatureChip(
            string chipId,
            string displayName,
            string heroId,
            string pathId,
            SignatureGrade grade,
            int resonanceValue,
            string description)
        {
            return new SignatureChipDefinition
            {
                ChipId = chipId,
                DisplayName = displayName,
                HeroId = heroId,
                PathId = pathId,
                Grade = grade,
                ResonanceValue = resonanceValue,
                Description = description,
                EffectDesc = string.Empty,
                GradeModifiers = Array.Empty<SignatureChipModifierDefinition>(),
                QualitativeEffectId = string.Empty,
                UpgradesTo = string.Empty,
                UpgradeCost = 0,
                IconPath = string.Empty,
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
