#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

bash tools/validation/check_boundary.sh
python3 -m unittest discover -s tools/tests -p 'test_*.py'
python3 tools/config_export/export_gatebreaker_config.py --dry-run
