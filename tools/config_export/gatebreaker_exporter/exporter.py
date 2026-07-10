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
    "DT_Hero": "DT_Hero.json",
    "DT_HeroPath": "DT_HeroPath.json",
    "DT_UniversalChip": "DT_UniversalChip.json",
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
        "DT_Hero": ("HeroId", "DisplayName", "ActiveAbilityId", "ActiveAbilityCooldownSeconds", "PathIds"),
        "DT_HeroPath": ("PathId", "HeroId", "DisplayName", "ResonanceCategories", "MilestoneEffects"),
        "DT_UniversalChip": ("ChipId", "DisplayName", "Category", "Rarity", "Modifiers", "ConditionalModifiers"),
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
            _validate_collision_layouts(row, index, errors)
        if table_name == "DT_Hero":
            _validate_hero(row, index, errors)
        if table_name == "DT_HeroPath":
            _validate_hero_path(row, index, errors)
        if table_name == "DT_UniversalChip":
            _validate_universal_chip(row, index, errors)

    if table_name == "DT_Hero":
        _validate_unique_ids(rows, "HeroId", table_name, errors)
    elif table_name == "DT_HeroPath":
        _validate_unique_ids(rows, "PathId", table_name, errors)
    elif table_name == "DT_UniversalChip":
        _validate_v1_universal_chip_set(rows, errors)


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


def _validate_collision_layouts(row: dict[str, Any], index: int, errors: list[str]) -> None:
    layouts = row.get("CollisionLayouts")
    if layouts is None:
        return

    if not isinstance(layouts, list):
        errors.append(f"DT_MapRule[{index}]: CollisionLayouts must be an array.")
        return

    seen_counts: set[int] = set()
    for layout_index, layout in enumerate(layouts):
        if not isinstance(layout, dict):
            errors.append(f"DT_MapRule[{index}].CollisionLayouts[{layout_index}]: every item must be an object.")
            continue

        player_count = _normalize_int(layout.get("PlayerCount"))
        if player_count is None or player_count <= 0:
            errors.append(f"DT_MapRule[{index}].CollisionLayouts[{layout_index}]: PlayerCount must be positive.")
        elif player_count in seen_counts:
            errors.append(f"DT_MapRule[{index}].CollisionLayouts[{layout_index}]: duplicate PlayerCount {player_count}.")
        else:
            seen_counts.add(player_count)

        segments = layout.get("BoundarySegments")
        if not isinstance(segments, list) or len(segments) < 3:
            errors.append(f"DT_MapRule[{index}].CollisionLayouts[{layout_index}]: BoundarySegments must contain at least 3 items.")
        elif player_count is not None and player_count > 0 and len(segments) < player_count:
            errors.append(f"DT_MapRule[{index}].CollisionLayouts[{layout_index}]: BoundarySegments must cover the player count.")
        else:
            for segment_index, segment in enumerate(segments):
                _validate_collision_segment(segment, index, layout_index, segment_index, errors)

        bindings = layout.get("PlayerSideBindings")
        if bindings is None:
            errors.append(f"DT_MapRule[{index}].CollisionLayouts[{layout_index}]: PlayerSideBindings is required.")
        else:
            _validate_layout_player_side_bindings(
                bindings,
                index,
                layout_index,
                len(segments) if isinstance(segments, list) else 0,
                player_count or 0,
                errors)


def _validate_collision_segment(
    segment: Any,
    map_index: int,
    layout_index: int,
    segment_index: int,
    errors: list[str],
) -> None:
    prefix = f"DT_MapRule[{map_index}].CollisionLayouts[{layout_index}].BoundarySegments[{segment_index}]"
    if not isinstance(segment, dict):
        errors.append(f"{prefix}: every item must be an object.")
        return

    for field in ("ScenePosition", "Start", "End"):
        if field not in segment:
            errors.append(f"{prefix}: missing required field {field}.")

    for field in ("Start", "End", "GoalCenter"):
        value = segment.get(field)
        if value is None:
            if field == "GoalCenter":
                continue
            errors.append(f"{prefix}.{field}: must be an object.")
            continue

        if not isinstance(value, dict):
            errors.append(f"{prefix}.{field}: must be an object.")
            continue

        for axis in ("X", "Y"):
            if _normalize_float(value.get(axis)) is None:
                errors.append(f"{prefix}.{field}.{axis}: must be a number.")

    for field in ("GoalHalfLength", "GoalTriggerInset"):
        value = segment.get(field)
        if value is not None:
            number = _normalize_float(value)
            if number is None:
                errors.append(f"{prefix}.{field}: must be a number.")
            elif number < 0:
                errors.append(f"{prefix}.{field}: must be non-negative.")


