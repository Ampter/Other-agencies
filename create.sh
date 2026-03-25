#!/usr/bin/env bash
set -euo pipefail

# Build and package Other-Agencies mod artifacts.
# Output layout:
#   Other_agencies/
#     GameData/Other-Agencies/
#       Plugins/OtherAgencies.dll
#       agencies.cfg
#     Ships/VAB/*.craft
#     Ships/SPH/*.craft
#     README.md
#     CONFIG.md
#   Other_agencies.zip

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/OtherAgencies.csproj"
BUILD_CONFIG="Release"
BUILD_TFM="net472"
PACKAGE_NAME="Other_agencies"

ROOT_DIR="$SCRIPT_DIR/$PACKAGE_NAME"
MOD_DIR="$ROOT_DIR/GameData/Other-Agencies"
PLUGINS_DIR="$MOD_DIR/Plugins"
SHIPS_DIR="$ROOT_DIR/Ships"
OUTPUT_VAB_DIR="$SHIPS_DIR/VAB"
OUTPUT_SPH_DIR="$SHIPS_DIR/SPH"
ZIP_FILE="$SCRIPT_DIR/${PACKAGE_NAME}.zip"
OUTPUT_DLL="$SCRIPT_DIR/src/bin/$BUILD_CONFIG/$BUILD_TFM/OtherAgencies.dll"
SOURCE_AGENCIES_CFG="$SCRIPT_DIR/agencies.cfg"
SOURCE_README="$SCRIPT_DIR/README.md"
SOURCE_CONFIG_DOC="$SCRIPT_DIR/CONFIG.md"
SOURCE_CRAFTS_DIR="$SCRIPT_DIR/crafts"
OUTPUT_AGENCIES_CFG="$MOD_DIR/agencies.cfg"
OUTPUT_README="$ROOT_DIR/README.md"
OUTPUT_CONFIG_DOC="$ROOT_DIR/CONFIG.md"

EXPECTED_VAB_CRAFTS=(
  "OA_KerbalX_Sounding.craft"
  "OA_KerbalX_Suborbital.craft"
  "OA_KerbalX_Orbiter.craft"
)

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

copy_crafts() {
  local source_dir="$1"
  local output_dir="$2"

  if [[ ! -d "$source_dir" ]]; then
    return
  fi

  shopt -s nullglob
  local craft_files=("$source_dir"/*.craft)
  shopt -u nullglob
  if [[ ${#craft_files[@]} -eq 0 ]]; then
    return
  fi

  mkdir -p "$output_dir"
  cp -f "${craft_files[@]}" "$output_dir/"
}

warn_missing_expected_crafts() {
  local craft_name=""
  for craft_name in "${EXPECTED_VAB_CRAFTS[@]}"; do
    if [[ ! -f "$SOURCE_CRAFTS_DIR/VAB/$craft_name" ]]; then
      echo "Warning: expected craft missing: crafts/VAB/$craft_name" >&2
    fi
  done
}

if [[ ! -f "$PROJECT_FILE" ]]; then
  echo "Could not find project file: $PROJECT_FILE" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_AGENCIES_CFG" ]]; then
  echo "Could not find agencies config file: $SOURCE_AGENCIES_CFG" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_README" || ! -f "$SOURCE_CONFIG_DOC" ]]; then
  echo "README.md and CONFIG.md must both exist before packaging." >&2
  exit 1
fi

echo "[1/7] Building mod DLL..."
dotnet build "$PROJECT_FILE" -c "$BUILD_CONFIG"

if [[ ! -f "$OUTPUT_DLL" ]]; then
  echo "Build finished but output DLL not found: $OUTPUT_DLL" >&2
  exit 1
fi

echo "[2/7] Resetting package output..."
rm -rf "$ROOT_DIR" "$ZIP_FILE"
mkdir -p "$PLUGINS_DIR"

echo "[3/7] Copying DLL into package..."
cp -f "$OUTPUT_DLL" "$PLUGINS_DIR/OtherAgencies.dll"

echo "[4/7] Copying config and docs..."
cp -f "$SOURCE_AGENCIES_CFG" "$OUTPUT_AGENCIES_CFG"
cp -f "$SOURCE_README" "$OUTPUT_README"
cp -f "$SOURCE_CONFIG_DOC" "$OUTPUT_CONFIG_DOC"

echo "[5/7] Copying craft templates..."
copy_crafts "$SOURCE_CRAFTS_DIR/VAB" "$OUTPUT_VAB_DIR"
copy_crafts "$SOURCE_CRAFTS_DIR/SPH" "$OUTPUT_SPH_DIR"
warn_missing_expected_crafts

echo "[6/7] Creating zip archive..."
create_zip_archive

echo "[7/7] Package ready."
echo "Mod directory: $MOD_DIR"
echo "Craft directory: $SHIPS_DIR"
echo "Zip archive: $ZIP_FILE"
