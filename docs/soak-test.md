# 🧪 Soak Test — 24/7 Stability

How to run the long soak of the CameraWall app (the server has its own report script, `scripts/soak-report-server.sh`). A 77-minute run on 2026-04-29 was clean across one RDP cycle, but a single event isn't enough to declare 24/7 stability. This document defines what to run, for how long, and what counts as a pass. It has never been run in full; it is listed in the [roadmap](../plans/roadmap.md).

## Pre-conditions

- [ ] The build under test deployed to the test machine (`DKPC-MTS1` or equivalent)
- [ ] At least 3 cameras configured with `RecordOnConnect=true`
- [ ] `EnableHourlySegmentation=true` (15-minute segments)
- [ ] `EnableDebugLogging=true` so the log file survives across the run
- [ ] Disk has headroom for the retention window (rough budget: 4 cameras × ~1 Mbps × 24 h ≈ 40 GB/day)
- [ ] Recording path on a drive with enough free space; cleanup retention sane (default 30 days)

## Pick the duration

| Length     | Catches                                                   | Cost          |
|------------|-----------------------------------------------------------|---------------|
| Overnight (~12 h) | Cumulative segmentation drift, basic memory growth | Low           |
| 24 h       | Diurnal patterns (camera reboots, IT scans), one of each disruption | Medium |
| Weekend (~60 h) | Slow-burn handle/memory leaks, multi-day file rotation | High          |

**Recommended starting point: 24 h** — enough to catch most issues, low enough
that one bad run doesn't burn a weekend.

## Disruption matrix

The point of the soak is to expose paths that the 77-minute run didn't
exercise. Trigger each scenario at a known time so the log is interpretable.

| # | Scenario              | Trigger                                                    | Expected outcome                                                                     |
|---|-----------------------|------------------------------------------------------------|--------------------------------------------------------------------------------------|
| 1 | Idle baseline         | Just leave it running                                      | 15-min segments roll cleanly; no errors; flat memory                                 |
| 2 | RDP disconnect+reconnect | `tscon` from a scheduled task, or manual                | `Session switch detected (RemoteConnect)` → `Replacing zombie tile` × N → fresh `.mkv` |
| 3 | Console lock+unlock   | Win+L, then sign back in                                   | `Session switch detected (SessionUnlock)` → cameras stay/come back Connected         |
| 4 | Network blip          | Unplug switch / disable NIC for ~30s                       | Auto-reconnect path runs; recordings resume on a new file                            |
| 5 | Single camera reboot  | Power-cycle one camera                                     | Only that camera's player errors and reconnects; others unaffected                   |
| 6 | App-host idle         | Lock the screen and leave overnight                        | Same as #1; no GPU / DPI re-init storms                                              |

Cadence for a 24 h soak: scenario 2 every 4 h (5×), scenario 3 once, scenario 4
once, scenario 5 once. Rest is idle baseline.

## Metrics to capture

Captured automatically in `camera-wall-{date}.log`. After the run, eyeball the
log for these:

- **Per-camera segment count.** With 15-min segments and 24 h: ~96 files per
  camera. Anything substantially less = gaps. (Account for the fractional first
  segment after start and after each reconnect.)
- **Total recorded bytes per camera.** Bitrate should be flat — pick the
  steady-state Mbps from the first hour and compare hour-by-hour.
- **Zombie tile replacements.** Should match disruption count (one per camera
  per RDP cycle and similar). More = something is rebuilding tiles
  spontaneously.
- **Auto-reconnects.** Triggered only by scenario 4/5; spurious reconnects
  during idle are a regression.
- **Recording-on-connect skip warnings.** Should be zero — these were the
  symptom of stale `RecordingService` sessions before the C1-equivalent fix.
- **Memory.** Capture private bytes at start and end via Task Manager /
  `Get-Process`. Drift > 200 MB over 24 h warrants investigation.

## Pass criteria

A run is considered a pass when **all** of:

1. Each `RecordOnConnect=true` camera has a continuous chain of `.mkv` files
   covering the soak window, with the only gaps being the deliberate disruption
   events (RDP, network blip, camera reboot).
2. No unexpected `[ERR]` lines.
3. No `[WRN]` lines outside the deliberate-disruption windows or the known
   first-recording snapshot misses (already demoted to Debug on this branch).
4. No "Recording-on-connect enabled but skipped" warnings during recording
   resumption.
5. At app close, the "Recording stopped for camera 'X'" lines reference the
   *current* file, not stale pre-disruption paths.
6. Memory drift < 200 MB over 24 h.

If any criterion fails, capture the log + the recording directory listing and
investigate before re-running.

## Log analysis helper

To make passing a log to analysis easy, a script
(`scripts/soak-report.sh` — TBD, write before the run) should produce:

- Per-camera segment count and total bytes
- Histogram of `[WRN]` / `[ERR]` lines by message template
- List of `Session switch detected` / `Replacing zombie tile` events with
  timestamps
- The "Recording stopped for camera 'X'" trailer at app close, with the file
  paths

Output should be plain text, ~80 lines, dropped into the same conversation
that's investigating the run.

## Post-soak checklist

- [ ] Snapshot the log file to `docs/soak-runs/{date}-{duration}.log` (or a
      shared location)
- [ ] Note which scenarios actually fired and at what times
- [ ] If pass: note the run (date, length, scenarios) in the roadmap
- [ ] If fail: open an issue with the log excerpt, do not release
- [ ] If two consecutive 24 h soaks pass, consider running a weekend soak as
      the final gate before declaring 24/7-ready

## Why not "just run it longer"

Because the failure modes we care about are timing-correlated (RDP cycle, GPU
session change, hour boundaries). A 7-day idle run with no disruptions catches
fewer real bugs than a 24 h run with one of each. Disruption coverage matters
more than wall-clock duration once you're past ~12 h.
