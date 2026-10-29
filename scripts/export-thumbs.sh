#!/usr/bin/env bash
# SPEC-03 step 6: exports the canvas command list of the 2D track thumbnail (drawThumb) to tools/thumb-ref/<id>.json (tools/export-thumbs.mjs).
# Prints the sha256 of every produced file; last line EXPORT_THUMBS_EXIT=<code>.
set -euo pipefail
cd "$(dirname "$0")/.."
rc=0
node tools/export-thumbs.mjs || rc=$?
if [ "$rc" -eq 0 ]; then
  (cd tools/thumb-ref && shasum -a 256 *.json) || rc=$?
fi
echo "EXPORT_THUMBS_EXIT=$rc"
exit "$rc"
