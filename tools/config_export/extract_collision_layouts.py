#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any


LAYOUT_PREFABS = {
    2: "Assets/HotUpdateContent/Res/prefabs/Scene2P.prefab",
    3: "Assets/HotUpdateContent/Res/prefabs/Scene3P.prefab",
    4: "Assets/HotUpdateContent/Res/prefabs/Scene4P.prefab",
}

PLAYER_BINDINGS = {
    2: {1: "Position01", 2: "Position04"},
    3: {1: "Position01", 2: "Position03", 3: "Position05"},
    4: {1: "Position01", 2: "Position03", 3: "Position05", 4: "Position07"},
}


@dataclass(frozen=True)
class TransformData:
    file_id: str
    game_object_id: str
    name: str
    active: bool
    parent_id: str
    children: tuple[str, ...]
    position: tuple[float, float]
    rotation_degrees: float
    scale: tuple[float, float]


@dataclass(frozen=True)
class ColliderData:
    transform_id: str
    size: tuple[float, float]
    offset: tuple[float, float]


def main() -> int:
    parser = argparse.ArgumentParser(description="Extract Gatebreaker collision layouts from scene prefabs.")
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--map-id", default="MAP_ARENA_01")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    config_path = args.repo_root / "Assets/Config/DT_MapRule.json"
    rows = json.loads(config_path.read_text(encoding="utf-8"))
    if not isinstance(rows, list):
        raise SystemExit("DT_MapRule.json must contain an array.")

    target = next((row for row in rows if row.get("MapId") == args.map_id), None)
    if target is None:
        raise SystemExit(f"MapId not found: {args.map_id}")

    layouts = []
    for player_count, prefab in LAYOUT_PREFABS.items():
        layouts.append(extract_layout(args.repo_root / prefab, player_count))
    target["CollisionLayouts"] = layouts

    encoded = json.dumps(rows, ensure_ascii=False, indent=2) + "\n"
    if args.dry_run:
        print(json.dumps(layouts, ensure_ascii=False, indent=2))
        return 0

    config_path.write_text(encoded, encoding="utf-8")
    print(f"updated {config_path}")
    return 0


def extract_layout(prefab_path: Path, player_count: int) -> dict[str, Any]:
    transforms, colliders = parse_prefab(prefab_path)
    positions = [
        transform for transform in transforms.values()
        if re.fullmatch(r"Position\d+", transform.name or "") and transform.active
    ]
    positions.sort(key=lambda item: int(item.name.replace("Position", "")))
    if len(positions) < 3:
        raise SystemExit(f"{prefab_path}: expected at least 3 active PositionNN objects.")

    center = average_position([transform_world_position(transforms, position.file_id) for position in positions])
    closed_segments = create_closed_position_segments(transforms, positions, center)
    boundary_segments = []
    segment_index_by_position: dict[str, int] = {}
    for position in positions:
        start, end = closed_segments[position.file_id]
        segment = extract_segment(transforms, colliders, position, center, start, end)
        segment_index_by_position[position.name] = len(boundary_segments)
        boundary_segments.append(segment)

    bindings = []
    for player_id, scene_position in PLAYER_BINDINGS[player_count].items():
        if scene_position not in segment_index_by_position:
            raise SystemExit(f"{prefab_path}: missing binding position {scene_position}.")
        bindings.append({
            "PlayerId": player_id,
            "ScenePosition": scene_position,
            "BoundarySegmentIndex": segment_index_by_position[scene_position],
        })

    return {
        "PlayerCount": player_count,
        "BoundarySegments": boundary_segments,
        "PlayerSideBindings": bindings,
    }


