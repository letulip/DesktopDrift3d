#!/usr/bin/env bash
# SPEC-03 step 7: finish summary of the REAL 2D engine (autopilot, Time Attack, fresh save) for all 12 track
# instances (6 tracks x forward/reversed) -> tools/engine-finish-ref.json, the reference of
# AllInstancesFinishSceneTests. One node process per instance (store.js caches the save per process).
set -euo pipefail
cd "$(dirname "$0")/.."
DD="${DESKTOPDRIFT_DIR:-/Users/letulip/Projects/Claude/DesktopDrift}"
IDS=(green-study steel-kitchen workbench cafe-marble dev-desk dining-oak)
tmp="$(mktemp)"
rc=0
for id in "${IDS[@]}"; do
  for suffix in "" "-rev"; do
    if [ "$rc" -eq 0 ]; then (cd tools && node engine-harness.mjs "ta-$id$suffix" --summary) >> "$tmp" || rc=$?; fi
  done
done
if [ "$rc" -eq 0 ]; then
  commit="$(git -C "$DD" rev-parse HEAD)"
  node -e '
    const fs = require("fs");
    const [tmp, commit, out] = process.argv.slice(1);
    const rows = fs.readFileSync(tmp, "utf8").trim().split("\n");
    const text = "{\n\"_source\": " + JSON.stringify({ commit, seed: 1, rng: "LCG 1664525/1013904223 mod 2^32" }) +
      ",\n\"instances\": [\n" + rows.join(",\n") + "\n]\n}\n";
    fs.writeFileSync(out, text);
  ' "$tmp" "$commit" tools/engine-finish-ref.json
  shasum -a 256 tools/engine-finish-ref.json
fi
rm -f "$tmp"
echo "ENGINE_FINISH_EXIT=$rc"
exit "$rc"
