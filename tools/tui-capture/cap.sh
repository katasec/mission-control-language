#!/bin/bash
# cap.sh <name> "<delays>" <command> [args...]
# Opens a new Ghostty instance running <command>, captures its window at each delay (seconds after
# launch) to $OUT/<name>_<delay>.png, then quits only the Ghostty process it launched (by PID).
# Env: OUT (default ./out), COLS/ROWS (window size in cells), POS_X/POS_Y (points; Ghostty may ignore).
set -uo pipefail
here=$(cd "$(dirname "$0")" && pwd)
name=$1; delays=$2; shift 2
out=${OUT:-./out}; mkdir -p "$out"
before=$(pgrep -x ghostty | sort || true)
open -na Ghostty --args --window-save-state=never --confirm-close-surface=false \
  --window-position-x=${POS_X:-100} --window-position-y=${POS_Y:-80} \
  --window-width=${COLS:-110} --window-height=${ROWS:-40} --title=tui-capture \
  "--initial-command=direct:$*"
pid=""
for _ in $(seq 1 50); do
  pid=$(comm -13 <(echo "$before") <(pgrep -x ghostty | sort) | head -1)
  [ -n "$pid" ] && break; sleep 0.1
done
[ -n "$pid" ] || { echo "no new ghostty process"; exit 1; }
echo "ghostty pid=$pid"
t=0
for d in $delays; do
  sleep "$(echo "$d - $t" | bc)"; t=$d
  win=$(swift "$here/win.swift" "$pid" | head -1)
  id=${win%% *}
  echo "t=$d window: $win"
  [ -n "$id" ] && screencapture -x -o -l "$id" "$out/${name}_$d.png" || echo "t=$d: no on-screen window (see winall.swift)"
done
kill "$pid" 2>/dev/null || true
