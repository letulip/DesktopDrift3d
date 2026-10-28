#!/usr/bin/env bash
# SPEC-00 B3: EditMode (default) or PlayMode tests through `unity test`; NUnit + JUnit reports land in artifacts/.
# Usage: [UNITY_TESTS_MODE=EditMode|PlayMode] scripts/unity-tests.sh [<filter>]   e.g. scripts/unity-tests.sh DesktopDrift.Core.Tests.PhysicsTests
set -euo pipefail
cd "$(dirname "$0")/.."
source ~/.unity/env
export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1
mkdir -p artifacts
MODE="${UNITY_TESTS_MODE:-EditMode}"
case "$MODE" in
  EditMode) report=editmode ;;
  PlayMode) report=playmode ;;
  *) echo "UNITY_TESTS_MODE must be EditMode or PlayMode, got: $MODE" >&2; exit 2 ;;
esac
NUNIT="$PWD/artifacts/$report.xml"
JUNIT="$PWD/artifacts/$report-junit.xml"
rm -f "$NUNIT" "$JUNIT"
filter=()
if [ -n "${1:-}" ]; then filter=(--filter "$1"); fi
start=$SECONDS
rc=0
unity test unity --mode "$MODE" --output "$NUNIT" --report-format nunit,junit --junit-output "$JUNIT" \
  --timeout 900 --json ${filter[@]+"${filter[@]}"} > artifacts/unity-tests.json 2>&1 || rc=$?
tail -c 1500 artifacts/unity-tests.json; echo
total=0; failed=0
if [ -f "$NUNIT" ]; then
  total=$(grep -o 'total="[0-9]*"' "$NUNIT" | head -1 | tr -dc '0-9')
  failed=$(grep -o 'failed="[0-9]*"' "$NUNIT" | head -1 | tr -dc '0-9')
fi
echo "UNITY_TESTS_TOTAL=${total:-0} UNITY_TESTS_FAILED=${failed:-0}"
if [ "${total:-0}" -eq 0 ] || [ "${failed:-0}" -ne 0 ]; then rc=1; fi
echo "UNITY_TESTS_TIME=$((SECONDS - start))s"
echo "UNITY_TESTS_EXIT=$rc"
exit "$rc"
