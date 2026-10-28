#!/usr/bin/env bash
# SPEC-01 step 7b: run the REAL 2D race engine (DesktopDrift/js/game-engine.js) headlessly with a
# scripted autopilot -> tools/engine-trace-<run>.json for every run (see tools/engine-harness.mjs).
set -euo pipefail
cd "$(dirname "$0")/.."
RUNS=(oval green-study green-study-rev green-study-zen-cap oval-sandbox)
rc=0
for t in "${RUNS[@]}"; do
  if [ "$rc" -eq 0 ]; then (cd tools && node engine-harness.mjs "$t") || rc=$?; fi
done
if [ "$rc" -eq 0 ]; then for t in "${RUNS[@]}"; do shasum -a 256 "tools/engine-trace-$t.json"; done; fi
echo "ENGINE_TRACE_EXIT=$rc"
exit "$rc"
