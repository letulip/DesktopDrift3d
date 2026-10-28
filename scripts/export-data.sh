#!/usr/bin/env bash
# SPEC-01 step 1: export the 2D game's data tables to unity/Assets/Content/_json/*.json (tools/export-data.mjs).
# Prints the sha256 of every produced file; last line EXPORT_EXIT=<code>.
set -euo pipefail
cd "$(dirname "$0")/.."
rc=0
node tools/export-data.mjs || rc=$?
if [ "$rc" -eq 0 ]; then
  (cd unity/Assets/Content/_json && shasum -a 256 *.json) || rc=$?
fi
echo "EXPORT_EXIT=$rc"
exit "$rc"
