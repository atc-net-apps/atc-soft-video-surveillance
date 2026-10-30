# 🗺️ AtcSoft.VideoSurveillance Roadmap

The one list of what is still open. Consolidated on **2026-10-19**: every item was checked against the code, and the plans of finished areas were deleted (they are in the git history; the last commit that has them is the one before the consolidation).

How things work today is in [`docs/`](../docs/) and in CLAUDE.md. This folder holds plans only.

*Last updated: 2026-10-19*

---

## 🗂️ Status Legend

| Emoji | Meaning |
|-------|---------|
| ⬜ | Not started |
| 🟨 | In progress, or built and awaiting verification |
| ✅ | Complete |
| ⏸️ | Deferred (intentionally not in current scope) |

Sizes: **S** an hour or two, **M** a day, **L** several days.

---

## 🎯 Focus

| What | Why now |
|---|---|
| **Push and see CI green on generator 3.0.1** | The build left the local package for the released Atc.Rest.Api.SourceGenerator 3.0.1 on 2026-10-27; nothing of the v3 work has been through CI yet. Generator findings go to its `issues/feedback-from-VideoSurveillance.md` |
| The hands-on checks below | Much of what was built since 2026-10-12 has unit, host and E2E tests but has not been looked at with real cameras |
| Run the E2E smoke tests before a release | `ATCSOFT_E2E=1 dotnet test test/AtcSoft.VideoSurveillance.E2E.Tests` (see [`docs/e2e-smoke-tests.md`](../docs/e2e-smoke-tests.md)) |

---

## 📚 Areas

| Area | Plan | Status | Open |
|---|---|:--:|---|
| 🟥 Bounding boxes and activity timeline | [roadmap-boundingbox.md](roadmap-boundingbox.md) | ✅ | Built: storybook harness, the new detector with tracking, track ids on the wire, the activity index beside every recording (`GET /recordings/activity`), the activity timeline under the player in all three clients. Built too: the server timeline (`GET /timeline`) and a Timeline view of every camera in the web and WPF clients. Built too: gaps tell their reason (offline, not recording, disk full, server down). Built too: *play all from here* in both API clients, and CameraWall's own Timeline window over its folders. Built too: *play events only*. Built too: sparklines in the recordings lists, and *Analyse recording* for older recordings in all three apps. Built too: grey frames from the engine, and ignore and watch zones in the detector, stored with each camera and drawn on a snapshot in all three apps, an event in its watch zone's colour on the timeline, and the timeline's zoom and hover with a picture of the moment (preview pictures of every recording), events close together joined. Built too: `sample/BoundingBoxWpfApp` with a deterministic clock and its FlaUI walk. Built too: strong wind taken in, *Analyse recording*'s 3 s warm-up, and the feedback loop (reviews of real recordings, verdicts from a right-click on the timeline in all three apps, the corpus test, the settings sweep), and the sample's debug layers, tuning panel and live clock. Done 2026-10-28; rain on a real camera and the first reviewed recordings are hands-on checks below; AI detection is planned in [AI support](roadmap-AI-support.md) |
| 🧠 AI support (object labels on motion tracks) | [roadmap-AI-support.md](roadmap-AI-support.md) | ⬜ | Planned: an `IObjectDetector` seam, in-process ONNX first (permissive model), a CodeProject.AI-compatible HTTP client second; labels on tracks, the activity index, the timeline, webhooks and `GET /events`; per-camera classes and minimum score. Takes over the bounding-box plan's phase 10 |
| 🧪 24/7 soak | [`docs/soak-test.md`](../docs/soak-test.md) | ⬜ | The procedure has never been run |

---

## 🔧 Open work a developer can do now

Nothing here needs a camera or a person. Each item says how it can be tested.

### Small (S)

| Item | Where it stands | Test |
|---|---|---|
| A message for a stored USB device that is not plugged in (`UsbDeviceMissing`) | No key or message exists; the picker may already cover it | Unit on the dialog view model |
| A rate limit on starting a recording or a stream | Neither has `x-ratelimit-*`. A second start only adds a lease, so it may not be needed | Host test |
| Remote-access notes (VPN, reverse proxy) in the docs | Nothing in `docs/` | — |
| A severity filter and an unread badge for notifications in the client | Only time, title, message and type columns | Unit and E2E |
| A theme toggle in the ribbon | Only in the settings dialog | E2E |
| Spike: RTSP `SET_PARAMETER` as keep-alive (new in FFmpeg 9) | Not tried | Engine test with the live test stream |

