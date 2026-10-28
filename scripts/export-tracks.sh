#!/usr/bin/env bash
# SPEC-01 step 4b: exports the 2D game's track geometry to tools/tracks-ref/<id>[.rev].json (tools/export-tracks.mjs).
# Prints the sha256 of every produced file; last line EXPORT_TRACKS_EXIT=<code>.
set -euo pipefail
cd "$(dirname "$0")/.."
rc=0
node tools/export-tracks.mjs || rc=$?
if [ "$rc" -eq 0 ]; then
  (cd tools/tracks-ref && shasum -a 256 *.json) || rc=$?
fi
echo "EXPORT_TRACKS_EXIT=$rc"
exit "$rc"