def extract_segment(
    transforms: dict[str, TransformData],
    colliders: dict[str, ColliderData],
    position: TransformData,
    center: tuple[float, float],
    start: tuple[float, float],
    end: tuple[float, float],
) -> dict[str, Any]:
    collider_ids = collect_active_collision_children(transforms, colliders, position.file_id)
    if not collider_ids:
        raise SystemExit(f"{position.name}: no active collision children found.")

    goal_min_x = math.inf
    goal_max_x = -math.inf
    goal_min_y = math.inf
    goal_max_y = -math.inf
    for transform_id in collider_ids:
        transform = transforms[transform_id]
        collider = colliders.get(transform_id) or create_fallback_collider(transform)
        if collider is None:
            continue
        local_min_x, local_max_x, local_min_y, local_max_y = project_collider_to_position_extents(
            transforms,
            position.file_id,
            transform_id,
            collider)
        if normalize_name(transform.name) == "net":
            goal_min_x = min(goal_min_x, local_min_x)
            goal_max_x = max(goal_max_x, local_max_x)
            goal_min_y = min(goal_min_y, local_min_y)
            goal_max_y = max(goal_max_y, local_max_y)

    start, end = orient_toward_center(start, end, center)
    result: dict[str, Any] = {
        "ScenePosition": position.name,
        "Start": vector(start),
        "End": vector(end),
    }

    if goal_max_x > goal_min_x:
        goal_center_y = (goal_min_y + goal_max_y) * 0.5
        goal_center = transform_point(transforms, position.file_id, ((goal_min_x + goal_max_x) * 0.5, goal_center_y))
        result["GoalCenter"] = vector(goal_center)
        result["GoalHalfLength"] = round((goal_max_x - goal_min_x) * transform_world_scale_x(transforms, position.file_id) * 0.5, 4)
        goal_trigger_inset = (goal_max_y - goal_min_y) * abs(transform_world_scale_y(transforms, position.file_id)) * 0.5
        result["GoalTriggerInset"] = round(goal_trigger_inset, 4)

    return result


def create_closed_position_segments(
    transforms: dict[str, TransformData],
    positions: list[TransformData],
    center: tuple[float, float],
) -> dict[str, tuple[tuple[float, float], tuple[float, float]]]:
    lines = {position.file_id: create_position_center_line(transforms, position.file_id) for position in positions}
    result: dict[str, tuple[tuple[float, float], tuple[float, float]]] = {}
    count = len(positions)
    for index, position in enumerate(positions):
        previous_position = positions[(index - 1) % count]
        next_position = positions[(index + 1) % count]
        current_line = lines[position.file_id]
        previous_line = lines[previous_position.file_id]
        next_line = lines[next_position.file_id]
        start = intersect_lines(current_line, previous_line)
        end = intersect_lines(current_line, next_line)
        if start is None or end is None:
            raise SystemExit(f"{position.name}: adjacent PositionNN center lines do not intersect.")
        result[position.file_id] = orient_toward_center(start, end, center)
    return result


def create_position_center_line(
    transforms: dict[str, TransformData],
    transform_id: str,
) -> tuple[tuple[float, float], tuple[float, float]]:
    origin = transform_point(transforms, transform_id, (0.0, 0.0))
    axis_point = transform_point(transforms, transform_id, (1.0, 0.0))
    direction = (axis_point[0] - origin[0], axis_point[1] - origin[1])
    length = math.hypot(direction[0], direction[1])
    if length <= 1e-6:
        raise SystemExit(f"{transforms[transform_id].name}: invalid PositionNN transform direction.")
    return origin, (direction[0] / length, direction[1] / length)


def intersect_lines(
    first: tuple[tuple[float, float], tuple[float, float]],
    second: tuple[tuple[float, float], tuple[float, float]],
) -> tuple[float, float] | None:
    first_origin, first_direction = first
    second_origin, second_direction = second
    denominator = cross(first_direction, second_direction)
    if abs(denominator) <= 1e-6:
        return None

    delta = (second_origin[0] - first_origin[0], second_origin[1] - first_origin[1])
    distance = cross(delta, second_direction) / denominator
    return (
        first_origin[0] + first_direction[0] * distance,
        first_origin[1] + first_direction[1] * distance,
    )


def cross(left: tuple[float, float], right: tuple[float, float]) -> float:
    return left[0] * right[1] - left[1] * right[0]


def collect_active_collision_children(
    transforms: dict[str, TransformData],
    colliders: dict[str, ColliderData],
    root_id: str,
) -> list[str]:
    result: list[str] = []
    stack = list(transforms[root_id].children)
    while stack:
        transform_id = stack.pop()
        transform = transforms[transform_id]
        if not transform.active:
            continue

        name = normalize_name(transform.name)
        if name in {"net", "notnet"} or name.startswith("square"):
            result.append(transform_id)
        stack.extend(transform.children)
    return result


def create_fallback_collider(transform: TransformData) -> ColliderData | None:
    name = normalize_name(transform.name)
    if name == "net":
        return ColliderData(transform.file_id, (3.16, 0.3), (0.0, 0.0))
    if name == "notnet":
        return ColliderData(transform.file_id, (5.0, 0.3), (0.0, 0.0))
    if name.startswith("square"):
        return ColliderData(transform.file_id, (0.6, 0.3), (0.0, 0.0))
    return None


