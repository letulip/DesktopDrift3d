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
  unity recompile --project-path "$PROJECT" --json 2>&1 | tee artifacts/compile-recompile.json | tail -20 || rc=$?
  echo "COMPILE_WARNINGS=$(grep -c 'warning' artifacts/compile-recompile.json || true)"
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
