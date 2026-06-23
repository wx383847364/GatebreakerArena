#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY_BIN="${UNITY_BIN:-/Applications/Tuanjie/Hub/Editor/2022.3.62t9/Tuanjie.app/Contents/MacOS/Tuanjie}"
OUTPUT_PATH="${1:-Builds/Android/GatebreakerArena-android-smoke.apk}"
LOG_PATH="${ROOT_DIR}/Logs/gatebreaker_android_smoke_build.log"

if [[ ! -x "${UNITY_BIN}" ]]; then
  echo "Unity/Tuanjie executable not found: ${UNITY_BIN}" >&2
  echo "Set UNITY_BIN to your editor executable and retry." >&2
  exit 2
fi

"${UNITY_BIN}" \
  -batchmode \
  -quit \
  -projectPath "${ROOT_DIR}" \
  -executeMethod GatebreakerSmokeBuildPipeline.BuildAndroidSmokeApkFromCommandLine \
  -gatebreakerOutput "${OUTPUT_PATH}" \
  -logFile "${LOG_PATH}"

echo "Android smoke APK: ${ROOT_DIR}/${OUTPUT_PATH}"
echo "Build log: ${LOG_PATH}"