def parse_prefab(prefab_path: Path) -> tuple[dict[str, TransformData], dict[str, ColliderData]]:
    objects = parse_unity_objects(prefab_path)
    names: dict[str, str] = {}
    actives: dict[str, bool] = {}
    transform_game_objects: dict[str, str] = {}
    transform_parents: dict[str, str] = {}
    transform_children: dict[str, list[str]] = {}
    transform_positions: dict[str, tuple[float, float]] = {}
    transform_rotations: dict[str, float] = {}
    transform_scales: dict[str, tuple[float, float]] = {}
    collider_by_go: dict[str, ColliderData] = {}

    for unity_type, file_id, lines in objects:
        if unity_type == 1:
            names[file_id] = find_name(lines)
            actives[file_id] = find_active(lines)
        elif unity_type == 4:
            transform_game_objects[file_id] = find_file_id(lines, "m_GameObject")
            transform_parents[file_id] = find_file_id(lines, "m_Father")
            transform_children[file_id] = find_children(lines)
            transform_positions[file_id] = find_vector3_xy(lines, "m_LocalPosition")
            transform_rotations[file_id] = find_z_rotation_degrees(lines)
            transform_scales[file_id] = find_vector3_xy(lines, "m_LocalScale", default=(1.0, 1.0))
        elif unity_type == 61:
            go = find_file_id(lines, "m_GameObject")
            collider_by_go[go] = ColliderData("", find_vector2(lines, "m_Size"), find_vector2(lines, "m_Offset"))

    transforms: dict[str, TransformData] = {}
    game_object_to_transform = {go: transform_id for transform_id, go in transform_game_objects.items()}
    for transform_id, go in transform_game_objects.items():
        transforms[transform_id] = TransformData(
            file_id=transform_id,
            game_object_id=go,
            name=names.get(go, ""),
            active=actives.get(go, True),
            parent_id=transform_parents.get(transform_id, "0"),
            children=tuple(transform_children.get(transform_id, [])),
            position=transform_positions.get(transform_id, (0.0, 0.0)),
            rotation_degrees=transform_rotations.get(transform_id, 0.0),
            scale=transform_scales.get(transform_id, (1.0, 1.0)),
        )

    colliders: dict[str, ColliderData] = {}
    for go, collider in collider_by_go.items():
        transform_id = game_object_to_transform.get(go)
        if transform_id:
            colliders[transform_id] = ColliderData(transform_id, collider.size, collider.offset)

    return transforms, colliders


def parse_unity_objects(prefab_path: Path) -> list[tuple[int, str, list[str]]]:
    objects: list[tuple[int, str, list[str]]] = []
    current: tuple[int, str, list[str]] | None = None
    for line in prefab_path.read_text(encoding="utf-8", errors="ignore").splitlines():
        match = re.match(r"--- !u!(\d+) &(-?\d+)", line)
        if match:
            if current is not None:
                objects.append(current)
            current = (int(match.group(1)), match.group(2), [])
        elif current is not None:
            current[2].append(line)
    if current is not None:
        objects.append(current)
    return objects


def project_collider_to_position_extents(
    transforms: dict[str, TransformData],
    position_id: str,
    transform_id: str,
    collider: ColliderData,
) -> tuple[float, float, float, float]:
    half_width = collider.size[0] * 0.5
    half_height = collider.size[1] * 0.5
    xs = []
    ys = []
    for x in (collider.offset[0] - half_width, collider.offset[0] + half_width):
        for y in (collider.offset[1] - half_height, collider.offset[1] + half_height):
            world = transform_point(transforms, transform_id, (x, y))
            local = inverse_transform_point(transforms, position_id, world)
            xs.append(local[0])
            ys.append(local[1])
    return min(xs), max(xs), min(ys), max(ys)


def transform_point(transforms: dict[str, TransformData], transform_id: str, point: tuple[float, float]) -> tuple[float, float]:
    chain = transform_chain(transforms, transform_id)
    x, y = point
    for transform in chain:
        x *= transform.scale[0]
        y *= transform.scale[1]
        angle = math.radians(transform.rotation_degrees)
        cos_a = math.cos(angle)
        sin_a = math.sin(angle)
        x, y = x * cos_a - y * sin_a, x * sin_a + y * cos_a
        x += transform.position[0]
        y += transform.position[1]
    return x, y


