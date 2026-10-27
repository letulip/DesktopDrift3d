#!/usr/bin/env bash
# SPEC-00 B4: player build through `unity build` with a size/time report in artifacts/build/report.md.
# Usage: scripts/build.sh android|mac
#   android – Build Profile "Android" (Assets/Settings/Build Profiles/Android.asset, created once with
#             `unity build unity --create-profile Android`), unsigned APK for device checks; AAB signing is SPEC-09.
#   mac     – DEBT: temporary stand-in on StandaloneOSX for desk checks only, never a release target.
set -euo pipefail
cd "$(dirname "$0")/.."
source ~/.unity/env
export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1
target="${1:-android}"
mkdir -p artifacts/build
start=$SECONDS
rc=0
case "$target" in
  android)
    out="$PWD/artifacts/build/android/DesktopDrift.apk"
    log="$PWD/artifacts/build-android.log"
    unity build unity --profile Android --output-path "$out" --android-export-type apk --allow-dirty-build \
      --no-tail --timeout 2400 -l "$log" --format json > artifacts/build/android-result.json 2> artifacts/build/android-progress.log || rc=$?
    ;;
  mac)
    out="$PWD/artifacts/build/mac/DesktopDrift.app"
    log="$PWD/artifacts/build-mac.log"
    echo "DEBT: mac build is a temporary stand-in (D-01 native-first Android), not a release target"
    unity build unity --target StandaloneOSX --output-path "$out" --allow-dirty-build \
      --no-tail --timeout 2400 -l "$log" --format json > artifacts/build/mac-result.json 2> artifacts/build/mac-progress.log || rc=$?
    ;;
  *) echo "usage: scripts/build.sh android|mac"; echo "BUILD_EXIT=2"; exit 2 ;;
esac
elapsed=$((SECONDS - start))
size="n/a"; if [ -e "$out" ]; then size=$(du -sh "$out" | cut -f1); fi
{
  echo "# Build report – $target ($(date -u +%Y-%m-%dT%H:%M:%SZ))"
  echo
  echo "- output: $out"
  echo "- size: $size"
  echo "- time: ${elapsed}s"
  echo "- exit: $rc"
  echo "- unity: $(sed -n 's/^m_EditorVersion: //p' unity/ProjectSettings/ProjectVersion.txt)"
  echo "- cli: $(unity version 2>/dev/null | awk '$1=="version"{print $2}')"
  echo "- stripping: managedStrippingLevel$(grep '^  managedStrippingLevel:' unity/ProjectSettings/ProjectSettings.asset | sed 's/^  managedStrippingLevel://') (per-platform map; {} = unset, Unity default), stripEngineCode$(grep '^  stripEngineCode:' unity/ProjectSettings/ProjectSettings.asset | sed 's/^  stripEngineCode://')"
  echo "- scripting backend Android: $(grep -A3 '^  scriptingBackend:' unity/ProjectSettings/ProjectSettings.asset | grep Android | tr -s ' ')"
  echo "- log: $log"
} > artifacts/build/report.md
cat artifacts/build/report.md
grep -n "error CS\|BuildFailedException\|Build completed with a result of\|licen[cs]e" "$log" 2>/dev/null | head -12 || true
echo "BUILD_SIZE=$size BUILD_TIME=${elapsed}s"
echo "BUILD_EXIT=$rc"
exit "$rc"
