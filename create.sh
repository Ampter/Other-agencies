#!/usr/bin/env bash
set -euo pipefail

# Build and package Other-Agencies mod artifacts.
# Output layout:
#   Other_agencies/
#     GameData/
#       Other-Agencies/
#         Plugins/OtherAgencies.dll
#         agencies.cfg

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/OtherAgencies.csproj"
BUILD_CONFIG="Release"
BUILD_TFM="net472"

ROOT_DIR="$SCRIPT_DIR/Other_agencies"
MOD_DIR="$ROOT_DIR/GameData/Other-Agencies"
OUTPUT_DLL="$SCRIPT_DIR/src/bin/$BUILD_CONFIG/$BUILD_TFM/OtherAgencies.dll"
SOURCE_AGENCIES_CFG="$SCRIPT_DIR/agencies.cfg"
OUTPUT_AGENCIES_CFG="$MOD_DIR/agencies.cfg"

if [[ ! -f "$PROJECT_FILE" ]]; then
  echo "Could not find project file: $PROJECT_FILE" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_AGENCIES_CFG" ]]; then
  echo "Could not find agencies config file: $SOURCE_AGENCIES_CFG" >&2
  exit 1
fi

echo "[1/4] Building mod DLL..."
dotnet build "$PROJECT_FILE" -c "$BUILD_CONFIG"

if [[ ! -f "$OUTPUT_DLL" ]]; then
  echo "Build finished but output DLL not found: $OUTPUT_DLL" >&2
  exit 1
fi



echo "[2/4] Creating package folders..."
mkdir -p "$PLUGINS_DIR" "$PLUGIN_DATA_DIR" "$LOCALIZATION_DIR"

echo "[3/4] Copying DLL into package..."
cp -f "$OUTPUT_DLL" "$PLUGINS_DIR/OtherAgencies.dll"

echo "[4/4] Copying agencies.cfg into package..."
cp -f "$SOURCE_AGENCIES_CFG" "$OUTPUT_AGENCIES_CFG"

echo "Done. Package created at: $MOD_DIR"
