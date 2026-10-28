#!/usr/bin/env bash
# scripts/capture.sh - save one play-mode frame of the project's scene camera to
# artifacts/capture/<timestamp>.png via Unity Pipeline commands, prove the frame
# is not black (mean brightness > 0.05) and that frames advanced during play.
#
# Requires a running editor on unity/ (GUI or headless resident, see
# docs/tooling/unity-mcp.md). Fallback: when `unity status` lists no editor, the
# script starts the headless resident editor itself (nohup Unity -batchmode
# -projectPath ... -logFile artifacts/capture-editor.log), waits for
# `unity status --until-ready`, captures, then shuts it down with
# `unity close unity --timeout 60` (and `kill` if the PID still lives).
# The fallback boots an editor (about 1 min); source=screen is unavailable
# headless, so capture uses source=camera.
#
# Env: CAPTURE_SCENE (default Assets/Scenes/Game.unity), CAPTURE_W/CAPTURE_H (1920/1080).
# Last line is always CAPTURE_EXIT=<code>.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/unity"
SCENE="${CAPTURE_SCENE:-Assets/Scenes/Game.unity}"
W="${CAPTURE_W:-1920}"
H="${CAPTURE_H:-1080}"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
OUT_DIR="$ROOT/artifacts/capture"
SCRATCH="$PROJECT/Assets/Captures"
ts="$(date +%Y%m%d-%H%M%S)"
rc=1
t0=$(date +%s)
pid=""
f0=""; f1=""

source ~/.unity/env 2>/dev/null || true
export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1

cmd() { unity command --project-path "$PROJECT" --timeout 120 "$@"; }
frame() { cmd script eval -- --code "return UnityEngine.Time.frameCount;" 2>&1 | grep -o '"result":[0-9]*' | tail -1 | tr -dc '0-9'; }

finish() {
  cmd editor.playmode stop >/dev/null 2>&1 || true
  if [ -n "$pid" ]; then
    unity close unity --timeout 60 >/dev/null 2>&1 || true
    if kill -0 "$pid" 2>/dev/null; then kill "$pid" 2>/dev/null || true; fi
  fi
  echo "CAPTURE_TIME=$(( $(date +%s) - t0 ))s"
  echo "CAPTURE_EXIT=$rc"
}
trap finish EXIT

run() {
  mkdir -p "$OUT_DIR" "$ROOT/artifacts"
  if ! unity status --project-path "$PROJECT" --format json 2>/dev/null | grep -q '"count": *[1-9]'; then
    echo "no editor running: starting headless resident editor (about 1 min)"
    nohup "$UNITY_BIN" -batchmode -projectPath "$PROJECT" -logFile "$ROOT/artifacts/capture-editor.log" >/dev/null 2>&1 &
    pid=$!
    unity status --until-ready --project-path "$PROJECT" --timeout 600 >/dev/null
  fi

  cmd scene open -- --path "$SCENE" >/dev/null
  cmd editor set_auto_tick >/dev/null
  cmd editor.playmode start >/dev/null 2>&1 || true

  local i ok=0
  for i in $(seq 1 20); do
    if cmd editor status 2>/dev/null | grep -q '"playMode": *"playing"'; then ok=1; break; fi
    sleep 3
  done
  [ "$ok" = 1 ] || { echo "ERROR: play mode not reached"; return 1; }

  f0="$(frame)"
  cmd capture game_view -- --source "${CAPTURE_SOURCE:-camera}" --width "$W" --height "$H" --save_path "Captures/$ts.png"
  f1="$(frame)"
  cmd editor.playmode stop >/dev/null 2>&1 || true

  if [ -f "$SCRATCH/$ts.png" ]; then
    mv "$SCRATCH/$ts.png" "$OUT_DIR/$ts.png"
  fi
  rm -f "$SCRATCH/$ts.png.meta"
  if [ -d "$SCRATCH" ] && [ -z "$(ls -A "$SCRATCH" 2>/dev/null)" ]; then
    rmdir "$SCRATCH"
    rm -f "$PROJECT/Assets/Captures.meta"
  fi
  cmd script eval -- --code "UnityEditor.AssetDatabase.Refresh(); return 0;" >/dev/null 2>&1 || true

  local png="$OUT_DIR/$ts.png"
  if [ ! -f "$png" ]; then echo "ERROR: PNG missing"; return 1; fi
  local size
  size="$(ffprobe -v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 "$png" 2>/dev/null || echo '?')"
  if ! command -v ffmpeg >/dev/null 2>&1; then
    echo "CAPTURE_FILE=$png CAPTURE_SIZE=$size"
    echo "CAPTURE_MEAN=n/a (ffmpeg missing)"
    return 1
  fi
  local m mean
  m=$(ffmpeg -v error -i "$png" -vf "format=gray,scale=1:1:flags=area" -f rawvideo -pix_fmt gray - | od -An -tu1 | tr -d ' ')
  mean=$(awk "BEGIN{printf \"%.3f\", $m/255}")
  echo "CAPTURE_FILE=$png CAPTURE_SIZE=$size CAPTURE_MEAN=$mean CAPTURE_FRAMES=$f0->$f1"
  if [ -z "$f0" ] || [ -z "$f1" ] || [ "$f1" -le "$f0" ]; then
    echo "WARNING: frame counter did not advance (frozen play mode?)"
  fi
  awk "BEGIN{exit !($mean > 0.05)}"
}

if run; then rc=0; else rc=1; fi
