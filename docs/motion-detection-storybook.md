# 🎬 Motion Detection Storybook

How the motion detector's bounding boxes are measured: scenes with known content are drawn, fed to a detector, and its boxes are scored against the truth. Every score is kept, and a test fails when one gets worse.

The plan, its history and the numbers of each step are in `plans/roadmap-boundingbox.md`. The detector and its settings are in [Motion Detection](motion-detection.md).

---

## 📖 Overview

A camera's recordings never say where the moving things were, so they cannot tell whether a box was right. A storybook can: it describes a scene (a background, distractors such as a tree in the wind, objects moving along paths), and the renderer draws it frame by frame and knows, for every frame, the box of each object from the pixels it drew.

| Part | Where |
|---|---|
| The model, the reader, the renderer, the scorer, the encoder | `src/AtcSoft.VideoSurveillance.Storybook` (net10.0, not published) |
| The scenes | `test/AtcSoft.VideoSurveillance.Storybook.Tests/Scenes/*.storybook.json` |
| Every score of every detector | `test/AtcSoft.VideoSurveillance.Storybook.Tests/baseline.json` |
| The detectors | `BuiltInMotionDetector` (what the apps use) and `FrameDifferenceMotionDetector` (the old detector, for comparison), both in Core behind `IMotionDetector` |

Each scene is run at the server's rate (4 pictures a second) and CameraWall's (15 a second), by both detectors.

---

## 🧪 The Tests

| Test class | Checks |
|---|---|
| `FrameDifferenceBaselineAt4Tests`, `…At15Tests` | The old detector's scores did not get worse than `baseline.json` |
| `BuiltInBaselineAt4Tests`, `…At15Tests` | The same for the shipped detector |
| `BuiltInDetectorExpectationAt4Tests`, `…At15Tests` | The shipped detector meets each scene's `expect` (recall, false boxes, tracks, id switches, latency) |
| `EncodedPathTests` | S5 encoded at a high quality scores as on the fast path, so the encoded path measures the detector and not the pipeline |
| `StorybookEncoderTests`, `TruthFileTests` | The encoder gives a frame per picture at its time, and the truth file reads back as written |
| `StorybookReaderTests`, scorer and renderer tests | The file format, its errors, the scoring rules |

The baseline and expectation tests share their scenes through `[MemberData]` on an abstract base class, one derived class per detector and rate. xUnit runs the cases of one class one after another and the classes in parallel, which is why they are split.

Run them:

```bash
dotnet test test/AtcSoft.VideoSurveillance.Storybook.Tests
```

The suite takes about 35 s on a developer machine (about 27 s without FFmpeg, where the encoded scenes skip).

---

## 📝 Writing a Scene

A storybook is a JSON file, `*.storybook.json`. The smallest useful one, S3:

```json
{
  "version": 1,
  "name": "S3 one person walking slowly",
  "video": { "width": 1280, "height": 720, "fps": 15, "durationSeconds": 12, "noise": 2.0 },
  "background": { "seed": 3, "distractors": [] },
  "objects": [
    {
      "id": "human",
      "kind": "human",
      "size": [0.05, 0.2],
      "path": [
        { "t": 1, "at": [0.2, 0.6] },
        { "t": 11, "at": [0.6, 0.6] }
      ]
    }
  ],
  "expect": { "tracks": 1 }
}
```

Coordinates and sizes are fractions of the picture (0–1), as shown. A size may go up to 3, for someone close to the lens.

### `video`

| Field | Default | Meaning |
|---|---|---|
| `width`, `height` | 1280, 720 | The picture size |
| `fps` | 15 | Frames a second |
| `durationSeconds` | 10 | Length |
| `noise` | 2 | Sensor noise (grey levels) |
| `bitrateKbps` | none | H.264 bitrate. With it, the scene is encoded by the FFmpeg tool and scored as a recording is read (see *Encoded Scenes* below); without it, the frames go straight to the detector |

### `background`

| Field | Meaning |
|---|---|
| `seed` | Seeds the procedural background and the noise. The same seed draws the same scene, frame for frame |
| `distractors` | Things that change but must not get a box (each has an `id`) |

