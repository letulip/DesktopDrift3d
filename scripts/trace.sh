#!/usr/bin/env bash
# SPEC-00 A4: regenerate the JS parity trace (tools/parity-trace.json) from the live 2D game.
set -euo pipefail
cd "$(dirname "$0")/.."
rc=0
node tools/parity-trace.mjs || rc=$?
if [ "$rc" -eq 0 ]; then shasum -a 256 tools/parity-trace.json; fi
echo "TRACE_EXIT=$rc"
exit "$rc"
