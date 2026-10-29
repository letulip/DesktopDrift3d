#!/usr/bin/env bash
# SPEC-03 step 5: exports the 2D game's item-clearance verdicts to tools/clearance-ref/<id>.json (tools/export-clearance.mjs).
# Prints the sha256 of every produced file; last line EXPORT_CLEARANCE_EXIT=<code>.
set -euo pipefail
cd "$(dirname "$0")/.."
rc=0
node tools/export-clearance.mjs || rc=$?
if [ "$rc" -eq 0 ]; then
  (cd tools/clearance-ref && shasum -a 256 *.json) || rc=$?
fi
echo "EXPORT_CLEARANCE_EXIT=$rc"
exit "$rc"
