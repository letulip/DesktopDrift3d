#!/usr/bin/env bash
# SPEC-00 B2: compile gate. Live editor (unity status sees the project) -> unity recompile;
# otherwise a headless unity run with the editor log as the error channel.
set -euo pipefail
cd "$(dirname "$0")/.."
source ~/.unity/env
export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1
PROJECT="$PWD/unity"
mkdir -p artifacts
start=$SECONDS
rc=0
# no editor open: status prints {"success": false, ... "count": 0, "instances": []}
if unity status --project-path "$PROJECT" --format json 2>/dev/null | grep -q '"count": [1-9]'; then
  echo "compile: live editor -> unity recompile"
  # Right after `unity open` the editor reports ready while its first script compilation is still running;
  # `unity recompile` then exits non-zero without any CS error (seen 2026-10-28) -> retry up to 6 x 5 s.
  for attempt in 1 2 3 4 5 6; do
    rc=0
    unity recompile --project-path "$PROJECT" --json > artifacts/compile-recompile.json 2>&1 || rc=$?
    if [ "$rc" -eq 0 ] || grep -q '"errors": *[1-9]\|error CS' artifacts/compile-recompile.json; then break; fi
    echo "compile: recompile not settled (exit $rc, attempt $attempt) -> retry in 5 s"; sleep 5
  done
  tail -20 artifacts/compile-recompile.json
  W=$(grep -o '"warnings": *[0-9]*' artifacts/compile-recompile.json | head -1 | tr -dc '0-9'); echo "COMPILE_WARNINGS=${W:-0}"
else
  echo "compile: headless -> unity run (log: artifacts/compile.log)"
  unity run unity --timeout 600 --no-tail -l artifacts/compile.log >/dev/null 2>&1 || rc=$?
  grep -n "error CS\|Exception" artifacts/compile.log | head -40 || true
  errors=$(grep -c "error CS" artifacts/compile.log || true)
  echo "COMPILE_ERRORS=${errors:-0}"
  echo "COMPILE_WARNINGS=$(grep -c "warning CS" artifacts/compile.log || true)"
  if [ "${errors:-0}" -ne 0 ]; then rc=1; fi
fi
echo "COMPILE_TIME=$((SECONDS - start))s"
echo "COMPILE_EXIT=$rc"
exit "$rc"
