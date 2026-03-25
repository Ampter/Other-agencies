#!/usr/bin/env bash
set -euo pipefail

# Build and package Other-Agencies mod artifacts.
# Output layout:
#   Other_agencies/
#     GameData/
#       Other-Agencies/
#         Plugins/OtherAgencies.dll
#         agencies.cfg
#   Other_agencies.zip

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/OtherAgencies.csproj"
BUILD_CONFIG="Release"
BUILD_TFM="net472"
PACKAGE_NAME="Other_agencies"

ROOT_DIR="$SCRIPT_DIR/$PACKAGE_NAME"
MOD_DIR="$ROOT_DIR/GameData/Other-Agencies"
PLUGINS_DIR="$MOD_DIR/Plugins"
ZIP_FILE="$SCRIPT_DIR/${PACKAGE_NAME}.zip"
OUTPUT_DLL="$SCRIPT_DIR/src/bin/$BUILD_CONFIG/$BUILD_TFM/OtherAgencies.dll"
SOURCE_AGENCIES_CFG="$SCRIPT_DIR/agencies.cfg"
OUTPUT_AGENCIES_CFG="$MOD_DIR/agencies.cfg"

to_native_path() {
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
  else
    printf '%s' "$1"
  fi
}

create_zip_archive() {
  if command -v zip >/dev/null 2>&1; then
    (
      cd "$SCRIPT_DIR"
      zip -rq "$ZIP_FILE" "$PACKAGE_NAME"
    )
    return
  fi

  local powershell_bin=""
  if command -v pwsh >/dev/null 2>&1; then
    powershell_bin="pwsh"
  elif command -v powershell >/dev/null 2>&1; then
    powershell_bin="powershell"
  fi

  if [[ -n "$powershell_bin" ]]; then
    local native_root_dir native_zip_file
    native_root_dir="$(to_native_path "$ROOT_DIR")"
    native_zip_file="$(to_native_path "$ZIP_FILE")"
    "$powershell_bin" -NoProfile -Command "Compress-Archive -Path '$native_root_dir' -DestinationPath '$native_zip_file' -Force" >/dev/null
    return
  fi

  echo "Could not create zip archive. Install 'zip' or PowerShell and try again." >&2
  exit 1
}

if [[ ! -f "$PROJECT_FILE" ]]; then
  echo "Could not find project file: $PROJECT_FILE" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_AGENCIES_CFG" ]]; then
  echo "Could not find agencies config file: $SOURCE_AGENCIES_CFG" >&2
  exit 1
fi

echo "[1/5] Building mod DLL..."
dotnet build "$PROJECT_FILE" -c "$BUILD_CONFIG"

if [[ ! -f "$OUTPUT_DLL" ]]; then
  echo "Build finished but output DLL not found: $OUTPUT_DLL" >&2
  exit 1
fi



echo "[2/5] Resetting package output..."
rm -rf "$ROOT_DIR" "$ZIP_FILE"
mkdir -p "$PLUGINS_DIR"

echo "[3/5] Copying DLL into package..."
cp -f "$OUTPUT_DLL" "$PLUGINS_DIR/OtherAgencies.dll"

echo "[4/5] Copying agencies.cfg into package..."
cp -f "$SOURCE_AGENCIES_CFG" "$OUTPUT_AGENCIES_CFG"

echo "[5/5] Creating zip archive..."
create_zip_archive

echo "Done. Package directory created at: $MOD_DIR"
echo "Done. Zip archive created at: $ZIP_FILE"
