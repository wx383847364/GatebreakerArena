#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Tuanjie/Hub/Editor/2022.3.62t9/Tuanjie.app/Contents/MacOS/Tuanjie}"

if [[ ! -x "$UNITY_EDITOR" ]]; then
  echo "[error] Unity/Tuanjie editor not found or not executable: $UNITY_EDITOR" >&2
  echo "[hint] Set UNITY_EDITOR=/path/to/editor and retry." >&2
  exit 1
fi

"$UNITY_EDITOR" \
  -batchmode \
  -projectPath "$ROOT_DIR" \
  -runTests \
  -testPlatform PlayMode \
  -testResults "$ROOT_DIR/TestResults_PlayMode.xml"
