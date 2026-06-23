using System;
using System.Collections.Generic;
using System.Globalization;
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
                var catalog = new GatebreakerModeCatalog(
                    ReadArray(root, "DT_ModeRule", ReadMode),
                    ReadArray(root, "DT_BallRule", ReadBall),
                    ReadArray(root, "DT_AIRule", ReadAi),
                    ReadArray(root, "DT_MapRule", ReadMap),
                    ReadArray(root, "DT_PlayerColorRule", ReadPlayerColor));

                return GatebreakerConfigLoadResult.Success(catalog, source, ReadOptionalInt(root, "Version"));
            }
            catch (Exception ex)
            {
                return GatebreakerConfigLoadResult.Fail(
                    GatebreakerConfigLoadFailureReason.ParseFailed,
                    source,
                    $"Failed to parse Gatebreaker rules JSON: {ex.Message}");
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