### Medium (M)

| Item | Where it stands | Test |
|---|---|---|
| A pre-record buffer for motion recordings (keep the last few seconds of packets, write them first) | A motion recording starts at the first keyframe after motion begins; since 2026-10-19 motion begins 0.2–1 s later (a track must confirm), so the start of an event is not in the file | Engine test with the live test stream |
| TLS verification as a per-camera setting | Off for every camera (`StreamOptions.VerifyTlsCertificate`); no setting in the dialogs or the API; must pass `SettingsRoundTripTests` | Unit and E2E dialog walk |
| Record into each folder layout end to end without a camera | The host tests record with the FFmpeg test stream, but not into each layout | Host |
| The audit log keeps the value before a change | `AuditRecord` has none | Unit |
| Clip export / trim | Only a download exists | Unit and E2E |
| MQTT publisher | None; webhooks exist | Unit and host |
| Webhook management in the WPF client | Web client only; whether it is wanted is open | E2E |
| Inline rename and drag-reorder in the client's camera and layout lists | Both grids are read-only | E2E |
| FFmpeg timeouts and probe sizes as settings; stream-health counters (packet loss, read errors) | Constants in `Demuxer` (15 s open, 10 s read) and `VideoPlayer` | Unit |
| Privacy and permission messages for a USB camera Windows refuses | Only documented in `docs/usb-cameras.md` | Unit for the message mapping |
| USB counters in the stats panel | None | Unit |
| Measure the swscale backends of FFmpeg 9 on the software-decode path | Not measured | Engine benchmark |
| Event stream: push the storage status and the progress of a folder reorganisation instead of polling | The client polls `GET /settings/storage` every 30 s | Host test on `GET /events` |

### Large (L)

| Item | Where it stands |
|---|---|
| Push notifications (WebPush) on confirmed events | None. Best after the bounding-box plan's tracker, so a tree does not notify |
| WPF client: multi-monitor, camera groups and tags, a camera health view | None of it exists. Tags and health need the server first |
| A per-camera size limit for recordings | Only a total limit exists (`MaxRecordingStorageGb`) |

---

## 💡 Ideas (not planned)

Mostly from the market comparison of 2026-04; none is picked up yet.

| Idea | Note |
|---|---|
| Mobile app (live view, push, timeline) | The HLS streams and `GET /events` are the backbone; needs remote access first |
| Remote access without port forwarding (WebRTC or a relay) | Today only a VPN or a reverse proxy |
| LPR and face recognition | After local AI detection ([AI support](roadmap-AI-support.md), phase 9, deferred); both need consent and privacy UX first |
| ONVIF Profile T metadata from cameras with their own AI | An alternative to running detection on the server ([AI support](roadmap-AI-support.md), phase 10, deferred) |
| PTZ control; two-way audio | Neither exists |
| Home Assistant integration (HACS) | After MQTT |
| Cloud copy of recordings (S3, Azure Blob) | Local only today |
| Alarm panels, inbound triggers from sensors | The REST API can start a recording; nothing aimed at low-code callers |
| Picture-in-picture; numeric zoom levels on a tile | Zoom is rubber-band only |
| Standby recording server | No failover |
| Generator client options (Atc.Rest.Api.SourceGenerator markers) | `"strictRequiredProperties": true` makes a response without a required property fail loudly; trim- and AOT-safe clients for a smaller Blazor publish without JSON trim warnings; `"readEnumMemberNames": true` if installed desktop clients of another version must ever read both enum spellings |

---

## ✋ Waiting on a Person

Built and tested, but not closed without hands, eyes or hardware.

