#!/usr/bin/env bash
set -euo pipefail

# Build and package Other-Agencies mod artifacts.
# Output layout:
#   Other_agencies_(<version>)/
#     GameData/
#       Other-Agencies/
#         Plugins/OtherAgencies.dll
#         PluginData/
#         Localization/

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/OtherAgencies.csproj"
BUILD_CONFIG="Release"
BUILD_TFM="net472"

VERSION_ARG="${1:-}"

if [[ -n "$VERSION_ARG" ]]; then
  VERSION="$VERSION_ARG"
else
  VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT_FILE" | head -n1)"
  if [[ -z "$VERSION" ]]; then
    VERSION="$(git -C "$SCRIPT_DIR" describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || true)"
  fi
  if [[ -z "$VERSION" ]]; then
    VERSION="0.1.0"
  fi
fi

SANITIZED_VERSION="$(printf '%s' "$VERSION" | tr ' ' '_' | tr -cd '[:alnum:]._-')"
if [[ -z "$SANITIZED_VERSION" ]]; then
  SANITIZED_VERSION="0.1.0"
fi

ROOT_DIR="$SCRIPT_DIR/Other_agencies_($SANITIZED_VERSION)"
MOD_DIR="$ROOT_DIR/GameData/Other-Agencies"
PLUGINS_DIR="$MOD_DIR/Plugins"
PLUGIN_DATA_DIR="$MOD_DIR/PluginData"
LOCALIZATION_DIR="$MOD_DIR/Localization"
OUTPUT_DLL="$SCRIPT_DIR/src/bin/$BUILD_CONFIG/$BUILD_TFM/OtherAgencies.dll"

if [[ ! -f "$PROJECT_FILE" ]]; then
  echo "Could not find project file: $PROJECT_FILE" >&2
  exit 1
fi

echo "Packaging version: $SANITIZED_VERSION"
echo "[1/3] Building mod DLL..."
dotnet build "$PROJECT_FILE" -c "$BUILD_CONFIG"

if [[ ! -f "$OUTPUT_DLL" ]]; then
  echo "Build finished but output DLL not found: $OUTPUT_DLL" >&2
  exit 1
fi

echo "[2/3] Creating package folders..."
mkdir -p "$PLUGINS_DIR" "$PLUGIN_DATA_DIR" "$LOCALIZATION_DIR"

echo "[3/3] Copying DLL into package..."
cp -f "$OUTPUT_DLL" "$PLUGINS_DIR/OtherAgencies.dll"

echo "Done. Package created at: $MOD_DIR"
