#!/usr/bin/env bash
# SPEC-03 step 1b: regenerate Assets/Content/Tracks/*.track.json + *.asset from the 2D game's SVGs (DESKTOPDRIFT_DIR,
# default /Users/letulip/Projects/Claude/DesktopDrift) through the headless editor importer. Idempotent.
# Refuses to run while an editor has the project open (a headless run would fight for the project lock).
set -euo pipefail
cd "$(dirname "$0")/.."
source ~/.unity/env
export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1
PROJECT="$PWD/unity"
mkdir -p artifacts
if unity status --project-path "$PROJECT" --format json 2>/dev/null | grep -q '"count": [1-9]'; then
  echo "import-tracks: an editor is open on the project - close it first (unity close) and re-run" >&2
  echo "IMPORT_TRACKS_EXIT=2"
  exit 2
fi
rc=0
# `unity run` adds -batchmode -quit itself and rejects them after `--`
unity run unity --timeout 600 --no-tail -l artifacts/import-tracks.log -- -executeMethod DesktopDrift.Editor.TrackImporter.ImportAll >/dev/null 2>&1 || rc=$?
grep -n "^TRACK \|error CS\|Exception" artifacts/import-tracks.log | head -40 || true
echo "IMPORT_TRACKS_EXIT=$rc"
exit "$rc"
