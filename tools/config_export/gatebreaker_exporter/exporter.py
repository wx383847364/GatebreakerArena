from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


SOURCE_FILES = {
    "DT_ModeRule": "DT_ModeRule.json",
    "DT_BallRule": "DT_BallRule.json",
    "DT_AIRule": "DT_AIRule.json",
    "DT_MapRule": "DT_MapRule.json",
    "DT_PlayerColorRule": "DT_PlayerColorRule.json",
}


@dataclass(frozen=True)
class ExportResult:
    success: bool
    errors: list[str]
    warnings: list[str]
    json_path: Path
    binary_path: Path
    payload: dict[str, Any]


def validate_all(repo_root: Path, config_root: Path, json_root: Path, binary_root: Path) -> ExportResult:
    payload, warnings, errors = _build_payload(config_root)
    return ExportResult(
        success=not errors,
        errors=errors,
        warnings=warnings,
        json_path=json_root / "gatebreaker_rules.json",
        binary_path=binary_root / "gatebreaker_rules.bytes",
        payload=payload,
    )


def export_all(repo_root: Path, config_root: Path, json_root: Path, binary_root: Path) -> ExportResult:
    result = validate_all(repo_root, config_root, json_root, binary_root)
    if not result.success:
        return result

    json_root.mkdir(parents=True, exist_ok=True)
    binary_root.mkdir(parents=True, exist_ok=True)
    encoded = json.dumps(result.payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    result.json_path.write_text(encoded, encoding="utf-8")
    result.binary_path.write_bytes(encoded.encode("utf-8"))
    return result


def _build_payload(config_root: Path) -> tuple[dict[str, Any], list[str], list[str]]:
    warnings: list[str] = []
    errors: list[str] = []
    payload: dict[str, Any] = {
        "Version": 1,
        "_FieldComments": _field_comments(),
    }

    if not config_root.exists():
        warnings.append(f"Config directory is missing, using built-in GDD v0.3 defaults: {config_root}")

    for table_name, file_name in SOURCE_FILES.items():
        source_path = config_root / file_name
        if source_path.is_file():
            try:
                rows = json.loads(source_path.read_text(encoding="utf-8"))
            except json.JSONDecodeError as exc:
                errors.append(f"{file_name}: invalid JSON: {exc}")
                rows = []
        else:
            warnings.append(f"{file_name} not found, using built-in GDD v0.3 defaults.")
            rows = _default_rows(table_name)

        if not isinstance(rows, list) or not rows:
            errors.append(f"{file_name}: expected a non-empty array.")
            rows = []

        _validate_table(table_name, rows, errors)
        payload[table_name] = rows

    return payload, warnings, errors


def _validate_table(table_name: str, rows: list[Any], errors: list[str]) -> None:
    if not all(isinstance(row, dict) for row in rows):
        errors.append(f"{table_name}: every row must be an object.")
        return

    required_fields = {
        "DT_ModeRule": (
            "ModeId", "ModeName", "InitialBallsInMatch", "MaxBallsInMatch", "BaseServeCooldown",
            "InitialServeAmmo", "MaxServeAmmo", "MaxOwnedBallsInField", "GoalPauseTime",
            "ScoreRuleType", "EnableOvertime", "OvertimeRuleType", "OvertimeDuration",
            "OvertimeEligibleOnly", "OvertimeWinScore", "AllowAimServe", "FinalPhaseStartTime",
            "FinalPhaseBallSpeedScale", "FinalPhaseCooldownScale", "TuningValues",
        ),
        "DT_BallRule": (
            "BallTypeId", "BallTypeName", "InitialSpeed", "MaxSpeed", "PaddleBounceFactor",
            "WallBounceFactor", "GoalReboundFactor", "SpeedGainOnPaddleHit",
            "MinVerticalVelocity", "DangerPromptThreshold", "BallContactRadius", "TrailStyle",
            "ColorTag",
        ),
        "DT_AIRule": (
            "AILevelId", "AILevelName", "ReactionDelay", "PredictError",
            "ServeDecisionInterval", "AggressionWeight", "DefenseWeight", "MultiBallPriority",
            "AimAccuracy", "TargetSwitchFrequency",
        ),
        "DT_MapRule": (
            "MapId", "MapName", "SupportedPlayerCount", "SpawnLayoutType", "HasObstacle",
            "InitialBallsModifier", "MaxBallsModifier", "ServeCooldownModifier",
            "PaddleMoveSpeed", "BallSpeedModifier", "GoalSizeModifier", "ArenaHalfWidth",
            "ArenaHalfHeight", "PaddleInset", "PaddleLength", "PaddleThickness",
            "GoalHalfLength", "GoalTriggerInset", "GoalContactLineInset", "BoundaryPoints",
            "GoalCenters",
        ),
        "DT_PlayerColorRule": ("PlayerId", "Red", "Green", "Blue", "Alpha"),
    }
    for index, row in enumerate(rows):
        for field in required_fields[table_name]:
            if field not in row:
                errors.append(f"{table_name}[{index}]: missing required field {field}.")
        if table_name == "DT_ModeRule":
            _normalize_mode_time_fields(row, index, errors)
            _validate_ball_speed_by_time(row, index, errors)
            _validate_tuning_values(row, index, errors)
        if table_name == "DT_BallRule":
            _validate_positive_number(row, "BallContactRadius", table_name, index, errors)
        if table_name == "DT_MapRule":
            _validate_positive_number(row, "PaddleMoveSpeed", table_name, index, errors)
            _validate_positive_number(row, "GoalHalfLength", table_name, index, errors)
            _validate_vector_array(row, "BoundaryPoints", 3, table_name, index, errors)
            _validate_vector_array(row, "GoalCenters", 1, table_name, index, errors)
            _validate_map_player_side_bindings(row, index, errors)


def _normalize_mode_time_fields(row: dict[str, Any], index: int, errors: list[str]) -> None:
    has_time = _has_value(row, "Time")
    has_match_duration = _has_value(row, "MatchDuration")
    if not has_time and not has_match_duration:
        errors.append(f"DT_ModeRule[{index}]: missing required field MatchDuration.")
        return

    if has_time and not has_match_duration:
        row["MatchDuration"] = row["Time"]
        row.pop("Time", None)
        return

    if has_match_duration and not has_time:
        return

    if _normalize_number(row["Time"]) != _normalize_number(row["MatchDuration"]):
        errors.append(f"DT_ModeRule[{index}]: Time and MatchDuration must match.")

    row.pop("Time", None)


def _validate_map_player_side_bindings(row: dict[str, Any], index: int, errors: list[str]) -> None:
    bindings = row.get("PlayerSideBindings")
    if bindings is None:
        return

    if not isinstance(bindings, list):
        errors.append(f"DT_MapRule[{index}]: PlayerSideBindings must be an array.")
        return

    seen_players: set[int] = set()
    seen_segments: set[int] = set()
    for binding_index, binding in enumerate(bindings):
        if not isinstance(binding, dict):
            errors.append(f"DT_MapRule[{index}].PlayerSideBindings[{binding_index}]: every item must be an object.")
            continue

        for field in ("PlayerId", "ScenePosition", "BoundarySegmentIndex"):
            if field not in binding:
                errors.append(f"DT_MapRule[{index}].PlayerSideBindings[{binding_index}]: missing required field {field}.")

        player_id = _normalize_int(binding.get("PlayerId"))
        segment_index = _normalize_int(binding.get("BoundarySegmentIndex"))
        if player_id is None or player_id <= 0:
            errors.append(f"DT_MapRule[{index}].PlayerSideBindings[{binding_index}]: PlayerId must be positive.")
        elif player_id in seen_players:
            errors.append(f"DT_MapRule[{index}].PlayerSideBindings[{binding_index}]: duplicate PlayerId {player_id}.")
        else:
            seen_players.add(player_id)

        if segment_index is None or segment_index < 0:
            errors.append(f"DT_MapRule[{index}].PlayerSideBindings[{binding_index}]: BoundarySegmentIndex must be non-negative.")
        elif segment_index in seen_segments:
            errors.append(f"DT_MapRule[{index}].PlayerSideBindings[{binding_index}]: duplicate BoundarySegmentIndex {segment_index}.")
        else:
            seen_segments.add(segment_index)


def _validate_ball_speed_by_time(row: dict[str, Any], index: int, errors: list[str]) -> None:
    value = row.get("BallSpeedByTime")
    if value is None:
        return

    if not isinstance(value, list):
        errors.append(f"DT_ModeRule[{index}]: BallSpeedByTime must be an array.")
        return

    previous_time: float | None = None
    for point_index, point in enumerate(value):
        if not isinstance(point, list) or len(point) != 2:
            errors.append(f"DT_ModeRule[{index}].BallSpeedByTime[{point_index}]: expected [timeSeconds, speed].")
            continue

        time_seconds = _normalize_float(point[0])
        speed = _normalize_float(point[1])
        if time_seconds is None:
            errors.append(f"DT_ModeRule[{index}].BallSpeedByTime[{point_index}][0]: timeSeconds must be a number.")
        elif time_seconds < 0:
            errors.append(f"DT_ModeRule[{index}].BallSpeedByTime[{point_index}][0]: timeSeconds must be non-negative.")
        elif previous_time is not None and time_seconds <= previous_time:
            errors.append(f"DT_ModeRule[{index}].BallSpeedByTime[{point_index}][0]: timeSeconds must increase.")
        else:
            previous_time = time_seconds

        if speed is None:
            errors.append(f"DT_ModeRule[{index}].BallSpeedByTime[{point_index}][1]: speed must be a number.")
        elif speed < 0:
            errors.append(f"DT_ModeRule[{index}].BallSpeedByTime[{point_index}][1]: speed must be non-negative.")


def _validate_tuning_values(row: dict[str, Any], index: int, errors: list[str]) -> None:
    value = row.get("TuningValues")
    if not isinstance(value, dict):
        errors.append(f"DT_ModeRule[{index}]: TuningValues must be an object.")
        return

    for field in ("HitOffsetInfluenceValue", "PaddleVelocityInfluenceValue", "MinimumOutwardShareValue"):
        number = _normalize_int(value.get(field))
        if number is None:
            errors.append(f"DT_ModeRule[{index}].TuningValues: missing numeric field {field}.")


def _validate_positive_number(
    row: dict[str, Any],
    field: str,
    table_name: str,
    index: int,
    errors: list[str],
) -> None:
    number = _normalize_float(row.get(field))
    if number is None:
        errors.append(f"{table_name}[{index}]: {field} must be a number.")
    elif number <= 0:
        errors.append(f"{table_name}[{index}]: {field} must be positive.")


def _validate_vector_array(
    row: dict[str, Any],
    field: str,
    min_count: int,
    table_name: str,
    index: int,
    errors: list[str],
) -> None:
    value = row.get(field)
    if not isinstance(value, list) or len(value) < min_count:
        errors.append(f"{table_name}[{index}]: {field} must be an array with at least {min_count} items.")
        return

    for point_index, point in enumerate(value):
        if not isinstance(point, dict):
            errors.append(f"{table_name}[{index}].{field}[{point_index}]: every item must be an object.")
            continue
        for coordinate in ("X", "Y"):
            if _normalize_float(point.get(coordinate)) is None:
                errors.append(f"{table_name}[{index}].{field}[{point_index}]: {coordinate} must be a number.")


def _normalize_int(value: Any) -> int | None:
    try:
        number = int(value)
    except (TypeError, ValueError):
        return None
    return number


def _normalize_float(value: Any) -> float | None:
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _has_value(row: dict[str, Any], field: str) -> bool:
    return field in row and row[field] is not None


def _normalize_number(value: Any) -> Any:
    try:
        number = float(value)
    except (TypeError, ValueError):
        return str(value)
    if number.is_integer():
        return int(number)
    return number


def _default_rows(table_name: str) -> list[dict[str, Any]]:
    defaults = {
        "DT_ModeRule": [
            {
                "ModeId": "PVE_STANDARD",
                "ModeName": "PVE标准",
                "MatchDuration": 60,
                "InitialBallsInMatch": 0,
                "MaxBallsInMatch": 4,
                "BaseServeCooldown": 6.0,
                "InitialServeAmmo": 2,
                "MaxServeAmmo": 2,
                "MaxOwnedBallsInField": 1,
                "GoalPauseTime": 0.4,
                "ScoreRuleType": "AddScore",
                "EnableOvertime": True,
                "OvertimeRuleType": "SuddenDeath",
                "OvertimeDuration": 60,
                "OvertimeEligibleOnly": True,
                "OvertimeWinScore": 1,
                "AllowAimServe": True,
                "FinalPhaseStartTime": 30,
                "FinalPhaseBallSpeedScale": 1.05,
                "FinalPhaseCooldownScale": 0.95,
                "TuningValues": {
                    "HitOffsetInfluenceValue": 90,
                    "PaddleVelocityInfluenceValue": 55,
                    "MinimumOutwardShareValue": 25,
                },
                "BallSpeedByTime": [[15, 10], [30, 15], [45, 20]],
            },
            {
                "ModeId": "PVP_FFA",
                "ModeName": "PVP乱斗",
                "MatchDuration": 60,
                "InitialBallsInMatch": 0,
                "MaxBallsInMatch": 4,
                "BaseServeCooldown": 6.0,
                "InitialServeAmmo": 2,
                "MaxServeAmmo": 2,
                "MaxOwnedBallsInField": 1,
                "GoalPauseTime": 0.4,
                "ScoreRuleType": "AddScore",
                "EnableOvertime": True,
                "OvertimeRuleType": "SuddenDeath",
                "OvertimeDuration": 60,
                "OvertimeEligibleOnly": True,
                "OvertimeWinScore": 1,
                "AllowAimServe": True,
                "FinalPhaseStartTime": 30,
                "FinalPhaseBallSpeedScale": 1.10,
                "FinalPhaseCooldownScale": 0.90,
                "TuningValues": {
                    "HitOffsetInfluenceValue": 90,
                    "PaddleVelocityInfluenceValue": 55,
                    "MinimumOutwardShareValue": 25,
                },
                "BallSpeedByTime": [[15, 10], [30, 15], [45, 20]],
            },
            {
                "ModeId": "PVP_TEAM",
                "ModeName": "PVP组队乱斗",
                "MatchDuration": 60,
                "InitialBallsInMatch": 0,
                "MaxBallsInMatch": 4,
                "BaseServeCooldown": 6.5,
                "InitialServeAmmo": 2,
                "MaxServeAmmo": 2,
                "MaxOwnedBallsInField": 1,
                "GoalPauseTime": 0.4,
                "ScoreRuleType": "TeamScore",
                "EnableOvertime": True,
                "OvertimeRuleType": "SuddenDeath",
                "OvertimeDuration": 60,
                "OvertimeEligibleOnly": True,
                "OvertimeWinScore": 1,
                "AllowAimServe": True,
                "FinalPhaseStartTime": 30,
                "FinalPhaseBallSpeedScale": 1.10,
                "FinalPhaseCooldownScale": 0.92,
                "TuningValues": {
                    "HitOffsetInfluenceValue": 90,
                    "PaddleVelocityInfluenceValue": 55,
                    "MinimumOutwardShareValue": 25,
                },
                "BallSpeedByTime": [[15, 10], [30, 15], [45, 20]],
            },
        ],
        "DT_BallRule": [
            {
                "BallTypeId": "BALL_NORMAL",
                "BallTypeName": "普通球",
                "InitialSpeed": 5.25,
                "MaxSpeed": 9.8,
                "PaddleBounceFactor": 1.0,
                "WallBounceFactor": 1.0,
                "GoalReboundFactor": 1.0,
                "SpeedGainOnPaddleHit": 0.15,
                "MinVerticalVelocity": 2.0,
                "DangerPromptThreshold": 1.2,
                "BallContactRadius": 0.08,
                "TrailStyle": "Default",
                "ColorTag": "Neutral",
                "PrefabLocation": "Assets/HotUpdateContent/Res/prefabs/Ball01.prefab",
            }
        ],
        "DT_AIRule": [
            {
                "AILevelId": "AI_NORMAL",
                "AILevelName": "普通",
                "ReactionDelay": 0.18,
                "PredictError": 0.25,
                "ServeDecisionInterval": 0.6,
                "AggressionWeight": 0.55,
                "DefenseWeight": 0.70,
                "MultiBallPriority": 0.65,
                "AimAccuracy": 0.60,
                "TargetSwitchFrequency": 0.50,
            }
        ],
        "DT_MapRule": [
            {
                "MapId": "MAP_ARENA_01",
                "MapName": "标准四边场",
                "SupportedPlayerCount": [2, 3, 4],
                "SpawnLayoutType": "FourSide",
                "HasObstacle": False,
                "InitialBallsModifier": 0,
                "MaxBallsModifier": 16,
                "ServeCooldownModifier": 0.0,
                "MaxServeAmmo": 5,
                "MaxOwnedBallsInField": 5,
                "ServeRechargeSeconds": 5.0,
                "PaddleMoveSpeed": 8,
                "BallSpeedModifier": 0.0,
                "GoalSizeModifier": 0.0,
                "ScenePrefabLocation": "Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab",
                "PaddlePrefabLocation": "Assets/HotUpdateContent/Res/prefabs/Baffle.prefab",
                "DefaultPlayerCount": 3,
                "ArenaHalfWidth": 2.81,
                "ArenaHalfHeight": 2.456,
                "PaddleInset": 0.18,
                "PaddleLength": 0.78,
                "PaddleThickness": 0.05,
                "GoalHalfLength": 1.0586,
                "GoalTriggerInset": 0.14,
                "GoalContactLineInset": 0.04,
                "BoundaryPoints": [
                    {"X": 1.379, "Y": -2.456},
                    {"X": 2.809, "Y": 0.021},
                    {"X": 1.411, "Y": 2.443},
                    {"X": -1.416, "Y": 2.443},
                    {"X": -2.809, "Y": 0.031},
                    {"X": -1.373, "Y": -2.456},
                ],
                "GoalCenters": [
                    {"X": 2.086, "Y": -1.231},
                    {"X": 2.118, "Y": 1.218},
                    {"X": 0.0, "Y": 2.443},
                    {"X": -2.114, "Y": 1.234},
                    {"X": -2.094, "Y": -1.207},
                    {"X": 0.0, "Y": -2.456},
                ],
                "PlayerSideBindings": [
                    {
                        "PlayerId": 1,
                        "ScenePosition": "Position01",
                        "BoundarySegmentIndex": 5,
                    },
                    {
                        "PlayerId": 2,
                        "ScenePosition": "Position03",
                        "BoundarySegmentIndex": 1,
                    },
                    {
                        "PlayerId": 3,
                        "ScenePosition": "Position05",
                        "BoundarySegmentIndex": 3,
                    },
                ],
            }
        ],
        "DT_PlayerColorRule": [
            {
                "PlayerId": 1,
                "ColorName": "Red",
                "Red": 1.0,
                "Green": 0.18,
                "Blue": 0.16,
                "Alpha": 1.0,
            },
            {
                "PlayerId": 2,
                "ColorName": "Blue",
                "Red": 0.20,
                "Green": 0.48,
                "Blue": 1.0,
                "Alpha": 1.0,
            },
            {
                "PlayerId": 3,
                "ColorName": "Green",
                "Red": 0.24,
                "Green": 0.86,
                "Blue": 0.34,
                "Alpha": 1.0,
            },
            {
                "PlayerId": 4,
                "ColorName": "Yellow",
                "Red": 1.0,
                "Green": 0.86,
                "Blue": 0.18,
                "Alpha": 1.0,
            },
        ],
    }
    return defaults[table_name]


def _field_comments() -> dict[str, dict[str, str]]:
    return {
        "DT_ModeRule": {
            "MatchDuration": "比赛时间，单位秒；表示常规比赛从开局到时间结束的总时长，最终以策划填写的数据为准。",
            "BallSpeedByTime": "球移动速度时间表，二维数组格式为 [[时间秒, 速度], ...]；例如 [[15,10],[30,15],[45,20]] 表示比赛进行到 15 秒、30 秒、45 秒时球速分别调整为 10、15、20。",
            "TuningValues": "弹板反弹调参，包含 HitOffsetInfluenceValue、PaddleVelocityInfluenceValue、MinimumOutwardShareValue，运行时按整数档位换算为实际系数。",
        },
        "DT_BallRule": {
            "BallContactRadius": "球体碰撞半径，单位为场景距离；用于球与边界、球门和挡板碰撞判定。",
        },
        "DT_MapRule": {
            "PaddleMoveSpeed": "板子移动速度，单位为场景距离/秒；数值越大，玩家板子沿可移动方向移动越快，最终以策划填写的数据为准。",
            "BoundaryPoints": "场地边界点，按顺时针或逆时针顺序填写；运行时用相邻点生成碰撞边。",
            "GoalCenters": "每条边对应的球门中心点；数量应与边界段数量匹配。",
        },
    }
