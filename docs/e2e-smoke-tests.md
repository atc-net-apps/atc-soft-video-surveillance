# End-to-end smoke tests

`test/AtcSoft.VideoSurveillance.E2E.Tests` starts the real apps and opens every window, page and
dialog they have, without cameras of their own and without touching anyone's data. A smoke test fails
on a step that breaks, on a binding error or unhandled exception in an app's log, on a console error,
page error or 5xx answer in the browser, and on a fatal entry in the server's output.

## Running them

The tests need a desktop session (they drive real windows), so they skip unless asked for:

```powershell
dotnet build
$env:ATCSOFT_E2E = "1"
dotnet test test/AtcSoft.VideoSurveillance.E2E.Tests
```

A run takes about three minutes. CI runs every test project, so there they skip.

What a run needs, and what happens without it:

| Needs | Without it |
|---|---|
| A desktop session, the screen not locked | The WPF walks cannot find their windows |
| The FFmpeg command-line tool (`ffmpeg.exe` where the engine finds FFmpeg) | No live test camera; the offline camera is still used |
| Microsoft Edge (Playwright uses the installed browser, nothing is downloaded) | The web client test fails to start a browser |

Leave the mouse alone while the CameraWall walk runs: a few steps are real clicks.

## What runs

- **The server**, as its own process on a free port, with a scratch data folder (`Storage__DataPath`).
  The fixture creates the first admin with the setup code, an offline camera (a port nothing listens
  on), a live camera (with FFmpeg) and a layout, all through the API.
- **CameraWall**, with `ATCSOFT_DATA_PATH` set to a scratch folder seeded with the app's own storage
  services: en-US, a log file, no recording, the two cameras in the start-up layout.
- **The WPF client**, pointed at the test server the way Aspire does it (`services__api__http__0`),
  signed in from `Auth__UserName` / `Auth__Password`, in a scratch folder of its own.
- **The FFmpeg set-up dialog** (`FFmpegSetupSmokeTests`): CameraWall and the WPF client started with
  `ATCSOFT_FFMPEG_SEARCH=explicit`, so the installed FFmpeg is not found. CameraWall takes the real
  FFmpeg folder typed into the dialog and starts; the client exits from the dialog. The install
  itself (a 100 MB download) is covered by the unit tests only.
- **The bounding box sample** (`BoundingBoxSampleSmokeTests`, `sample/BoundingBoxWpfApp`), started with
  `--storybook` or `--recording` in a scratch folder of its own. It opens S5 and checks the score panel
  against a run made in the test and against `baseline.json`, then at two pictures compares the boxes
  the overlays show to UI Automation (`box-{trackId}`, the box in video pixels as help text) with that
  run, the detector's and the truth's. A second walk opens a recording made from S3 with the FFmpeg
  tool (skipped without it), marks the walker as a reviewed stretch, checks the score by the review and
  saves it, then reads `{video}.review.json` back. A third opens a copy of S5 and checks the debug
  layers (the debug line and a label for every blob the detector found, as a run made in the test with
  its diagnostics), sets the smallest object to 0.5 % in the tuning panel and waits for the score of
  that run, saves the settings into the copy and reads them back. A fourth plays S3 on the live clock
  (`--live`, the video streamed to the engine's player) turned 0, 90, 180 and 270° (S7), and checks
  that a box in `VideoHost`'s overlay lies on the walker as the truth turned the same way, and the
  score at the end, and a recording made from S3 played live until its stream ends (a recording has
  no truth and its times need not start at 0). `VideoHost` puts its picture and its overlay in child windows WPF does not know
  of, so they are not in the window's UI Automation tree; `ChildWindows.Find` asks UI Automation for
  each child window by its handle, and reads the overlay's boxes from there. A picture of the window is saved in its folder:
  Windows draws the window alone (`WindowPicture`), so a picture never holds the rest of the desktop.
  A third walk sweeps the motion settings over two short scenes (`--sweep`, an empty `--corpus`) and
  checks the ranked report in the window and in `{data}\sweeps`.
- **The web client**, served by its development server on the port the test server allows as a CORS
  origin. The browser is pointed at the test server by answering the app's `appsettings.json` request.

The live camera is an FFmpeg test pattern served over HTTP. FFmpeg serves one client and ends when it
goes, so `LiveTestCamera` starts it again on the same port: the camera survives a reconnect.

Each WPF walk selects every tab and opens every drop-down in each dialog before closing it; binding
errors show up when a template is first used.

All three walks draw a motion zone in a camera form without saving it: *Add zone* picks the new zone
(CameraWall also adds a corner, and the web walk drags the zone with the mouse). The API client walks
press *Take Snapshot* first and wait for the answer, a picture or "Failed to capture snapshot.": the
live test camera serves one client, which the live view's stream usually holds, so the server has
no picture to give. The web walk loads the camera form again before it draws: a page load renews the
session, so this step also checks that a renewal racing a page load does not sign the walk out.

Both WPF walks also play a recording: CameraWall the one the fixture writes (with an activity index
beside it that has one event), the client the one the server recorded from the live camera. The player
must show its activity timeline; the event buttons are pressed (in the client only when the detector
found a track in the test pattern), and the zoom buttons must be there (a three-second recording is too
short to zoom). CameraWall's walk rests the mouse on the middle of the timeline and waits for the tooltip
to say "Moving: 1". The web walk plays the server's recording too and waits for the timeline (an SVG
named Activity), which shows whether or not the browser can play the file, its zoom buttons, and the
words over it when the mouse rests on it. The web and
WPF client walks also open the Timeline view and wait for the live camera's lane, then press *Play all
from here*: the live camera's tile must leave "Loading..." (the web walk accepts a tile the browser cannot
play, and saves `play-all.png`), and Pause must turn into Play. CameraWall's walk opens its Timeline
window (ribbon View) over the seeded recordings and does the same with a tile per camera. In the player,
each walk right-clicks the middle of the timeline and gives a verdict from the menu: CameraWall *Boxed
correctly* (on the seeded event), the WPF and web clients *Missed something here* (always offered); the
player says it is saved, and the WPF walks read the review back (`VerdictWalk`). It then selects the oldest recording, seeded without an activity index, presses *Analyze recording* and waits for the message that it has its timeline now: the engine decodes the file and the detector writes the index for real. The server
fixture records one camera, so the API client walks never see two tiles kept in step.

