# 🟥 Bounding Boxes and Activity Timeline Roadmap

Plan for making motion detection find **one box per moving object**, **no box for a tree in the wind**, and for showing **where in a recording something happened**. A sample app and a scored test harness come first, so every later change is measured against a stream whose content we know.

Affects **`AtcSoft.VideoSurveillance.Core`** (the detector), **`AtcSoft.VideoEngine`** (frames for analysis), both motion services, the overlays in **both WPF apps** and the **web client**, recordings (a new activity file), the **server** (a timeline of every camera across its recordings), the **API spec**, and a new **`sample/BoundingBoxWpfApp`**.

← Back to the [roadmap home](roadmap.md)

**Status: ✅ Complete** — planned on 2026-10-18, done on 2026-10-28. Rain on a real camera is on the roadmap's list of hands-on checks.

---

## 🗂️ Status Legend

| Emoji | Meaning |
|-------|---------|
| ⬜ | Not started |
| 🟨 | In progress |
| ✅ | Complete |
| ⏸️ | Deferred (intentionally not in current scope) |

Sizes: **S** an hour or two, **M** a day, **L** several days.

---

## 🎯 Goal

| What | Done when |
|---|---|
| One box per moving object | The storybook scene *car + human + cat* gives exactly three tracks, one box each, for the whole time they move |
| No boxes for things that only sway | The scene *tree in the wind* gives no box and no motion event |
| Small and slow objects are found | A small cat far away and a slow walker are boxed within 0.5 s |
| Boxes are drawn where the object is | The FlaUI test reads the drawn boxes and they match the truth, also at 90 / 180 / 270° |
| Every recording shows its activity | A 60 min recording with a person for 5 s at minute 48 shows a visible mark there, in both WPF apps and the web client |
| The server has a timeline of every camera | One view shows all cameras over a day (or a week), with where recordings exist, where the gaps are and where something happened; a click plays from that moment, across segment boundaries; it grows live |
| Real recordings become tests | A recording plus the user's verdicts (correct / false box / missed) runs as a regression test |
| Later: what the box is | A local ONNX model labels a box *person*, *car*, *cat* or *dog* (⏸️) |

---

## 🔍 Today's Problems (with evidence)

Checked against the code on 2026-10-18.

### How it works today

| Step | Where |
|---|---|
| The player encodes the whole frame (full resolution) as **PNG**, on a pool thread | `VideoPlayer.CaptureFrameAsync`, `src/AtcSoft.VideoEngine/VideoPlayer.cs:539-588` |
| The PNG is decoded again with GDI+, scaled to 800×600 **without keeping the aspect ratio**, and made grey | CameraWall: its own copy, `MotionDetectionService.cs:371-421`; server: `GdiMotionFrameReader.cs:25-28` |
| The new grey picture is compared with **the previous analysed picture only** | `MotionDetectionService.cs:297,347`; `ServerMotionDetectionService.cs:248-255` |
| Pixel threshold 15–35 from *Sensitivity*; 20×20 cells; a cell is active with ≥ 33 changed pixels; 8-connected cells form a box | `MotionFrameAnalyzer.cs:41,45,80,88-116` |
| Box padding, sort by area | `MotionFrameAnalyzer.cs:145-156` |
| Motion is "active" when the **whole picture's** changed share ≥ `MinimumChangePercent` (0.5 %) | `MotionDetectionService.cs:307-308`; `ServerMotionDetectionService.cs:257` |
| WPF draws at most 10 boxes, smoothed per **list position** | `MotionBoundingBoxOverlay.xaml.cs:16,139-180` |
| Web draws every box, no smoothing | `LiveView.razor:169-172` |

### Why there are too many boxes

| Cause | Evidence | Effect |
|---|---|---|
| **Two-frame difference, no background model** | `MotionFrameAnalyzer.Analyze(previous, current…)`; previous = last analysed frame (`MotionDetectionService.cs:347`) | A moving object changes only at its **leading and trailing edges**; a plain-coloured inside does not change. One person becomes two or three clusters. A fast object leaves a "ghost" box where it was |
| **No morphology, no blob merge** | Only cell counting and flood fill (`MotionFrameAnalyzer.cs:53-116`); padding without merging (`:145-152`) | Fragments 1 cell apart stay separate boxes; padded boxes overlap and are still drawn twice |
| **Foliage lights scattered cells** | One fixed threshold for every pixel (`:41`), no per-pixel variance | Leaves cross the threshold every frame; each cluster of leaves is a box — the "10+ boxes for a tree" |
| **No persistence, no tracking** | Boxes go straight from one comparison to the event (`MotionDetectionService.cs:314-324`) | Something there for one frame gets a box; nothing tells a swaying branch (moves back and forth on the spot) from a person (travels) |
| **`MinArea` does nothing** | Default 10 px² (`BoundingBoxSettings.cs:16`), but the smallest cluster is one cell = 400 px² (`MotionFrameAnalyzer.cs:45,137-143`) | The setting meant to filter noise never filters |
| **The 10-box limit is in the renderer** | `MaxBoundingBoxes = 10` (`MotionBoundingBoxOverlay.xaml.cs:16`); none in the web client | The detector reports any number; the cap hides it in WPF only |
| **No handling of a lighting change** | Nothing checks for a whole-picture change | Auto exposure, a cloud or the IR switch at dusk becomes one huge box or many |

### Why detection sometimes misses

| Cause | Evidence | Effect |
|---|---|---|
| **Global gate on the share of changed pixels** | `changePercent >= MinimumChangePercent` (0.5 %) over the whole picture (`MotionDetectionService.cs:308`, `ServerMotionDetectionService.cs:257`) | 0.5 % of 800×600 is 2 400 pixels. A small cat or a far person changes fewer (edges only), so **no motion and no box even when a cluster was found** |
| **Slow movers** | Two-frame difference at the analysis interval | A person walking slowly changes little between two pictures; at CameraWall's up to 15/s the difference is smallest |
| **Noise counts toward the gate** | Scattered pixels count in `totalChangedPixels` (`MotionFrameAnalyzer.cs:66,76`); pinned by the test `Scattered_Noise_Counts_As_Change_But_Gets_No_Box` | Motion can be "active" with **no box**, and a real object competes with noise for the same percentage |
| **The server only watches some cameras** | `ShouldWatchForMotion` = record-on-motion (`CameraConnectionService.cs:377-378`) | A camera that records continuously has no motion data at all — and so no activity timeline |

### Why the boxes jump or flicker