def _validate_layout_player_side_bindings(
    bindings: Any,
    map_index: int,
    layout_index: int,
    segment_count: int,
    player_count: int,
    errors: list[str],
) -> None:
    if not isinstance(bindings, list):
        errors.append(f"DT_MapRule[{map_index}].CollisionLayouts[{layout_index}]: PlayerSideBindings must be an array.")
        return

    if player_count > 0 and len(bindings) < player_count:
        errors.append(f"DT_MapRule[{map_index}].CollisionLayouts[{layout_index}]: PlayerSideBindings must cover the player count.")

    seen_players: set[int] = set()
    seen_segments: set[int] = set()
    for binding_index, binding in enumerate(bindings):
        prefix = f"DT_MapRule[{map_index}].CollisionLayouts[{layout_index}].PlayerSideBindings[{binding_index}]"
        if not isinstance(binding, dict):
            errors.append(f"{prefix}: every item must be an object.")
            continue

        for field in ("PlayerId", "ScenePosition", "BoundarySegmentIndex"):
            if field not in binding:
                errors.append(f"{prefix}: missing required field {field}.")

        player_id = _normalize_int(binding.get("PlayerId"))
        segment_index = _normalize_int(binding.get("BoundarySegmentIndex"))
        if player_id is None or player_id <= 0:
            errors.append(f"{prefix}: PlayerId must be positive.")
        elif player_id in seen_players:
            errors.append(f"{prefix}: duplicate PlayerId {player_id}.")
        else:
            seen_players.add(player_id)

        if segment_index is None or segment_index < 0:
            errors.append(f"{prefix}: BoundarySegmentIndex must be non-negative.")
        elif segment_count > 0 and segment_index >= segment_count:
            errors.append(f"{prefix}: BoundarySegmentIndex must be within BoundarySegments.")
        elif segment_index in seen_segments:
            errors.append(f"{prefix}: duplicate BoundarySegmentIndex {segment_index}.")
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


def _validate_hero(row: dict[str, Any], index: int, errors: list[str]) -> None:
    if not isinstance(row.get("PathIds"), list) or len(row["PathIds"]) != 2:
        errors.append(f"DT_Hero[{index}]: PathIds must contain exactly two V1 paths.")
    if _normalize_float(row.get("ActiveAbilityCooldownSeconds")) is None or float(row["ActiveAbilityCooldownSeconds"]) <= 0:
        errors.append(f"DT_Hero[{index}]: ActiveAbilityCooldownSeconds must be positive.")


def _validate_hero_path(row: dict[str, Any], index: int, errors: list[str]) -> None:
    categories = row.get("ResonanceCategories")
    if not isinstance(categories, list) or len(categories) != 2:
        errors.append(f"DT_HeroPath[{index}]: ResonanceCategories must contain exactly two categories.")
    elif any(category not in {"Strike", "Guard", "Flow", "Chaos"} for category in categories):
        errors.append(f"DT_HeroPath[{index}]: ResonanceCategories contains an invalid category.")

    milestones = row.get("MilestoneEffects")
    if not isinstance(milestones, list) or {item.get("PathLevel") for item in milestones if isinstance(item, dict)} != {1, 2}:
        errors.append(f"DT_HeroPath[{index}]: MilestoneEffects must define exactly M1 and M2.")


