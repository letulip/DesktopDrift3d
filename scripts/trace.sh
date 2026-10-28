#!/usr/bin/env bash
# SPEC-00 A4 / SPEC-01 step 7a: regenerate the JS parity traces from the live 2D game:
# tools/parity-trace.json (physics) + tools/parity-{wall,props,cones}.json (collision scenarios).
set -euo pipefail
cd "$(dirname "$0")/.."
rc=0
node tools/parity-trace.mjs || rc=$?
for s in wall props cones; do
  if [ "$rc" -eq 0 ]; then node tools/parity-trace.mjs --scenario "$s" || rc=$?; fi
done
if [ "$rc" -eq 0 ]; then shasum -a 256 tools/parity-trace.json tools/parity-wall.json tools/parity-props.json tools/parity-cones.json; fi
echo "TRACE_EXIT=$rc"
exit "$rc"
