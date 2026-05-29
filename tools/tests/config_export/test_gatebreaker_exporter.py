from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_ROOT = Path(__file__).resolve().parents[2] / "config_export"
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

from gatebreaker_exporter import export_all, validate_all  # noqa: E402


def _mode_row(**overrides: object) -> dict[str, object]:
    row: dict[str, object] = {
        "ModeId": "PVE_STANDARD",
        "InitialBallsInMatch": 1,
        "MaxBallsInMatch": 4,
        "InitialServeAmmo": 1,
        "MaxServeAmmo": 2,
        "MaxOwnedBallsInField": 1,
    }
    row.update(overrides)
    return row


class GatebreakerConfigExporterTests(unittest.TestCase):
    def test_defaults_match_gdd_v03_samples(self) -> None:
        with tempfile.TemporaryDirectory(prefix="gatebreaker_export_defaults_") as temp_dir:
            repo_root = Path(temp_dir) / "GatebreakerArena"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"

            result = validate_all(repo_root, config_root, json_root, binary_root)

            self.assertTrue(result.success, "\n".join(result.errors))
            self.assertEqual(result.payload["DT_ModeRule"][0]["ModeId"], "PVE_STANDARD")
            self.assertEqual(result.payload["DT_ModeRule"][0]["Time"], 60)
            self.assertEqual(result.payload["DT_ModeRule"][0]["MatchDuration"], 60)
            self.assertEqual(result.payload["DT_ModeRule"][0]["MaxBallsInMatch"], 4)
            self.assertEqual(result.payload["DT_BallRule"][0]["InitialSpeed"], 5.25)
            self.assertEqual(result.payload["DT_BallRule"][0]["MaxSpeed"], 9.8)
            self.assertEqual(result.payload["DT_PlayerColorRule"][0]["PlayerId"], 1)
            self.assertEqual(result.payload["DT_PlayerColorRule"][0]["ColorName"], "Red")
            self.assertTrue(result.warnings)

    def test_export_writes_json_and_bytes(self) -> None:
        with tempfile.TemporaryDirectory(prefix="gatebreaker_export_write_") as temp_dir:
            repo_root = Path(temp_dir) / "GatebreakerArena"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"

            result = export_all(repo_root, config_root, json_root, binary_root)

            self.assertTrue(result.success, "\n".join(result.errors))
            self.assertTrue(result.json_path.is_file())
            self.assertTrue(result.binary_path.is_file())
            payload = json.loads(result.json_path.read_text(encoding="utf-8"))
            self.assertIn("DT_MapRule", payload)
            self.assertIn("DT_PlayerColorRule", payload)
            self.assertEqual(result.binary_path.read_bytes(), result.json_path.read_text(encoding="utf-8").encode("utf-8"))

    def test_invalid_source_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="gatebreaker_export_invalid_") as temp_dir:
            repo_root = Path(temp_dir) / "GatebreakerArena"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"
            config_root.mkdir(parents=True)
            (config_root / "DT_ModeRule.json").write_text("{}\n", encoding="utf-8")

            result = validate_all(repo_root, config_root, json_root, binary_root)

            self.assertFalse(result.success)
            self.assertTrue(any("expected a non-empty array" in error for error in result.errors))

    def test_mode_time_field_accepts_time_only(self) -> None:
        with tempfile.TemporaryDirectory(prefix="gatebreaker_export_time_only_") as temp_dir:
            repo_root = Path(temp_dir) / "GatebreakerArena"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"
            config_root.mkdir(parents=True)
            (config_root / "DT_ModeRule.json").write_text(json.dumps([_mode_row(Time=45)]) + "\n", encoding="utf-8")

            result = validate_all(repo_root, config_root, json_root, binary_root)

            self.assertTrue(result.success, "\n".join(result.errors))
            self.assertEqual(result.payload["DT_ModeRule"][0]["Time"], 45)
            self.assertEqual(result.payload["DT_ModeRule"][0]["MatchDuration"], 45)

    def test_mode_time_field_accepts_legacy_match_duration_only(self) -> None:
        with tempfile.TemporaryDirectory(prefix="gatebreaker_export_duration_only_") as temp_dir:
            repo_root = Path(temp_dir) / "GatebreakerArena"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"
            config_root.mkdir(parents=True)
            (config_root / "DT_ModeRule.json").write_text(json.dumps([_mode_row(MatchDuration=30)]) + "\n", encoding="utf-8")

            result = validate_all(repo_root, config_root, json_root, binary_root)

            self.assertTrue(result.success, "\n".join(result.errors))
            self.assertEqual(result.payload["DT_ModeRule"][0]["Time"], 30)
            self.assertEqual(result.payload["DT_ModeRule"][0]["MatchDuration"], 30)

    def test_mode_time_field_rejects_conflicting_values(self) -> None:
        with tempfile.TemporaryDirectory(prefix="gatebreaker_export_time_conflict_") as temp_dir:
            repo_root = Path(temp_dir) / "GatebreakerArena"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"
            config_root.mkdir(parents=True)
            (config_root / "DT_ModeRule.json").write_text(
                json.dumps([_mode_row(Time=60, MatchDuration=45)]) + "\n",
                encoding="utf-8")

            result = validate_all(repo_root, config_root, json_root, binary_root)

            self.assertFalse(result.success)
            self.assertTrue(any("Time and MatchDuration must match" in error for error in result.errors))


if __name__ == "__main__":
    unittest.main()