def _validate_universal_chip(row: dict[str, Any], index: int, errors: list[str]) -> None:
    if row.get("Category") not in {"Strike", "Guard", "Flow", "Chaos"}:
        errors.append(f"DT_UniversalChip[{index}]: Category must be a V1 chip category.")
    if row.get("Rarity") != "Common":
        errors.append(f"DT_UniversalChip[{index}]: every V1 universal chip must be Common Lv1.")
    modifiers = row.get("Modifiers")
    if not isinstance(modifiers, list) or not modifiers:
        errors.append(f"DT_UniversalChip[{index}]: Modifiers must be a non-empty array.")
        return
    valid_ops = {"Add", "Multiply", "Override", "Flag"}
    for modifier_index, modifier in enumerate(modifiers):
        if not isinstance(modifier, dict):
            errors.append(f"DT_UniversalChip[{index}].Modifiers[{modifier_index}]: every item must be an object.")
            continue
        for field in ("ModifierType", "Op", "ValueLv1", "ValueLv2", "ValueLv3"):
            if field not in modifier:
                errors.append(f"DT_UniversalChip[{index}].Modifiers[{modifier_index}]: missing required field {field}.")
        if modifier.get("Op") not in valid_ops:
            errors.append(f"DT_UniversalChip[{index}].Modifiers[{modifier_index}]: invalid Op.")
        for field in ("ValueLv1", "ValueLv2", "ValueLv3"):
            if _normalize_float(modifier.get(field)) is None:
                errors.append(f"DT_UniversalChip[{index}].Modifiers[{modifier_index}].{field}: must be numeric.")
    conditionals = row.get("ConditionalModifiers")
    if not isinstance(conditionals, list):
        errors.append(f"DT_UniversalChip[{index}]: ConditionalModifiers must be an array.")


def _validate_unique_ids(rows: list[Any], field: str, table_name: str, errors: list[str]) -> None:
    values = [row.get(field) for row in rows if isinstance(row, dict)]
    if len(values) != len(set(values)):
        errors.append(f"{table_name}: {field} values must be unique.")


def _validate_v1_universal_chip_set(rows: list[Any], errors: list[str]) -> None:
    expected = {
        "STRIKE_POWER", "STRIKE_SERVE", "STRIKE_OVERCHARGE",
        "GUARD_LENGTH", "GUARD_GOAL", "GUARD_BOUNCE",
        "FLOW_SPEED", "FLOW_AMMO", "FLOW_CAPACITY",
        "CHAOS_SPIN", "CHAOS_RICOCHET", "CHAOS_DISRUPT",
    }
    actual = {row.get("ChipId") for row in rows if isinstance(row, dict)}
    if actual != expected:
        errors.append("DT_UniversalChip: must contain exactly the 12 frozen V1 universal chip ids.")


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
        "DT_Hero": _v1_default_heroes(),
        "DT_HeroPath": _v1_default_paths(),
        "DT_UniversalChip": _v1_default_universal_chips(),
    }
    return defaults[table_name]


def _v1_default_heroes() -> list[dict[str, Any]]:
    return [
        {"HeroId": "HERO_FROST_QUEEN", "DisplayName": "冰雪女王", "Description": "", "ActiveAbilityId": "ABILITY_FROST_BLIZZARD", "ActiveAbilityCooldownSeconds": 12.0, "PathIds": ["PATH_FROST_EXTREME", "PATH_FROST_CRYSTAL"]},
        {"HeroId": "HERO_THORN_GUARDIAN", "DisplayName": "荆棘守护者", "Description": "", "ActiveAbilityId": "ABILITY_THORN_ARMOR", "ActiveAbilityCooldownSeconds": 12.0, "PathIds": ["PATH_THORN_BRISTLE", "PATH_THORN_GROWTH"]},
        {"HeroId": "HERO_RADIANT_PALADIN", "DisplayName": "辉光圣骑", "Description": "", "ActiveAbilityId": "ABILITY_RADIANT_SHIELD", "ActiveAbilityCooldownSeconds": 12.0, "PathIds": ["PATH_RADIANT_HOLY_LIGHT", "PATH_RADIANT_RAY"]},
    ]


