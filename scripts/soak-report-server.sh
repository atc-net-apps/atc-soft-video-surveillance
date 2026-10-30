#!/usr/bin/env bash
# Server-edition soak-test report generator. Reads a video-surveillance-api
# log file and prints a concise summary suitable for handing back into the
# chat conversation investigating the run.
#
# Usage: scripts/soak-report-server.sh <log-file> [recordings-root]
#
#   <log-file>         Required. Path to video-surveillance-api-{date}.log.
#   [recordings-root]  Optional. If present, the script also reports
#                      per-camera segment counts and total bytes.
#
# Differences from soak-report.sh (WPF edition):
#   - Disruption grep targets server-side patterns (reaper, backoff,
#     pipeline failures, disk thresholds) instead of WPF's tile/RDP events.
#   - Adds a heartbeat trend section showing the first and last beats so
#     the operator can spot drift in working set, handle count, GC counts,
#     and free disk space across the run.
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
echo "Server soak report: $LOG"
echo "=========================================="
echo

echo "--- Run window ---"
first=$(head -n 1 "$LOG" | awk '{print $1, $2}')
last=$(tail -n 1 "$LOG" | awk '{print $1, $2}')
echo "First entry : $first"
echo "Last entry  : $last"
echo

echo "--- Cameras with managed recordings ---"
# Server logs the friendly name on Recording started / Camera connected.
grep -E "Recording started for camera '[^']+'" "$LOG" \
    | sed -E "s/.*Recording started for camera '([^']+)'.*/\1/" \
    | sort -u
echo

echo "--- Disruption events ---"
events=$(grep -nE "Reaping inactive recording session|Reaper swept|Dead recording pipeline detected|Pipeline connection failed|Disk space (LOW|CRITICAL)|Demux loop failed|End of stream reached|Exceeded .* consecutive read errors|backing off|reconnected after" "$LOG" || true)
if [ -z "$events" ]; then
    echo "(none)"
else
    echo "$events" | head -50
fi
echo

echo "--- Recording timeline (per camera) ---"
grep -E "Recording (started|switched|stopped|segmented)" "$LOG" \
    | sed -E 's/^([0-9-]+ [0-9:.]+) .*: (Recording (started|switched|stopped|segmented)[^)]*)/\1 \2/' \
    | head -100
echo

echo "--- Counts ---"
starts=$(grep -c "Recording started for camera '" "$LOG" || true)
switches=$(grep -c "Recording switched atomically to:" "$LOG" || true)
segments=$(grep -c "Recording segmented for camera '" "$LOG" || true)
stops=$(grep -c "Recording stopped for camera '" "$LOG" || true)
reaped=$(grep -c "Reaping inactive recording session" "$LOG" || true)
backoffs=$(grep -c "backing off" "$LOG" || true)
reconnects=$(grep -c "reconnected after " "$LOG" || true)
heartbeats=$(grep -c "Heartbeat: uptime=" "$LOG" || true)
errs=$(grep -cE "^[^[]*\[ERR\]" "$LOG" || true)
wrns=$(grep -cE "^[^[]*\[WRN\]" "$LOG" || true)
printf "Recording started        : %s\n" "$starts"
printf "Atomic segment switches  : %s\n" "$switches"
printf "Server-side segments     : %s\n" "$segments"
printf "Recording stopped        : %s\n" "$stops"
printf "Reaper reaped sessions   : %s\n" "$reaped"
printf "Backoffs scheduled       : %s\n" "$backoffs"
printf "Successful reconnects    : %s\n" "$reconnects"
printf "Heartbeats emitted       : %s\n" "$heartbeats"
printf "Errors  [ERR]            : %s\n" "$errs"
printf "Warnings [WRN]           : %s\n" "$wrns"
echo

echo "--- Heartbeat trend (first / last) ---"
first_hb=$(grep "Heartbeat: uptime=" "$LOG" | head -1)
last_hb=$(grep "Heartbeat: uptime=" "$LOG" | tail -1)
if [ -z "$first_hb" ]; then
    echo "(no heartbeats — server-mode logging may be disabled)"
else
    echo "first: $first_hb"
    echo " last: $last_hb"
fi
echo

echo "--- Top warning/error message templates ---"
grep -E "\[(ERR|WRN)\]" "$LOG" \
    | sed -E 's/^[0-9-]+ [0-9:.]+ \[(ERR|WRN)\] [^:]+: //' \
    | sed -E "s/'[^']*'/'X'/g; s/[0-9a-f]{8}-[0-9a-f-]{27}/{guid}/g; s/[0-9]+ms/Nms/g; s/[0-9]+s/Ns/g" \
    | sort | uniq -c | sort -rn | head -15
echo

echo "--- Termination trailer ---"
trailer_line=$(grep -nE "Application is shutting down|Video Surveillance API terminated|Heartbeat stopped" "$LOG" | tail -1 | cut -d: -f1)
if [ -n "$trailer_line" ]; then
    sed -n "${trailer_line},\$p" "$LOG" | head -25
else
    echo "(no shutdown marker — process did not exit gracefully or log was truncated)"
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