| Distractor `kind` | Fields |
|---|---|
| `swayingTree` | `at`, `size`, `amplitude`, `periodSeconds` |
| `clockOverlay` | `corner` (`topRight` by default), `startsAt` (`HH:MM:SS`, `12:00:00` by default): a camera clock that ticks |
| `brightnessStep` | `atSeconds`, `delta`: the whole picture gets brighter or darker at once (lamp, IR switch) |
| `rain`, `snow` | `count`: drops per frame (rain) or flakes in the scene (snow) |

### `objects`

| Field | Meaning |
|---|---|
| `id` | Names the object in spans and scores |
| `kind` | `car`, `human`, `cat` or `dog`: a simple silhouette with some texture and a shadow |
| `size` | Width and height |
| `path` | Key points `{ "t": seconds, "at": [x, y] }`, optionally with their own `size` (someone walking up to the camera grows). Linear between key points; before the first and after the last the object is not in the picture |
| `shade` | Grey level (0–255) of a cat or a dog, for a faint animal close to the background's grey; a cat is dark (40) and a dog light (150) by default |

### `detector`

The settings the scene is meant for, where they differ from the defaults: `sensitivity` (0 to 100), `smallestObjectPercent` (0 to 100), `stoppedObjectSeconds` (10 to 600) and `ignoreLightingChanges` (S14, a door camera, sets it to `false`). Both the baseline and the expectation tests run the scene with them. The sample's *Save as storybook settings* writes this section (`StorybookWriter`), only what differs from the defaults, and leaves the rest of the file as it is; saving into a scene of the suite changes its baseline.

### `expect`

| Field | Default | Meaning |
|---|---|---|
| `tracks` | none | The number of track ids the shipped detector should show |
| `minRecall` | 0.95 | Share of must-box frames in which the object had a box |
| `maxFalseBoxesPerFrame` | 0.02 | Boxes on no object, per scored frame |
| `maxIdSwitchesPerObject` | 1 | Track id changes per object |
| `maxLatencySeconds` | 0.5 | Time allowed from an object's first must-box frame to its first box; frames in it are not scored |
| `matchIou` | 0.3 | Overlap for a box to match an object (motion boxes are padded, so 0.5 is too strict) |
| `spans` | none | Times or things that are judged differently (below) |
| `knownBad` | none | A reason. The scene shows a problem not solved yet: its scores stay in the baseline, its expectations are skipped with the reason, and the test fails once it passes, so the mark is taken off |

Each span names an `object` or a `distractor`, a `from` and `to` in seconds (the whole scene by default), a `state` and a `why`. A `dontCare` span that names neither leaves the whole picture unscored for its time (a lighting step):