def inverse_transform_point(transforms: dict[str, TransformData], transform_id: str, point: tuple[float, float]) -> tuple[float, float]:
    chain = transform_chain(transforms, transform_id)
    x, y = point
    for transform in reversed(chain):
        x -= transform.position[0]
        y -= transform.position[1]
        angle = math.radians(-transform.rotation_degrees)
        cos_a = math.cos(angle)
        sin_a = math.sin(angle)
        x, y = x * cos_a - y * sin_a, x * sin_a + y * cos_a
        x /= transform.scale[0] if abs(transform.scale[0]) > 1e-6 else 1.0
        y /= transform.scale[1] if abs(transform.scale[1]) > 1e-6 else 1.0
    return x, y


def transform_chain(transforms: dict[str, TransformData], transform_id: str) -> list[TransformData]:
    chain: list[TransformData] = []
    current = transform_id
    while current and current != "0" and current in transforms:
        transform = transforms[current]
        chain.append(transform)
        current = transform.parent_id
    return chain


def transform_world_position(transforms: dict[str, TransformData], transform_id: str) -> tuple[float, float]:
    return transform_point(transforms, transform_id, (0.0, 0.0))


def transform_world_scale_x(transforms: dict[str, TransformData], transform_id: str) -> float:
    scale = 1.0
    for transform in transform_chain(transforms, transform_id):
        scale *= transform.scale[0]
    return scale


def transform_world_scale_y(transforms: dict[str, TransformData], transform_id: str) -> float:
    scale = 1.0
    for transform in transform_chain(transforms, transform_id):
        scale *= transform.scale[1]
    return scale


def orient_toward_center(
    start: tuple[float, float],
    end: tuple[float, float],
    center: tuple[float, float],
) -> tuple[tuple[float, float], tuple[float, float]]:
    edge = (end[0] - start[0], end[1] - start[1])
    normal = (-edge[1], edge[0])
    midpoint = ((start[0] + end[0]) * 0.5, (start[1] + end[1]) * 0.5)
    to_center = (center[0] - midpoint[0], center[1] - midpoint[1])
    if normal[0] * to_center[0] + normal[1] * to_center[1] < 0:
        return end, start
    return start, end


def average_position(points: list[tuple[float, float]]) -> tuple[float, float]:
    return (
        sum(point[0] for point in points) / len(points),
        sum(point[1] for point in points) / len(points),
    )


def vector(point: tuple[float, float]) -> dict[str, float]:
    return {"X": round(point[0], 4), "Y": round(point[1], 4)}


def normalize_name(name: str) -> str:
    return (name or "").strip().strip("'").strip().lower()


def find_name(lines: list[str]) -> str:
    for line in lines:
        match = re.search(r"m_Name: (.*)$", line)
        if match:
            return match.group(1).strip()
    return ""


def find_active(lines: list[str]) -> bool:
    for line in lines:
        match = re.search(r"m_IsActive: (\d+)", line)
        if match:
            return bool(int(match.group(1)))
    return True


def find_file_id(lines: list[str], field: str) -> str:
    for line in lines:
        match = re.search(rf"{field}: \{{fileID: (-?\d+)\}}", line)
        if match:
            return match.group(1)
    return "0"


def find_children(lines: list[str]) -> list[str]:
    children: list[str] = []
    in_children = False
    for line in lines:
        if line.strip() == "m_Children:":
            in_children = True
            continue
        if in_children and not line.startswith("  - "):
            break
        if in_children:
            match = re.search(r"- \{fileID: (-?\d+)\}", line)
            if match:
                children.append(match.group(1))
    return children


def find_vector2(lines: list[str], field: str, default: tuple[float, float] = (0.0, 0.0)) -> tuple[float, float]:
    for line in lines:
        match = re.search(rf"{field}: \{{x: ([^,]+), y: ([^}}]+)\}}", line)
        if match:
            return float(match.group(1)), float(match.group(2))
    return default


def find_vector3_xy(lines: list[str], field: str, default: tuple[float, float] = (0.0, 0.0)) -> tuple[float, float]:
    for line in lines:
        match = re.search(rf"{field}: \{{x: ([^,]+), y: ([^,]+), z: ([^}}]+)\}}", line)
        if match:
            return float(match.group(1)), float(match.group(2))
    return default


def find_z_rotation_degrees(lines: list[str]) -> float:
    for line in lines:
        match = re.search(r"m_LocalRotation: \{x: [^,]+, y: [^,]+, z: ([^,]+), w: ([^}]+)\}", line)
        if match:
            z = float(match.group(1))
            w = float(match.group(2))
            return math.degrees(2.0 * math.atan2(z, w))
    return 0.0


if __name__ == "__main__":
    raise SystemExit(main())