## Where to look after a run

Everything of a run is kept in `%TEMP%\AtcSoft.E2E\{yyyyMMdd-HHmmss}`:

- `camera-wall\logs`, `wpf-client\logs`: the apps' logs (binding errors are logged at Warning by
  `AtcSoft.BindingErrors`).
- `server-output.log`, `web-client-output.log`, `*-test-camera.log`: the processes' output.
- `camera-wall-ui`, `wpf-client-ui`: the UI Automation tree of every window at each failure.
- `web-client-ui`: a screenshot and the page text at each failure, and `browser-problems.txt`.

## Adding a step

Controls have no automation ids; the walks find them by their en-US text (the apps run in en-US) and
control type. To see what a window offers, run the tree dump:

```powershell
$env:ATCSOFT_E2E = "1"; $env:ATCSOFT_E2E_DUMP = "1"
dotnet test test/AtcSoft.VideoSurveillance.E2E.Tests --filter-class "*UiTreeDumpTests"
```

- **Owned windows:** UI Automation shows an owned (modal) dialog as a child of its owner, not of the
  desktop. `DesktopApp.Windows()` follows them, and leaves context-menu pop-ups out.
- **Steps go on after a failure:** `SmokeSteps` records a failed step and goes on, so one run reports
  every broken dialog. Its recovery closes what was left open.
- **Patterns, not keys:** the walks use UI Automation patterns (Invoke, SelectionItem,
  ExpandCollapse, Window.Close), not keystrokes.

## Left out on purpose

- **Opens something outside the app:** save and folder dialogs, opening the recordings folder,
  update downloads in a browser, Change Server (it starts a new process), and Start with Windows.
- **Changes or ends things:** deleting, the webhook test send, Restore defaults, signing out,
  switching the web client's language (it reloads the page), and Exit.
- **A tile's context menu in CameraWall:** it needs a real right click on the video, and from a test
  it does not open reliably once the walk has opened other windows. Its Edit Camera is the camera
  dialog the walk already opens through Add Camera. Through UI Automation, "Show in Full Screen" also
  stayed disabled on a connected tile; check that by hand before relying on it.

## What the smoke tests found (2026-10-17)

- The client's camera dialog never showed or saved the protocol, connection type, rotation, overlay
  position or RTSP transport (the drop-downs were bound to the generated structs).
- The client ignored environment variables, so the documented unattended sign-in did not work.
- Reloading the web client ten times a minute from one address signed it out (session renewal was
  under the sign-in limit, and any failed renewal forgot the session).
- The web client's user menu was labelled "Signed in as {0}".
- The web client could only reach a server on `http://localhost:5000` (no `appsettings.json`).
