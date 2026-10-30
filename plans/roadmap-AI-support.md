# 🧠 AI Support Roadmap

Plan for telling **what** moved: a label (*person*, *car*, *cat*, *dog*, …) and a score on the tracks our motion detector already finds. It runs on top of motion detection, never instead of it. [CodeProject.AI Server](https://github.com/codeproject/CodeProject.AI-Server) is the main inspiration.

Affects **`AtcSoft.VideoSurveillance.Core`** (a detector seam, labels on tracks, the activity index), a new **detection project** (ONNX Runtime, not in Core), both motion services, the **server** (spec, timeline, webhooks, `GET /events`), the overlays and timelines in **both WPF apps** and the **web client**, the settings dialogs, and the **storybook**.

← Back to the [roadmap home](roadmap.md) · Builds on the [bounding-box plan](roadmap-boundingbox.md) (detector, tracker, activity index, timeline)

**Status: ⬜ Not started** — planned on 2026-10-19. It takes over phase 10 (*Local AI detection (ONNX) on motion regions*) and the AI-model decision of the bounding-box plan.

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
| A track says what it is | A confirmed track of a person walking past gets the label *person* with a score, in the live overlay of all three clients |
| Only some things count | A camera set to *person, car* starts no motion recording, webhook or notification for a cat; the cat is still in the activity index |
| The timeline shows the class | Event bands are coloured by class, with an icon too (never colour alone), in the per-recording timeline and the server timeline |
| The cost is bounded | Detection runs on crops around confirmed tracks, a few times per track, never on every frame. Ten cameras with normal traffic stay inside a set CPU budget, measured |
| No lock-in, no licence trap | The default model is permissively licensed, weights included. A user can point us at a CodeProject.AI-compatible server instead |
| It fails safe | When the detector is missing, slow or down, motion still works exactly as today |

---

## 🧱 What Is Already There

Checked against the code on 2026-10-19.

| Part | Where | What it gives this plan |
|---|---|---|
| **Detector seam** | `IMotionDetector` (`Core/Services/IMotionDetector.cs`): `Process(MotionFrame)`, `Idle(now)`, `Reset()`. One instance per camera | The pattern for a second seam. Both motion services, the storybook scorer and later the sample app use only the interface |
| **Built-in motion detector** | `BuiltInMotionDetector` (Core, pure C#): blur, a per-pixel background (mean and variance), a lighting check, morphology, blobs, merging | The regions where something really moves. AI runs only there |
| **Tracker** | `MotionTracker` (Core, SORT-like): a track is confirmed after about 0.4 s **and** some travel; it keeps its id; it ends after 1 s unseen | Stable ids to hang a label on. A label is decided once per track, not per frame |
| **Track ids on the box** | `BoundingBox.TrackId` (Core), `MotionBox.trackId` in `src/VideoSurveillance.yaml`, on `GET /events` | The wire already carries one optional per-box field; `label` and `score` follow the same way |
| **Activity index** | `{video}.activity.jsonl` beside every recording (`RecordingPathHelper.BuildActivityPath`). Header `{"v":1,"start":…}`, then one line per analysed second: `t`, `a`, `n`, `ids`. Written by `ActivityLogWriter` (fed by `ActivityAccumulator`), read by `ActivityLogFormat` | A place for labels. The reader (`ActivityLogFormat.TryReadSecond`) **already skips unknown keys**, and the header check accepts any numeric `v`, so an extra optional field does not break old readers |
| **Summaries and server timeline** | `ActivitySummarizer` (10 s stretches by max, events), `ActivitySummaryCache`, `TimelineBuilder`; `GET /timeline`, `GET /timeline/events`, `GET /recordings/activity` | Events to carry labels; a filter by class later |
| **Storybook harness** | `src/AtcSoft.VideoSurveillance.Storybook` (model, deterministic renderer with truth, scorer); scenes and `baseline.json` in `test/AtcSoft.VideoSurveillance.Storybook.Tests` (`ATCSOFT_BBOX_UPDATE_BASELINE=1` writes new scores) | Scenes with a known class per object (`car`, `human`, `cat`, `dog`). The silhouettes are too simple for a real model (see phase 3) |

---

## 🛰️ CodeProject.AI Server: What We Found

Only what the sources confirm. "Not checked" means no source was found or read, not that the thing is absent. Read on 2026-10-19 from the GitHub repositories (through the GitHub API) and the project's docs site.

### Architecture

| Part | What it is | Source |
|---|---|---|
| Server | An ASP.NET Core HTTP server (the README says .NET 9 since 2.9), default port **32168**. It takes a request, puts it on a queue, and returns the module's answer | [README](https://github.com/codeproject/CodeProject.AI-Server/blob/main/README.md), [API reference](https://codeproject.github.io/codeproject.ai/api/api_reference.html) |
| Modules | Separate processes (Python or .NET), each in its own folder, described by a **`modulesettings.json`** | [YOLOv5-6.2 module settings](https://github.com/codeproject/CodeProject.AI-Server/blob/main/modules/ObjectDetectionYOLOv5-6.2/modulesettings.json) |
| How modules get work | A module **long-polls** the server for work (`GET v1/queue/{name}`) and posts its answer back (`POST v1/queue/{reqid}`). The Python SDK says it keeps "a reasonably short 'long poll' time" so the dashboard gets regular pings | [`QueueController.cs`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/src/server/Controllers/QueueController.cs), [`module_runner.py`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/src/SDK/Python/src/codeproject_ai_sdk/module_runner.py) |
| Queues and back-pressure | One **bounded channel** per queue name, `MaxQueueLength` **32**, `ResponseTimeout` **60 s**. A full queue answers "Request queue … is full"; a slow module answers "The request timed out" | [`QueueProcessingOptions.cs`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/src/server/Backend/QueueProcessingOptions.cs), [`QueueServices.cs`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/src/server/Backend/QueueServices.cs) |
| Shared queue per task | All object detectors use one queue, `objectdetection_queue`, so whichever detector module is running answers `vision/detection`. `Parallelism` 0 means "number of CPUs / 2" | Module settings above |
| Module manifest | `modulesettings.json` has: `PublishingInfo` (description, **`License`**, `LicenseUrl`, `BasedOn`), `LaunchSettings` (`Runtime`, `FilePath`, `Queue`, `Parallelism`, `AutoStart`), `EnvironmentVariables` (model size, model folders), `GpuOptions` (`InstallGPU`, `EnableGPU`, `HalfPrecision`), `InstallOptions` (`Platforms`, `ModuleReleases` with a server version range per module version), `ModelRequirements` (`Task`, `Architecture`, `Format`), `UIElements` (menus), `RouteMaps` (route, method, inputs with defaults and ranges, outputs). Variants per platform (`modulesettings.jetson.json`, `….raspberrypi.json`, …) | Module settings above |
| Installing modules and models | `v1/module/list/available`, `install/{moduleId}/{version}`, `uninstall/{moduleId}`; `v1/model/list/downloadable`, `download/{moduleId}/{filename}/…`. A module's install script downloads its models from CodeProject's server (for example `yolonet-models.zip`). The model description (`ModelConfig`: id, name, file name, folder, size) has **no hash or checksum field** | [`ModuleController.cs`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/src/server/Controllers/ModuleController.cs), [`ModelController.cs`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/src/server/Controllers/ModelController.cs), [`ModelDownload.cs`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/src/SDK/NET/Modules/ModelDownload.cs), [YOLOv5Net `install.bat`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/modules/ObjectDetectionYOLOv5Net/install.bat) |
| Health and status | `v1/server/status/ping`, `…/version`, `…/system-status`, `…/updateavailable`, `…/modules/list` (the legacy `v1/status/…` still works); `v1/module/list/status`. Every answer reports `inferenceMs`, `processMs`, `analysisRoundTripMs`, `moduleId`, `executionProvider`, `canUseGPU` | [`ServerStatusController.cs`](https://github.com/codeproject/CodeProject.AI-Server/blob/main/src/server/Controllers/ServerStatusController.cs), [API reference](https://codeproject.github.io/codeproject.ai/api/api_reference.html) |
| Hardware | "CPU on each platform, DirectML on Windows, CUDA on Windows and Linux, support for Apple Silicon GPUs, RockChip NPUs and Coral.AI TPUs. Support depends on the module itself." | [README](https://github.com/codeproject/CodeProject.AI-Server/blob/main/README.md) |
| Model formats (by module) | PyTorch (YOLOv5 6.2, YOLOv5 3.1, YOLOv8, face), **ONNX** (YOLOv5 .NET, via DirectML on Windows), TensorFlow Lite (Coral: YOLOv5, YOLOv8, EfficientDet, MobileNet), PaddlePaddle (ALPR, PP-OCRv3), RKNN (RockChip). **TensorRT: not found** in the modules read | Each module's `ModelRequirements` |

### The HTTP API (vision part)

All `POST` with a form upload (`image`), base `http://localhost:32168/v1/`. From the [API reference](https://codeproject.github.io/codeproject.ai/api/api_reference.html):

| Route | What |
|---|---|
| `vision/detection` | Objects (80 COCO classes). Input `min_confidence` (default 0.4) |
| `vision/custom/{model}`, `vision/custom/list` | Objects with a custom model; the list of custom models |
| `vision/face`, `face/recognize`, `face/register`, `face/list`, `face/delete`, `face/match` | Face detection and recognition, with a face database kept on the server |
| `vision/alpr` (legacy `image/alpr`) | Licence plates: finds and reads the characters |
| `vision/scene` | Scene classification (365 scenes) |

The detection answer: `success`, `message`, `error`, `count`, and `predictions`, each with `label`, `confidence`, `x_min`, `y_min`, `x_max`, `y_max` (pixels of the sent image), plus the timing and module fields above. These are the DeepStack routes. The project's [integrations page](https://codeproject.github.io/codeproject.ai/why/integrations.html) quotes Agent DVR ("integrates with DeepStack AI and senseAI") and Blue Iris ("Code Project's SenseAI, now our preferred no-extra-cost AI provider over DeepStack"); SenseAI is the name the page uses for CodeProject's server. Because the routes are shared, an NVR that speaks this API can use DeepStack, CodeProject.AI or anything else that answers the same way.

### How NVRs use it

| NVR | How | Source |
|---|---|---|
| **Agent DVR** | A list of AI servers (name, URL such as `http://localhost:32168/`, optional API key, timeout); each camera picks a server per function. *Object recognition* runs **on motion** or **on an interval**; a list of classes to find and a confidence. **Motion pass-through**: "If your AI server is non-responsive and you're using it to filter motion detection Alerts, Agent DVR will treat all events as valid alerts until the server resumes" (a switch) | [AI servers](https://www.ispyconnect.com/docs/agent/ai-servers), [AI configuration](https://www.ispyconnect.com/docs/agent/ai-config) |
| **Blue Iris** | An AI tab with the server's address and port; per camera, on the *Trigger* tab, AI confirms a motion trigger with the object types to look for. Details from community guides only; Blue Iris's own help not checked | [Wundertech guide](https://www.wundertech.net/how-to-set-up-codeprojectai-on-blue-iris/) |

Both use AI as a **filter on motion**: motion finds that something happened, AI decides whether it is worth an alert. That is the design of this plan too.

### Licence

| What | Licence | Source |
|---|---|---|
| **CodeProject.AI Server** (the repository) | **Server Side Public License (SSPL) v1**: the file `LICENCE.md` holds the SSPL text ("Copyright © 2018 MongoDB, Inc."); the README badge says SSPL. GitHub's licence detection reports "Other" (`NOASSERTION`) | [LICENCE.md](https://github.com/codeproject/CodeProject.AI-Server/blob/main/LICENCE.md), [SSPL](https://www.mongodb.com/licensing/server-side-public-license) |
| Third-party code in the server | Bootstrap and Swashbuckle.AspNetCore (MIT) are listed | [THIRD-PARTY-NOTICES.md](https://github.com/codeproject/CodeProject.AI-Server/blob/main/THIRD-PARTY-NOTICES.md) |
| Object Detection (YOLOv5 6.2) module | GPL-3.0, "based on Ultralytics YOLOv5" | [modulesettings.json](https://github.com/codeproject/CodeProject.AI-Server/blob/main/modules/ObjectDetectionYOLOv5-6.2/modulesettings.json) |
| Object Detection (YOLOv5 .NET) module | MIT, based on yolov5-net (the folder's `LICENSE`: "Copyright (c) 2021 Mentalstack"). Its models come as `yolonet-models.zip` from CodeProject's server; **their licence: not checked** | [modulesettings.json](https://github.com/codeproject/CodeProject.AI-Server/blob/main/modules/ObjectDetectionYOLOv5Net/modulesettings.json), [install.bat](https://github.com/codeproject/CodeProject.AI-Server/blob/main/modules/ObjectDetectionYOLOv5Net/install.bat) |
| Object Detection (YOLOv5 3.1) module | GPL-3.0, based on DeepStack | [repository](https://github.com/codeproject/CodeProject.AI-ObjectDetectionYOLOv5-3.1) |
| Object Detection (YOLOv8) module | **AGPL-3.0**, based on Ultralytics | [repository](https://github.com/codeproject/CodeProject.AI-ObjectDetectionYOLOv8) |
| Object Detection (Coral) module | Apache-2.0, based on the Coral.ai examples | [repository](https://github.com/codeproject/CodeProject.AI-ObjectDetectionCoral) |
| Face Processing module | GPL-3.0, based on DeepStack | [repository](https://github.com/codeproject/CodeProject.AI-FaceProcessing) |
| ALPR module | **SSPL**, PaddleOCR models (PP-OCRv3) | [repository](https://github.com/codeproject/CodeProject.AI-ALPR) |
| The **weights** each module downloads | **Not checked** for any module. The module licences above are what each `modulesettings.json` declares for the module code | — |

**What it means for us:** we take **ideas and API shapes** only, never code. SSPL is a copyleft licence written for software offered as a service; mixing its code into our server is not an option we want to study. The same applies to the GPL / AGPL modules. This is the same rule as the bounding-box plan's tracker, written from the SORT paper because its reference code is GPL.

### Maintenance status

| Signal | Value (as of 2026-10-19) | Source |
|---|---|---|
| Archived | No | [GitHub API](https://api.github.com/repos/codeproject/CodeProject.AI-Server) |
| Last release | **v2.9.5, 2024-12-11** | [Releases](https://github.com/codeproject/CodeProject.AI-Server/releases) |
| Last commit on `main` | **2025-05-23** (a merged pull request: benchmark fix, ALPR in the benchmark) | [Commits](https://github.com/codeproject/CodeProject.AI-Server/commits/main) |
| Last push to the repository | 2025-07-14 | GitHub API (`pushed_at`) |
| Module repositories | Last pushed 2025-05-24 (most of them) | [codeproject on GitHub](https://github.com/codeproject) |
| Open issues | 33 | GitHub API |
| Reports that the lead developers left in late 2024 | **Unconfirmed**: only search snippets of [ipcamtalk](https://ipcamtalk.com/posts/913322/) threads were seen; the pages refused to load. No official statement found | — |

**Reading:** no release for almost two years and no commit for about 17 months. Not archived, but **not actively maintained by any visible measure**. We should not depend on it. We can still speak its API, because DeepStack-style routes are what NVR users already run, and a fork or another server may answer them.

---

## ⚖️ Options

### (a) In-process ONNX Runtime

`Microsoft.ML.OnnxRuntime` (MIT) inside the server and CameraWall, running a permissively licensed model.

| ✅ For | ❌ Against |
|---|---|
| Nothing else to install or run; works offline, as the apps do today | A native runtime (tens of MB) in our installers and the server |
| Lowest latency: the crop never leaves the process; no JPEG, no HTTP | A native crash or a leak ends the 24/7 process. Mitigated by running it rarely (crops of confirmed tracks) and behind a seam, and testable with the storybook |
| One code path for CameraWall (no server) and the server | GPU choice is ours: the CPU provider everywhere; **DirectML on Windows is "in sustained engineering"**, new work goes to Windows ML ([ONNX Runtime docs](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html)); CUDA needs a large separate package |
| Deterministic on the CPU provider, so storybook and corpus scores compare across machines | We choose, download and check the models ourselves |
| Same language and debugger; the seam keeps it replaceable | **Cannot go into Core** (Core is a published lockstep package, see CLAUDE.md). It needs a project of its own, like the OpenCV option in the bounding-box plan |

### (b) An external AI server over HTTP, CodeProject.AI-compatible

We call `POST /v1/vision/detection` on a server the user names (CodeProject.AI, DeepStack, a fork, or later our own host).

| ✅ For | ❌ Against |
|---|---|
| Users with a CodeProject.AI or DeepStack box (Blue Iris, Agent DVR users) reuse it | Another program to install and keep running; CodeProject.AI itself is unmaintained |
| No native code in our process; the GPU, Coral or RockChip work is someone else's | A crop must be encoded (JPEG) and sent; latency and a network hop |
| Their modules give face and ALPR without us building them (with their licences) | We do not control the models; their licences (GPL, AGPL, SSPL) are the user's business, but they are not what we would recommend |
| Small for us: an HTTP client and a mapping | Needs a queue, timeouts and a fail-open / fail-closed rule (Agent DVR's *motion pass-through*) |
| | A secret (an API key) to keep out of the logs, stored like camera passwords (`SecretProtector`, `StoredSecret`) |

### (c) Our own module host in the style of CodeProject.AI

A host process of ours that starts detector processes, queues work for them, installs models, and serves the same API.

| ✅ For | ❌ Against |
|---|---|
| Isolation: a native crash takes down only the module process | By far the most work: process supervision, an installer, versions, a dashboard |
| Any runtime (Python, TensorRT, vendor NPUs) behind one API | Python environments are what CodeProject.AI's README says it was built to fight ("fighting versions and libraries and models") |
| One host serves several apps or machines | Little gain over (a) + (b) for our scale (a handful to tens of cameras) |

### Recommendation

**Build (a) first, behind a seam, and add (b) as a second implementation. Do not build (c) now.**

- A new seam **`IObjectDetector`** in Core: crops (or a picture with regions) in, labelled boxes with scores out, asynchronous, cancellable. No ONNX types in Core.
- **`OnnxObjectDetector`** in a new project (for example `AtcSoft.VideoSurveillance.Detection.Onnx`), referenced by the server and CameraWall only. Not packable at first. If it ever is, it joins the lockstep and the Trusted Publishing globs (CLAUDE.md).
- **`HttpObjectDetector`**: a client for the CodeProject.AI / DeepStack `vision/detection` shape, so a user can point a camera at such a server.
- **Why this order:** (a) works for everyone with nothing else installed, is fastest and is deterministic for the tests. (b) is small once the seam exists, serves users who already run an AI box, and gives an escape hatch for hardware we do not support. (c) only makes sense if (a) proves unstable in-process; the seam keeps that door open, since our own host would just be another `HttpObjectDetector` target.

---

## 🔁 What to Borrow from CodeProject.AI, and What Not

| Borrow | How it looks here |
|---|---|
| **The `vision/detection` API shape** (form upload, `min_confidence`, `predictions` with `label`, `confidence`, `x_min…y_max`) | `HttpObjectDetector` speaks it; if we ever host our own detector (option c), it answers the same, so Blue Iris or Agent DVR could use it |
| **A module manifest** | A small `model.json` per model, our own format: id, task, architecture, format (ONNX), input size, class names, **licence and its URL for code and weights**, source URL, **SHA-256**, size. The settings dialog shows the licence |
| **A bounded queue with a timeout** (32 deep, 60 s there) | One bounded channel per detector with a short timeout (seconds, not 60 s: a label older than the track is useless). When full, drop the **oldest** crop of a track that already has a label |
| **One queue per task** (all object detectors share one) | One queue per detector instance, shared by all cameras, with a fair share per camera |
| **Run only on motion** (as Agent DVR and Blue Iris use it) | Only crops around **confirmed tracks**, a few times per track: on confirmation, then when it grows clearly, then stop once the label is sure |
| **Timing in every answer** (`inferenceMs`, `processMs`, `executionProvider`, `canUseGPU`) | The same numbers in our health: per detector the provider, mean and p95 inference time, queue length, dropped crops |
| **Health and status routes** | The detector's state in the server's `/health` (a stale or failing detector shows) and in `GET /settings/…` for the clients; an Atc.Hosting worker with health tracking (CLAUDE.md) |
| **Agent DVR's motion pass-through** | A per-camera choice when the detector is down: treat motion as an event (fail open) or not (fail closed). See decisions |
| **Per-module hardware choice** (`GpuOptions`) | A setting *Run AI on*: `auto`, `cpu`, `gpu`; we report what was actually used |
| **Model download** | From the model's own release page, checked against the **SHA-256 in our manifest** before use. CodeProject.AI's model description has no checksum; this is our addition, the same pattern as `FFmpegInstaller.Package` (a URL and a SHA-256) |

| Do not borrow | Why |
|---|---|
| **Any code** | SSPL server, GPL / AGPL / SSPL modules |
| **Its models** | GPL / AGPL code, weights' licences not checked; we pick our own (decision 2) |
| **Python modules, per-module environments, installer scripts** | The complexity its own README complains about; not needed for one ONNX model |
| **A general AI platform** (LLMs, text, sound, background removal) | Out of scope; we detect objects on cameras |
| **Face recognition and ALPR now** | Personal data: only after the consent and privacy work (phase 9) |
| **Server-wide face database by default** | If face recognition ever comes, it is opt-in per camera, with retention and deletion |

---

## 🔌 Integration Points

| Where | What changes |
|---|---|
| **Detection loop** | The motion service hands confirmed tracks (box, id, the latest frame) to a per-camera **label scheduler**: crop with a margin, at the model's input size, from the full-resolution frame (not the 800×600 analysis picture: a small cat far away needs the pixels). The answer is matched back to the track by overlap with the crop's box |
| **Tracks** | A track keeps its best label and score (or a vote over a few crops). `BoundingBox` gets optional `Label` and `Score`, next to `TrackId` |
| **What starts things** | Motion recordings, webhooks and notifications may require a class (per camera). Without AI configured, nothing changes |
| **Activity index** | An optional per-track label in the line: for example `"lb":{"4":"person"}` beside `ids`. Old readers skip unknown keys already (`ActivityLogFormat.TryReadSecond`); whether to raise `v` to 2 is decision 6. A writer test pins the format, a reader test reads both versions |
| **Summaries and server timeline** | `ActivitySummarizer` keeps the classes of each event; `TimelineEvent` gets optional `labels`; `GET /timeline` and `GET /timeline/events` get an optional class filter. The summary cache key includes the format version |
| **Timeline colours** | Per class, **by name** (CLAUDE.md "One vocabulary": colours by name), and always with an icon. One colour table in Core, a copy in the web client's `Logic/` with an equality test |
| **`GET /events` and webhooks** | `MotionBox` gets optional `label` and `score`; `motionDetected` (and later `activityStarted`) carry the classes; webhook payloads too. Optional fields, so older clients ignore them; the server and desktop client are released together anyway |
| **Clients' overlays** | The live box shows the label and score (WPF `MotionBoundingBoxOverlay` in both apps, the web live view). Label texts through `Translations` (four languages); the wire value stays English |
| **Per-camera settings** | *Detect objects* (on / off), *classes* (person, vehicle, animal, … as a list), *minimum score*, *when the detector is down* (fail open / closed). Global: detector (*built-in*, *AI server*), server URL and API key, *run AI on* (auto / CPU / GPU), the CPU budget. End to end: Core, `MotionDetectionPolicy.Resolve` and the per-camera overrides, the spec (update requests with nothing required, applied only when present), both clients (the web copy in `Logic/` with its equality test), `SettingsRoundTripTests`, texts in **en-US, en-GB, da-DK, de-DE**, steps in the E2E walks for the dialogs |
| **Secrets** | The AI server's API key is stored through `SecretProtector` (server) and `StoredSecret` (CameraWall), never logged, never sent to a client |
| **Storybook** | Each scene object already has a kind (`car`, `human`, `cat`, `dog`); the truth gains the expected label, the scorer gains label accuracy per class (phase 3) |
| **Budget** | Per detector: a cap on inferences per second across all cameras, a fair share per camera, the queue length in the health. Measured in a benchmark test and the sample app's CPU panel |
| **Privacy** | No face recognition or licence plates before phase 9. Labels are *what*, never *who* |

---

## 🛣️ Phases

| # | Phase | Size | Status |
|---|-------|:--:|:--:|
| 0 | Licence and model study: candidates, licences of code **and weights**, a first ONNX export and its speed on CPU | M | ⬜ |
| 1 | The seam: `IObjectDetector`, labels on tracks, the label scheduler, a fake detector | M | ⬜ |
| 2 | `OnnxObjectDetector` in its own project; model manifest, download with SHA-256, CPU and DirectML | L | ⬜ |
| 3 | Storybook and corpus: label truth, label scoring, the regression ratchet | M | ⬜ |
| 4 | Labels on the wire and in the files: `MotionBox`, `GET /events`, webhooks, the activity index, summaries, the timeline API | M | ⬜ |
| 5 | Settings end to end: classes, minimum score, fail open / closed, detector choice | L | ⬜ |
| 6 | The clients: labels in the overlays, per-class colours and icons on the timelines, a class filter | L | ⬜ |
| 7 | `HttpObjectDetector`: a CodeProject.AI-compatible server as the detector | M | ⬜ |
| 8 | Budget and health: inference caps, fair share, `/health`, the numbers in the clients | M | ⬜ |
| 9 | Privacy and consent UX; then face recognition and ALPR, if wanted | L | ⏸️ |
| 10 | ONVIF Profile T metadata: use the labels of cameras with their own AI | L | ⏸️ |
| 11 | Our own module host (option c) | L | ⏸️ |

### Phase 0 — Licence and model study ⬜ (M)

- For each candidate (YOLOX, RF-DETR N/S/M/L, D-FINE, RT-DETR, NanoDet, whose licence is not checked yet): the code's `LICENSE`, the **weights'** licence (where they are published and under what terms), the training data (COCO), the ONNX export path, the input size, the speed on one CPU core at a crop.
- Write the findings into this plan (decision 2) with links; "not checked" where unconfirmed.
- A throwaway console in the scratch area exports a model and runs it on a few real crops. Nothing lands in `src/`.

**Test:** none in the repository; the result is a decision with numbers.

### Phase 1 — The seam ⬜ (M)

- Core: `IObjectDetector` (`Task<IReadOnlyList<DetectedObject>> DetectAsync(ObjectDetectionRequest, CancellationToken)`; a request is one picture with regions), `DetectedObject` (box, label, score), `BoundingBox.Label` and `Score`.
- Core: a per-camera **label scheduler**: which confirmed tracks to send, when (on confirmation, on clear growth, stop when sure), the bounded queue (drop the oldest for a labelled track), matching answers back to tracks, a vote over a few crops.
- A `NullObjectDetector` (the default: nothing changes) and a `FakeObjectDetector` for the tests that answers from a given table.
- Both motion services pass confirmed tracks to the scheduler when a detector is configured.

**Test:** unit tests of the scheduler (how many calls per track, the queue drops, the vote, a slow detector does not hold motion back); the storybook fast path with the fake detector shows labels on the right track ids.

### Phase 2 — ONNX in-process ⬜ (L)

- New project `AtcSoft.VideoSurveillance.Detection.Onnx` (net10.0, not packable): `OnnxObjectDetector` with the model chosen in phase 0; pre- and post-processing in C# (letterbox; NMS for YOLO-style models, none for the DETR family); the CPU provider by default, DirectML on Windows when asked and available.
- The model manifest (`model.json`): id, classes, input size, licence and URL of code and weights, source URL, SHA-256, size. A download into the data folder (`ServerDataPaths` on the server, `ApplicationPaths` in CameraWall), checked against the SHA-256 before it is loaded; a model with a wrong hash is refused and the reason logged.
- The server and CameraWall reference it; Core does not.

**Test:** unit tests of pre- and post-processing against fixed tensors; a test that runs the model on a handful of licensed test images (skips without the model file, like the FFmpeg tests); a test that a wrong hash is refused; a benchmark of a crop on CPU.

### Phase 3 — Storybook and corpus ⬜ (M)

- The truth of each scene object carries its expected label (from its kind: `human` → *person*, `car` → *car*, `cat` / `dog`); the scorer reports **label accuracy per class**, unlabelled tracks and wrong labels; `baseline.json` keeps them, and the ratchet fails on a drop.
- With the **fake detector**, every scene checks the plumbing exactly: the right label on the right track, the right number of calls.
- The C# silhouettes are **not** a test of a real model. Real accuracy comes from real recordings in `ATCSOFT_BBOX_CORPUS` with the reviews of the bounding-box plan's phase 9 (whose review format already has a `label`), plus, if found, licensed photo sprites.
- New scenes: a person and a dog at once; a car parked, then driving off; a cat at night (grey).

**Test:** the storybook tests (fast path, fake detector, no model needed); the corpus test with the ONNX detector (skips without the corpus or the model).

### Phase 4 — Labels on the wire and in the files ⬜ (M)

- Spec: optional `label` and `score` on `MotionBox`; optional `labels` on `TimelineEvent`; an optional class filter on `GET /timeline` and `GET /timeline/events`; the classes in the `motionDetected` event and the webhook payload. The label's wire type follows decision 5.
- Activity index: an optional per-track label in each second; summaries keep the classes per event.
- Old files without labels read as today; files with labels read by an old reader as today (already true).

**Test:** writer and reader round trips (with and without labels); handler tests for the filter; a host test that sees a label in `GET /events` with the fake detector (the live test stream moves, the fake answers *person*).

### Phase 5 — Settings end to end ⬜ (L)

- Per camera: *Detect objects*, *classes*, *minimum score*, *when the detector is down*. Global: the detector (*none*, *built-in AI*, *AI server*), the server URL and API key, *run AI on*, the budget.
- Core, `MotionDetectionPolicy.Resolve`, the overrides, the spec (update requests apply only what is there), both settings dialogs and the web settings, `SettingsRoundTripTests`, four languages, the web copy in `Logic/` with its equality test.
- Recording, webhooks and notifications honour the classes.

**Test:** `SettingsRoundTripTests`; unit tests of the policy; E2E steps for the dialogs; a host test that a camera set to *car* starts no motion recording for a *person* (fake detector).

### Phase 6 — The clients ⬜ (L)

- Overlays: the label and score on each live box (both WPF apps, the web live view).
- Timelines: per-class colours **and icons** on event bands (per recording and on the server timeline); a class filter; the hover shows the classes.
- The colour table in Core by name, its web copy in `Logic/` with an equality test; all texts through `Translations`.

**Test:** unit tests of the colour and icon choice and the filter; the drawn-to-bitmap timeline test with labels; E2E steps in all three walks.

### Phase 7 — A CodeProject.AI-compatible server ⬜ (M)

- `HttpObjectDetector`: `POST {url}/v1/vision/detection` with the crop as JPEG and `min_confidence`; maps `predictions` back to the crop; an optional API key; a short timeout; uses the generated client's resilience where it fits, but **no retry** of a detection (a late label is worthless).
- A *Test connection* button calls `GET {url}/v1/server/status/ping` (and shows `version`).
- The failure rule of decision 4 applies.

**Test:** unit tests against a fake HTTP handler (answers in the documented shape, a full queue, a timeout, an error); a manual check against a real CodeProject.AI Server (to verify by hand).

### Phase 8 — Budget and health ⬜ (M)

- A cap on inferences per second per detector, a fair share per camera, queue lengths and drops counted.
- The server's `/health` includes the detector (stale or failing shows); the provider used, mean and p95 time per inference, drops: in the settings views of both clients.

**Test:** unit tests of the budget (many cameras, one slow detector: no camera starves, motion never waits); a benchmark test with ten simulated cameras.

### Phase 9 — Privacy, then faces and plates ⏸️ (L)

- First the privacy work: what is stored, for how long, who can see it, a per-camera opt-in, a notice in the UI, deletion of a person's data; an audit entry for every look-up.
- Only then, if wanted: face recognition and licence-plate reading, as the [roadmap's ideas](roadmap.md#-ideas-not-planned) say. Models with checked licences (CodeProject.AI's face module is GPL-3.0, its ALPR module SSPL).

### Phase 10 — ONVIF Profile T metadata ⏸️ (L)

- Cameras with their own AI publish object metadata (Profile T). Reading it is an alternative source of labels: another `IObjectDetector`-like source that needs no CPU on our side.
- Only after phases 1–6, so the labels have somewhere to go.

### Phase 11 — Our own module host ⏸️ (L)

- Only if the in-process runtime proves unstable or a needed accelerator has no ONNX Runtime provider. It would serve the same `vision/detection` API, so `HttpObjectDetector` is already its client.

---

## ❓ Decisions Needed

| # | Question | Recommendation |
|---|----------|----------------|
| 1 | **In-process ONNX, an external server, or our own host?** | **In-process ONNX first, behind `IObjectDetector`, in a project of its own; an HTTP client for a CodeProject.AI-compatible server second; no host of our own now.** See [Options](#️-options) |
| 2 | **Which model?** (moved from the bounding-box plan, decision 8) | A permissively licensed one, **code and weights**: **YOLOX (Apache-2.0)** or **RF-DETR in the N, S, M or L size (Apache-2.0)**. Note: RF-DETR's Atto, Femto, Pico, XL and 2XL sizes are under **PML 1.0**, not Apache-2.0 ([RF-DETR README](https://github.com/roboflow/rf-detr)). **Not Ultralytics YOLO (AGPL-3.0)** unless an enterprise licence is bought. D-FINE and RT-DETR (both Apache-2.0 repositories) and NanoDet (licence not checked) are candidates. Check each repository's LICENSE **and the weights' licence** before choosing (phase 0) |
| 3 | **Which execution provider by default?** | **CPU.** DirectML on Windows when the user asks or a GPU is found and a check passes; DirectML is in sustained engineering, so watch Windows ML. CUDA not shipped (too large); a user with CUDA can use an AI server (phase 7) |
| 4 | **When the detector is down or too slow: fail open or closed?** | Per camera, default **fail open** (motion counts as an event, as Agent DVR's *motion pass-through*): missing a burglar is worse than an extra alert. A camera filtering out a busy street can choose closed. The health shows the state either way |
| 5 | **The label on the wire: a free string or a spec enum?** | **A spec enum** of the classes we name (person, car, truck, bicycle, motorcycle, bus, cat, dog, bird, …) with lower-case wire values, read in the clients as extensible structs (CLAUDE.md: `Names`, `IsKnown`), so a new class does not break an old client. An AI server's unknown label is mapped or dropped |
| 6 | **Raise the activity index to `v: 2` for labels?** | **Not needed for reading** (readers skip unknown keys, the header accepts any `v`). Raise it only if a field changes meaning; otherwise add the optional field under `v: 1` and pin it with a test |
| 7 | **Crop size and source frame** | Crops from the **full-resolution** frame with a margin, letterboxed to the model's input. A capture per inference is rare (a few per track), so the PNG path is acceptable until the bounding-box plan's grey frames (phase 2) bring a cheaper colour crop |
| 8 | **How often per track?** | On confirmation, again on clear growth, at most a set number per track; stop once the vote is sure. Settled by the storybook and the corpus numbers |
| 9 | **Does CameraWall get AI too?** | **Yes**, the same project; it is the app without a server. The HTTP option works there as well |
| 10 | **Models in the installer or downloaded?** | **Downloaded on first use**, checked by SHA-256, with the licence shown before the download. Keeps the installers small and the licence choice visible |
| 11 | **Face recognition and ALPR?** | **Not before phase 9** (privacy and consent). Labels say *what*, never *who* |

---

## ✋ To Verify by Hand

| What | Needs |
|---|---|
| A person, a car and a cat on real cameras get the right label within a second of being boxed | Cameras and some traffic |
| The CPU cost with eight cameras recording and AI on; the GPU path (DirectML) on a machine with a GPU | A machine and cameras |
| A camera set to *person* starts no recording for a cat or a car passing | A camera and an animal or a street |
| `HttpObjectDetector` against a real CodeProject.AI Server (2.9.x): labels, *Test connection*, the server stopped (fail open) | A CodeProject.AI install |
| Night (IR, grey picture): labels still right, or at least no wrong ones above the minimum score | A camera with IR, an evening |
| The model download on a clean machine: licence shown, hash checked, works offline afterwards | A clean machine |
| The labels and colours on the timeline of a real day | The server, cameras, a day |

---

## 📚 Sources

CodeProject.AI Server:

- Repository and README: https://github.com/codeproject/CodeProject.AI-Server
- Licence (SSPL): https://github.com/codeproject/CodeProject.AI-Server/blob/main/LICENCE.md; third-party notices: https://github.com/codeproject/CodeProject.AI-Server/blob/main/THIRD-PARTY-NOTICES.md; the SSPL itself: https://www.mongodb.com/licensing/server-side-public-license
- Releases (v2.9.5, 2024-12-11): https://github.com/codeproject/CodeProject.AI-Server/releases; commits: https://github.com/codeproject/CodeProject.AI-Server/commits/main
- API reference: https://codeproject.github.io/codeproject.ai/api/api_reference.html
- Integrations (Blue Iris, Agent DVR, DeepStack): https://codeproject.github.io/codeproject.ai/why/integrations.html
- Server code read: `src/server/Backend/QueueServices.cs`, `QueueProcessingOptions.cs`, `src/server/Controllers/QueueController.cs`, `ServerStatusController.cs`, `ModuleController.cs`, `ModelController.cs`, `src/SDK/NET/Modules/ModelDownload.cs`, `src/SDK/Python/src/codeproject_ai_sdk/module_runner.py`
- Module settings read: `modules/ObjectDetectionYOLOv5-6.2/modulesettings.json`, `modules/ObjectDetectionYOLOv5Net/modulesettings.json` and `install.bat`, and the `modulesettings.json` of [ALPR](https://github.com/codeproject/CodeProject.AI-ALPR), [FaceProcessing](https://github.com/codeproject/CodeProject.AI-FaceProcessing), [YOLOv8](https://github.com/codeproject/CodeProject.AI-ObjectDetectionYOLOv8), [Coral](https://github.com/codeproject/CodeProject.AI-ObjectDetectionCoral), [YOLOv5 3.1](https://github.com/codeproject/CodeProject.AI-ObjectDetectionYOLOv5-3.1)
- Community reports on its status (unconfirmed, pages not readable): https://ipcamtalk.com/posts/913322/, https://ipcamtalk.com/posts/882227/

NVR integrations:

- Agent DVR: [AI servers](https://www.ispyconnect.com/docs/agent/ai-servers), [AI configuration](https://www.ispyconnect.com/docs/agent/ai-config)
- Blue Iris (community guide): https://www.wundertech.net/how-to-set-up-codeprojectai-on-blue-iris/

Runtime and models:

- ONNX Runtime (MIT): https://github.com/microsoft/onnxruntime; [DirectML execution provider](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html) ("DirectML is in sustained engineering"); [Microsoft.ML.OnnxRuntime.DirectML](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML)
- [YOLOX](https://github.com/Megvii-BaseDetection/YOLOX) (Apache-2.0), [RF-DETR](https://github.com/roboflow/rf-detr) (Apache-2.0 for N/S/M/L, PML 1.0 for the other sizes), [D-FINE](https://github.com/Peterande/D-FINE) (Apache-2.0), [RT-DETR](https://github.com/lyuwenyu/RT-DETR) (Apache-2.0), NanoDet (licence not checked), [Ultralytics](https://docs.ultralytics.com/) (AGPL-3.0 or an enterprise licence). Weights' licences: not checked (phase 0)
- The code of this repository as read on 2026-10-19 (the names above)