def _v1_default_paths() -> list[dict[str, Any]]:
    definitions = [
        ("PATH_FROST_EXTREME", "HERO_FROST_QUEEN", "极寒", ["Strike", "Guard"]),
        ("PATH_FROST_CRYSTAL", "HERO_FROST_QUEEN", "冰晶", ["Guard", "Flow"]),
        ("PATH_THORN_BRISTLE", "HERO_THORN_GUARDIAN", "荆棘", ["Strike", "Guard"]),
        ("PATH_THORN_GROWTH", "HERO_THORN_GUARDIAN", "生长", ["Guard", "Flow"]),
        ("PATH_RADIANT_HOLY_LIGHT", "HERO_RADIANT_PALADIN", "圣光", ["Strike", "Guard"]),
        ("PATH_RADIANT_RAY", "HERO_RADIANT_PALADIN", "光芒", ["Strike", "Flow"]),
    ]
    return [
        {"PathId": path_id, "HeroId": hero_id, "DisplayName": name, "ResonanceCategories": categories,
         "MilestoneEffects": [
             {"PathLevel": 1, "EffectId": f"{path_id}_M1", "Description": "", "Modifiers": []},
             {"PathLevel": 2, "EffectId": f"{path_id}_M2", "Description": "", "Modifiers": []},
         ]}
        for path_id, hero_id, name, categories in definitions
    ]


def _v1_default_universal_chips() -> list[dict[str, Any]]:
    definitions = [
        ("STRIKE_POWER", "蓄能击", "Strike", [("PaddleBounceSpeedMultiplier", "Multiply", 1.2)]),
        ("STRIKE_SERVE", "重发球", "Strike", [("ServeInitialSpeedMultiplier", "Multiply", 1.15)]),
        ("STRIKE_OVERCHARGE", "过载", "Strike", [("BallMaxSpeedMultiplier", "Multiply", 1.1), ("PaddleLengthMultiplier", "Multiply", 0.9)]),
        ("GUARD_LENGTH", "长板", "Guard", [("PaddleLengthMultiplier", "Multiply", 1.15)]),
        ("GUARD_GOAL", "收缩门", "Guard", [("GoalHalfLengthMultiplier", "Multiply", 0.9)]),
        ("GUARD_BOUNCE", "弹性墙", "Guard", [("EnemyWallBounceSpeedMultiplier", "Multiply", 0.9)]),
        ("FLOW_SPEED", "疾驰", "Flow", [("PaddleMoveSpeedMultiplier", "Multiply", 1.15)]),
        ("FLOW_AMMO", "快装填", "Flow", [("ServeCooldownMultiplier", "Multiply", 0.8333333)]),
        ("FLOW_CAPACITY", "弹药库", "Flow", [("ServeAmmoCapacity", "Add", 1.0)]),
        ("CHAOS_SPIN", "旋球", "Chaos", [("WallBounceDeflectionDegrees", "Add", 3.0)]),
        ("CHAOS_RICOCHET", "连锁弹射", "Chaos", [("RicochetRequiredCollisionCount", "Override", 3.0), ("RicochetSpeedMultiplier", "Multiply", 1.1)]),
        ("CHAOS_DISRUPT", "扰乱", "Chaos", [("EnemyPaddleMoveSpeedMultiplier", "Multiply", 0.85), ("EnemyPaddleSlowDurationSeconds", "Override", 1.5)]),
    ]
    return [
        {"ChipId": chip_id, "DisplayName": name, "Category": category, "Rarity": "Common", "Description": "",
         "Modifiers": [{"ModifierType": kind, "Op": op, "ValueLv1": value, "ValueLv2": value, "ValueLv3": value} for kind, op, value in modifiers],
         "ConditionalModifiers": [], "LinkedQuantumEvent": "", "IconPath": ""}
        for chip_id, name, category, modifiers in definitions
    ]


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
            "CollisionLayouts": "按玩家人数记录的地图阻挡线数据；运行时优先用它生成边界、进球判定和调试红线。",
        },
        "DT_Hero": {
            "PathIds": "V1 每名英雄固定关联两条共鸣路径；对局开始时仅使用这里定义的路径。",
            "ActiveAbilityCooldownSeconds": "主动技能冷却，单位秒；倒计时结束进入 Playing 后才开始计时。",
        },
        "DT_HeroPath": {
            "ResonanceCategories": "路径匹配的两个芯片类别；觉醒 1/2 张匹配芯片分别触发 M1/M2。",
            "MilestoneEffects": "仅定义 M1 与 M2 的数据标识和说明；实际玩法由 HotUpdate 的英雄系统处理。",
        },
        "DT_UniversalChip": {
            "Modifiers": "Lv1 通用芯片的直接静态规则修饰；注入器按 ChipId 稳定排序应用。",
            "ConditionalModifiers": "可选的英雄/路径门槛修饰；满足 HeroId、PathId 和 MinimumPathLevel 时应用。",
        },
    }
