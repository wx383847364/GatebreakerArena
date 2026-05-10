# Gatebreaker Arena Tools

`tools/` holds repository-level helpers that are not Unity runtime code.

## Common Entrypoints

- `bash tools/validation/check_boundary.sh`
  - Checks `App.AOT / App.Shared / App.HotUpdate` boundary rules.
- `python3 tools/config_export/export_gatebreaker_config.py --dry-run`
  - Validates Gatebreaker config sources without writing outputs.
- `python3 tools/config_export/export_gatebreaker_config.py`
  - Exports Gatebreaker rules into JSON and hot-update bytes.
- `bash tools/validation/run_gatebreaker_validation.sh`
  - Runs boundary checks and Python tool tests.
- `bash tools/validation/run_gatebreaker_playmode_smoke.sh`
  - Runs the Unity/Tuanjie PlayMode smoke entry when an editor is available.
