#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESOURCE_DIR="$ROOT_DIR/DTSoft.AppService/Resources"
DEFAULT_RESX="$RESOURCE_DIR/DTResource.resx"
ZH_RESX="$RESOURCE_DIR/DTResource.zh-CN.resx"
EN_RESX="$RESOURCE_DIR/DTResource.en-US.resx"
TMP_DIR="${TMPDIR:-/tmp}/dtsoft-localization-check"

mkdir -p "$TMP_DIR"

extract_resx_keys() {
  perl -nE 'say $1 if /<data name="([^"]+)"/' "$1" | sort -u
}

extract_code_keys() {
  find "$ROOT_DIR/DTSoft.Core" "$ROOT_DIR/DTSoft.AppService" "$ROOT_DIR/DTSoftServerApp" "$ROOT_DIR/DTSoft.Models" \
    -path '*/bin' -prune -o \
    -path '*/obj' -prune -o \
    -name '*.cs' -print |
    xargs perl -nE '
      while(/ErrorMessage\s*=\s*"([A-Za-z][A-Za-z0-9_.-]+)"/g){ say $1 }
      while(/(?:localizer|_localizer|startupLocalizer)\s*\[\s*"([A-Za-z][A-Za-z0-9_.-]+)"\s*\]/g){ say $1 }
      while(/\bL\(\s*"([A-Za-z][A-Za-z0-9_.-]+)"/g){ say $1 }
      while(/\b(?:Text|Format)\(\s*(?:localizer|_localizer|startupLocalizer)\s*,\s*"([A-Za-z][A-Za-z0-9_.-]+)"/g){ say $1 }
      while(/DbProviderMessages\.Text\(\s*(?:localizer|_localizer|startupLocalizer)\s*,\s*"([A-Za-z][A-Za-z0-9_.-]+)"/g){ say $1 }
    ' | sort -u
}

extract_resx_keys "$DEFAULT_RESX" > "$TMP_DIR/default.keys"
extract_resx_keys "$ZH_RESX" > "$TMP_DIR/zh.keys"
extract_resx_keys "$EN_RESX" > "$TMP_DIR/en.keys"
extract_code_keys > "$TMP_DIR/code.keys"

FAILED=0

check_empty_diff() {
  local name="$1"
  local left="$2"
  local right="$3"
  local output="$TMP_DIR/$name.diff"

  comm -3 "$left" "$right" > "$output"
  if [[ -s "$output" ]]; then
    echo "Localization key mismatch: $name"
    cat "$output"
    FAILED=1
  fi
}

check_empty_diff "default-vs-zh" "$TMP_DIR/default.keys" "$TMP_DIR/zh.keys"
check_empty_diff "default-vs-en" "$TMP_DIR/default.keys" "$TMP_DIR/en.keys"

comm -23 "$TMP_DIR/code.keys" "$TMP_DIR/default.keys" > "$TMP_DIR/missing.keys"
if [[ -s "$TMP_DIR/missing.keys" ]]; then
  echo "Resource keys used by code but missing from DTResource.resx:"
  cat "$TMP_DIR/missing.keys"
  FAILED=1
fi

if [[ "$FAILED" -ne 0 ]]; then
  exit 1
fi

echo "Localization key check passed."