| What | Needs |
|---|---|
| Connection-type icon in all four info-box corners, grid and full screen; tint on connect and disconnect | A visual check |
| ONVIF connection-type detection against a wired, a Wi-Fi and a Tapo camera | Real cameras |
| Boxes at 0 / 90 / 180 / 270° in the grid and full screen; rotation after a setting change and a tile swap; snapshots and thumbnails upright | A real camera |
| Record, browse, play and delete in each of the four folder layouts; an old flat archive; midnight rollover | The app and some recording time |
| USB: unplug and replug in CameraWall (recording resumes within 5 s); recording with sound | Two USB cameras |
| "Show in Full Screen" on a CameraWall tile: through UI Automation it stayed disabled on a connected HTTP camera ([E2E notes](../docs/e2e-smoke-tests.md#left-out-on-purpose)) | The app and a camera |
| The client's live grid draws motion boxes | The server client and a camera that moves |
| The new motion detector and the activity index on real cameras: a person, a car, a tree in the wind, a change of light; a `.activity.jsonl` beside each recording in both apps, with motion where something moved; the CPU cost of analysing every recorded camera | Cameras and some recording time |
| The grid sizes, filters, disk indicators and unplugged text in both desktop apps; the keyboard shortcuts; the camera dialog's drop-downs | A look at the apps |
| The Users dialog (WPF) and the Users page (web) | A look at the clients |
| The web client against real cameras: H.264 and H.265 live view, rotated cameras, playback of real recordings | A browser, the server and cameras |
| Webhooks against a real receiver (Home Assistant, Slack) | A receiver |
| FFmpeg 9: hardware and software decode, recordings in MKV and MP4, rotation, snapshots, motion, USB cameras, an HTTPS camera with a self-signed certificate | The apps and cameras |
| The FFmpeg set-up dialog's *Install FFmpeg* on a machine without FFmpeg 9; the release pipeline's FFmpeg step in CI | A clean machine; a release run |
| The disk-space guard on a really full disk and on a network share | A small disk; a share |
| The event stream through a reverse proxy for longer than its idle timeout | A proxy |
| The browser's six HTTP/1.1 connections per origin: one event stream plus the HLS requests of a 3×3 live view | The web client and nine cameras |
| The 24/7 soak ([procedure](../docs/soak-test.md)) | A machine, three cameras and a day |
| The web client's E2E walk (`ATCSOFT_E2E=1`, `WebClientSmokeTests`) on a steady network, then the whole E2E suite after the timeline verdicts and the sweep. On 2026-10-28 every step passed but the walk failed: a USB network adapter without a cable ("Ethernet 3", Realtek USB GbE #2) came and went every ten seconds, and Edge dropped its downloads on each change (`ERR_NETWORK_CHANGED`) | A network cable in the dock, or the adapter unplugged |
| The motion detector in wind and rain on live cameras: the balcony through the cat net, the trees seen through the kitchen window (not helped by the wind fix), a camera in rain (no footage yet) | Cameras on a windy and a rainy day |
| *Analyse recording*'s 3 s warm-up on a recording that motion started: the timeline should show the event from 3 s on, with no wrong boxes in the first seconds | A motion-triggered recording |
| Right-click verdicts on the recording player's timeline in CameraWall, the WPF client and the web client: the menu where the mouse is, its words in Danish and German, the saved message; a viewer is refused in the server's words; the `{video}.review.json` beside the recording (the server's for the API clients) | The three apps and a recording with an event |
| The first reviewed real recordings: review a few in the sample app (*Review* panel) or with the right-click, put them with their reviews in a folder outside git, run `CorpusTests` with `ATCSOFT_BBOX_CORPUS` and `ATCSOFT_BBOX_UPDATE_BASELINE=1` once, then *Sweep scenes…* with the corpus to see whether other defaults do better on real footage | Recordings and someone to judge them |

---

## 🐛 Known Bugs

| Bug | Where |
|---|---|
| The web client is signed out when the page is left or reloaded while it renews its session: `POST /auth/refresh` ends 499, the next one 400. The server replaces the refresh token as soon as it is used, so an answer the browser never read leaves it with a dead token. Seen before the FFmpeg and disk work too (checked 2026-10-18). A short grace period for a replaced token would fix it; that is a security trade-off to decide. The E2E web walk fails at *Add user* until then | `AccountService.Refresh` |
| Motion boxes reported misplaced at 180°: two possible causes fixed, not confirmed. The bounding-box plan's rotation scene will test it | [Bounding boxes](roadmap-boundingbox.md) |

---

## ✍️ Conventions

- **This page is the list of what is open.** An area gets a plan file of its own (`roadmap-<area>.md`) when it needs design and decisions; its open items are listed here too.
- **Every area plan has** a phase table with a status per phase, a "Decisions" section, and a "To verify by hand" section when something needs hardware or eyes.
- **Update this page in the same change** that closes or opens an item.
- **When an area is finished**, what still matters moves to `docs/` or CLAUDE.md, its open items move here, and the plan file is deleted. The git history keeps it.
- **How things work today** is documentation, not a plan: it belongs in [`docs/`](../docs/).
