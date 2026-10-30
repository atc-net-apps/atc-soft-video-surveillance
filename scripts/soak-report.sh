#!/usr/bin/env bash
# Soak-test report generator. Reads a camera-wall log file and prints a
# concise summary suitable for handing back into the chat conversation
# investigating the run.
#
# Usage: scripts/soak-report.sh <log-file> [recordings-root]
#
#   <log-file>         Required. Path to camera-wall-{date}.log.
#   [recordings-root]  Optional. If present, the script also reports
#                      per-camera segment counts and total bytes.
#
# Designed to be best-effort: any section that can't be computed prints a
# short note and the rest of the report still runs.

set -uo pipefail

LOG="${1:-}"
RECORDINGS="${2:-}"

if [ -z "$LOG" ] || [ ! -f "$LOG" ]; then
    echo "Usage: $0 <log-file> [recordings-root]" >&2
    exit 1
fi

echo "=========================================="
echo "Soak report: $LOG"
echo "=========================================="
echo

echo "--- Run window ---"
first=$(head -n 1 "$LOG" | awk '{print $1, $2}')
last=$(tail -n 1 "$LOG" | awk '{print $1, $2}')
echo "First entry : $first"
echo "Last entry  : $last"
echo

echo "--- Cameras configured ---"
grep -E "Camera: '[^']+' - RecordOnConnect:" "$LOG" | head -20
echo

echo "--- Disruption events ---"
events=$(grep -nE "Session switch detected|Replacing zombie tile|Tile unloaded|Auto-reconnect attempting|Stream stale detected|Connection timeout|Exceeded consecutive read errors|End of stream|Demux loop failed|Reconnecting .* on tile reload" "$LOG" || true)
if [ -z "$events" ]; then
    echo "(none)"
else
    echo "$events" | head -50
fi
echo

echo "--- Recording timeline (per camera) ---"
grep -E "Recording (started|switched|stopped)" "$LOG" \
    | sed -E 's/^([0-9-]+ [0-9:.]+) .*: (Recording (started|switched|stopped)[^)]*)/\1 \2/' \
    | head -100
echo

echo "--- Counts ---"
starts=$(grep -c "Recording started:" "$LOG" || true)
switches=$(grep -c "Recording switched atomically to:" "$LOG" || true)
stops=$(grep -c "Recording stopped$" "$LOG" || true)
zombies=$(grep -c "Replacing zombie tile" "$LOG" || true)
unloads=$(grep -c "Tile unloaded:" "$LOG" || true)
sessions=$(grep -cE "Session switch detected" "$LOG" || true)
errs=$(grep -cE "^[^[]*\[ERR\]" "$LOG" || true)
wrns=$(grep -cE "^[^[]*\[WRN\]" "$LOG" || true)
printf "Recording started        : %s\n" "$starts"
printf "Atomic segment switches  : %s\n" "$switches"
printf "Recording stopped (raw)  : %s\n" "$stops"
printf "Tile unloaded events     : %s\n" "$unloads"
printf "Zombie tiles replaced    : %s\n" "$zombies"
printf "Session switch events    : %s\n" "$sessions"
printf "Errors  [ERR]            : %s\n" "$errs"
printf "Warnings [WRN]           : %s\n" "$wrns"
echo

echo "--- Top warning/error message templates ---"
# Strip leading timestamp/level/category to bucket by message template.
grep -E "\[(ERR|WRN)\]" "$LOG" \
    | sed -E 's/^[0-9-]+ [0-9:.]+ \[(ERR|WRN)\] [^:]+: //' \
    | sed -E "s/'[^']*'/'X'/g; s/[0-9a-f]{8}-[0-9a-f-]{27}/{guid}/g; s/[0-9]+ms/Nms/g; s/[0-9]+s/Ns/g" \
    | sort | uniq -c | sort -rn | head -15
echo

echo "--- App close trailer ---"
close_line=$(grep -n "App closing" "$LOG" | tail -1 | cut -d: -f1)
if [ -n "$close_line" ]; then
    sed -n "${close_line},\$p" "$LOG" | head -25
else
    echo "(no 'App closing' entry — process did not exit gracefully or log was truncated)"
fi
echo

if [ -n "$RECORDINGS" ] && [ -d "$RECORDINGS" ]; then
    echo "--- Per-camera segments on disk (under $RECORDINGS) ---"
    for cam_dir in "$RECORDINGS"/*/; do
        [ -d "$cam_dir" ] || continue
        cam=$(basename "$cam_dir")
        # The separate thumbnail folder is not a camera, and videos sit in day folders below the camera.
        [ "$cam" = "thumbnails" ] && continue
        count=$(find "$cam_dir" -maxdepth 2 -type f \( -iname "*.mkv" -o -iname "*.mp4" \) | wc -l)
        bytes=$(find "$cam_dir" -maxdepth 2 -type f \( -iname "*.mkv" -o -iname "*.mp4" \) -printf "%s\n" 2>/dev/null | awk '{s+=$1} END {print s+0}')
        # Convert bytes to GB for readability when large.
        if [ "$bytes" -gt 1073741824 ]; then
            size_str=$(awk -v b="$bytes" 'BEGIN { printf "%.2f GB", b/1073741824 }')
        elif [ "$bytes" -gt 1048576 ]; then
            size_str=$(awk -v b="$bytes" 'BEGIN { printf "%.1f MB", b/1048576 }')
        else
            size_str="${bytes} B"
        fi
        printf "%-30s segments=%-4s total=%s\n" "$cam" "$count" "$size_str"
    done
fi

echo
echo "=========================================="
echo "End of report"
echo "=========================================="
