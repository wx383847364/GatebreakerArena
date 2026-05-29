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
    payload: dict[str, Any] = {"Version": 1}

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
        "DT_ModeRule": ("ModeId", "InitialBallsInMatch", "MaxBallsInMatch", "InitialServeAmmo", "MaxServeAmmo", "MaxOwnedBallsInField"),
        "DT_BallRule": ("BallTypeId", "InitialSpeed", "MaxSpeed", "SpeedGainOnPaddleHit", "MinVerticalVelocity"),
        "DT_AIRule": ("AILevelId", "ReactionDelay", "ServeDecisionInterval", "AggressionWeight", "DefenseWeight"),
        "DT_MapRule": ("MapId", "SupportedPlayerCount", "SpawnLayoutType"),
    }
    for index, row in enumerate(rows):
        for field in required_fields[table_name]:
            if field not in row:
                errors.append(f"{table_name}[{index}]: missing required field {field}.")
        if table_name == "DT_ModeRule":
            _normalize_mode_time_fields(row, index, errors)


def _normalize_mode_time_fields(row: dict[str, Any], index: int, errors: list[str]) -> None:
    has_time = _has_value(row, "Time")
    has_match_duration = _has_value(row, "MatchDuration")
    if not has_time and not has_match_duration:
        errors.append(f"DT_ModeRule[{index}]: missing required field Time.")
        return

    if has_time and not has_match_duration:
        row["MatchDuration"] = row["Time"]
        return

    if has_match_duration and not has_time:
        row["Time"] = row["MatchDuration"]
        return

    if _normalize_number(row["Time"]) != _normalize_number(row["MatchDuration"]):
        errors.append(f"DT_ModeRule[{index}]: Time and MatchDuration must match.")


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
                "Time": 60,
                "MatchDuration": 60,
                "InitialBallsInMatch": 1,
                "MaxBallsInMatch": 4,
                "BaseServeCooldown": 6.0,
                "InitialServeAmmo": 1,
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
            },
            {
                "ModeId": "PVP_FFA",
                "ModeName": "PVP乱斗",
                "Time": 60,
                "MatchDuration": 60,
                "InitialBallsInMatch": 1,
                "MaxBallsInMatch": 4,
                "BaseServeCooldown": 6.0,
                "InitialServeAmmo": 1,
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
            },
            {
                "ModeId": "PVP_TEAM",
                "ModeName": "PVP组队乱斗",
                "Time": 60,
                "MatchDuration": 60,
                "InitialBallsInMatch": 1,
                "MaxBallsInMatch": 4,
                "BaseServeCooldown": 6.5,
                "InitialServeAmmo": 1,
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
                "BallSpeedModifier": 0.0,
                "GoalSizeModifier": 0.0,
                "ScenePrefabLocation": "Assets/HotUpdateContent/Res/prefabs/Scene3v3.prefab",
                "PaddlePrefabLocation": "Assets/HotUpdateContent/Res/prefabs/Baffle.prefab",
            }
        ],
    }
    return defaults[table_name]