| Cause | Evidence | Effect |
|---|---|---|
| **Smoothing by list index while the list is re-sorted by area** | `smoothedBoxes[i]` (`MotionBoundingBoxOverlay.xaml.cs:150`) vs `boundingBoxes.Sort(...)` (`MotionFrameAnalyzer.cs:156`) | When the car's box becomes smaller than the person's, box 0 slides from the car to the person |
| **CameraWall ends motion on the first quiet frame** | `context.IsMotionDetected = false` (`MotionDetectionService.cs:341`); the tile hides the boxes at once (`CameraTile.xaml.cs:2640`) | Boxes blink on and off, while the server holds motion for `PostMotionDurationSeconds` (`ServerMotionDetectionService.cs:270-271`) |
| **The two services disagree** | CameraWall 2–15/s round robin with skips (`MotionDetectionService.cs:13-14,233-237`); server ≤ 4/s (`ServerMotionDetectionService.cs:21`); different fallback sizes (320×240 vs the settings' 800×600) | The same scene scores differently on the desktop and the server, because a frame difference depends on the interval |
| **Aspect ratio lost** | 16:9 or a portrait (rotated) picture squeezed into 800×600 (`MotionDetectionService.cs:377`) | Cells are not square in the real picture; box sizes differ by direction |

### Smaller findings

- `docs/motion-detection.md:98` says `AnalysisFrameRate` defaults to 30; the code says 15 (`MotionDetectionSettings.cs:12`).
- CameraWall has its own grey conversion; the server uses `GdiMotionFrameReader`. The same code twice.
- `CameraTile.UpdateMotionBoundingBoxes` defaults to 320×240 (`CameraTile.xaml.cs:2796-2799`). Harmless today (only used with no boxes), but a trap.
- The known bug *boxes misplaced at 180°* ([roadmap](roadmap.md#-known-bugs)) has no automated test. A storybook can carry a rotation and become that test.

---

## 🧭 Design

### The pipeline after this plan

| Step | What | Why |
|---|---|---|
| 1 | **Grey frame with its time** from the engine (Y plane, scaled by swscale, plus the frame's pts) | No PNG round trip; frames can be matched to the truth by time |
| 2 | Scale keeping the aspect ratio (width given, height follows); a light blur (3×3 or 5×5) | Square cells; sensor noise and compression blocks removed |
| 3 | **Global change check**: if more than ~60 % of pixels change, or the mean brightness jumps, re-learn the background and suppress boxes for a moment | Lighting, IR switch, a camera re-exposing |
| 4 | **Background model** per pixel: running mean and variance (a one-Gaussian model; MOG2-like with 2–3 modes only if the scores need it) | A slow mover differs from the background, not from the last frame; swaying leaves get a high variance and stop triggering |
| 5 | Foreground = \|pixel − mean\| > k·σ (with a minimum); foreground pixels are learnt slowly | One whole silhouette instead of two edges |
| 6 | **Morphology**: open (remove specks), then close (fill holes) | No speckle boxes; one blob per object |
| 7 | **Connected components** on the pixel mask (two-pass union–find) | Exact blobs; no 20 px quantisation |
| 8 | **Filter**: minimum area as **% of the picture**, masks and zones, aspect limits | Settings that mean the same at any resolution |
| 9 | **Merge**: blobs closer than a gap, or overlapping after padding, become one box | One box per object |
| 10 | **Tracker**: match boxes to tracks by IoU or centre distance with a constant-velocity prediction (SORT-like; Kalman optional). A track is **confirmed** after N hits *and* a minimum net travel; it ends after M misses | Persistence; the tree never travels, a person does; stable ids |
| 11 | **Output**: confirmed tracks only, each with a track id | Motion is "active" while a confirmed track exists — not on a global percentage |

All of this is **one stateful class in Core** (`MotionDetector`, one per camera). `MotionDetectionService` (CameraWall), `ServerMotionDetectionService` and the sample app only feed it frames and pass on what it returns. Same code, same results everywhere.

### Activity index (for the timeline)

| Part | Design |
|---|---|
| What is stored | Per second: activity level (largest foreground share, 0–255), number of confirmed tracks, flags (lighting change, masked). Per track: id, first and last time, largest box; later label and score |
| Where | A sidecar next to each recording segment (`….mp4` → `….activity.jsonl`). Its path comes from **`RecordingPathHelper`** (`BuildActivityPath` / `FindActivityPath`); `RecordingLayoutReorganizer.Synchronize` moves it with the video |
| Format | JSON Lines: a header line `{"v":1,…}`, then one line per second, appended while recording; readable while `IsBeingWritten` |
| Size | About 3 600 short lines per hour (~100 KB) |
| Written by | Both recording services, from the detector's output. Detection must therefore run for **every camera that records**, not only for record-on-motion (`CameraConnectionService.cs:377`) |
| Read by | `GET /recordings/activity?filePath=…&buckets=…&durationSeconds=…` (recordings are named by their path, as for delete; all three roles; no cache, the file grows) for the clients; CameraWall reads the file directly |
| Old recordings | An *Analyse recording* action (the sample app's offline path, reused) writes the index afterwards |
| Deleting | A deleted recording takes its sidecar with it. The clean-up code belongs to the disk-space work in progress: a dependency to agree, not a change made by this plan |

### Activity timeline (UI)

| Element | Design |
|---|---|
| Activity bars | A strip under the player; bar height = activity level. **Downsample with max, never mean**: at 60 min on 1 000 px each pixel is 3.6 s, and averaging erases a 5 s event |
| Event bands | Merged active periods (with pre/post padding) as coloured bands, at least 3 px wide, so a 5 s event in an hour is always visible |
| Colours | One colour for motion now; per class later (person, vehicle, animal), always **with an icon** too (never colour alone). Grey for ignored (lighting change, masked) |
| Hover | Time, track count, label, and a picture: a preview strip of the whole recording (a tile a second up to 600 tiles, made when the recording ends), and on an event the tile of its busiest second |
| Navigation | *Previous / next event* buttons and keys; a click on a band jumps there with a few seconds' lead-in |
| Skip the quiet | *Play events only*: plays each event and jumps over the rest |
| Zoom | Mouse wheel / pinch, from the whole recording down to a minute |
| Recordings lists | A small sparkline and an event count per row; a filter *with activity only* |
| Feedback | Right-click a band: *correct* / *false alarm* / *missed something here* — writes the review file of phase 9 |
| Where | WPF: `FullScreenRecordingWindow` (above its seek slider) and the `RecordingsBrowserDialog` rows, one control in Wpf.Core shared by CameraWall and the client. Web: the Recordings page player, its logic in `Logic/` with tests |

### Server timeline (every camera, across recordings)

The per-recording timeline answers *where in this file*. The server timeline answers *what happened at the site*: every camera in one view, over hours or days, without opening recordings one by one. It is built from the activity files of phase 7, so it adds no second detector.

| Part | Design |
|---|---|
| What it shows | One lane per camera, over a chosen span (default the last 24 h): **coverage** (where a recording exists), **gaps** (camera offline, not recording, disk full: from the connection and recording events), **activity bars** and **event bands** as in the per-recording timeline, and a **now line** |
| Day index on the server | Per camera and day, a summary built from that day's activity files: coverage spans with their recording ids, 10 s buckets (max level, max tracks), and the merged events (start, end, peak level, track count, peak thumbnail, later the labels). Kept in the data folder (`{data}/timeline/{camera}/{yyyy-MM-dd}.json`, paths from `ServerDataPaths`) and in memory for recent days. **Rebuilt from the activity files when missing or stale**, so it is a cache, never the only copy |
| Kept up to date | The recording service appends to today's index as each activity line is written; a finished segment closes its coverage span. The cleanup and the disk-space guard drop deleted recordings from the index (an agreed hook in the guard's result, not a scan) |
| Live | New events go out on `GET /events` (`activityStarted`, `activityEnded` with camera, time, level, later the label), so an open timeline grows without polling |
| API (spec-first, all three roles) | `GET /timeline?from=&to=&cameraIds=&width=` returns per camera the coverage spans, the gaps, the buckets **downsampled by max** to the requested width, and the events in the span. `GET /timeline/events?cameraIds=&from=&direction=next\|previous&limit=` finds the next or previous event across all recordings. Spans over a few days are cached (`x-cache-*`) and capped (a `maximum` on the span in the spec) |
| Playback across segments | A click resolves to *recording id + offset*. The player starts there and moves on to the next segment of that camera at its end, so a long timeline plays as one stream. A gap is skipped or shown, as the user chooses |
| Several cameras at once | *Play all from here*: the live grid's layout plays every lane from the same moment, kept roughly in step (the slowest sets the pace) |
| Filters | Cameras or a layout; *with activity only* hides quiet lanes; later per class (person, vehicle, animal) |
| Thumbnails | Each event's peak frame, stored when the event ends (the suggestion below), served behind the media token like recording thumbnails |
| Where | Web: a new *Timeline* page (lanes, day picker, zoom from a week to a minute, keyboard next / previous). WPF client: a *Timeline* view with the same control as the per-recording timeline, in lane mode. CameraWall: the same Core index builder over its own folders, later (it has no server) |

---

## 🛰️ Market Comparison: Activity Timelines

Only what the searches confirmed. "Not checked" means no source was found, not that the product lacks it.

| Product | Activity level | Event marks | Per-class colour / icon | Preview on hover | Skip / filter | Search | OpenCV in motion detection |
|---|---|---|---|---|---|---|---|
| **Frigate** | ✅ lines whose length is the amount of motion | ✅ shaded bands: alerts red, detections orange; motion yellow | Severity colours; person and car are alerts by default | Not checked | ✅ *motion only* mode skips periods already covered by alerts and detections; all cameras scrubbed together | Review items grouped per camera | ✅ **Yes**: its motion detector calls `cv2.accumulateWeighted` (running background), `absdiff`, `threshold`, `dilate`, `findContours`, `boundingRect`; the blur is SciPy |
| **Google Home / Nest** | Grey for motion outside zones | ✅ dots and bars | ✅ **colour per activity zone**; event filters | Not checked | Gaps between events (event plans) | Not checked | ❔ Closed source; no component list found |
| **UniFi Protect** | Not checked | ✅ timeline in the detection player; horizontal event scrubber | Colours not confirmed | ✅ frame preview scrubbing in detection clips | Not checked | ✅ *Find Anything* (replaced the Detections page) | ❔ Closed source; Ubiquiti names no libraries |
| **Synology Surveillance Station** | Not checked | ✅ | ✅ **colour per trigger** (motion, I/O, manual, …) | Not checked | Smart Time Lapse condenses long recordings | Smart Search (not in Timeline) | ❔ Licence list inside the package; not checked |
| **Milestone XProtect** | Not checked | ✅ bookmarks (blue), other markers in other colours | Marker colour by source | ✅ bookmark preview on hover | Not checked | ✅ motion search in a drawn area | ❔ Third-party terms ship in the install folder; not checked |
| **Blue Iris** (community sources only) | Not checked | ✅ clip bars, alert markers (orange/red) | Per camera; add-ons colour by AI class | Not checked | High-speed playback | Not checked | ❔ Not found; its help calls its own algorithms "fairly simple" (one is "Gaussian") |
| **Reolink** | Not checked | ✅ | ✅ **icons**: dot for motion, person, vehicle, paw | Not checked | Not checked | Not checked | ❔ Not found (detection is in the camera) |
| **Ring** | Not checked | ✅ event history timeline | Event types person / animal / vehicle | Not checked | ✅ filter by device and type | Not checked | ❔ Not found (cloud) |
| **Scrypted** | Not checked | Not checked | Not checked | Not checked | Not checked | Not checked | ✅ **Optional**: an *OpenCV Motion Detection* plugin (more CPU and memory, better results than its PAM Diff detector); its NVR uses a WebAssembly motion detector instead |
| Arlo, Eufy, HomeKit Secure Video | Not checked | Not checked | Not checked | Not checked | Not checked | Not checked | ❔ Not found |

**OpenCV:** confirmed only where the code is open: **Frigate** (core) and **Scrypted** (an optional plugin). The closed products publish no component lists that a search found, so ❔ means unknown, not "no". What Frigate does with OpenCV is the same short chain this plan writes in C# (decision 1): a running-average background, a difference, a threshold, dilation and contours. So the pure-C# detector follows a proven design without bringing OpenCV's native runtime into the published Core package.

**What to take:**

- From Frigate: **the amount of motion as a bar**, events as bands, and *motion only* playback. It is the most complete and the best documented.
- From Google Home: colours that **mean something the user chose** (zones). We use them for classes, and later for zones too.
- From Reolink and Ring: **an icon per class** and a filter by type.
- From UniFi and Milestone: **preview on hover**.
- Our own additions: max-downsampling with a minimum width (a 5 s event in an hour never disappears), *next event* keys, and **feedback from the timeline** straight into the test corpus. None of the products above was found doing that.

---

## 🧪 Sample App and Test Harness

### `sample/BoundingBoxWpfApp`

| Feature | Notes |
|---|---|
| Open a recording | Any MP4/MKV, played by the repo's `VideoEngine` and `VideoHost` |
| Open a storybook | Generates the video from it (cached by the storybook's hash), then plays it with its truth |
| The real pipeline | The Core `MotionDetector` and the Wpf.Core `MotionBoundingBoxOverlay` — referenced, never copied |
| Two clocks | *Live*: real time, as in the apps. *Deterministic*: every frame analysed by its pts, then paused, so a run always gives the same result |
| Debug layers | Foreground mask; rejected blobs in grey with the reason (too small, masked, not travelled, not confirmed); track ids; truth boxes in green |
| Tuning panel | Every detector setting, applied at once; *Save as storybook settings* |
| Score panel | Precision, recall, false boxes per frame, id switches, latency — per object and per time span |
| Review mode | For real recordings (phase 9): mark spans and boxes as correct / false / missed |
| Automation | Each drawn box is exposed to UI Automation (`AutomationId` `box-{trackId}`, its rectangle in video coordinates in `HelpText`); the score as JSON in a text element |
| Not shipped | `IsPackable` false, not in the release; in the solution under a `sample` folder |

### The play-storybook file (`*.storybook.json`)

A scene, its objects and what is expected. The generator draws the frames from it, so the truth is exact.

```json
{
  "version": 1,
  "name": "car-human-cat",
  "video": { "width": 1280, "height": 720, "fps": 15, "durationSeconds": 20,
             "noise": 2.0, "bitrateKbps": 2000, "rotation": "none" },
  "background": {
    "kind": "procedural", "seed": 42,
    "distractors": [
      { "id": "tree", "kind": "swayingTree", "at": [0.12, 0.55], "size": 0.25, "amplitude": 0.015, "periodSeconds": 1.7 },
      { "id": "clock", "kind": "clockOverlay", "corner": "topRight" },
      { "id": "dusk", "kind": "brightnessStep", "atSeconds": 15, "delta": 40 }
    ]
  },
  "objects": [
    { "id": "car", "kind": "car", "size": [0.22, 0.12],
      "path": [ { "t": 2, "at": [-0.2, 0.75] }, { "t": 8, "at": [1.2, 0.75] } ] },
    { "id": "human", "kind": "human", "size": [0.05, 0.18],
      "path": [ { "t": 3, "at": [0.3, 0.6] }, { "t": 7, "at": [0.6, 0.6] },
                { "t": 9, "at": [0.6, 0.6] }, { "t": 12, "at": [0.9, 0.62] } ] },
    { "id": "cat", "kind": "cat", "size": [0.04, 0.03],
      "path": [ { "t": 4, "at": [0.8, 0.85] }, { "t": 9, "at": [0.5, 0.85] } ] }
  ],
  "expect": {
    "tracks": 3,
    "minRecall": 0.95, "maxFalseBoxesPerFrame": 0.02, "maxIdSwitchesPerObject": 1,
    "maxLatencySeconds": 0.5, "matchIou": 0.3,
    "spans": [
      { "object": "human", "from": 7.2, "to": 9.0, "state": "dontCare", "why": "stands still" },
      { "distractor": "tree", "state": "mustNotBox" },
      { "distractor": "clock", "state": "mustNotBox" },
      { "from": 15, "to": 16, "state": "dontCare", "why": "lighting step" }
    ]
  }
}
```

| Part | Rules |
|---|---|
| Coordinates | Fractions of the picture (0–1), as shown (after rotation); a size up to 3, for someone close to the lens |
| Paths | Key points with times, linear between them; before the first and after the last time the object is not in the picture |
| Truth states | **mustBox** (a moving object; the default), **mustNotBox** (distractors), **dontCare** (standing still, entering with less than half of it in the picture unless that half fills half the picture, a lighting step) |
| Detector | `"detector": { "ignoreLightingChanges": false }`: the settings the scene is meant for, where they differ from the defaults (a door camera); both detector tests run the scene with them |
| Known bad | `"expect": { "knownBad": "why" }`: a scene that shows a problem not solved yet. Its scores are kept in the baseline; its expectations are skipped with the reason while it fails, and the test fails once it passes, so the mark is taken off |
| Objects | Simple silhouettes drawn in C# (car, human, dog, cat, with some texture and a shadow). Good enough for motion; **not** for an AI model — phase 10 needs real recordings or licensed photo sprites |
| Truth file | The generator writes `*.truth.jsonl` next to the video: per frame, each object's box from the pixels it actually drew |

### Generator

| Choice | Why |
|---|---|
| Frames drawn in managed C# (a BGRA buffer), deterministic from the seed | The truth comes from the same drawing; no image library dependency |
| Encoded by piping raw frames to the `ffmpeg.exe` the installer provides (H.264 at the storybook's bitrate) | Real compression artefacts, as from a camera |
| A fast path without encoding: frames go straight to the detector | Unit tests run in milliseconds, without FFmpeg |
| A live path: the generated file streamed over UDP by `ffmpeg -re` | Drives the real player and both apps, like the existing test cameras |

### Scoring

| Measure | How |
|---|---|
| Match | Per frame, Hungarian (or greedy) matching of drawn boxes to the truth by **IoU ≥ 0.3** or centre inside (motion boxes are padded, so 0.5 is too strict) |
| Recall / precision | Over *mustBox* frames, after a latency allowance at each object's start |
| False boxes per frame | Boxes matched to nothing, or inside a *mustNotBox* region |
| Track count, id switches, fragmentation | From the track ids |
| Latency | From an object's first *mustBox* frame to its first matched box |
| Report | JSON (`score.json`) and a short text table; the same code in the sample app and in the tests |

### Tests

| Test | Where | Needs |
|---|---|---|
| Storybook parsing, path maths, truth generation | `test/AtcSoft.VideoSurveillance.Storybook.Tests` | Nothing |
| Scorer against hand-made box lists | same | Nothing |
| **Detector against every scene, fast path** (the main regression test) | same | Nothing |
| Detector through encode and decode | same | FFmpeg; skips without |
| Overlay mapping (analysis → screen, letterbox, rotation) | `Core.Tests` (`MotionBoxMapper`), extended | Nothing |
| **FlaUI walk of the sample app**: open a storybook, run deterministic, compare the drawn boxes (automation) with the detector output and the truth at chosen frames, read the score | `test/AtcSoft.VideoSurveillance.E2E.Tests`, a new `BoundingBoxSampleSmokeTests` | `ATCSOFT_E2E=1`, a desktop, FFmpeg |
| Real recordings with reviews | `Storybook.Tests`, the corpus folder | `ATCSOFT_BBOX_CORPUS`; skips without |

### Scene catalogue

| # | Scene | Checks |
|---|---|---|
| S1 | Empty scene, sensor noise | No box, no motion |
| S2 | Tree in the wind (and moving grass) | No box, no motion |
| S3 | One person walking slowly | Found within 0.5 s, one box, one id |
| S4 | A car passing fast | One box, no ghost behind it |
| S5 | **Car + human + cat at the same time**, crossing once | Exactly three tracks, at most one id switch each |
| S6 | Lighting step and IR switch (colour → grey) | No box during the step; detection resumes after |
| S7 | S3 at 90 / 180 / 270° | Boxes on the object in the drawn picture (the 180° bug). Through the player only (phase 3): the fast path already sees the shown picture. Walked on the sample's live clock at all four turns |
| S8 | A small cat far away | Found |
| S9 | A person stops for 5 s, then walks on | Same id after the stop |
| S10 | Rain, and snow with large flakes near the lens (known bad) | No box |
| S11 | S5 at a low bitrate (blocky) | As S5 |
| S12 | A camera clock in the corner | No box for the clock |
| S13 | A person walks up to the camera | One track while growing |
| S14 | A door camera: a person comes close, fills most of the picture, waits 2.5 s, leaves (lighting changes not ignored) | Found throughout; no ghost after |

---

## 🔁 Feedback Loop for Real Recordings

| Step | What |
|---|---|
| 1 | Open a real recording in the sample app (review mode); the detector runs deterministic |
| 2 | Mark spans: *correct*, *false box*, *missed*, *wrong count* (with the right count); optionally drag a box on a paused frame and name it (person, car, cat, dog, …) |
| 3 | Saved as `*.review.json` next to the recording: the recording's SHA-256, the detector version and settings, and the verdicts |
| 4 | The review becomes truth: marked spans become *mustBox* / *mustNotBox* with their counts; everything not marked is *dontCare* |
| 5 | The tests run every recording in `ATCSOFT_BBOX_CORPUS` with its review and compare with the last accepted score; a drop fails |
| 6 | Later, the timeline's right-click verdicts in the apps write the same format, so a user can hand over a recording with verdicts |

```json
{
  "version": 1,
  "recording": { "file": "garden-2026-10-20.mp4", "sha256": "…" },
  "detector": { "version": "2.0", "settings": { "…": "…" } },
  "spans": [
    { "from": "00:47:58", "to": "00:48:03", "verdict": "correct", "count": 1, "label": "person" },
    { "from": "00:12:00", "to": "00:14:30", "verdict": "falseBox", "note": "tree" },
    { "from": "00:31:10", "to": "00:31:14", "verdict": "missed", "count": 1, "label": "cat",
      "box": { "atFrame": 28012, "x": 0.71, "y": 0.80, "w": 0.04, "h": 0.03 } }
  ]
}
```

The recordings stay out of git (size, privacy). The small review files may go in `test/…/corpus/` and name their recording by its hash.

---

## 🛣️ Phases

| # | Phase | Size | Status |
|---|-------|:--:|:--:|
| 0 | Quick fixes in today's pipeline | S | ✅ |
| 1 | Storybook, generator, truth and scorer; today's baseline | L | ✅ fast path, scorer, the encoder pipe with `*.truth.jsonl`, fifteen scenes (S11 through the encoder) |
| 2 | Grey frames with their time from the engine | M | ✅ the offline file reader and the live grey pictures; both apps analyse without PNG |
| 3 | `sample/BoundingBoxWpfApp` and its FlaUI test | M | ✅ deterministic and live clocks, the truth on screen, the score, boxes in UI Automation, the FlaUI walks, debug layers, tuning panel, the detector's time; S7 through the player |
| 4 | Detector v2 in Core (background model, morphology, merge) | L | ✅ detector built and used by both apps, its settings in plain words in all three apps, analysis in the picture's shape, the steps on vectors (5× faster), every scene, S11 through the encoder included (snow known bad); real cameras by evening and by day, strong wind taken in (S15), trees through a window left to an ignore zone; rain on a real camera on the hands-on list |
| 5 | Tracker: persistence, travel, ids; smoothing by id | M | ✅ tracker, ids on the wire, smoothing by id in all three clients |
| 6 | Masks and zones | L | ✅ done 2026-10-20: ignore and watch zones, drawn on a snapshot in all three apps; an event in its watch zone's colour on the timeline |
| 7 | Activity index (sidecar, API) | M | ✅ Core, server, CameraWall, the setting and *Analyse recording* in all three apps |
| 8 | Activity timeline in both WPF apps and the web client | L | ✅ timeline, jumps, click to seek, play events only, sparklines, zoom, hover with a picture, events close together joined, in all three clients |
| 8b | Server timeline: activity summaries, `GET /timeline`, next / previous event | M | ✅ |
| 8c | Timeline view of every camera: web page and WPF client view, playback across segments, live events (`activityStarted` / `activityEnded`), gap reasons | L | ✅ both API clients and CameraWall: playing on across segments, growing live (CameraWall: on recording changes and every 15 s), zoom, gap reasons (API clients), play all from here |
| 9 | Feedback loop: review mode and the regression corpus | M | ✅ the review file, scoring by count, the corpus test, review mode in the sample app, verdicts from the timeline in all three apps, the settings sweep; the corpus grows as recordings are reviewed |
| 10 | Local AI detection (ONNX) on motion regions: moved to the [AI support plan](roadmap-AI-support.md) | L | ⏸️ |

### Phase 0 — Quick fixes ✅ (S)

Done 2026-10-19: `BoxSmoother` (Core) smooths each box against its own previous box; `MotionHold` (Core) holds motion on the desktop as on the server; CameraWall reads its pictures through `IMotionFrameReader`; the doc default is fixed.

- Smoothing matched to the **nearest previous box** (IoU or centre), not to the list index (`MotionBoundingBoxOverlay.xaml.cs:150`).
- CameraWall holds motion for `PostMotionDurationSeconds` as the server does, so boxes stop blinking (`MotionDetectionService.cs:326-343`).
- CameraWall uses `IMotionFrameReader` instead of its own grey conversion.
- `docs/motion-detection.md`: `AnalysisFrameRate` default 15.

**Test:** unit tests for the overlay matching (moved into a plain class, out of the code-behind); the existing `MotionFrameAnalyzerTests` stay green.

### Phase 1 — Storybook, generator, truth and scorer ✅ (L)

**Done 2026-10-19:**

- The detector seam `IMotionDetector` (Core) with today's detection as `FrameDifferenceMotionDetector`; CameraWall and the server use only the seam, so the baseline measures what ships.
- `src/AtcSoft.VideoSurveillance.Storybook` (not packable): the model and its reader (errors name the place, such as `objects[0].path[1].t`), a deterministic renderer (our own random generator keyed by seed and frame; truth from the pixels actually drawn; standing still and less than half visible are *dontCare*), the scorer and the fast-path runner. The runner squeezes to 800×600 as the apps do and scores the boxes the apps show (kept until the detector replaces them or motion ends).
- Scenes S1, S2, S3, S5 and S12; a ratchet: `baseline.json` holds every score, a test fails when one gets worse, `ATCSOFT_BBOX_UPDATE_BASELINE=1` writes new scores on purpose.

**Baseline of today's detector** (default settings; recall over must-box frames after 0.5 s, false boxes per analysed frame):

| Scene | 4/s recall | 4/s false/frame | 15/s recall | 15/s false/frame |
|---|---|---|---|---|
| S1 empty, noise | 1.000 | 0.000 | 1.000 | 0.000 |
| S2 tree in the wind | 1.000 | **2.286** | 1.000 | **3.591** |
| S3 slow walker | **0.405** | 0.531 | **0.021** | 0.845 |
| S5 car + human + cat | 0.955 | **1.612** | 0.805 | **3.199** |
| S12 camera clock | 1.000 | 0.000 | 1.000 | 0.000 |

It shows both complaints: a tree gives two to four boxes in every frame, and a slow walker is mostly missed, at 15/s almost always (two pictures 1/15 s apart barely differ). The clock gives no box because its change stays under the 0.5 % gate, the gate that also hides small objects.

**Done 2026-10-27:** the encoder pipe and scene S11. `StorybookEncoder` draws every frame and pipes it to the FFmpeg tool (H.264, one thread, no B-frames, a key frame every 2 s, so a scene gives the same video on every run) at the storybook's `video.bitrateKbps`, and writes `*.truth.jsonl` (`TruthFile`) next to the video. `StorybookRunner.RunEncoded` reads the video back with `GreyFrameFileReader`, as *Analyse recording* does, and scores each picture with the truth of its frame. A scene with a bitrate is scored on the encoded path and skips without FFmpeg. S11 (S5 at 400 kbps: the noise smeared into blotches) meets S5's expectations: recall 1.000 at 4/s and 0.994 at 15/s, no false box, 3 tracks, the same as S5 encoded at a high quality, so the compression costs the new detector nothing. `EncodedPathTests` keeps the pipeline honest: S5 encoded at a high quality scores as on the fast path (new detector). Today's frame difference does not: through the encoder at a high quality it already moves (recall 0.955 → 0.926 at 4/s, 0.720 → 0.833 at 15/s), the way the picture is scaled (swscale's area filter against the fast path's bilinear) being enough, and S11 adds nothing to that. The render time: the suite took 57 s because one test class ran its 52 cases one after another; one class per detector and rate brings it to 27 s, with the same frames and scores (about 35 s with the encoded scenes).

- A non-packable `src/AtcSoft.VideoSurveillance.Storybook` (net10.0): the model, JSON reading with clear errors, the frame renderer, the truth writer, the encoder pipe, the scorer.
- Scenes S1–S12 as files under `test/AtcSoft.VideoSurveillance.Storybook.Tests/Scenes`.
- **Baseline**: today's `MotionFrameAnalyzer`, with the services' gating, scored on every scene. The numbers go into this plan, so phase 4 shows its gain. The tests first assert the baseline as it is (marked known-bad), then tighten.

**Test:** the parsing, truth and scorer tests; the detector-against-scenes test.

### Phase 2 — Grey frames with their time ✅ (M)

**Done 2026-10-19:** the offline reader, `GreyFrameFileReader` (engine): opens a recording, decodes every frame as fast as it can, and scales the ones asked for straight to grey at the analysis size with swscale (area filter), turned the way the picture is displayed, with their place in the file; no PNG, no player. Engine tests on a recording the FFmpeg tool makes (rate, order, size, which side is bright, a quarter turn). It serves *Analyse recording*. The live half (done 2026-10-19): `VideoPlayer.CaptureGreyAsync(width, height)` scales the latest decoded picture straight to grey with the same `GreyScaler` (a hardware frame is copied to memory first), turned the way the picture is displayed, with its place in the stream, under the same lock as the PNG capture. `IMediaPipeline.CaptureGreyAsync` passes it on, and both motion services use it, keeping the PNG read back as grey (`IMotionFrameReader`) only for a pipeline that cannot make one. Measured on a 720p stream of a flat black-and-white picture, decoded in software: 0.4 ms a grey picture against 2.8 ms for the PNG alone, before it was read back (a real scene's PNG costs more, so the saving is understated). With hardware decoding (CameraWall's default) a grey picture copies the frame back from the graphics card, about 9 ms at 1080p; checked with a throwaway console on D3D11 at 1080p H.264: grey and PNG captures from eight threads at once, then the player disposed under them, three rounds, no crash, and the grey pictures match software decoding (the device is multithread-protected, and FFmpeg's transfer uses its own staging texture). An engine test on a live stream checks size, rotation, order and that it is cheaper. Both interfaces got the method with a default body, so other implementations keep compiling.

- Engine: `TryCaptureAnalysisFrame(width, height)` returns the latest decoded frame's **Y plane**, scaled by swscale and rotated, with its pts. No PNG.
- Engine: an offline reader that decodes a file frame by frame (as fast as it can) to the same grey frames, for the sample app, the scorer and *Analyse recording*.
- Both services switch to it; `GdiMotionFrameReader` stays only as a fallback if needed.

**Test:** engine tests against the live test stream (pts increase, size and rotation right); the CPU cost against the PNG path in the test output.

### Phase 3 — Sample app and FlaUI test ✅ (M)

**Done 2026-10-27:** `sample/BoundingBoxWpfApp` (in the solution under `sample`, not packable, not translated) opens a storybook or a recording (from the window or `--storybook` / `--recording` with `--rate`), runs it through `BuiltInMotionDetector` and shows each analysed picture with three `MotionBoundingBoxOverlay`s: the detector's boxes in red (smoothed, as the apps show them), the objects to box in green, the distractors in orange. Step, play and a slider; 4 or 15 pictures a second; the score as a line and as JSON. A storybook without a bitrate runs on the fast path, so its score is the baseline's (S5 at 4/s: recall 1.000, no false box, 3 tracks); one with a bitrate is encoded once into `{data}\videos`, named by the storybook's hash; a recording runs as *Analyse recording* reads it (`StorybookRunner.RunRecording`), without a truth. `StorybookRunOptions.KeepPictures` keeps each grey picture for the window; the tests leave it off. The overlay (Wpf.Core) now shows each box to UI Automation: automation id `box-{trackId}` (`box-at-{place}` without a track) and, as help text, the detector's box in video pixels before smoothing and fitting, so the apps' walks can read boxes too. `BoundingBoxSampleSmokeTests` opens S5, compares the score panel with a run made in the test and with `baseline.json`, and at two pictures compares the automation boxes (detector and truth) with that run; a second walk opens a recording made from S3 and checks its boxes, without a truth. A picture of the window is saved in the run folder.

**Changed from the design:** the engine's player plays a stream, not a file: it has no pause or seek, and a file runs to its end at once. So the deterministic clock does not use `VideoHost`: it shows the grey pictures the detector was given (what the detector sees), read with the same reader as the tests. **Debug layers, tuning panel and the detector's time (done 2026-10-28):** `BuiltInMotionDetector.KeepDiagnostics` (off by default, and then nothing is made) leaves `MotionDiagnostics` for each picture: the stage (learning, warm-up, change of light, settling, analysed), the foreground after the morphology packed to a bit a pixel, the ignore zones, and every blob with what became of it (`MotionCandidate`: too small, too new, not travelled, shown; its travel as a share of the confirming travel; held). Asking for them changes no box (S5, S6, S14, S15 at both rates; the storybook baseline unchanged). The sample draws the foreground in cyan, the ignore zones in purple and each blob with its reason, and names the stage under the picture. The tuning panel holds the four settings and the inner options (confirm time and travel, merge gap, track lost, background time, settle after light); a change runs the detector again over the pictures kept (`StorybookRunner.Rerun`, the same run with the same detector), within a second at the picture shown. *Save as storybook settings* writes the four settings into the storybook's `detector` section (which now takes all four, `StorybookWriter`, the rest of the file untouched), asking first for a scene of the suite. The score panel and each picture's line give the detector's time, with the diagnostics; a Debug build is many times slower than the apps (S5: 40 ms a picture in Debug, 1.7 ms in the cost test). The walk checks the debug line and a label per blob against a run made in the test, the tuned score against a run with that setting, and the saved file. Changed from the design: there is no *masked* reason, as an ignored pixel is never foreground; the ignore zones are a layer instead. The inner options are tried, not saved: they are not settings. **The live clock (done 2026-10-28):** *Play live…* (or `--live 0|90|180|270`) streams what is open to the engine's player as a camera sends it (`ffmpeg -re -copyts -muxdelay 0` over UDP, so the stream keeps the video's times from 0 and the truth is found by the picture's time), shows it in `VideoHost` turned as chosen with the boxes in its overlay window, and gives the detector the player's grey pictures at the analysis rate, as CameraWall does; the score is kept against the truth turned the same way. S7 through the player: S3 at 4/s turned 0, 90, 180 and 270°, recall 0.91-0.97, no false box, one track, found after 1.0-1.5 s (0.73 s on the deterministic clock; the player's first pictures come later). Whether FlaUI sees the overlay window: not in the window's tree, as `VideoHost` puts its picture and overlay in child windows WPF does not know of; asked by the child window's handle, UI Automation reads the overlay's boxes (`ChildWindows.Find` in the E2E infrastructure). The walk plays S3 at the four turns and checks a box on the walker and the score at the end. Live runs differ a little from run to run, so the deterministic clock stays the one compared with the baseline.


- `sample/BoundingBoxWpfApp` as described above, added to `AtcSoft.VideoSurveillance.slnx` under `sample`.
- The overlay exposes each box to UI Automation (useful for the apps' E2E walks too).
- `BoundingBoxSampleSmokeTests`: open S5, run deterministic, pause at chosen times, compare the automation boxes with the truth, read the score panel. Also open a plain recording.

**Test:** the FlaUI walk (`ATCSOFT_E2E=1`). To check early: whether FlaUI sees elements inside the `VideoHost` overlay window.

### Phase 4 — Detector v2 ✅ (L)

**Done 2026-10-19:** `BuiltInMotionDetector` (Core, behind `IMotionDetector`): a 3×3 blur, a background per pixel (running mean and variance, time-based learning rates), a lighting-change check, open and close on the mask, blobs with a smallest share of the picture, merging of near blobs, and the tracker of phase 5. CameraWall and the server use it; `FrameDifferenceMotionDetector` stays for comparison. `BoundingBox` carries an optional `TrackId`. Options come from the existing *Sensitivity*; no new settings yet.

| Scene | Today 4/s: recall, false/frame | New 4/s | Today 15/s | New 15/s |
|---|---|---|---|---|
| S1 empty, noise | 1.000, 0.000 | 1.000, 0.000 | 1.000, 0.000 | 1.000, 0.000 |
| S2 tree in the wind | 1.000, **2.286** | 1.000, **0.000** | 1.000, **3.591** | 1.000, **0.011** |
| S3 slow walker | **0.405**, 0.531 | **1.000**, 0.000 | **0.021**, 0.845 | **0.979**, 0.000 |
| S5 car + human + cat | 0.955, **1.612** | 1.000, **0.000**, 3 tracks | 0.805, **3.199** | 0.996, **0.000**, 3 tracks |
| S12 camera clock | 1.000, 0.000 | 1.000, 0.000 | 1.000, 0.000 | 1.000, 0.000 |

Scenes written after the tuning, run without changing it (unseen), with a size per key point added to the storybook for S13:

| Scene | Today 4/s | New 4/s | Today 15/s | New 15/s |
|---|---|---|---|---|
| S4 fast car | 1.000, **0.673** | 1.000, 0.000 | 1.000, **0.801** | 1.000, 0.000 |
| S6 lighting step | 1.000, 0.100 | 1.000, 0.000 | **0.405**, 0.547 | 1.000, 0.000 |
| S8 small cat far away | **0.000**, 0.000 | **1.000**, 0.000 | **0.000**, 0.000 | **0.979**, 0.000 |
| S9 stops for 4 s, walks on | 1.000, 0.082 | 1.000, 0.000 | **0.000**, 0.873 | 1.000, 0.000 |
| S13 walks up to the camera | 0.966, **0.918** | 0.966, 0.000 | **0.509**, 0.530 | 0.973, 0.000 |

S4, S8 and S9 passed at once. S13 needed growth to count as travel (a sustained change of 30 % in the smoothed size), and the lighting change now shifts the background by the overall brightness change instead of re-learning everything (which left a ghost of whoever was in the picture). Today's detector never finds the small cat: the 0.5 % gate.

Cost per picture at 800×600: about 11 ms (today's 2 ms), so about 4–5 % of one core per camera at the server's 4/s; about 2 KB allocated per picture (the working arrays are kept per detector). Latency: 0.2–0.5 s for most, 0.7 s for a slow walker or a small cat (3 % of the width first), 0.7–1 s for someone coming straight at the camera.

The settings in plain words (done 2026-10-20), end to end in all three apps and the API: *smallest object* (% of the picture, 0.02 by default; a watch zone without its own takes the camera's), *something that stops becomes background after* (seconds, 60 by default: the detector's foreground learning; the background's own 4 s stays, so a short setting cannot swallow a slow walker) and *ignore lighting changes* (on by default; off, someone filling most of the picture is seen sooner, as for a camera at a door). `MotionDetectorOptions.From` maps them, the defaults give exactly the tuning the baseline was scored with, and CameraWall has them per camera. `MinArea` is **not** converted, unlike the design above: the new detector never read it, and its stored default (10 px², 0.002 % at 800×600) is ten times below the tuned smallest object, so converting would have changed detection everywhere. It and *minimum change %* are kept for the comparison detector, deprecated in the API and left out of the dialogs, where they did nothing. Analysis size from the picture's shape (done 2026-10-20): `AnalysisWidth` is the longer side and the other follows the picture as displayed (`MotionAnalysisSize` in Core; the pipelines tell their displayed size, `MediaProbe.GetDisplaySize` for *Analyse recording*), so a 16:9 camera is analysed at 800 × 450 instead of squeezed into 800 × 600, and one turned upright at 450 × 800, never more pixels than today. The events carry the size analysed. The longer side rather than the width, as the design said: a camera turned upright would otherwise cost 2.4 times as much. `AnalysisHeight` is deprecated in the API and left out of the dialogs; the choices are the longer side, CameraWall's default now Core's 800 (it was 320 × 240), and CameraWall's tiles now take the app's size (they took a fixed 800 × 600 unless the camera had its own). The storybook runner analyses at the same shape (done 2026-10-21; the scenes are 1280 × 720, so 800 × 450 instead of the squeezed 800 × 600; the old way stays as an option). The new detector's scores moved little: the tree's false boxes 0.011 → 0.005 at 15/s, S13 at 15/s 0.973 → 0.955 with its latency 0.73 → 0.87 s, S10's walker found after 0.73 s at 15/s like S3's (it was at once); the rest are the same. The zone editors' hint (done 2026-10-21): a watch zone's empty smallest-object field shows the camera's value in CameraWall (its own or the app's, following the dialog), and "Camera's own" in the WPF and web clients, which cannot always read the server's setting (`GET /settings` is for admins). The steps on vectors (done 2026-10-21): the blur, the background and the morphology run on `Vector<T>`, giving the same bytes and floats to the bit as the plain loops at every vector width (32 and 16 bytes and without vector hardware; `VectorStepsTests` keeps the plain loops to compare). Per 800 × 600 picture: blur 1.0 → 0.14 ms, background 2.6 → 0.5 ms, open 2.4 → 0.24 ms, close 3.5 → 0.4 ms, the whole detector 8.1 → 1.7 ms; the storybook scores are unchanged. Blobs (0.5 ms) are now the largest step. The lighting check's sum stays a plain loop, as summing in another order changes it. S10 rain (done 2026-10-21): heavy rain over a walker, no box, the walker kept. No ghost of the first picture (done 2026-10-21): a pixel learns slowly only once it has matched the background since it was learnt; before that the background is the likely mistake (someone in the first picture who left), so it learns at the background's speed and the ghost goes within a second (it stayed 6-15 s). The stopped-object time honoured (done 2026-10-21, the user's choice of three): it did not do what it said. A stopped object raised its own pixels' variance and was absorbed in about 6 s whatever the setting (1 s at the 10 s minimum, where S13 fell to recall 0.48 with 0.90 false boxes). Now only the background's own pixels learn the variance, a foreground pixel keeps the background it covered and returns to it at once when what covered it leaves (no residue ghost), and the foreground's time constant is a third of the setting, so at 60 s something stopped is background after 14 s (low contrast) to 40 s (high), at 300 s after 70 to 200 s. While most of the picture differs (lighting changes not ignored) it learns at the background's speed, so a lamp switched on is taken in within seconds. Cost: the tree at 15/s 0.005 -> 0.011 false boxes a picture; in double and triple wind 0.22-0.83 (0.14-0.52 before). S13 at the 10 s minimum: 0.25 false boxes (0.90 before), as someone who stands longer than that is taken in before walking on. `StoppedObjectTests` pins the meaning. S10 snow and S14 door camera (done 2026-10-21). A scene can now be marked known-bad and can name the detector settings it is meant for (see the storybook file). Snow (large flakes near the lens, a walker through them) is in the suite marked known-bad: 2.8 false boxes a picture at 4/s and 11.9 at 15/s at the default; none at either rate with smallest object 0.1 %, which loses the small far cat; object labels are the real answer. S14: a person at a door comes close until the picture is mostly them, waits 2.5 s and leaves. It runs with lighting changes not ignored, as the setting's help says for a door: recall 1.000 with 0.031 false boxes a picture at 4/s, 1.000 with 0.012 at 15/s (a leg split off for a moment). At the default the person is taken for light while close (recall 0.52 at both rates, no false box): the known trade-off. S14 found a ghost: while most of the picture differs, the foreground learns at the background's speed (the lamp case), so the close person was taken in, their pixels forgot the background they covered, and when they walked off a ghost of 928 × 558 stayed (0.44 false boxes a picture). A covered pixel now keeps the background it covered until the picture is that background again, also after what covered it was taken in. So a car parked longer than the stopped-object time that drives off leaves no ghost either, and a lamp taken in and switched off is background again at once. No other scene's score moved. Real cameras, a first evening (2026-10-21, 19:33-20:06, dark outside, IR on the outdoor cameras): five of the user's cameras (H.264 and H.265, 1280 × 720 to 2560 × 1440, one turned 270°), 30 minutes, with a scratch tool that runs the detectors on the live grey pictures as CameraWall does (15/s) and as the server does (4/s) and saves a picture with the boxes whenever a track starts. The detector takes 1.8-2.4 ms a picture (800 × 450, live). An empty kitchen turned upright (450 × 800), its clock running down the side: no box in 20 minutes, the clock included. Another kitchen: one box of 1.5 s at the far table, where the living room starts. Living room: a cat grooming in front of the camera and someone at the table, boxed 35 % of the time; the cat sometimes in two boxes (head and back, 30 px apart, more than the 8 px that joins blobs). Garden, at night through a net: cars and their headlights on the street, a neighbour at a lit door, boxed 12-16 % of the time. Front door, with a wet lens (drops all over the picture): a car on the road, and short boxes of 1-2 s at the picture's left edge, the only doubtful ones. Today's old detector boxed none of it (0.0-0.5 % of the pictures: its 0.5 % gate). Real cameras by day (2026-10-28, from recordings): CameraWall's recordings of 2026-10-17 around noon (three Tapo cameras, 1920 × 1080 to 2560 × 1440, one upright; 20 minutes) and 2026-10-13 16:31 (a balcony camera looking through a cat net at a car park, bright sun, wind; 36 s), read as *Analyse recording* reads them, at 4 and 15 pictures a second, default settings. The detector takes 1.7-2.2 ms a picture, the same after the change below. People and both cats are boxed; the kitchens are quiet (0-6 % of the pictures). Wrong boxes, by kind: **wind** (the balcony: the net and a tree top, 39 % of the pictures boxed at 4/s, 50 % at 15/s; the upright kitchen: trees through the window blinds, a few short boxes); **the first seconds** of a recording, while the background is still empty (the balcony's 11-48 % boxes in its first 10 s; *Analyse recording* starts every segment cold); **the camera's clock** when its minute changes (16:31 to 16:32, a box of 2 s). Why wind got through: a false track confirms on its centre, which jumps 25-50 px as fragments of foliage join and split (a walker confirms on 18-24 px, so the travel cannot simply go up; smoothing the centre cost S3 and S5 0.3 s of latency for little gain), and the stopped-object fix of 2026-10-21 froze the variance of every covered pixel, so a swaying pixel only ever learnt from its small differences. **Strong wind taken in** (done 2026-10-28): a covered pixel that goes back to the background gains flicker (0.5 a return, fading at the background's speed); with more than 0.3 it learns its variance from its swings, fully at 1. Something that stops never goes back, so it keeps the stopped-object time (`SwayTests`). A walker's swinging legs flicker too, so the boxes of tracks that have travelled three times the confirming distance (`MotionTracker.CertainTravel`, 9 % of the width) are held out of it in the next picture; holding every track kept a false one on a branch alive (its own pixels never learnt), at twice the distance S15 failed, at five times S13 split, so those two scenes pin the value. Vector and plain step equal to the bit (`VectorStepsTests`, with a swaying stripe and held pixels). New scene **S15 strong wind**: S2's trees in five times the wind for 40 s, the first 15 s not judged (the sway is learnt), a walker at 28-38 s. A tuned scene, not an unseen one: the values above were chosen on it, S13 and stronger-wind variants of S2. The detector before the change: 0.19 and 1.35 false boxes a picture after 15 s at 4 and 15/s; now none, the walker found within 1 s (0.73 s in S3: in front of swaying trees it takes longer). Every other scene scores as before or better (S5 at 15/s: recall 0.996 to 1.000; snow, still known bad, 2.80 to 2.53 and 11.85 to 9.24). Memory: a float per analysed pixel and a byte for the held boxes, about 1.8 MB more per camera at 800 × 450. Real recordings: the balcony 39 % to 22 % of the pictures boxed at 4/s and 50 % to 16 % at 15/s, the rest of its boxes in its first 10 s; the living room's cats still boxed; the other cameras as before. The camera clock (done 2026-10-28): its box on the balcony is gone with the wind fix (it had been joined to wind boxes beside it); the storybook clock takes a start time (`startsAt`) and S12 now rolls over from 12:59:59 to 13:00:00, every digit after the hour at once: no box, at either rate. The first seconds of a recording (done 2026-10-28, the user's choice of three): *Analyse recording* starts every file cold, and the balcony's first 10 s had boxes of up to half the picture; with the wind fix one of them even lasted 6 s, as a track that travelled far was held. `BuiltInMotionDetector.WarmUpSeconds` (0 by default, so live cameras are as before) makes the first seconds after its first picture learn the scene only, through the lighting change's settle; `RecordingAnalysis` sets 3 s. The balcony: 21.7 % to 11.2 % of the pictures boxed (5 s would give 8.4 %). A motion-started recording loses the first 3 s of its boxes on the timeline. One box for an animal (tried 2026-10-28, not kept): joining a small piece to a much larger blob near it (at most a share R of its area, within G of its longer side) joins S5's cat to the car whenever they are close (3 tracks become 4-5, at every setting that joins anything; at R 0.05 S5 holds but nothing is joined any more, not the cat's head at 17 % of its back). The living room's recordings show why a plain gap rule cannot work either: two cats 12 px apart are two boxes, rightly, while one cat behind chair legs splits with a 10 px gap. Telling those apart needs to know what the pieces are (object labels, the AI plan) or how they move together over time; a rare second box on one animal stays. Trees seen through a window (looked into 2026-10-28 with the sample's diagnostics): the upright kitchen's three recordings of 2026-10-17 (13 minutes), read as *Analyse recording* reads them at 15/s, are now boxed in 5.0, 0.0 and 0.6 % of their pictures (5.1, 9.8 and 0.6 % before the wind fix and the warm-up). What is left from the windows is four short tracks (0.1 to 3 s, 0.1 to 0.3 % of the picture) over the blinds; the rest is something coming in at the bottom of the picture (it travels 27 times the confirming distance). An ignore zone over the two windows takes the window tracks away and keeps the one coming in, so this is a camera's zones, not a change of the detector: a gust too short to learn and a small thing passing outside look the same to it. Done 2026-10-28; real cameras in rain are on the roadmap's list of hands-on checks.

- `IMotionDetector` (the seam) and `BuiltInMotionDetector` (stateful, in Core, pure C# with `Vector128` / `Vector<T>`): steps 2–9 of the pipeline above. An OpenCV detector can later plug into the same seam from a package of its own.
- Both services and the sample app use it; `MotionFrameAnalyzer` remains only until the services have moved.
- New settings in plain words: *smallest object* (% of the picture), *learning speed*, *ignore lighting changes*. End to end: Core, `MotionDetectionPolicy.Resolve`, the overrides, both settings dialogs, the API contract, `SettingsRoundTripTests`, four languages, the web copy in `Logic/` with its equality test.
- `MinArea` (px²) is replaced by the relative minimum; an old value is read and converted.
- Analysis size from the width only; the height follows the aspect ratio.

**Test:** scenes S1, S2, S3, S4, S6, S8, S10, S11 and S12 pass their thresholds; S5 has at most one box per object per frame (the track count comes in phase 5).

### Phase 5 — Tracker ✅ (M)

**Done 2026-10-19:** `MotionTracker` (constant-velocity guess, matching by overlap or nearness, confirmation after 0.4 s and 3 % travel, end after 1 s unseen); motion is on while a confirmed track exists, so webhooks and motion recordings already start on a confirmed track. `MotionBox.trackId` in the spec and on `GET /events`; Core's `BoxSmoother` follows a box by its id first; the web client smooths by id too (`Logic/MotionBoxSmoother`, kept equal to Core by a test). **Done too (2026-10-19):** the ids reach the WPF overlays: `MotionBoundingBoxOverlay` takes boxes with their ids (`MotionBoxes` and `UpdateMotionBoxes`), `MotionBoxMapper` keeps the id, and CameraWall's tiles, both full-screen windows and the client's tiles pass it on, so two boxes that cross keep their own tracks. A host test sees `trackId` on `GET /events`. (the `Rect` ones stay, and the new overload is named `UpdateMotionBoxes` so existing callers keep compiling). **Left as it is:** S9 still allows one id switch per object (`maxIdSwitchesPerObject: 1`), not none; the overlay's cap of 10 drawn boxes stays as a guard for the UI, though the bullet below planned to drop it (the new detector rarely gives more than a few).

- A SORT-style tracker written from the paper (the reference code is GPL): prediction, IoU or centre matching, confirmation after N hits **and** a minimum net travel, end after M misses.
- Motion is active while a confirmed track exists. `MinimumChangePercent` stays readable in old files but no longer decides (deprecated, like the server's `language`).
- `MotionBox` gets an optional `trackId` in the spec; both clients smooth **by id** (WPF overlay; web: a `Logic/` class with tests). The render cap goes; the detector's output is already small.
- Webhooks and motion recordings start on a confirmed track, not on a changed percentage.

**Test:** S5 gives exactly three tracks; S9 keeps the id; S2 gives none; a host test sees track ids in the event stream.

### Phase 6 — Masks and zones ✅ (L)

Covers the backlog row *Motion zones / exclusion masks*.

- Per camera: polygons that are **ignored**, and zones with **their own sensitivity and smallest object**, in fractions of the shown picture.
- Drawn on a snapshot in both camera dialogs and in the web camera form.
- A zone's colour is reused on the timeline (the Google Home idea).

**Test:** a storybook scene with a mask over the tree; dialog steps in the E2E walks.

**Done so far (2026-10-19):**

- The detector: a camera's ignore zones (`MotionZone`, `ZonePoint` in Core, kind `Ignore` or `Watch`, corners in fractions of the picture as shown) are filled in at the analysis size (`ZoneMask`, by pixel centres, even-odd, so a concave shape keeps its notch) and are never foreground; they do not count towards the changed share, so the activity level and the lighting check leave them out too. Storybook: the swaying trees of S2 masked leave no more activity than the empty scene's noise and no boxes; the walker of S3 is missed inside a mask and boxed once out of it.
- Stored with the camera: `CameraConfiguration.MotionZones` in Core and in Wpf.Core (CameraWall's `cameras.json`, written only when there are any). In the spec, `motionZones` on `Camera` (always sent), `CreateCameraRequest` and `UpdateCameraRequest` (left out keeps them, `[]` removes them), with limits (16 zones, 3 to 32 corners, 0 to 1).
- Rotation: a rotation changed without new zones turns the zones with the picture (`MotionZoneGeometry.Turn`), so they stay on the same part of the scene; zones sent with the rotation are taken as drawn.
- To the detector: both apps start the analysis (watching and index-only) with the camera's zones, and a running analysis takes edited zones at once without forgetting its background (`IMotionDetector.UseZones`, `IMotionDetectionService.UseZones`); on the server the camera update hands them over.

- The editors (done 2026-10-20): the zones drawn on a snapshot in all three apps. `MotionZoneEditing` (Core, a `Logic/` copy kept equal by a test) adds, moves and removes zones and corners within the spec's limits and finds what is under the pointer; `MotionZoneDraft` keeps a form's zones turned with the rotation chosen there, always from the zones as last drawn (turning step by step drifts: 0.1 turned twice by a half is 0.09999999999999998), and tells whether they changed. The WPF editor (`MotionZoneEditor` with the drawn `MotionZoneCanvas`, Wpf.Core) is a tab of CameraWall's camera dialog and a box in the client's; the web one an SVG panel of the camera form (`MotionZoneEditorState`, `MotionZoneShapes`). A zone is dragged by its inside, a corner by its handle; a double click adds a corner on an edge or removes one; buttons do the same from the keyboard. Snapshots: CameraWall takes the tile's picture (a USB camera opens once), else opens the camera; the API clients ask the server, which now takes a recorded camera's picture from its recording instead of opening it again. The picture is shown turned to the rotation chosen. CameraWall saves the turned zones and hands them to the running analysis (`UseZones`); the API clients send zones only when they changed, so the server keeps and turns the stored ones. Each snapshot an API client takes is also saved in the server's snapshot folder, as every `GET /cameras/{id}/snapshot` is; left so: the clean-up removes old ones only when it includes snapshots (off by default). Texts in four languages; a step in each walk (the live test camera serves one client, so the API walks accept a snapshot that is not there; the web walk got one and checks it is drawn, and drags a zone with the mouse; CameraWall's double-clicks a corner off and on). A host test records a turned test stream that serves one client and gets its snapshot, 240 x 320, from the recording.

- *Analyse recording* with the camera's zones (done 2026-10-20): `RecordingAnalysis.DetectorFor` (Core) gives both apps the camera's own settings and its ignore zones, turned from the camera's rotation now to the one the recording was made at (its display matrix). CameraWall's browser finds the camera by the recording's folder, so it no longer analyses with the app's settings. A recording of a camera no longer known is analysed with the app's settings and no zones.

- Watch zones (done 2026-10-20): `MotionZone.Sensitivity` (0 to 100, the camera's formula, `MotionDetectorOptions.Thresholds`) and `SmallestObjectPercent` (of the picture, default 0.02), both optional (empty is the camera's sensitivity and the default size). `ZoneMask.Watched` fills in which watch zone each pixel is in (an ignore zone wins, of two watch zones the one drawn last); the background takes each pixel's zone's thresholds (`BackgroundModel.ApplyZoned`), a blob whose middle is in a watch zone that zone's smallest object. Without watch zones the detector runs the path it had and the storybook baseline is unchanged. Storybook (`WatchZoneTests`): a cat smaller than the camera takes, and a faint cat below its thresholds (storybook animals take a `shade` now), are boxed inside a watch zone over their path and missed outside it; noise in a zone at full sensitivity gives no boxes; a watch zone without values detects as the camera does. In the spec (`sensitivity`, `smallestObjectPercent` on `MotionZone`, with limits), the server, both clients and CameraWall's `cameras.json`. The editors pick the type and, for a watch zone, the two values (read in the user's culture, `MotionZoneEditing.TryRead…`, with the web copy). The smallest object is not a camera setting yet (phase 1's open item); a watch zone's default is the detector's.

- The zone colour on the timeline (done 2026-10-20): a box gets the name of the watch zone its middle is in (`BoundingBox.Zone`, not sent with motion events); the activity index keeps a second's zones (`"z"`, left out when none; still version 1, as its reader skips what it does not know, which a test holds), a stretch the zones of its seconds (`ActivityBucket.Zones`, `RecordingActivityBucket.zones` in the spec), and an event the zone of most of its stretches, the earlier on a tie (`ActivityEvent.Zone`, `ActivityTimelineLayout.MainZone`, with the web copy). A watch zone gets a colour by name in the editors (the 16 named colours; the zone is drawn in it too). The timeline under the player draws an event in its zone's colour, orange without one, in CameraWall's recordings browser and both API clients, which load the camera's zones now with the activity.

**Left as they are:** zones are known by name, so renaming a zone loses its colour on older recordings, and of two zones with one name the first one's colour counts. CameraWall's Timeline window, the Timeline views' lanes and *play all* tiles keep orange events.

### Phase 7 — Activity index ✅ (M)

**Done 2026-10-19:**

- Core: `{video}.activity.jsonl` beside the video in every layout (`RecordingPathHelper.BuildActivityPath`). A header `{"v":1,"start":…}`, then one line per analysed second: `t` offset, `a` level (the largest changed share, 0–100, the tree included), `n` confirmed tracks, `ids`. A second without a line was not analysed, which is not the same as quiet. `ActivityAccumulator` (fed by every analysed picture, not by motion events), `ActivityLogWriter` (a line as soon as the second is over), `ActivityLogReader` (reads while written, skips a cut-off line), `ActivityDownsampler.ByMax`.
- The index is a companion of its recording (`GetCompanionPaths`): deleted by the remover, the clean-up and the disk-space guard, protected while recorded, moved by the reorganiser.
- Both apps: `ActivityIndexRecorder` (Core) writes the index of every recording, a new file per segment; each app runs it from a hosted `ActivityIndexService`. It needs only `IActiveRecordings`, which both recording interfaces now extend. A recorded camera is analysed even when it is not watched for motion, in an index-only mode of `IMotionDetectionService` (`StartIndexing`) that reports no motion: no badge, webhook or motion recording comes of it. In CameraWall a tile starting, restarting or stopping its motion watch leaves the index running.
- `GET /recordings/activity` with `hasIndex` false for a recording without one. Unit tests, handler tests, and a host test that records the live test stream and reads the activity back.

**Done since, and known limits left as they are:**

- ~~A setting to turn the index off~~ done 2026-10-19: `RecordingSettings.EnableActivityIndex` (on by default), in the spec, both API clients and CameraWall's settings (Capture), asked by `ActivityIndexRecorder` as each recording or segment starts; turned off, no index and no index-only analysis.
- *Analyse recording* for older recordings: done in CameraWall 2026-10-19. `RecordingAnalysis` (Core) runs the detector over the pictures of `GreyFrameFileReader` at 4 a second and writes the index beside the recording through a `.part` file, so a recording never has half an index; the time in the name is read in the zone it was written in. CameraWall's recordings browser has an *Analyze recording* button for a finished recording without an index (with the app's motion settings, not the camera's own); the walk analyses a seeded recording. On the server too (done 2026-10-19): `POST /recordings/activity` (operators and admins) queues a recording without an index and answers 202; 409 when it has one, is still being made, or is queued already. `RecordingAnalysisService` analyses the queue one recording at a time, each with its camera's own motion settings, reading names as UTC. Both API clients have an *Analyze recording* button (the WPF client's Recordings view, a row action on the web page) and wait until the index is there, then refresh the list. Handler tests and a host test that analyses a real file. Measured: ten minutes of 1080p H.264 (libx264, 25 fps) took 169 s with two decoder threads on a 16-thread machine, so an hour takes about a quarter of an hour; the decoders are held to two so recording and live viewing go on. `RecordingActivity.isBeingAnalysed` lets a client tell a failed analysis from one still going, and the clients say so. **Open:** an event on `GET /events` when an analysis ends, instead of the clients asking every three seconds; a `.part` file left by a crash is not cleaned up.
- The time base is the session's start, not the first keyframe the recorder waited for, so the index may run up to a GOP ahead of the video. Phase 8b, which joins segments, should measure that against a real camera.
- Analysis costs a PNG capture per picture for every recorded camera; a camera analysed only for the index is held to 4 a second in both apps. Phase 2's grey frames would make it cheap.
- The `MaxRecordingStorageGb` cap counts the videos only, not their thumbnails and indexes (an index is about 2 MB per camera per day). The clean-up's orphan pass removes an index whose video is gone.

### Phase 8 — Activity timeline ✅ (L)

**Done 2026-10-19 (both WPF apps and the web client):** `ActivityTimelineLayout` in Core (bar height by square root, event bands of at least 3 px, next and previous event); the drawn `ActivityTimeline` control in Wpf.Core above the player's seek bar, with previous / next event buttons (texts in four languages). CameraWall reads the index beside the file, the server client asks `GET /recordings/activity`; the client's Recordings view got a Play button (it played only on a double click, which a keyboard cannot do). The web player draws the same timeline as SVG under the video (its layout a `Logic/` copy kept equal to Core's), with the same jumps; it shows also when the browser cannot play the file. SVG numbers are written with a point in every language (`SvgNumber`), which fixes the live view's motion boxes in Danish and German too. Unit tests, a drawn-to-bitmap control test, and a step in all three walks. *Play events only* (done 2026-10-19), in the desktop player and the web player: a toggle beside the event buttons; the player keeps playing inside an event (a second before it, two after), jumps over the quiet stretches, skips an analysed recording in which nothing happened and, after the last event, moves on to the next recording of a timeline or stops (`ActivityTimelineLayout.EventsOnlyTarget` in Core, a `Logic/` copy kept equal). A recording without an index plays as it is. The web timeline got its playhead line. Sparklines in the recordings lists (done 2026-10-19): `ActivitySparkline` (Core) turns a recording's activity summary into 24 levels by max; the server's `GET /recordings` carries them as `activity` (left out without an index, read in parallel through the summary cache), CameraWall's browser reads them beside the files, and all three lists draw a small bar chart (Wpf.Core `ActivitySparklineBars`, the web `Logic/ActivitySparklineShapes`), orange from `BusyLevel`. Zoom of the per-recording timeline (done 2026-10-20), in the desktop player and the web player: the mouse wheel zooms around the place under the mouse, *Zoom in* / *Zoom out* buttons around the player, from the whole recording down to a minute; the part shown follows the player when it plays or is moved out of it, but a look elsewhere stays (`ActivityTimelineZoom` in Core, a `Logic/` copy kept equal). Events, jumps and *play events only* stay in fractions of the whole recording; only drawing and clicks go through the part shown. The activity is now loaded in one stretch a second (400 to 4 000, the server's limit), so a minute zoomed in has its seconds, and drawn at most one bar per column, by max; the web player, opened without the length (from the Timeline page), loads it again once the video tells it. Events are found in those seconds too: before, an hour in 400 stretches joined bursts less than about 9 s apart into one event, now two passers-by 3 s apart are two, so *next event* and *play events only* stop more often on long recordings. Merging events closer than a few seconds (the padding the design names) came after, see below. The web timeline listens to the wheel itself, as Blazor's wheel listener cannot stop the page scrolling. Hover (done 2026-10-20, without the thumbnail): resting the mouse on the timeline tells the time there (clock time when the recording's start is known), how many things moved at once (*Moving: 2*, *Nothing moving*, *Not analysed*) and the watch zone of an event there, from the stretches under the mouse as wide as the narrowest band (`ActivityTimelineHover` in Core, a `Logic/` copy kept equal; `ActivityHoverText` in each client, texts in four languages). The desktop control's tooltip follows the mouse with the hint under the words; the web player shows the words above the timeline. The hover's picture (done 2026-10-20), in all three clients: preview pictures of each recording, made when it or its segment ends (and again when it is analysed, and for an older recording when it is first played), in one JPEG beside it (`{video}.previews.jpg`, its index `{video}.previews.json`): a grid of tiles evenly over the recording (a second apart, at most 600, the longest side 160 px, turned as displayed) and a tile of the busiest second of each event (the most moving, then the most changed), decoded exactly, not from key frames. The tooltip (desktop) and the box above the timeline (web) show the event's tile on an event, else the nearest one, and the time of the picture; moving the mouse fetches nothing, as the JPEG is loaded once. Made by `RecordingPreviewBuilder` (VideoEngine.Windows, GDI+, two decoder threads) through `RecordingPreviewMaker` (Core: one at a time, never a recording still being written), on the server by `RecordingPreviewService`, served by `GET /recordings/previews` and `/recordings-files/` (with the media token). They are companions of the recording: moved, protected, deleted and cleaned up with it. A recording still being written has words only until it ends. This differs from the design above (the event's peak frame, stored when the event ends): a strip of the whole recording answers the hover anywhere, not only on events, and the event tiles still give each event its picture. Events close together are joined (done 2026-10-20), in all three clients: moving stretches with 5 s or less of quiet between them are one event (`ActivityTimelineLayout.FindEvents(buckets, durationSeconds)`, the gap in seconds whatever the stretches, a `Logic/` copy kept equal), the rule of the timeline of every camera (`ActivitySummarizer.MergeGapSeconds`), so a recording's events are the same as the day's. Someone stopping for a moment is one event for *next event*, *play events only*, the bands and the event's preview tile. The event tile for notifications and webhooks (a crop around the track) is left to the suggestion *One thumbnail per event*.

- A shared `ActivityTimeline` control in Wpf.Core (both WPF apps) and a web component with its logic in `Logic/`.
- Bars, bands, hover with thumbnail, next and previous event, *play events only*, zoom, sparklines in the recordings lists.
- All texts through `Translations`, in four languages.

**Test:** unit (layout maths: a 5 s event in 60 min is at least 3 px; max downsampling); E2E steps for the recording window and the web Recordings page.

### Phase 8b — Server timeline ✅ (M)

**Done 2026-10-19:** each activity index is summarised once (`ActivitySummarizer`: 10 s stretches by max, events to the second, moving seconds 5 s apart joined) and kept in `ActivitySummaryCache` (capped, a changed file is read again); `TimelineBuilder` builds a camera's lane from its recordings, with **coverage from the video files** (the name's time to the last write, now for one in progress), so a recording without an index is *recorded but not analysed*, not a gap, and events are joined across segments. `RecordingCatalog` is the one walk of the recordings folder, shared with the listing. `GET /timeline` (span at most 7 days, checked in the handler; no `x-cache`, a span reaching now grows) and `GET /timeline/events` (`after` or `before`, looking at most 7 days away). Reading an index is three times faster than in phase 7 (one reader per line): a cold day of ten cameras takes under a second, summaries are read in parallel. Unit, handler and host tests (two segments of the live test stream, one lane without a gap). **Moved to 8c:** the live `activityStarted` / `activityEnded` events and the reasons of gaps (offline, not recording, disk full), which the view needs. **Not built:** the day files of decision 11, see there.


- `TimelineIndex` in Core: builds a camera's day from its activity files (coverage, gaps, 10 s buckets by max, merged events). Pure and shared, so CameraWall can use it later.
- The server keeps the day indexes in `{data}/timeline/` and in memory, appends while recording, drops deleted recordings, rebuilds from the activity files when missing.
- `GET /timeline` and `GET /timeline/events` in the spec (roles, span maximum, caching); `activityStarted` / `activityEnded` on `GET /events`.

**Test:** unit (index from activity files; a gap where no recording exists; max downsampling; next / previous event across days); a host test records two segments of the live test stream with a moving object and reads one lane with coverage and an event back; the event stream test sees `activityStarted`.

### Phase 8c — Timeline view of every camera ✅ (L)

**Done 2026-10-19:** a *Timeline* page in the web client and a *Timeline* view in the WPF client (ribbon, Ctrl+7): every camera as a lane over the last 24 hours or a chosen day (midnight to midnight in the viewer's zone, 23 or 25 hours when the clocks change), with gaps, recorded-but-not-analysed stretches, bars, orange event bands, a red now line and hour marks (`TimelineMarks` in Core, a `Logic/` copy kept equal). A click plays the recording that covers that moment from that far in (coverage now carries the recording's own start); previous / next event jump across recordings through `GET /timeline/events`. Wpf.Core has the drawn `CameraTimelineLane` and `TimelineMarksBar`, ready for CameraWall. Unit tests, drawn-to-bitmap control tests, and steps in the web and WPF client walks. When a recording ends the player moves on to the camera's next recording in the span, gaps skipped (WPF player window and web dialog alike). Both views grow live while they show a span up to now: the server sends `activityChanged` on `GET /events` (one type, `isActive` true or false, instead of the planned `activityStarted` / `activityEnded`) from every recorded camera, and the views reload on it and on recording changes, at most every five seconds. Zoom in and out around the place last played (it keeps its place on the screen) and move earlier or later by half a span, from ten minutes to seven days, never past now (`TimelineZoom` in Core, a `Logic/` copy kept equal). Gaps tell their reason (done 2026-10-19): the server keeps a history per camera of online / offline, recording started / stopped and stopped by the disk-space guard, plus its own starts and stops (a crash is noted from a heartbeat every 30 s), in `{data}/availability` (`AvailabilityHistoryService`, `AvailabilityLog` in Core); `TimelineGaps` (Core) finds the gaps from the coverage and explains each from that history (a gap is cut where the reason changes), and `GET /timeline` gives them per lane (`offline`, `notRecording`, `diskFull`, `serverDown`, `unknown` before there was a history). Both clients colour a gap by its reason, name it on hover and show a legend. *Play all from here* in the WPF client (done 2026-10-19): a button on the Timeline view opens a grid of the lanes shown (at most 9 tiles) from the place last clicked or played, else from the first recording in the span; each tile plays its camera's recordings one after another, and `PlayAllPacer` (Core) keeps them roughly in step: the clock is the slowest tile playing, a tile more than 1 s ahead is held until it is within 0.25 s, a tile loading longer than 15 s is given up (shown as unavailable), and with no tile playing the clock jumps to the next recording of any camera. One pause for all. The window (`PlayAllWindow`) is in Wpf.Core, ready for CameraWall. The web client has the same as a dialog on the Timeline page, with at most 4 tiles (a browser opens six connections to a server, one of them the event stream); its pacer is a `Logic/` copy kept equal by a test that runs the same ticks through both. A tile counts as playing only once its picture is near where it was asked to start (`PlayAllStart`), a tile whose picture stops moving for 15 s is given up like one that does not load, and a recording that fails is skipped for the camera's next. Steps in both walks; the walks record one camera, so tiles kept in step by real players are still to be checked by hand (see the table at the end). CameraWall has its own *Timeline* window (done 2026-10-19; ribbon View, Ctrl+T) over its own folders: `LocalTimeline` (Core) finds each camera's recordings in the global folder and the cameras' own folders (a file belongs to the camera whose folder it is in, below the innermost of them), reads the time in a name in the zone it was written in (CameraWall names in local time; the activity index holds an instant, so bars and bands line up), takes a recording in progress from the active sessions, and feeds `TimelineBuilder`; zoom, move, previous / next event, a click that plays on across the camera's recordings and *play all from here* (the Wpf.Core `PlayAllWindow`), growing while open on recording changes and every 15 s (it does not hear the activity events the server sends). A step in the CameraWall walk over the seeded recordings. **Left out:** gap reasons in CameraWall (it keeps no history of when a camera could be recorded, so its gaps are plain); a renamed camera's older recordings (a folder no camera owns) get no lane; on the night the clocks go back a name in the repeated hour is read as its first time.


- Web: a *Timeline* page with its logic in `Logic/` (lane layout, zoom, now line); WPF client: a *Timeline* view using the Wpf.Core control in lane mode.
- A click plays from that moment and moves on across segments; *play all from here* for a layout.
- From 8b: `activityStarted` / `activityEnded` on `GET /events`, so an open timeline grows without polling; the reasons of gaps (offline, not recording, disk full) from the connection and recording events.
- Texts through `Translations`, in four languages.

**Test:** unit (lane layout, the click-to-recording mapping across segment boundaries, live growth from events); E2E steps for the web page and the WPF view against the test server.

### Phase 9 — Feedback loop ✅ (M)

**Done 2026-10-28:** the review file in Core (`Helpers.Review`: `RecordingReview`, `RecordingReviewFile`; `{video}.review.json` through `RecordingPathHelper.BuildReviewPath`, a sidecar, so it moves, is protected and is deleted with its recording). Times are positions in the video; the recording is named by SHA-256; the detector settings and zones it is analysed with are in the review. Scoring by count (`ReviewScorer`, Storybook): only marked stretches are judged, a picture expects the counts over it added up, a box beyond that is false, the first second of a stretch with things moving is not judged. `ReviewRunner` runs a reviewed recording as *Analyse recording* does (its settings and zones, the 3 s warm-up, 4 a second). `CorpusTests` runs every review under `ATCSOFT_BBOX_CORPUS` with its recording and fails on a worse score than the accepted one (`accepted-scores.json` in that folder, outside git with the recordings and reviews: they show the user's cameras). `ReviewedRecordingTests` judges S3 encoded by the FFmpeg tool. Review mode in the sample app (mark start, add stretch with verdict, count and label, remove, save; the score panel scores by the review), walked by `BoundingBoxSampleSmokeTests`. The walks' pictures are now of their window alone (`WindowPicture`, `PrintWindow`), never the rest of the desktop. **Changed from the design:** the format keeps the verdicts and counts and an optional box, but scoring uses counts only, as a person marks stretches, not boxes. Verdicts from the timelines (done 2026-10-28): a right-click on a recording player's activity timeline offers, on an event, *Boxed correctly* and *False alarm* over it, and anywhere *Missed something here* over five seconds around the place (`TimelineVerdicts` in Core, a copy in the web client's `Logic/` held equal by a test). CameraWall writes the review itself (`RecordingVerdictSavers.ToFile`, with the camera's settings and zones); the WPF and web clients send it to the server, `POST /recordings/review/verdicts` (operator and admin, audited, not retried), which adds it with the recording's camera's settings and zones (`IRecordingReviewWriter`). One review is written at a time; one that cannot be read is not written over. Each segment played after keeps its own recording's saver. Texts in four languages. The three walks give a verdict. The settings sweep (done 2026-10-28): `StorybookSweep` tries combinations of the settings a person can change (sensitivity 10 to 70, smallest object 0.01, 0.02 and 0.05 %, stopped-object time 60 s) on top of each scene's own settings at 4 and 15/s, and over the reviewed recordings, ranks them and writes a Markdown report; each scene is drawn once per rate (`StorybookRunner.Draw`), so the whole catalogue takes about a minute. In the sample app as *Sweep scenes…* and `--sweep folder` (`--corpus folder` or `ATCSOFT_BBOX_CORPUS`), walked by `BoundingBoxSampleSmokeTests`. The first sweep over all fifteen scenes: the shipped defaults rank first, meeting all 28 judged scene runs; every other combination misses at least one (a smallest object of 0.05 % loses the small far cat, sensitivity 50 and up gives the tree false boxes). The scenes were tuned with the defaults, so this mainly says nothing near them does better; the reviewed real recordings are what can say otherwise. **Left to the user:** reviewing real recordings (the sample's review mode or the timelines' right-click) into the corpus.

- Review mode in the sample app; `*.review.json`; review → truth; the corpus test with `ATCSOFT_BBOX_CORPUS`.
- The timeline's right-click verdicts in the apps write the same format.
- A *sweep* command in the sample app: runs a grid of settings over all scenes and the corpus and lists the best defaults.

**Test:** round trip of a review file; the corpus test on one small recording.

### Phase 10 — Local AI detection ⏸️ (L)

Moved to its own plan on 2026-10-19: [roadmap-AI-support.md](roadmap-AI-support.md).

---

## ⚖️ Pure C# or OpenCvSharp?

The detector of phase 4 can be written in plain C# or built on [OpenCvSharp](https://github.com/shimat/opencvsharp), the .NET wrapper of OpenCV. This is the biggest technical choice in the plan, so here are both sides.

### What the detector needs

| Step | Size of the job in plain C# |
|---|---|
| Scale the grey picture, keep the aspect ratio | Done by FFmpeg's swscale in phase 2, so neither option is needed for it |
| Light blur (3×3 or 5×5 box or Gaussian) | A separable pass over rows and columns |
| Running mean and variance per pixel (the background) | One pass with `Vector<T>` |
| Difference against the background, threshold | Same pass |
| Morphology: open, close (3×3 or 5×5) | Separable min and max passes |
| Connected components with their boxes | Two-pass union–find; one pass over the mask |
| Merge, filters, tracker, scoring | Plain logic, the same in both options |

At an analysis size of 640×360 a picture is 230 400 grey pixels, and each step is one or two passes over them. That is small work in either option. Later work that OpenCV would make easier: optical flow, CLAHE (contrast in the dark), several-mode background models (MOG2, KNN).

### Pure C#

| ✅ For | ❌ Against |
|---|---|
| **No native runtime in the published Core package.** Core ships to NuGet with five other packages in lockstep; every consumer would have to deploy OpenCV next to the FFmpeg they already deploy | **We write and keep the algorithms.** Bugs in edge cases (borders, odd sizes, rotation) are ours to find; the storybook scores are the safety net |
| **One language, one debugger.** Step through a frame, inspect a mask as a `byte[]`, test any step alone | **Slower on heavy steps.** OpenCV's hand-tuned SIMD (and Intel IPP) beats a first C# version on big kernels; `Vector128`/`Vector256` closes much of the gap for the small kernels this plan needs |
| **Deterministic everywhere.** The same bytes in give the same boxes on every CPU, which the scorer and the regression corpus depend on | **Nothing ready-made for later needs.** Optical flow, CLAHE or MOG2 would each be written by us, or bring OpenCV in after all |
| **No interop:** no `Mat` lifetimes, no leaks of native memory, no native crash that takes down a 24/7 server or the desktop app | **No direct reuse of others' OpenCV code.** Frigate's parameters carry over, its code does not |
| **Runs wherever .NET runs**, the web client's WebAssembly included, should the web ever analyse locally | |
| **No clash with our FFmpeg 9.** OpenCV's video I/O can bring its own FFmpeg DLLs | |
| **Small:** a few hundred lines in Core; nothing to add to the MSI installers or the server | |

### OpenCvSharp

| ✅ For | ❌ Against |
|---|---|
| **Mature and fast:** OpenCV's optimised code, OpenCL through `UMat` | **A native runtime per platform:** `OpenCvSharp4.runtime.win` alone is a 38 MB package; Linux and macOS need their own; Windows x86 is no longer built |
| **Everything is there:** MOG2 and KNN background subtractors, morphology, `findContours`, `connectedComponentsWithStats`, optical flow, CLAHE | **It cannot go into Core** without forcing the runtime on every NuGet consumer. It would need a separate package, so the desktop and the server would no longer share one plain detector by default |
| **Frigate's design maps one to one:** `accumulateWeighted`, `absdiff`, `threshold`, `dilate`, `findContours` | **Native failure modes:** `Mat` objects must be disposed; a leak or an access violation in native code ends the process, where a C# bug only throws |
| **Fast to prototype**, especially in the sample app; a large community and many examples | **Version churn:** OpenCvSharp4 is in maintenance and OpenCvSharp5 (OpenCV 5) is the new line, so a move is ahead either way |
| **Apache-2.0**, actively released (4.13.0.20260627, June 2026), x64 and ARM64 on Windows, Linux and macOS, also WebAssembly | **Possibly less deterministic:** optimised paths and threads can differ by CPU, which makes scores harder to compare across machines |
| | **Deployment grows:** the MSIs, the server and the E2E machines all carry the runtime; the full Linux package needs GTK, the slim one drops contrib and video I/O |
| | **No CUDA** in the packages; GPU help would come from OpenCL only |

### The middle way

- **Production: pure C#** in Core, behind an `IMotionDetector` interface, used by both services and the sample app.
- **OpenCvSharp as a reference, never shipped.** The sample app and the storybook tests may run an OpenCvSharp detector (Frigate's chain, or MOG2) on the same scenes as an oracle. If it scores clearly better, we see by how much, and on which scenes, before deciding to bring OpenCV in.
- **A way back in, if needed.** Because of the interface, an OpenCvSharp detector could later ship as a package of its own (for example `AtcSoft.VideoSurveillance.Detection.OpenCv`) that a host chooses. Core stays free of native code.

### When to revisit

| Tripwire | Measured where |
|---|---|
| The C# detector misses the phase 4 thresholds on the scenes, and the OpenCvSharp oracle meets them | Storybook scores (phase 1 baseline, phase 4) |
| Detection costs more than a few milliseconds per picture per camera at 2–4 pictures a second, at the analysis size | The sample app's CPU panel; a benchmark test |
| A later phase needs optical flow, CLAHE or a several-mode background model | Phases 6 and 10 |

### Decided (2026-10-19)

**Our own implementation in pure C#, for as long as it does as well.** OpenCV stays an option we control, not a dependency:

| Part | How |
|---|---|
| The seam | `IMotionDetector` in Core: a picture (grey, with its time) in, boxes and track ids out. Both services, the sample app and the scorer use only the interface |
| The default | `BuiltInMotionDetector` in Core, pure C# |
| OpenCV, if ever needed | A separate, optional package (for example `AtcSoft.VideoSurveillance.Detection.OpenCv`) with an `OpenCvMotionDetector`, chosen per camera or per app by a setting (`MotionDetectorEngine`: `builtIn` or `openCv`). Core and the default installers stay free of native code; only a host that picks it carries the runtime. Since OpenCV 4 has no C API, "natively" in practice means through OpenCvSharp's thin native layer, or a small native shim of our own if we ever need to own that too |
| Deciding by numbers | The sample app and the storybook tests can run both detectors on the same scenes. The tripwires above say when OpenCV earns its place |

Nothing of the OpenCV option is built now: only the seam is, in phase 4.

---

## ❓ Decisions Needed

| # | Question | Recommendation |
|---|----------|----------------|
| 1 | **Pure C# or OpenCvSharp for the detector?** | ✅ **Decided 2026-10-19: our own pure C# detector** behind `IMotionDetector`, as long as it does as well; an optional OpenCV detector in a separate package stays possible, chosen by a setting. See [Pure C# or OpenCvSharp?](#️-pure-c-or-opencvsharp) |
| 2 | **Storybook in JSON or YAML?** | **JSON**, read with System.Text.Json: no new dependency, and a JSON schema gives editor help |
| 3 | **Draw frames in C# or with FFmpeg filters?** | **C#**: the truth comes from the same drawing. FFmpeg only encodes |
| 4 | **Where do storybook, generator and scorer live?** | A new non-packable `AtcSoft.VideoSurveillance.Storybook` project, used by the sample and the tests. **Not in Core** (published) |
| 5 | **Activity index: a sidecar per recording or a database on the server?** | **Sidecar.** It moves and is deleted with the file, works for CameraWall without a server, and follows the folder layouts. The server can cache it in memory |
| 6 | **Run detection for every recording camera?** | **Yes, at 2–4/s, while the index is on** (default on). Without it a continuous recording has no timeline. The grey-frame path of phase 2 makes it cheap |
| 7 | **What decides "motion is active"?** | **A confirmed track.** The global `MinimumChangePercent` is deprecated and kept readable |
| 8 | **Which AI model, later?** | Moved to the [AI support plan](roadmap-AI-support.md#-decisions-needed) (its decision 2) |
| 9 | **Where do the real recordings for the corpus live?** | Outside git, in a folder named by `ATCSOFT_BBOX_CORPUS`. The review files may be in git and name their recording by SHA-256 |
| 10 | **Release the wire change (`trackId`) with the desktop client?** | Yes. It is optional, so older clients ignore it, and the server and the desktop client are released together anyway |
| 11 | **Server timeline: day files or a database?** | ✅ **Decided 2026-10-19: neither, for now: summaries in memory** (`ActivitySummaryCache`), read from the activity files on first use. Nothing to keep in step, a deleted recording falls out by itself. Day files (in the data folder, rebuilt from the activity files) only if measurement says a cold span is too slow; SQLite only after that |
| 12 | **How far back does the timeline reach?** | **As far as the recordings do.** The index follows the recordings; the cleanup and the disk-space guard remove days whose recordings are gone |
| 13 | **Gaps: what explains them?** | Coverage comes from the recordings; the reason (offline, not recording, disk full, the guard stopped it) from the connection and recording events, kept in the day index. A gap without a known reason is shown as *no recording* |

---

## 💡 Suggestions

- **A debug view in the apps too** (behind a setting): the foreground mask and the rejected blobs with their reason. Tuning without it is guessing.
- **Settings in plain words, with presets**: *Garden (trees)*, *Street*, *Indoor*. Each preset is a tested set from the sweep (phase 9).
- **Defaults from data**: run the sweep over the scenes and the corpus, and set the defaults to what scores best.
- **One thumbnail per event** (the peak frame, cropped around the track): for notifications and webhooks. The timeline hover has its picture from the previews (done 2026-10-20), whose event tiles are this frame uncropped.
- **Notifications and webhooks only on confirmed tracks**: the change that removes the tree boxes also removes the tree alerts.
- **Camera clock overlays** (date and time burnt into the picture) change every second. The travel rule ignores them; offer a mask when one is found.
- **Day and night**: record the IR switch as its own flag in the activity index; it explains a grey band in the timeline.
- **CPU per camera** in the sample app's score panel, so the cost of each step is known before it reaches a server with many cameras.

---

## ✋ To Verify by Hand

| What | Needs |
|---|---|
| A camera facing a tree on a windy day: no boxes for the tree, one box for a person walking past | A camera, wind |
| Dusk: the IR switch gives no box and no event | A camera with IR, an evening |
| Rain or snow at night | Weather |
| Boxes on the object at 0 / 90 / 180 / 270° in the grid and full screen, in both WPF apps and the web client | A camera |
| A real 60 min recording with a short event: the event is visible and *next event* finds it, in all three clients | A recording |
| The first real recording with the user's review file runs as a test | The user's recording and verdicts |
| CPU with eight cameras recording with the activity index on | A machine and cameras |
| The server timeline over a day with several cameras: coverage, gaps after unplugging a camera, events, a click that plays across a segment boundary, live growth | The server, cameras, a day |
| *Play all from here* with two or more cameras, in both API clients: the tiles stay in step (the clock within a couple of seconds of each tile), one held and resumed, the 2×2 / 3×3 grid, started from a click in the middle of a recording (the seek lands where asked), a camera whose recording fails moves on | The server, two HTTP test cameras |

---

## 📚 Sources

- OpenCvSharp repository (licence, packages, platforms, limitations): https://github.com/shimat/opencvsharp
- OpenCvSharp4.runtime.win on NuGet (version, size): https://www.nuget.org/packages/OpenCvSharp4.runtime.win
- Frigate's motion detector, `frigate/motion/improved_motion.py`: https://github.com/blakeblackshear/frigate/blob/dev/frigate/motion/improved_motion.py
- Scrypted motion detection: https://docs.scrypted.app/detection/motion-detection.html and the HomeKit Secure Video setup: https://github.com/koush/scrypted/wiki/HomeKit-Secure-Video-Setup
- Blue Iris help on its motion algorithms (quoted on IPCamTalk): https://ipcamtalk.com/threads/blue-iris-motion-detection.60238/post-616819
- Milestone's third-party terms notice: https://www.milestonesys.com/globalassets/techcomm/ltsb/provms/english-united-states/2769.htm
- Ubiquiti's open-source licence statement: https://www.ui.com/download/unifi-protect

Detection and tracking:

- Frigate, [Tuning motion detection](https://docs.frigate.video/configuration/motion_detection) (threshold, contour area, lightning threshold, improve contrast) and [Masks](https://docs.frigate.video/configuration/masks) (motion regions passed to the object detector).
- Frigate source, [`frigate/util/object.py`](https://git.laurivan.com/Mirrors/blakeblackshear.frigate/src/branch/dev/frigate/util/object.py) (region grid).
- OpenCV, [`BackgroundSubtractorMOG2`](https://docs.opencv.org/3.4/javadoc/org/opencv/video/BackgroundSubtractorMOG2.html) (history, varThreshold, shadows, background ratio).
- Bewley et al., [Simple Online and Realtime Tracking](https://arxiv.org/pdf/1602.00763) (SORT: Kalman prediction, Hungarian matching); the reference implementation is GPL.

AI models and runtime:

- ONNX Runtime, [DirectML execution provider](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider); [Microsoft.ML.OnnxRuntime.DirectML](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML) on NuGet.
- [YOLOX](https://playground.roboflow.com/models/megvii/yolox) (Apache-2.0); [RF-DETR is Apache-2.0](https://blog.roboflow.com/rf-detr-is-free-to-use-commercially/); [Ultralytics](https://docs.ultralytics.com/) (AGPL-3.0 or an enterprise licence); [Why AGPL-3.0 is a risk](https://blog.roboflow.com/why-agpl-3-0-is-a-risk-for-computer-vision-teams/) (a vendor's view).

Timelines:

- Frigate, [Review](https://docs.frigate.video/usage/review), [History](https://docs.frigate.video/usage/history), [Review configuration](https://docs.frigate.video/configuration/review), [0.14 release notes](https://newreleases.io/project/github/blakeblackshear/frigate/release/v0.14.0-beta1).
- Google, [Nest video history on a computer](https://support.google.com/googlenest/answer/9225631) and [Video history in the Google Home app](https://support.google.com/googlenest/answer/10904254).
- UniFi Protect, [5.2.42 overview](https://lazyadmin.nl/home-network/unifi-protect-5-2-42-overview/) and the [App Store release notes](https://apps.apple.com/us/app/unifi-protect/id1392492235).
- Synology, [Work with Timeline](https://www.synology.com/en-us/knowledgebase/Surveillance/help/SurveillanceStation/multievent_player) and the [Surveillance Station spec](https://www.synology.com/en-us/dsm/7.0/software_spec/surveillance_station).
- Milestone, [The main timeline](https://doc.milestonesys.com/2025R1/en-US/standard_features/sf_sc/sf_funda/sc_uitimelinemain.htm) and [Searching in XProtect Smart Client](https://doc.milestonesys.com/2025R1/en-US/standard_features/sf_sc/sf_investigate/current/sc_searchinginsc.htm).
- Blue Iris, community posts on [ipcamtalk](https://ipcamtalk.com/goto/post?id=181173) and the [UI3 timeline](https://www.ipcamtalk.net/goto/post?id=628778).
- Reolink, [Icons on the Reolink app](https://support.reolink.com/articles/22170873096729-Introduction-to-the-Icons-on-Reolink-App/); Ring, [Event History Timeline](https://ring.com/support/articles/16fks/Using-Event-History-Timeline).
- The code as read on 2026-10-18 (the file and line references above).