| State | Means |
|---|---|
| `mustBox` | A moving object must have a box (every object's default) |
| `mustNotBox` | A box here is false (every distractor's default) |
| `dontCare` | Not scored: standing still, a lighting step. The renderer also marks an object entering with less than half of it in the picture (unless that half fills half the picture) |

The reader names the place of an error, such as `objects[0].path[1].t: …` or `video.bitrateKbps: above 0, at most 100000`.

---

## 🧮 How a Scene Is Scored

1. **Pictures.** The scene's frames are scaled as the apps scale a camera: 800 on the longer side, the other side following the picture's shape (`MotionAnalysisSize`), so a 1280 × 720 scene is analysed at 800 × 450. A picture is taken at the analysis rate (4 or 15 a second).
2. **Boxes.** The detector gets each picture with its time. The scorer sees the boxes the apps would show: kept until the detector replaces them or motion ends.
3. **Matching.** Per frame, boxes are matched to objects greedily, best overlap first. A box matches an object at `matchIou` or when it holds the object's centre. Each object takes one box; a second box on the same object counts as false, since the goal is one box per object.
4. **Score.** Recall and precision, false boxes (in total, per frame, and per distractor), latency per object, the number of track ids and the id switches.

`StorybookScore.Summary` gives it as one line, which the tests write to their output:

```text
recall 1.000, precision 1.000, false boxes/frame 0.000 (0), latency 0.27 s, tracks 3, id switches 0
```

---

## 📈 The Baseline

`baseline.json` holds recall, precision, false boxes per frame and latency for every scene, rate and detector, keyed by the file name, the rate and the detector:

| Key | Run |
|---|---|
| `s05-car-human-cat@4` | The old detector at 4/s |
| `s05-car-human-cat@15/builtIn` | The shipped detector at 15/s |

A test fails when recall drops or false boxes rise by more than 0.005, and when a scene has no entry. It works as a ratchet: scores only go one way.

After an improvement, or for a new scene, write the current scores:

```bash
ATCSOFT_BBOX_UPDATE_BASELINE=1 dotnet test test/AtcSoft.VideoSurveillance.Storybook.Tests
```

Then check the diff of `baseline.json`: only the scores you meant to change should move. The tests read the copy in the output folder, so build again before running them without the variable.

---

## 🎞️ Encoded Scenes

The fast path feeds drawn frames straight to the detector. A camera's picture has been through an encoder, and a recording is read back through the engine; a scene with `video.bitrateKbps` takes that way:

1. `StorybookEncoder` draws every frame and pipes it to the FFmpeg tool as raw grey video, encoded to H.264 (`libx264`, one thread, no B-frames, a key frame every 2 s, so a scene gives the same video on every run) at the scene's bitrate.
2. Next to the video it writes the truth, `{video}.truth.jsonl`, one frame per line (`TruthFile`): the frame's index and time, each object's box and state, and whether the whole frame is ignored.
3. `StorybookRunner.RunEncoded` reads the video back with `GreyFrameFileReader`, as *Analyse recording* does, and scores each picture with the truth of its frame.

The tests encode each scene once per run, into `%TEMP%\atcsoft-storybook-{process id}`, which is deleted when the run ends.

| Needs | Without it |
|---|---|
| The FFmpeg command-line tool (`VideoEngineBootstrap.FindFFmpegPath()`, installed with FFmpeg 9 under `C:\ProgramData\AtcSoft\ffmpeg`) | The encoded scenes and the encoder tests skip |
| The FFmpeg 9 libraries, for reading back | The test fails: the tool is there but the engine cannot load |

CI does not install FFmpeg, so the encoded scenes are only measured on a machine that has it.

S11 is S5 at 400 kbps, where the noise is smeared into blotches. The shipped detector scores it as S5. The old detector's scores move through the encoder even at a high quality, because the engine scales with swscale's area filter where the fast path scales bilinearly; that is a property of the old detector, not of the compression.

---

## 📚 Scenes

| # | File | Checks |
|---|---|---|
| S1 | `s01-empty-noise` | No box in sensor noise |
| S2 | `s02-tree-in-the-wind` | No box for a swaying tree |
| S3 | `s03-slow-walker` | A slow walker found, one track |
| S4 | `s04-fast-car` | A fast car, no ghost behind it |
| S5 | `s05-car-human-cat` | Car, human and cat at once: three tracks |
| S6 | `s06-lighting-step` | No box during a lighting step; detection goes on after |
| S8 | `s08-small-far-cat` | A small cat far away is found |
| S9 | `s09-stop-and-go` | A person stops and walks on with the same track |
| S10 | `s10-rain`, `s10-snow` | No box for rain; snow near the lens is known bad |
| S11 | `s11-low-bitrate` | S5 encoded at 400 kbps scores as S5 |
| S12 | `s12-camera-clock` | No box for the camera clock, also when every digit after the hour changes at once (12:59:59 to 13:00:00) |
| S13 | `s13-coming-closer` | Someone walking up to the camera stays one track |
| S14 | `s14-door-camera` | A door camera (lighting changes not ignored): a person close to the lens is found throughout, no ghost after |
| S15 | `s15-strong-wind` | The trees of S2 in five times the wind, 40 s: after the first 15 s (not judged, the sway is learnt) no box, and a walker found within 1 s |

S7 (rotation) is not a file: the fast path already sees the shown picture, so it is to be checked through the player, in the sample app's live clock (still open).

---

## 🖥️ The Sample App

`sample/BoundingBoxWpfApp` shows a run picture by picture: the grey picture the detector was given, its boxes in red (smoothed, as the apps show them), the objects to box in green and the distractors in orange, with the score beside it as a line and as JSON.

```bash
dotnet run --project sample/BoundingBoxWpfApp -- --storybook test/AtcSoft.VideoSurveillance.Storybook.Tests/Scenes/s05-car-human-cat.storybook.json --rate 15
dotnet run --project sample/BoundingBoxWpfApp -- --recording path/to/recording.mp4
```

Or open a file from the window. *Previous*, *Next*, *Play* and the slider move through the pictures; the rate (4 or 15 a second) runs the file again.

| Opened | Way | Needs |
|---|---|---|
| A storybook without a bitrate | The fast path: its score is the one in `baseline.json` | Nothing |
| A storybook with a bitrate | Encoded once (kept in `{data}\videos`, named by the storybook's hash), then read back | FFmpeg; the set-up dialog offers it |
| A recording | As *Analyse recording* reads it: the first 3 s only teach the scene, with its review's settings and zones or the defaults; no truth, so without a review the score only counts boxes | FFmpeg |

The score panel also shows how long the detector took a picture (with its diagnostics; a Debug build is many times slower than the apps), and that time on each picture's line.

**Debug layers** show what the detector saw (`BuiltInMotionDetector.KeepDiagnostics`, `MotionDiagnostics`): the foreground after the morphology in cyan, the ignore zones of a review in purple, and every blob it found with what became of it, in white when shown (`#3 2.1× held`: track 3 has travelled 2.1 times the travel that confirms it and is held out of the background's learning) and in grey when left out: *too small* (under the smallest object, before near blobs are merged), *too new* (seen for less than the confirm time) or *not travelled* (swaying on the spot). The line under the picture names the stage: learning the first picture, warming up, a change of light, settling after it, or analysed.

**The tuning panel** holds the four settings of the apps and the inner options that are not settings (the confirm time and travel, the merge gap, how long a track may go unseen, the background's time, the settle after light). A change runs the detector again over the pictures kept (`StorybookRunner.Rerun`), within a second, on the picture shown; *The file's settings* goes back. *Save as storybook settings* writes the four settings into the storybook's `detector` section; the inner options are never saved. Saving into a scene of the suite asks first, as its baseline follows.

The clock is deterministic: every picture is analysed before it is shown, so the same file always shows the same boxes. The engine's player plays streams and cannot pause or seek a file, so this clock does not use it.

**The live clock** (*Play live…*, or `--live 0|90|180|270` after `--storybook` or `--recording`) does: the video is streamed to the engine's player as a camera sends it (`ffmpeg -re -copyts` over UDP to this machine, so the stream keeps the video's times from 0), shown by `VideoHost` turned as chosen, with the boxes in its overlay window; the detector gets the player's grey pictures at the analysis rate, as CameraWall does, and its boxes are scored against the truth of the picture's time, turned the same way. A storybook without a bitrate is encoded once at a high quality for it (`{name}-{hash}-live.mkv`); a recording's turn is told to the player, as the stream does not carry it. Two runs differ a little, as the pictures taken depend on timing; the deterministic clock stays the one to compare with the baseline. S3 at 4 a second, turned 0, 90, 180 and 270°: recall 0.91 to 0.97, no false box, one track, found after 1.0 to 1.5 s (0.73 s on the deterministic clock: the player's first pictures come later than the video's). Its data and logs are in `%ProgramData%\AtcSoft\BoundingBoxSample` (or under `ATCSOFT_DATA_PATH`).

Each box the overlay draws is shown to UI Automation, with the automation id `box-{trackId}` (`box-at-{place}` without a track) and the detector's box in video pixels as its help text. `BoundingBoxSampleSmokeTests` reads them; see [End-to-end smoke tests](e2e-smoke-tests.md).

---

## 📝 Reviewing a Real Recording

A real recording has no truth until someone says what happens in it. In the apps, a right-click on a recording player's activity timeline gives a quick verdict: on an event, *Boxed correctly* or *False alarm* over it; anywhere, *Missed something here* over five seconds around the place. CameraWall writes the review itself, the WPF and web clients through the server (`POST /recordings/review/verdicts`). For a closer review, open the recording in the sample app and mark stretches in the *Review* panel: go to the first picture, *Mark start*, go to the last picture, choose the verdict, how many things move and what they are, then *Add stretch to here*. *Save review* writes `{video}.review.json` beside the recording; it moves, is protected and is deleted with it, like the activity index.

| Verdict | Means | Expects |
|---|---|---|
| `correct` | Boxed as it should be | `count` boxes (1 when not given) |
| `missed` | Something moved and was not boxed | `count` boxes |
| `wrongCount` | Boxed, with the wrong number of boxes | `count` boxes |
| `falseBox` | Boxed, but nothing that matters moves (a tree, a reflection) | No box |

Only the marked stretches are judged; the rest of the recording is not. A picture expects the counts of the stretches over it added up, a box beyond that is false, and the first second of a stretch with things moving is not judged (the detector needs a moment to be sure). The score panel shows the review's score as soon as there is a stretch.

```json
{
  "version": 1,
  "recording": { "file": "garden.mkv", "sha256": "…" },
  "detector": { "name": "builtIn", "settings": { "sensitivity": 30, "…": "…" }, "zones": [] },
  "spans": [
    { "from": "00:00:47.500", "to": "00:00:53.000", "verdict": "correct", "count": 1, "label": "person" },
    { "from": "00:12:00.000", "to": "00:14:30.000", "verdict": "falseBox", "note": "tree" }
  ]
}
```

Times are positions in the video, as a player shows them. The review names its recording by SHA-256, so a review of another file is not used, and carries the settings and zones the recording is analysed with (the camera's, turned to the recording's rotation).

### The corpus

Put reviewed recordings with their reviews in one folder, outside git (they show the user's cameras), and point `ATCSOFT_BBOX_CORPUS` at it. `CorpusTests` runs each as *Analyse recording* reads it and fails when a score is worse than the accepted one; `ATCSOFT_BBOX_UPDATE_BASELINE=1` accepts the current scores. They are kept in `accepted-scores.json` in the same folder, by the recording's SHA-256.

```powershell
$env:ATCSOFT_BBOX_CORPUS = "D:\corpus"
dotnet test test/AtcSoft.VideoSurveillance.Storybook.Tests --filter CorpusTests
```

### The sweep

Which defaults do best? *Sweep scenes…* in the sample app (or `--sweep folder`, with `--corpus folder` or `ATCSOFT_BBOX_CORPUS` for reviewed recordings) tries combinations of the settings a person can change: sensitivity 10 to 70 in steps of 10, the smallest object 0.01, 0.02 and 0.05 % of the picture, the stopped-object time 60 s. Each goes on top of a scene's own settings (S14 keeps its lighting), at 4 and 15 pictures a second, and over every reviewed recording as *Analyse recording* reads it (the review's zones). The combinations are ranked: the most scene runs meeting their expectations first (known-bad scenes not judged), then the corpus's recall and false boxes, then the scenes' mean recall and false boxes. The report (Markdown, in `{data}\sweeps`) says where the shipped defaults stand and in which scenes the best differs from them. Scenes run on the fast path, each drawn once per rate (`StorybookRunner.Draw`), so a scene with a bitrate is not encoded here.

```powershell
dotnet run --project sample/BoundingBoxWpfApp -- --sweep test/AtcSoft.VideoSurveillance.Storybook.Tests/Scenes
```

---

## ➕ Adding a Scene

1. Write `Scenes/sNN-what-it-shows.storybook.json`. Name it after what it checks, and give it `expect` values that say what a good detector does.
2. Run the tests once. The new scene fails with "no baseline".
3. Write its scores with `ATCSOFT_BBOX_UPDATE_BASELINE=1` and check that only its keys were added to `baseline.json`.
4. If the shipped detector does not meet the expectations yet, add `"knownBad": "why"` instead of lowering them.
5. Add it to the scene table here and in the plan.

A problem seen on a real camera is best turned into a scene: it is then measured on every change from then on.

---

## 🔧 Troubleshooting

| Symptom | Cause |
|---|---|
| "no baseline" right after writing the baseline | The output folder still has the old `baseline.json`; build again |
| The encoded tests are skipped | No FFmpeg tool found; install FFmpeg 9 with the desktop app's setup dialog |
| An encoded test fails with "FFmpeg is installed … but did not load" | The tool was found, but not the FFmpeg 9 libraries next to it |
| A score moved on a scene you did not touch | The detector, the scaling or the scorer changed: compare the old and new `Summary` lines in the test output |
| Old `%TEMP%\atcsoft-storybook-*` folders | A test run was killed before it could clean up; they can be deleted |
