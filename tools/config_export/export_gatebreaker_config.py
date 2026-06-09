#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

from gatebreaker_exporter import export_all, validate_all


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Gatebreaker Arena config exporter.")
    parser.add_argument("--dry-run", action="store_true", help="Validate config sources without writing outputs.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parents[2]
    config_root = repo_root / "Assets" / "Config"
    json_root = config_root / "json"
    binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"

    result = validate_all(repo_root, config_root, json_root, binary_root) if args.dry_run else export_all(repo_root, config_root, json_root, binary_root)
    for warning in result.warnings:
        print(f"[warn] {warning}")
    for error in result.errors:
        print(f"[error] {error}")

    if not result.success:
        return 1

    if args.dry_run:
        print("[info] dry-run complete.")
    else:
        print(f"[info] json output: {result.json_path}")
        print(f"[info] binary output: {result.binary_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
