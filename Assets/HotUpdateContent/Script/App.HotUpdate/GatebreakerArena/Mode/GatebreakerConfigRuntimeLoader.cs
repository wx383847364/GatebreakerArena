using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Core;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Mode
{
    public sealed class GatebreakerConfigRuntimeLoader
    {
        public const string RulesAssetLocation = "Assets/HotUpdateContent/Config/gatebreaker_rules.bytes";

        public async Task<GatebreakerConfigLoadResult> LoadAsync(IAssetsRuntime assetsRuntime)
        {
            if (assetsRuntime == null)
            {
                return GatebreakerConfigLoadResult.Fail(
                    GatebreakerConfigLoadFailureReason.AssetsRuntimeMissing,
                    RulesAssetLocation,
                    "IAssetsRuntime is not available.");
            }

            IAssetHandle handle = null;
            try
            {
                handle = await assetsRuntime.LoadAssetAsync(RulesAssetLocation);
                if (handle?.AssetObject == null)
                {
                    return GatebreakerConfigLoadResult.Fail(
                        GatebreakerConfigLoadFailureReason.AssetLoadFailed,
                        RulesAssetLocation,
                        $"Failed to load Gatebreaker rules asset at {RulesAssetLocation}.");
                }

                if (!(handle.AssetObject is TextAsset textAsset))
                {
                    return GatebreakerConfigLoadResult.Fail(
                        GatebreakerConfigLoadFailureReason.UnsupportedAssetType,
                        RulesAssetLocation,
                        $"Gatebreaker rules asset must be a TextAsset, but was {handle.AssetObject.GetType().Name}.");
                }

                string json = !string.IsNullOrEmpty(textAsset.text)
                    ? textAsset.text
                    : Encoding.UTF8.GetString(textAsset.bytes ?? Array.Empty<byte>());

                return ParseJson(json, RulesAssetLocation);
            }
            catch (Exception ex)
            {
                return GatebreakerConfigLoadResult.Fail(
                    GatebreakerConfigLoadFailureReason.AssetLoadFailed,
                    RulesAssetLocation,
                    $"Exception while loading Gatebreaker rules: {ex.Message}");
            }
            finally
            {
                handle?.Release();
            }
        }

        public static GatebreakerConfigLoadResult ParseJson(string json, string source = RulesAssetLocation)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return GatebreakerConfigLoadResult.Fail(
                    GatebreakerConfigLoadFailureReason.EmptyContent,
                    source,
                    "Gatebreaker rules JSON is empty.");
            }

            try
            {
                var root = JsonValueParser.ParseObject(json);
                int version = ReadOptionalInt(root, "Version") ?? 0;
                if (version < 2)
                {
                    throw new FormatException("Gatebreaker V1 rules require schema Version >= 2.");
                }
                IEnumerable<BrickDuelRuleDefinition> brickDuelRules = version >= 3
                    ? ReadArray(root, "DT_BrickDuelRule", ReadBrickDuelRule)
                    : Array.Empty<BrickDuelRuleDefinition>();
                BrickDuelItemDropDefinition[] itemDrops = version >= 3
                    ? (ReadOptionalArray(root, "DT_BrickDuelItemDrop", ReadBrickDuelItemDrop)
                        ?? Array.Empty<BrickDuelItemDropDefinition>()).ToArray()
                    : Array.Empty<BrickDuelItemDropDefinition>();
                if (itemDrops.Length > 0)
                {
                    AttachItemDrops(brickDuelRules, itemDrops);
                }

                var catalog = new GatebreakerModeCatalog(
                    ReadArray(root, "DT_ModeRule", ReadMode),
                    ReadArray(root, "DT_BallRule", ReadBall),
                    ReadArray(root, "DT_AIRule", ReadAi),
                    ReadArray(root, "DT_MapRule", ReadMap),
                    ReadArray(root, "DT_PlayerColorRule", ReadPlayerColor),
                    ReadArray(root, "DT_UniversalChip", ReadUniversalChip),
                    ReadArray(root, "DT_SignatureChip", ReadSignatureChip),
                    ReadArray(root, "DT_Hero", ReadHero),
                    ReadArray(root, "DT_HeroPath", ReadHeroPath),
                    brickDuelRules);

                ValidateV1Catalog(catalog);
                if (version >= 3)
                {
                    ValidateBrickDuelCatalog(catalog);
                }

                return GatebreakerConfigLoadResult.Success(catalog, source, version);
            }
            catch (Exception ex)
            {
                return GatebreakerConfigLoadResult.Fail(
                    GatebreakerConfigLoadFailureReason.ParseFailed,
                    source,
                    $"Failed to parse Gatebreaker rules JSON: {ex.Message}");
            }
        }

        private static void ValidateV1Catalog(GatebreakerModeCatalog catalog)
        {
            string[] heroIds = { "HERO_FROST_QUEEN", "HERO_MECH_ENGINEER", "HERO_RADIANT_PALADIN" };
            if (catalog.AllHeroes.Count != 3 || heroIds.Any(id => !catalog.AllHeroes.ContainsKey(id)))
            {
                throw new FormatException("DT_Hero must contain exactly the three V1 heroes.");
            }
            if (catalog.AllHeroPaths.Count != 6 || catalog.AllUniversalChips.Count != 12 || catalog.AllSignatureChips.Count != 12)
            {
                throw new FormatException("V1 requires exactly 6 paths, 12 universal chips and 12 signature variants.");
            }

            foreach (HeroDefinition hero in catalog.AllHeroes.Values)
            {
                string[] pathIds = (hero.PathIds ?? Array.Empty<string>()).ToArray();
                if (pathIds.Length != 2 || pathIds.Distinct(StringComparer.Ordinal).Count() != 2)
                {
                    throw new FormatException($"Hero '{hero.HeroId}' must declare exactly two unique paths.");
                }
                foreach (string pathId in pathIds)
                {
                    if (!catalog.AllHeroPaths.TryGetValue(pathId, out HeroPathDefinition path) || path.HeroId != hero.HeroId)
                    {
                        throw new FormatException($"Hero path '{pathId}' does not belong to '{hero.HeroId}'.");
                    }
                    SignatureChipDefinition[] variants = catalog.AllSignatureChips.Values
                        .Where(chip => chip.HeroId == hero.HeroId && chip.PathId == pathId).ToArray();
                    if (variants.Length != 2 || variants.Any(chip => chip.ResonanceValue != 3) ||
                        !variants.Any(chip => chip.VariantKind == "Stable") || !variants.Any(chip => chip.VariantKind == "Style"))
                    {
                        throw new FormatException($"Path '{pathId}' must have Stable/Style signature variants with +3 resonance.");
                    }
                }
            }
        }

        private static void ValidateBrickDuelCatalog(GatebreakerModeCatalog catalog)
        {
            if (!catalog.HasBrickDuelRule ||
                !catalog.TryGetBrickDuelRule("BRICK_DUEL_V0", out BrickDuelRuleDefinition rule))
            {
                throw new FormatException("DT_BrickDuelRule must contain BRICK_DUEL_V0.");
            }

            if (rule.SimulationFps != 30 ||
                rule.CountdownSeconds != 5 ||
                rule.InitialCoreHealth != 5 ||
                rule.InitialRows != 3 ||
                rule.Columns != 9)
            {
                throw new FormatException("BRICK_DUEL_V0 must use 30 FPS, 5 second countdown, 5 health and a 3x9 opening grid.");
            }

            if (rule.GreenHealth != 1 || rule.RedHealth != 2 ||
                rule.YellowHealth != 3 || rule.MysteryHealth != 1 ||
                rule.BrickCoreDamage != 1)
            {
                throw new FormatException("BRICK_DUEL_V0 brick health/damage values do not match the endless rules.");
            }

            if (rule.ArenaHalfWidth <= 0f || rule.CoreLineY <= 0f ||
                rule.PaddleSpawnY <= 0f || rule.PaddleSpawnY >= rule.CoreLineY ||
                rule.PaddleHalfWidth <= 0f || rule.PaddleHalfHeight <= 0f ||
                rule.PaddleMoveSpeed <= 0f || rule.BrickWidth <= 0f ||
                rule.BrickHeight <= 0f || rule.BallRadius <= 0f ||
                rule.BallSpeed <= 0f || rule.BaseTideSpeed <= 0f ||
                rule.StuckTimeoutSeconds <= 0f || rule.StuckMovementEpsilon <= 0f ||
                rule.DangerDistance <= 0f)
            {
                throw new FormatException("BRICK_DUEL_V0 geometry, movement and timeout values must be positive and inside the field.");
            }

            if (Math.Abs(rule.BallResetSeconds - 0.5f) > 0.0001f ||
                Math.Abs(rule.PressureIntervalSeconds - 30f) > 0.0001f ||
                Math.Abs(rule.PressureIncrement - 0.25f) > 0.0001f)
            {
                throw new FormatException("BRICK_DUEL_V0 must use the documented reset and pressure timing.");
            }

            if (Math.Abs(rule.BrickCompositionIntervalSeconds - 30f) > 0.0001f)
            {
                throw new FormatException("BRICK_DUEL_V0 BrickCompositionIntervalSeconds must be 30.");
            }

            ValidateBrickCompositionWeights(
                rule.GreenWeight,
                rule.RedWeight,
                rule.YellowWeight,
                rule.MysteryWeight,
                "BRICK_DUEL_V0 top-level");

            IReadOnlyList<BrickDuelCompositionStageDefinition> stages =
                rule.BrickCompositionStages ?? Array.Empty<BrickDuelCompositionStageDefinition>();
            if (stages.Count != 6)
            {
                throw new FormatException("BRICK_DUEL_V0 BrickCompositionStages must contain exactly 6 stages.");
            }

            for (int stageIndex = 0; stageIndex < stages.Count; stageIndex++)
            {
                BrickDuelCompositionStageDefinition stage = stages[stageIndex];
                if (stage == null)
                {
                    throw new FormatException($"BRICK_DUEL_V0 BrickCompositionStages[{stageIndex}] is required.");
                }

                ValidateBrickCompositionWeights(
                    stage.GreenWeight,
                    stage.RedWeight,
                    stage.YellowWeight,
                    stage.MysteryWeight,
                    $"BRICK_DUEL_V0 BrickCompositionStages[{stageIndex}]");
            }

            string[] patterns = (rule.InitialRowPatterns ?? Array.Empty<string>()).ToArray();
            if (patterns.Length != rule.InitialRows)
            {
                throw new FormatException("BRICK_DUEL_V0 InitialRowPatterns must match InitialRows.");
            }

            var allowedTypes = new HashSet<string>(
                new[] { "Green", "Red", "Yellow", "Mystery" },
                StringComparer.Ordinal);
            for (int rowIndex = 0; rowIndex < patterns.Length; rowIndex++)
            {
                string[] cells = (patterns[rowIndex] ?? string.Empty)
                    .Split(',')
                    .Select(cell => cell.Trim())
                    .ToArray();
                if (cells.Length != rule.Columns || cells.Any(cell => !allowedTypes.Contains(cell)))
                {
                    throw new FormatException(
                        $"BRICK_DUEL_V0 InitialRowPatterns[{rowIndex}] must contain {rule.Columns} valid brick names.");
                }
            }

            if (rule.Columns * rule.BrickWidth > rule.ArenaHalfWidth * 2f + 0.0001f ||
                rule.InitialRows * rule.BrickHeight >= rule.PaddleSpawnY ||
                rule.BallRadius >= rule.ArenaHalfWidth ||
                rule.DangerDistance > rule.CoreLineY)
            {
                throw new FormatException(
                    "BRICK_DUEL_V0 grid, ball and danger geometry must fit inside the configured half-field.");
            }

            if (string.IsNullOrWhiteSpace(rule.AiLevelId))
            {
                throw new FormatException("BRICK_DUEL_V0 AiLevelId is required.");
            }
            catalog.GetAi(rule.AiLevelId);

            string[] assetLocations =
            {
                rule.ScenePrefabLocation,
                rule.PaddlePrefabLocation,
                rule.PlayerBallPrefabLocation,
                rule.AiBallPrefabLocation,
                rule.GreenBrickPrefabLocation,
                rule.RedBrickPrefabLocation,
                rule.YellowBrickPrefabLocation,
                rule.MysteryBrickPrefabLocation,
            };
            if (assetLocations.Any(string.IsNullOrWhiteSpace))
            {
                throw new FormatException("BRICK_DUEL_V0 requires all scene, paddle, ball and brick prefab locations.");
            }
        }

        private static ModeRuleDefinition ReadMode(Dictionary<string, object> item)
        {
            int matchDuration = ReadOptionalInt(item, "MatchDuration") ?? ReadInt(item, "Time");
            return new ModeRuleDefinition
            {
                ModeId = ReadString(item, "ModeId"),
                ModeName = ReadString(item, "ModeName"),
                MatchDuration = matchDuration,
                InitialBallsInMatch = ReadInt(item, "InitialBallsInMatch"),
                MaxBallsInMatch = ReadInt(item, "MaxBallsInMatch"),
                BaseServeCooldown = ReadFloat(item, "BaseServeCooldown"),
                InitialServeAmmo = ReadInt(item, "InitialServeAmmo"),
                MaxServeAmmo = ReadInt(item, "MaxServeAmmo"),
                MaxOwnedBallsInField = ReadInt(item, "MaxOwnedBallsInField"),
                GoalPauseTime = ReadFloat(item, "GoalPauseTime"),
                ScoreRuleType = ReadEnum<ScoreRuleType>(item, "ScoreRuleType"),
                EnableOvertime = ReadBool(item, "EnableOvertime"),
                OvertimeRuleType = ReadEnum<OvertimeRuleType>(item, "OvertimeRuleType"),
                OvertimeDuration = ReadInt(item, "OvertimeDuration"),
                OvertimeEligibleOnly = ReadBool(item, "OvertimeEligibleOnly"),
                OvertimeWinScore = ReadInt(item, "OvertimeWinScore"),
                AllowAimServe = ReadBool(item, "AllowAimServe"),
                FinalPhaseStartTime = ReadInt(item, "FinalPhaseStartTime"),
                FinalPhaseBallSpeedScale = ReadFloat(item, "FinalPhaseBallSpeedScale"),
                FinalPhaseCooldownScale = ReadFloat(item, "FinalPhaseCooldownScale"),
                BallSpeedByTime = ReadOptionalFloatPairList(item, "BallSpeedByTime"),
                TuningValues = ReadTuningValues(item),
            };
        }

        private static BallRuleDefinition ReadBall(Dictionary<string, object> item)
        {
            return new BallRuleDefinition
            {
                BallTypeId = ReadString(item, "BallTypeId"),
                BallTypeName = ReadString(item, "BallTypeName"),
                InitialSpeed = ReadFloat(item, "InitialSpeed"),
                MaxSpeed = ReadFloat(item, "MaxSpeed"),
                PaddleBounceFactor = ReadFloat(item, "PaddleBounceFactor"),
                WallBounceFactor = ReadFloat(item, "WallBounceFactor"),
                GoalReboundFactor = ReadFloat(item, "GoalReboundFactor"),
                SpeedGainOnPaddleHit = ReadFloat(item, "SpeedGainOnPaddleHit"),
                MinVerticalVelocity = ReadFloat(item, "MinVerticalVelocity"),
                DangerPromptThreshold = ReadFloat(item, "DangerPromptThreshold"),
                BallContactRadius = ReadPositiveFloat(item, "BallContactRadius"),
                TrailStyle = ReadString(item, "TrailStyle"),
                ColorTag = ReadString(item, "ColorTag"),
                PrefabLocation = ReadOptionalString(item, "PrefabLocation"),
            };
        }

        private static AiRuleDefinition ReadAi(Dictionary<string, object> item)
        {
            return new AiRuleDefinition
            {
                AILevelId = ReadString(item, "AILevelId"),
                AILevelName = ReadString(item, "AILevelName"),
                ReactionDelay = ReadFloat(item, "ReactionDelay"),
                PredictError = ReadFloat(item, "PredictError"),
                ServeDecisionInterval = ReadFloat(item, "ServeDecisionInterval"),
                AggressionWeight = ReadFloat(item, "AggressionWeight"),
                DefenseWeight = ReadFloat(item, "DefenseWeight"),
                MultiBallPriority = ReadFloat(item, "MultiBallPriority"),
                AimAccuracy = ReadFloat(item, "AimAccuracy"),
                TargetSwitchFrequency = ReadFloat(item, "TargetSwitchFrequency"),
            };
        }

        private static MapRuleDefinition ReadMap(Dictionary<string, object> item)
        {
            return new MapRuleDefinition
            {
                MapId = ReadString(item, "MapId"),
                MapName = ReadString(item, "MapName"),
                SupportedPlayerCount = ReadIntList(item, "SupportedPlayerCount"),
                SpawnLayoutType = ReadEnum<SpawnLayoutType>(item, "SpawnLayoutType"),
                HasObstacle = ReadBool(item, "HasObstacle"),
                InitialBallsModifier = ReadInt(item, "InitialBallsModifier"),
                MaxBallsModifier = ReadInt(item, "MaxBallsModifier"),
                ServeCooldownModifier = ReadFloat(item, "ServeCooldownModifier"),
                MaxServeAmmo = ReadOptionalInt(item, "MaxServeAmmo"),
                MaxOwnedBallsInField = ReadOptionalInt(item, "MaxOwnedBallsInField"),
                ServeRechargeSeconds = ReadOptionalFloat(item, "ServeRechargeSeconds"),
                PaddleMoveSpeed = ReadPositiveFloat(item, "PaddleMoveSpeed"),
                BallSpeedModifier = ReadFloat(item, "BallSpeedModifier"),
                GoalSizeModifier = ReadFloat(item, "GoalSizeModifier"),
                ScenePrefabLocation = ReadOptionalString(item, "ScenePrefabLocation"),
                PaddlePrefabLocation = ReadOptionalString(item, "PaddlePrefabLocation"),
                DefaultPlayerCount = ReadOptionalInt(item, "DefaultPlayerCount") ?? 0,
                ArenaHalfWidth = ReadPositiveFloat(item, "ArenaHalfWidth"),
                ArenaHalfHeight = ReadPositiveFloat(item, "ArenaHalfHeight"),
                PaddleInset = ReadPositiveFloat(item, "PaddleInset"),
                PaddleLength = ReadPositiveFloat(item, "PaddleLength"),
                PaddleThickness = ReadPositiveFloat(item, "PaddleThickness"),
                GoalHalfLength = ReadPositiveFloat(item, "GoalHalfLength"),
                GoalTriggerInset = ReadNonNegativeFloat(item, "GoalTriggerInset"),
                GoalContactLineInset = ReadNonNegativeFloat(item, "GoalContactLineInset"),
                BoundaryPoints = ReadVector2Array(item, "BoundaryPoints", 3),
                GoalCenters = ReadVector2Array(item, "GoalCenters", 1),
                PlayerSideBindings = ReadOptionalArray(item, "PlayerSideBindings", ReadPlayerSideBinding),
                CollisionLayouts = ReadOptionalArray(item, "CollisionLayouts", ReadCollisionLayout),
            };
        }

        private static MapCollisionLayoutDefinition ReadCollisionLayout(Dictionary<string, object> item)
        {
            return new MapCollisionLayoutDefinition
            {
                PlayerCount = ReadInt(item, "PlayerCount"),
                BoundarySegments = ReadOptionalArray(item, "BoundarySegments", ReadBoundarySegment),
                PlayerSideBindings = ReadOptionalArray(item, "PlayerSideBindings", ReadPlayerSideBinding),
            };
        }

        private static MapBoundarySegmentDefinition ReadBoundarySegment(Dictionary<string, object> item)
        {
            return new MapBoundarySegmentDefinition
            {
                ScenePosition = ReadOptionalString(item, "ScenePosition"),
                Start = ReadVector2(item, "Start"),
                End = ReadVector2(item, "End"),
                GoalCenter = ReadOptionalVector2(item, "GoalCenter"),
                GoalHalfLength = ReadOptionalFloat(item, "GoalHalfLength") ?? 0f,
                GoalTriggerInset = ReadOptionalFloat(item, "GoalTriggerInset") ?? 0f,
            };
        }

        private static MapPlayerSideBindingDefinition ReadPlayerSideBinding(Dictionary<string, object> item)
        {
            return new MapPlayerSideBindingDefinition
            {
                PlayerId = ReadInt(item, "PlayerId"),
                ScenePosition = ReadOptionalString(item, "ScenePosition"),
                BoundarySegmentIndex = ReadInt(item, "BoundarySegmentIndex"),
            };
        }

        private static PlayerColorRuleDefinition ReadPlayerColor(Dictionary<string, object> item)
        {
            return new PlayerColorRuleDefinition
            {
                PlayerId = ReadInt(item, "PlayerId"),
                ColorName = ReadOptionalString(item, "ColorName"),
                Red = ReadFloat(item, "Red"),
                Green = ReadFloat(item, "Green"),
                Blue = ReadFloat(item, "Blue"),
                Alpha = ReadFloat(item, "Alpha"),
            };
        }

        private static BrickDuelRuleDefinition ReadBrickDuelRule(Dictionary<string, object> item)
        {
            return new BrickDuelRuleDefinition
            {
                RuleId = ReadString(item, "RuleId"),
                SimulationFps = ReadInt(item, "SimulationFps"),
                CountdownSeconds = ReadInt(item, "CountdownSeconds"),
                InitialCoreHealth = ReadInt(item, "InitialCoreHealth"),
                InitialRows = ReadInt(item, "InitialRows"),
                Columns = ReadInt(item, "Columns"),
                ArenaHalfWidth = ReadFloat(item, "ArenaHalfWidth"),
                CoreLineY = ReadFloat(item, "CoreLineY"),
                PaddleSpawnY = ReadFloat(item, "PaddleSpawnY"),
                PaddleHalfWidth = ReadFloat(item, "PaddleHalfWidth"),
                PaddleHalfHeight = ReadFloat(item, "PaddleHalfHeight"),
                PaddleMoveSpeed = ReadFloat(item, "PaddleMoveSpeed"),
                BrickWidth = ReadFloat(item, "BrickWidth"),
                BrickHeight = ReadFloat(item, "BrickHeight"),
                BallRadius = ReadFloat(item, "BallRadius"),
                BallSpeed = ReadFloat(item, "BallSpeed"),
                BaseTideSpeed = ReadFloat(item, "BaseTideSpeed"),
                BallResetSeconds = ReadFloat(item, "BallResetSeconds"),
                StuckTimeoutSeconds = ReadFloat(item, "StuckTimeoutSeconds"),
                StuckMovementEpsilon = ReadFloat(item, "StuckMovementEpsilon"),
                PressureIntervalSeconds = ReadFloat(item, "PressureIntervalSeconds"),
                PressureIncrement = ReadFloat(item, "PressureIncrement"),
                DangerDistance = ReadFloat(item, "DangerDistance"),
                GreenHealth = ReadInt(item, "GreenHealth"),
                RedHealth = ReadInt(item, "RedHealth"),
                YellowHealth = ReadInt(item, "YellowHealth"),
                MysteryHealth = ReadInt(item, "MysteryHealth"),
                BrickCoreDamage = ReadInt(item, "BrickCoreDamage"),
                GreenWeight = ReadFloat(item, "GreenWeight"),
                RedWeight = ReadFloat(item, "RedWeight"),
                YellowWeight = ReadFloat(item, "YellowWeight"),
                MysteryWeight = ReadFloat(item, "MysteryWeight"),
                BrickCompositionIntervalSeconds = ReadFloat(item, "BrickCompositionIntervalSeconds"),
                BrickCompositionStages = ReadOptionalArray(item, "BrickCompositionStages", ReadBrickCompositionStage),
                RandomSeed = ReadInt(item, "RandomSeed"),
                AiLevelId = ReadString(item, "AiLevelId"),
                InitialRowPatterns = ReadOptionalStringList(item, "InitialRowPatterns"),
                ScenePrefabLocation = ReadString(item, "ScenePrefabLocation"),
                PaddlePrefabLocation = ReadString(item, "PaddlePrefabLocation"),
                PlayerBallPrefabLocation = ReadString(item, "PlayerBallPrefabLocation"),
                AiBallPrefabLocation = ReadString(item, "AiBallPrefabLocation"),
                GreenBrickPrefabLocation = ReadString(item, "GreenBrickPrefabLocation"),
                RedBrickPrefabLocation = ReadString(item, "RedBrickPrefabLocation"),
                YellowBrickPrefabLocation = ReadString(item, "YellowBrickPrefabLocation"),
                MysteryBrickPrefabLocation = ReadString(item, "MysteryBrickPrefabLocation"),
            };
        }

        private static BrickDuelItemDropDefinition ReadBrickDuelItemDrop(Dictionary<string, object> item)
        {
            return new BrickDuelItemDropDefinition
            {
                DropTableId = ReadString(item, "DropTableId"),
                SortOrder = ReadInt(item, "SortOrder"),
                ItemId = ReadString(item, "ItemId"),
                ItemName = ReadString(item, "ItemName"),
                DropWeight = ReadFloat(item, "DropWeight"),
                BagCopies = ReadInt(item, "BagCopies"),
                Enabled = item.ContainsKey("Enabled") ? ReadBool(item, "Enabled") : true,
                IconLocation = ReadOptionalString(item, "IconLocation"),
                PrefabLocation = ReadOptionalString(item, "PrefabLocation"),
            };
        }

        private static void AttachItemDrops(
            IEnumerable<BrickDuelRuleDefinition> brickDuelRules,
            IReadOnlyList<BrickDuelItemDropDefinition> itemDrops)
        {
            BrickDuelItemDropDefinition[] ordered = itemDrops
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.ItemId, StringComparer.Ordinal)
                .ToArray();
            foreach (BrickDuelRuleDefinition rule in brickDuelRules)
            {
                rule.ItemDrops = ordered;
            }
        }

        private static BrickDuelCompositionStageDefinition ReadBrickCompositionStage(
            Dictionary<string, object> item)
        {
            return new BrickDuelCompositionStageDefinition
            {
                GreenWeight = ReadFloat(item, "GreenWeight"),
                RedWeight = ReadFloat(item, "RedWeight"),
                YellowWeight = ReadFloat(item, "YellowWeight"),
                MysteryWeight = ReadFloat(item, "MysteryWeight"),
            };
        }

        private static void ValidateBrickCompositionWeights(
            float greenWeight,
            float redWeight,
            float yellowWeight,
            float mysteryWeight,
            string context)
        {
            if (greenWeight < 0f || redWeight < 0f || yellowWeight < 0f || mysteryWeight < 0f)
            {
                throw new FormatException($"{context} brick weights must be non-negative.");
            }

            float weightTotal = greenWeight + redWeight + yellowWeight + mysteryWeight;
            if (Math.Abs(weightTotal - 1f) > 0.0001f)
            {
                throw new FormatException($"{context} brick weights must total 1.");
            }

            if (greenWeight <= 0f && redWeight <= 0f && yellowWeight <= 0f && mysteryWeight <= 0f)
            {
                throw new FormatException($"{context} must include at least one positive brick weight.");
            }
        }

        private static UniversalChipDefinition ReadUniversalChip(Dictionary<string, object> item)
        {
            return new UniversalChipDefinition
            {
                ChipId = ReadString(item, "ChipId"),
                DisplayName = ReadString(item, "DisplayName"),
                Category = ReadEnum<ChipCategory>(item, "Category"),
                Rarity = ReadEnum<ChipRarity>(item, "Rarity"),
                Description = ReadOptionalString(item, "Description"),
                Modifiers = ReadOptionalArray(item, "Modifiers", ReadUniversalChipModifier),
                ConditionalModifiers = ReadOptionalArray(item, "ConditionalModifiers", ReadUniversalChipConditionalModifier),
                LinkedQuantumEvent = ReadOptionalString(item, "LinkedQuantumEvent"),
                IconPath = ReadOptionalString(item, "IconPath"),
            };
        }

        private static UniversalChipModifierDefinition ReadUniversalChipModifier(Dictionary<string, object> item)
        {
            return new UniversalChipModifierDefinition
            {
                ModifierType = ReadString(item, "ModifierType"),
                Op = ReadEnum<ModifierOp>(item, "Op"),
                ValueLv1 = ReadFloat(item, "ValueLv1"),
                ValueLv2 = ReadFloat(item, "ValueLv2"),
                ValueLv3 = ReadFloat(item, "ValueLv3"),
            };
        }

        private static UniversalChipConditionalModifierDefinition ReadUniversalChipConditionalModifier(Dictionary<string, object> item)
        {
            return new UniversalChipConditionalModifierDefinition
            {
                HeroId = ReadOptionalString(item, "HeroId"),
                PathId = ReadOptionalString(item, "PathId"),
                MinimumPathLevel = ReadOptionalInt(item, "MinimumPathLevel") ?? 0,
                ModifierType = ReadString(item, "ModifierType"),
                Op = ReadEnum<ModifierOp>(item, "Op"),
                Value = ReadFloat(item, "Value"),
            };
        }

        private static HeroDefinition ReadHero(Dictionary<string, object> item)
        {
            return new HeroDefinition
            {
                HeroId = ReadString(item, "HeroId"),
                DisplayName = ReadOptionalString(item, "DisplayName"),
                Description = ReadOptionalString(item, "Description"),
                ActiveAbilityId = ReadOptionalString(item, "ActiveAbilityId"),
                ActiveAbilityCooldownSeconds = ReadOptionalFloat(item, "ActiveAbilityCooldownSeconds") ?? 0f,
                PathIds = ReadOptionalStringList(item, "PathIds"),
            };
        }

        private static HeroPathDefinition ReadHeroPath(Dictionary<string, object> item)
        {
            return new HeroPathDefinition
            {
                PathId = ReadString(item, "PathId"),
                HeroId = ReadString(item, "HeroId"),
                DisplayName = ReadOptionalString(item, "DisplayName"),
                ResonanceCategories = ReadOptionalEnumList<ChipCategory>(item, "ResonanceCategories"),
                MilestoneEffects = ReadOptionalArray(item, "MilestoneEffects", ReadHeroPathEffect),
            };
        }

        private static HeroPathEffectDefinition ReadHeroPathEffect(Dictionary<string, object> item)
        {
            return new HeroPathEffectDefinition
            {
                PathLevel = ReadInt(item, "PathLevel"),
                EffectId = ReadOptionalString(item, "EffectId"),
                Description = ReadOptionalString(item, "Description"),
                Modifiers = ReadOptionalArray(item, "Modifiers", ReadUniversalChipModifier),
            };
        }

        private static SignatureChipDefinition ReadSignatureChip(Dictionary<string, object> item)
        {
            return new SignatureChipDefinition
            {
                ChipId = ReadString(item, "ChipId"),
                DisplayName = ReadString(item, "DisplayName"),
                HeroId = ReadString(item, "HeroId"),
                PathId = ReadString(item, "PathId"),
                VariantKind = ReadOptionalString(item, "VariantKind"),
                Parameters = ReadFloatMap(item, "Parameters"),
                Grade = item.ContainsKey("Grade") ? ReadEnum<SignatureGrade>(item, "Grade") : SignatureGrade.Refined,
                ResonanceValue = ReadInt(item, "ResonanceValue"),
                Description = ReadOptionalString(item, "Description"),
                EffectDesc = ReadOptionalString(item, "EffectDesc"),
                GradeModifiers = ReadOptionalArray(item, "GradeModifiers", ReadSignatureChipModifier),
                QualitativeEffectId = ReadOptionalString(item, "QualitativeEffectId"),
                UpgradesTo = ReadOptionalString(item, "UpgradesTo"),
                UpgradeCost = ReadOptionalInt(item, "UpgradeCost") ?? 0,
                IconPath = ReadOptionalString(item, "IconPath"),
            };
        }

        private static SignatureChipModifierDefinition ReadSignatureChipModifier(Dictionary<string, object> item)
        {
            return new SignatureChipModifierDefinition
            {
                ModifierType = ReadString(item, "ModifierType"),
                Op = ReadEnum<ModifierOp>(item, "Op"),
                Value = ReadFloat(item, "Value"),
            };
        }

        private static IEnumerable<T> ReadArray<T>(
            Dictionary<string, object> root,
            string key,
            Func<Dictionary<string, object>, T> read)
        {
            if (!root.TryGetValue(key, out object value) || !(value is List<object> array))
            {
                throw new FormatException($"Missing JSON array '{key}'.");
            }

            var result = new List<T>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is Dictionary<string, object> item))
                {
                    throw new FormatException($"'{key}' item {i} must be an object.");
                }

                result.Add(read(item));
            }

            return result;
        }

        private static IReadOnlyList<T> ReadOptionalArray<T>(
            Dictionary<string, object> root,
            string key,
            Func<Dictionary<string, object>, T> read)
        {
            if (!root.TryGetValue(key, out object value) || value == null)
            {
                return Array.Empty<T>();
            }

            if (!(value is List<object> array))
            {
                throw new FormatException($"JSON field '{key}' must be an array.");
            }

            var result = new List<T>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is Dictionary<string, object> item))
                {
                    throw new FormatException($"'{key}' item {i} must be an object.");
                }

                result.Add(read(item));
            }

            return result;
        }

        private static IReadOnlyDictionary<string, int> ReadIntMap(Dictionary<string, object> item, string key)
        {
            object value = ReadRequired(item, key);

            if (!(value is Dictionary<string, object> map))
            {
                throw new FormatException($"JSON field '{key}' must be an object.");
            }

            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in map)
            {
                if (pair.Value is double number)
                {
                    result[pair.Key] = Convert.ToInt32(number);
                    continue;
                }

                if (pair.Value is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    result[pair.Key] = parsed;
                    continue;
                }

                throw new FormatException($"'{key}.{pair.Key}' must be an integer.");
            }

            return result;
        }

        private static IReadOnlyDictionary<string, float> ReadFloatMap(Dictionary<string, object> item, string key)
        {
            object value = ReadRequired(item, key);
            if (!(value is Dictionary<string, object> map))
            {
                throw new FormatException($"JSON field '{key}' must be an object.");
            }

            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in map)
            {
                if (pair.Value is double number)
                {
                    result[pair.Key] = (float)number;
                }
                else if (pair.Value is string text && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                {
                    result[pair.Key] = parsed;
                }
                else
                {
                    throw new FormatException($"'{key}.{pair.Key}' must be numeric.");
                }
            }
            return result;
        }

        private static IReadOnlyDictionary<string, int> ReadTuningValues(Dictionary<string, object> item)
        {
            IReadOnlyDictionary<string, int> values = ReadIntMap(item, "TuningValues");
            RequireTuningValue(values, "HitOffsetInfluenceValue");
            RequireTuningValue(values, "PaddleVelocityInfluenceValue");
            RequireTuningValue(values, "MinimumOutwardShareValue");
            return values;
        }

        private static void RequireTuningValue(IReadOnlyDictionary<string, int> values, string key)
        {
            if (values == null || !values.ContainsKey(key))
            {
                throw new FormatException($"Missing required JSON field 'TuningValues.{key}'.");
            }
        }

        private static IReadOnlyList<MapVector2Definition> ReadVector2Array(Dictionary<string, object> item, string key, int minCount)
        {
            object value = ReadRequired(item, key);

            if (!(value is List<object> array))
            {
                throw new FormatException($"JSON field '{key}' must be an array.");
            }

            if (array.Count < minCount)
            {
                throw new FormatException($"JSON field '{key}' must contain at least {minCount} items.");
            }

            var result = new List<MapVector2Definition>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is Dictionary<string, object> point))
                {
                    throw new FormatException($"'{key}' item {i} must be an object.");
                }

                result.Add(new MapVector2Definition
                {
                    X = ReadFloat(point, "X"),
                    Y = ReadFloat(point, "Y"),
                });
            }

            return result;
        }

        private static MapVector2Definition ReadVector2(Dictionary<string, object> item, string key)
        {
            object value = ReadRequired(item, key);
            if (!(value is Dictionary<string, object> point))
            {
                throw new FormatException($"JSON field '{key}' must be an object.");
            }

            return new MapVector2Definition
            {
                X = ReadFloat(point, "X"),
                Y = ReadFloat(point, "Y"),
            };
        }

        private static MapVector2Definition ReadOptionalVector2(Dictionary<string, object> item, string key)
        {
            if (!item.TryGetValue(key, out object value) || value == null)
            {
                return null;
            }

            if (!(value is Dictionary<string, object> point))
            {
                throw new FormatException($"JSON field '{key}' must be an object.");
            }

            return new MapVector2Definition
            {
                X = ReadFloat(point, "X"),
                Y = ReadFloat(point, "Y"),
            };
        }

        private static string ReadString(Dictionary<string, object> item, string key)
        {
            object value = ReadRequired(item, key);
            return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string ReadOptionalString(Dictionary<string, object> item, string key)
        {
            if (!item.TryGetValue(key, out object value) || value == null)
                return string.Empty;

            return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int ReadInt(Dictionary<string, object> item, string key)
        {
            object value = ReadRequired(item, key);
            if (value is double number)
                return Convert.ToInt32(number);

            if (value is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                return parsed;

            throw new FormatException($"'{key}' must be an integer.");
        }

        private static int? ReadOptionalInt(Dictionary<string, object> item, string key)
        {
            if (!item.ContainsKey(key) || item[key] == null)
                return null;

            return ReadInt(item, key);
        }

        private static float? ReadOptionalFloat(Dictionary<string, object> item, string key)
        {
            if (!item.ContainsKey(key) || item[key] == null)
                return null;

            return ReadFloat(item, key);
        }

        private static float ReadFloat(Dictionary<string, object> item, string key)
        {
            object value = ReadRequired(item, key);
            if (value is double number)
                return Convert.ToSingle(number);

            if (value is string text && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;

            throw new FormatException($"'{key}' must be a number.");
        }

        private static float ReadPositiveFloat(Dictionary<string, object> item, string key)
        {
            float value = ReadFloat(item, key);
            if (value <= 0f)
            {
                throw new FormatException($"'{key}' must be greater than 0.");
            }

            return value;
        }

        private static float ReadNonNegativeFloat(Dictionary<string, object> item, string key)
        {
            float value = ReadFloat(item, key);
            if (value < 0f)
            {
                throw new FormatException($"'{key}' must be greater than or equal to 0.");
            }

            return value;
        }

        private static bool ReadBool(Dictionary<string, object> item, string key)
        {
            object value = ReadRequired(item, key);
            if (value is bool flag)
                return flag;

            if (value is string text && bool.TryParse(text, out bool parsed))
                return parsed;

            throw new FormatException($"'{key}' must be a boolean.");
        }

        private static T ReadEnum<T>(Dictionary<string, object> item, string key) where T : struct
        {
            object value = ReadRequired(item, key);
            if (value is string text)
            {
                if (Enum.TryParse(text, true, out T parsed))
                    return parsed;

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericText))
                    return (T)Enum.ToObject(typeof(T), numericText);
            }

            if (value is double number)
                return (T)Enum.ToObject(typeof(T), Convert.ToInt32(number));

            throw new FormatException($"'{key}' must be a valid {typeof(T).Name} name or value.");
        }

        private static IReadOnlyList<int> ReadIntList(Dictionary<string, object> item, string key)
        {
            object value = ReadRequired(item, key);
            if (!(value is List<object> array))
            {
                throw new FormatException($"'{key}' must be an array.");
            }

            var result = new List<int>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                object element = array[i];
                if (element is double number)
                {
                    result.Add(Convert.ToInt32(number));
                    continue;
                }

                if (element is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    result.Add(parsed);
                    continue;
                }

                throw new FormatException($"'{key}' item {i} must be an integer.");
            }

            return result;
        }

        private static IReadOnlyList<string> ReadOptionalStringList(Dictionary<string, object> item, string key)
        {
            if (!item.TryGetValue(key, out object value) || value == null)
            {
                return Array.Empty<string>();
            }

            if (!(value is List<object> array))
            {
                throw new FormatException($"'{key}' must be an array.");
            }

            var result = new List<string>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is string text))
                {
                    throw new FormatException($"'{key}' item {i} must be a string.");
                }

                result.Add(text);
            }

            return result;
        }

        private static IReadOnlyList<T> ReadOptionalEnumList<T>(Dictionary<string, object> item, string key) where T : struct
        {
            if (!item.TryGetValue(key, out object value) || value == null)
            {
                return Array.Empty<T>();
            }

            if (!(value is List<object> array))
            {
                throw new FormatException($"'{key}' must be an array.");
            }

            var result = new List<T>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                object element = array[i];
                if (element is string text && Enum.TryParse(text, true, out T parsed))
                {
                    result.Add(parsed);
                    continue;
                }

                if (element is string numberText && int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericText))
                {
                    result.Add((T)Enum.ToObject(typeof(T), numericText));
                    continue;
                }

                if (element is double number)
                {
                    result.Add((T)Enum.ToObject(typeof(T), Convert.ToInt32(number)));
                    continue;
                }

                throw new FormatException($"'{key}' item {i} must be a valid {typeof(T).Name} name or value.");
            }

            return result;
        }

        private static IReadOnlyList<BallSpeedTimePointDefinition> ReadOptionalFloatPairList(
            Dictionary<string, object> item,
            string key)
        {
            if (!item.TryGetValue(key, out object value) || value == null)
            {
                return Array.Empty<BallSpeedTimePointDefinition>();
            }

            if (!(value is List<object> array))
            {
                throw new FormatException($"'{key}' must be an array.");
            }

            var result = new List<BallSpeedTimePointDefinition>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is List<object> pair) || pair.Count != 2)
                {
                    throw new FormatException($"'{key}' item {i} must be an array with exactly two numbers.");
                }

                result.Add(new BallSpeedTimePointDefinition
                {
                    TimeSeconds = ConvertToFloat(pair[0], $"{key}[{i}][0]"),
                    Speed = ConvertToFloat(pair[1], $"{key}[{i}][1]"),
                });
            }

            return result;
        }

        private static object ReadRequired(Dictionary<string, object> item, string key)
        {
            if (!item.TryGetValue(key, out object value) || value == null)
            {
                throw new FormatException($"Missing required JSON field '{key}'.");
            }

            return value;
        }

        private static float ConvertToFloat(object value, string key)
        {
            if (value is double number)
                return Convert.ToSingle(number);

            if (value is string text && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;

            throw new FormatException($"'{key}' must be a number.");
        }

        private sealed class JsonValueParser
        {
            private readonly string _json;
            private int _index;

            private JsonValueParser(string json)
            {
                _json = json;
            }

            public static Dictionary<string, object> ParseObject(string json)
            {
                var parser = new JsonValueParser(json);
                object value = parser.ParseValue();
                parser.SkipWhitespace();
                if (!parser.IsEnd)
                {
                    throw new FormatException("Unexpected trailing JSON content.");
                }

                return value as Dictionary<string, object>
                    ?? throw new FormatException("Root JSON value must be an object.");
            }

            private bool IsEnd => _index >= _json.Length;

            private object ParseValue()
            {
                SkipWhitespace();
                if (IsEnd)
                    throw new FormatException("Unexpected end of JSON.");

                char current = _json[_index];
                if (current == '{')
                    return ParseObjectValue();
                if (current == '[')
                    return ParseArrayValue();
                if (current == '"')
                    return ParseString();
                if (current == 't')
                    return ParseLiteral("true", true);
                if (current == 'f')
                    return ParseLiteral("false", false);
                if (current == 'n')
                    return ParseLiteral("null", null);
                if (current == '-' || char.IsDigit(current))
                    return ParseNumber();

                throw new FormatException($"Unexpected JSON token '{current}'.");
            }

            private Dictionary<string, object> ParseObjectValue()
            {
                Consume('{');
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (TryConsume('}'))
                    return result;

                while (true)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    Consume(':');
                    result[key] = ParseValue();
                    SkipWhitespace();
                    if (TryConsume('}'))
                        return result;

                    Consume(',');
                }
            }

            private List<object> ParseArrayValue()
            {
                Consume('[');
                var result = new List<object>();
                SkipWhitespace();
                if (TryConsume(']'))
                    return result;

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                        return result;

                    Consume(',');
                }
            }

            private string ParseString()
            {
                Consume('"');
                var builder = new StringBuilder();
                while (!IsEnd)
                {
                    char current = _json[_index++];
                    if (current == '"')
                        return builder.ToString();

                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }

                    if (IsEnd)
                        throw new FormatException("Unterminated JSON string escape.");

                    char escaped = _json[_index++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeEscape());
                            break;
                        default:
                            throw new FormatException($"Unsupported JSON string escape '\\{escaped}'.");
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (_index + 4 > _json.Length)
                    throw new FormatException("Invalid JSON unicode escape.");

                string hex = _json.Substring(_index, 4);
                _index += 4;
                return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            private object ParseLiteral(string literal, object value)
            {
                if (_index + literal.Length > _json.Length ||
                    string.CompareOrdinal(_json, _index, literal, 0, literal.Length) != 0)
                {
                    throw new FormatException($"Invalid JSON literal near index {_index}.");
                }

                _index += literal.Length;
                return value;
            }

            private double ParseNumber()
            {
                int start = _index;
                if (_json[_index] == '-')
                    _index++;

                ReadDigits();
                if (!IsEnd && _json[_index] == '.')
                {
                    _index++;
                    ReadDigits();
                }

                if (!IsEnd && (_json[_index] == 'e' || _json[_index] == 'E'))
                {
                    _index++;
                    if (!IsEnd && (_json[_index] == '+' || _json[_index] == '-'))
                        _index++;

                    ReadDigits();
                }

                string text = _json.Substring(start, _index - start);
                return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private void ReadDigits()
            {
                int start = _index;
                while (!IsEnd && char.IsDigit(_json[_index]))
                {
                    _index++;
                }

                if (start == _index)
                    throw new FormatException($"Expected JSON digit near index {_index}.");
            }

            private void SkipWhitespace()
            {
                while (!IsEnd && char.IsWhiteSpace(_json[_index]))
                {
                    _index++;
                }
            }

            private void Consume(char expected)
            {
                SkipWhitespace();
                if (IsEnd || _json[_index] != expected)
                {
                    throw new FormatException($"Expected '{expected}' near index {_index}.");
                }

                _index++;
            }

            private bool TryConsume(char expected)
            {
                SkipWhitespace();
                if (IsEnd || _json[_index] != expected)
                    return false;

                _index++;
                return true;
            }
        }
    }

    public sealed class GatebreakerConfigLoadResult
    {
        private GatebreakerConfigLoadResult(
            bool succeeded,
            GatebreakerModeCatalog catalog,
            GatebreakerConfigLoadFailureReason failureReason,
            string source,
            string message,
            int? version)
        {
            Succeeded = succeeded;
            Catalog = catalog;
            FailureReason = failureReason;
            Source = source;
            Message = message;
            Version = version;
        }

        public bool Succeeded { get; }
        public GatebreakerModeCatalog Catalog { get; }
        public GatebreakerConfigLoadFailureReason FailureReason { get; }
        public string Source { get; }
        public string Message { get; }
        public int? Version { get; }
        public bool CanUseDefaultCatalogFallback => !Succeeded;

        public static GatebreakerConfigLoadResult Success(GatebreakerModeCatalog catalog, string source, int? version)
        {
            return new GatebreakerConfigLoadResult(
                true,
                catalog ?? throw new ArgumentNullException(nameof(catalog)),
                GatebreakerConfigLoadFailureReason.None,
                source,
                string.Empty,
                version);
        }

        public static GatebreakerConfigLoadResult Fail(
            GatebreakerConfigLoadFailureReason failureReason,
            string source,
            string message)
        {
            return new GatebreakerConfigLoadResult(
                false,
                null,
                failureReason,
                source,
                message ?? string.Empty,
                null);
        }
    }

    public enum GatebreakerConfigLoadFailureReason
    {
        None,
        AssetsRuntimeMissing,
        AssetLoadFailed,
        UnsupportedAssetType,
        EmptyContent,
        ParseFailed,
    }
}
