#!/usr/bin/env bash
# SPEC-01 step 7b: run the REAL 2D race engine (DesktopDrift/js/game-engine.js) headlessly with a
# scripted autopilot -> tools/engine-trace-{oval,green-study}.json (see tools/engine-harness.mjs).
set -euo pipefail
cd "$(dirname "$0")/.."
rc=0
for t in oval green-study; do
  if [ "$rc" -eq 0 ]; then (cd tools && node engine-harness.mjs "$t") || rc=$?; fi
done
if [ "$rc" -eq 0 ]; then shasum -a 256 tools/engine-trace-oval.json tools/engine-trace-green-study.json; fi
echo "ENGINE_TRACE_EXIT=$rc"
exit "$rc"
