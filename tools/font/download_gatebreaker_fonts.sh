#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLIENT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
FONT_ROOT="$CLIENT_ROOT/Assets/HotUpdateContent/Res/fonts"
SOURCE_DIR="$FONT_ROOT/source"
LICENSE_DIR="$FONT_ROOT/licenses"

mkdir -p "$SOURCE_DIR" "$LICENSE_DIR" "$FONT_ROOT/tmp" "$FONT_ROOT/preview"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

cd "$TMP_DIR"
curl -L -o fusion.zip 'https://github.com/TakWolf/fusion-pixel-font/releases/download/2026.05.07/fusion-pixel-font-12px-proportional-otf-v2026.05.07.zip'
unzip -q fusion.zip
cp fusion-pixel-12px-proportional-zh_hans.otf "$SOURCE_DIR/FusionPixel12-Proportional-zh_hans.otf"
cp OFL.txt "$LICENSE_DIR/OFL_FusionPixel.txt"
cp LICENSE/*.txt "$LICENSE_DIR/"

curl -L -o "$SOURCE_DIR/PressStart2P-Regular.ttf" 'https://raw.githubusercontent.com/google/fonts/main/ofl/pressstart2p/PressStart2P-Regular.ttf'
curl -L -o "$LICENSE_DIR/OFL_PressStart2P.txt" 'https://raw.githubusercontent.com/google/fonts/main/ofl/pressstart2p/OFL.txt'

curl -L -o "$SOURCE_DIR/PixelifySans-wght.ttf" 'https://raw.githubusercontent.com/google/fonts/main/ofl/pixelifysans/PixelifySans%5Bwght%5D.ttf'
curl -L -o "$LICENSE_DIR/OFL_PixelifySans.txt" 'https://raw.githubusercontent.com/google/fonts/main/ofl/pixelifysans/OFL.txt'

curl -L -o "$SOURCE_DIR/DotGothic16-Regular.ttf" 'https://raw.githubusercontent.com/google/fonts/main/ofl/dotgothic16/DotGothic16-Regular.ttf'
curl -L -o "$LICENSE_DIR/OFL_DotGothic16.txt" 'https://raw.githubusercontent.com/google/fonts/main/ofl/dotgothic16/OFL.txt'

find "$FONT_ROOT" -maxdepth 2 -type f | sort
