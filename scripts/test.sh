#!/usr/bin/env bash
# SPEC-00 A2: dotnet test over the Core + Core.Tests sources (the same .cs files Unity compiles).
set -euo pipefail
cd "$(dirname "$0")/.."
start=$SECONDS
rc=0
dotnet test core-tests -v q --nologo 2>&1 | tail -15 || rc=$?
echo "TEST_TIME=$((SECONDS - start))s"
echo "TEST_EXIT=$rc"
exit "$rc"
