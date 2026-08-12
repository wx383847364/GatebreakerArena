from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from gatebreaker_exporter.exporter import validate_all


class BrickDuelAiValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[3]
        cls.source_config = cls.repo_root / "Assets" / "Config"

    def test_emergency_distance_cannot_exceed_core_line(self) -> None:
        result = self._validate_with_ai_value("EmergencyDistance", 99.0)

        self.assertFalse(result.success)
        self.assertTrue(
            any("EmergencyDistance must not exceed" in error for error in result.errors),
            result.errors,
        )

    def test_move_dead_zone_cannot_exceed_arena_half_width(self) -> None:
        result = self._validate_with_ai_value("MoveDeadZone", 99.0)

        self.assertFalse(result.success)
        self.assertTrue(
            any("MoveDeadZone must not exceed" in error for error in result.errors),
            result.errors,
        )

    def _validate_with_ai_value(self, field: str, value: float):
        with tempfile.TemporaryDirectory(prefix="gatebreaker-ai-export-") as temporary:
            temporary_root = Path(temporary)
            config_root = temporary_root / "Config"
            shutil.copytree(self.source_config, config_root)
            ai_path = config_root / "DT_BrickDuelAiRule.json"
            rows = json.loads(ai_path.read_text(encoding="utf-8"))
            rows[0][field] = value
            ai_path.write_text(
                json.dumps(rows, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )
            return validate_all(
                self.repo_root,
                config_root,
                temporary_root / "json",
                temporary_root / "bytes",
            )


if __name__ == "__main__":
    unittest.main()
