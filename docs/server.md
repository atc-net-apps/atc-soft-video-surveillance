# 🖧 Server

How the headless server (`AtcSoft.VideoSurveillance.Api`) is configured and what it offers besides the REST endpoints in `src/VideoSurveillance.yaml`. The settings that the clients edit (recording folder, formats, clean-up and so on) are described in [settings.md](settings.md).

---

## ⚙️ Configuration

These are read from the server's configuration: `appsettings.json` next to the server, environment variables, or the command line. They are not part of the settings the clients can change.

| Key | Environment variable | Default | Purpose |
|---|---|---|---|
| `Storage:DataPath` | `Storage__DataPath` | `%ProgramData%\AtcSoft\VideoSurveillance` | The folder the server keeps its own files in |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, ... | The local web client addresses | Which web addresses may call the API |
| `Webhooks:Endpoints` | `Webhooks__Endpoints__0__Url`, ... | None | Where camera events are posted — see [Webhooks](#-webhooks) |

### FFmpeg

The server needs FFmpeg 9 (the libraries for the engine, `ffmpeg.exe` for live streams). Without it, it logs what is needed and what it found, and exits with code 1. Start it once with `--install-ffmpeg` and it downloads the pinned build, checks it and puts it in `%ProgramData%\AtcSoft\ffmpeg\9.0.2\bin`, where the desktop apps find it too. `ATCSOFT_FFMPEG_PATH` names a folder to use instead. See the [README](../README.md#-install-ffmpeg-building-from-source) for every folder looked in.

### Data folder

With `Storage:DataPath` set, the server keeps `settings.json` and `cameras.json` in that folder. The recording, snapshot and log folders are moved into it too, as long as they still point at their default location; a folder you picked yourself in the settings is left alone.

Use it to run a second server on the same machine, or to keep the server's data on another drive:

```powershell
$env:Storage__DataPath = "D:\Surveillance"
.\AtcSoft.VideoSurveillance.Api.exe --urls http://localhost:5000
```

The log folder is decided before the rest of the configuration is read. For the debug log to follow the data folder, give `Storage:DataPath` as an environment variable or on the command line, not only in `appsettings.json`.

The data folder also holds the server's security files. Keep them with the rest of it, and out of reach of anyone who should not run the server:

| File | What it is |
|---|---|
| `accounts.json` | The users (passwords hashed), their sessions (refresh tokens hashed) and the API keys (hashed) |
| `signing.key` | The key the access tokens are signed with. Deleting it signs everyone out |
| `keys\` | The keys the camera passwords and webhook secrets are encrypted with. Without it they cannot be read: the server logs which camera or receiver lost its secret, and it has to be entered again |
| `setup-code.txt` | The one-time code that creates the first admin. Only there while the server has no user |
| `audit.log` | Who changed what |

---

## 🔐 Sign-in and access

Every operation needs a signed-in user or an API key; only the sign-in itself (`/auth/setup`, `/auth/token`, `/auth/refresh`, `/auth/sign-out`) and `GET /health` do not. `GET /health/recordings` is for an admin.

### The first admin

A server without users writes a one-time code to `setup-code.txt` in its data folder and logs where it is (never the code). The first client to connect asks for it: whoever can read the server's data folder makes the first admin (`POST /auth/setup`). The file is deleted once the admin exists.

### Sessions

`POST /auth/token` with a user name and password answers a session:

| Token | Lifetime | Used for |
|---|---|---|
| `accessToken` | 1 hour | `Authorization: Bearer ...` on every call |
| `refreshToken` | 30 days, renewed with every use | `POST /auth/refresh` gets a new session and a new refresh token; the replaced one still works for 2 minutes (see below) |
| `mediaToken` | 12 hours | `?media=...` on the addresses a player or an `<img>` fetches itself: `/recordings-files/...` (a recording, a thumbnail) |

`POST /auth/sign-out` with the refresh token signs out. Wrong credentials answer 400, as OAuth's `invalid_grant` does; sign-ins (and the first admin's setup code) are limited to 10 a minute per address. Renewing a session has a limit of its own, 60 a minute per address: a refresh token cannot be guessed, and the web client renews on every page load.

Both clients keep the session across a restart: the web client in the browser's local storage, the desktop client in a file under `%LOCALAPPDATA%\AtcSoft\VideoSurveillance\sessions`, encrypted for the Windows user. They renew the access token a minute before it runs out, and once after a 401. Before a renewal a client reads its stored session: when another browser tab or desktop instance has renewed already, its newer refresh token is the one sent. Only a refused refresh token (400) ends the session, and the stored session is forgotten only while it still holds the refused token, so a newer one another tab saved stays. A 429, a server error or no server keeps the session (the call that needed the renewal fails), and no renewal is tried for 15 seconds, so the calls in between do not run into the renewal's limit. A web tab whose stored session another tab removed (signed out) signs out too.

**Replaced refresh tokens.** Each renewal replaces the refresh token. For 2 minutes the replaced token still renews, answering with the session's newest refresh token (not another new one) and fresh access and media tokens: tabs that wake at once, or a renewal whose answer was lost to a reload, all end up with the same token. This is kept in the server's memory only. The replaced token used after its 2 minutes means it was most likely copied: the session ends, and the server logs a warning naming the user. Only the token just before the current one is recognised so; older ones are simply refused. After a server restart the replays are forgotten: the token just replaced is refused (not taken for a copy) until its 2 minutes are over, then it ends the session as above. Signing out works with the current token or one replaced within its 2 minutes.

For an unattended desktop client (a wall screen), `Auth:UserName` and `Auth:Password` in its configuration sign it in without the dialog.

### Roles

| Role | May |
|---|---|
| `viewer` | Watch, list, read the events, play recordings |
| `operator` | Also add, change and delete cameras and layouts, take snapshots, start and stop recordings, delete recordings |
| `admin` | Also change the settings, the webhooks, the users and the API keys, and read the audit log |

The roles are declared per operation in `src/VideoSurveillance.yaml` (`x-authorize-roles`). An admin manages the users and the API keys in either client (the web client's *Users* page, the desktop client's *Users* in the backstage); the last admin cannot be removed or made something else.

### API keys

For a program that calls the server (Home Assistant, a script): an admin makes a key with a name and a role, and it is shown **once**. Send it as `X-API-Key: vsk_...`. The server keeps only its hash.

### Audit log

Every change that succeeded (anything but a read, a sign-in or a lease renewal) is written to `audit.log` with the time, the user or key, the operation and what it was done to. Refused sign-ins and setup attempts are written too. `GET /audit` reads the latest entries.

### Secrets at rest

The camera passwords in `cameras.json` and the webhook secrets in `webhooks.json` are encrypted with ASP.NET Core Data Protection (`enc:v1:...`), with the keys in `keys\`. A file written before this is encrypted the first time the server reads it, its backup included. The keys are not encrypted themselves, so the folder can move to another machine: a copy of `cameras.json` alone gives no password away, a copy of the whole data folder does. The cameras in memory keep their passwords as they are, since the streams need them; no password or secret goes to a client or into a log.

### HTTPS

Give the server an HTTPS address (`--urls https://+:5001`, a Kestrel endpoint with a certificate, or `https_port` behind a proxy that ends HTTPS) and it sends plain HTTP on to HTTPS and tells browsers to stay there (HSTS; not for `localhost`). Not in Development, where Aspire also hands the clients the plain address. Give the clients the `https://` address: an HTTP client does not send the `Authorization` header on to a redirected address. Without HTTPS, the tokens, and the media token in the addresses, cross the network readable.

---

## ❗ Errors

Every error the server answers is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem, `application/problem+json`, as `src/VideoSurveillance.yaml` declares:

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "Not Found", "status": 404, "detail": "Camera 6f0c… not found." }
```

- `detail` says what went wrong, in words a person can read. A client shows it as it is.
- The framework's own errors are problems too: an address the server does not have (404) and a body that is no JSON (400).
- A value outside the limits in the specification (a name of 1-256 characters, a port of 1-65535, a timeout of at least a second, ...) is refused before the handler runs: a 400 validation problem with one entry per field in `errors`, keyed by the name the client sent (`port`, `displayName`). Nothing is stored.
- The clients show the server's words: the field messages of a 400, else `detail`. The desktop client in its "Failed to ..." dialogs, the web client after the page's own sentence.
- An unhandled exception is a 500 problem without the exception's text. In Development the developer exception page shows it instead.
- A snapshot of a camera that exists but gives no picture is 503; 404 is a camera the server does not know.
- A 503 that will pass says when to ask again in `Retry-After` (seconds): 5 for a snapshot of a camera that is still connecting, 30 for a recording disk that cannot be read.
- Too many calls from one client address are a 429 with `Retry-After` (see *Limits, caching and retries*).

In a handler, the generated factories make the problem: `NotFound("…")`, `Conflict("…")` and `ServiceUnavailable("…")` and `BadRequest("…")` take the message, and `retryAfter:` where the specification declares the header.

An enum is written and read as the specification writes it: `rtsp`, `topLeft`, `rotate90`. Clients built on generator 2.x sent the C# member name (`Rtsp`, `TopLeft`); the server refuses that with a 400, so update the server and the desktop client together.

---

## ✏️ Changes

An update changes only what it sends. A property the request leaves out keeps its value: in `PUT /settings`, `PUT /cameras/{id}` and `PUT /layouts/{id}`. So a client that does not know a property yet cannot reset it. To clear a text, send it empty: a camera's password, for example, is removed with `""` and kept when it is left out.

A response lists in `required` everything the server always sends. What it may leave out is optional: a camera's connection state and USB settings, a recording's thumbnail.

A create (`POST /cameras`, `/layouts`, `/webhooks`) answers 201 with the new resource, and its address in `Location` (`/cameras/{id}`).

---

## 🛡️ Limits, caching and retries

All three are declared in `src/VideoSurveillance.yaml`, and the generator builds them; nothing is configured by hand.

- **Rate limits** (`x-ratelimit-*`), per client address. A call over the limit is a 429 problem with `Retry-After`.

  | Operation | Limit | Why |
  |---|---|---|
  | `GET /cameras/{id}/snapshot` | 20 per 10 s | a snapshot opens the camera's stream |
  | `POST /webhooks/{id}/test` | 10 per minute | a test sends a request to someone else's server |
  | `POST /auth/token`, `POST /auth/setup` | 10 per minute | guessing a password or the setup code |
  | `POST /auth/refresh` | 60 per minute | renewals from several browsers behind one address; a refresh token cannot be guessed |
  | `POST /auth/setup`, `POST /auth/token` | 10 per minute | guessing a password or the setup code |

- **Output cache** (`x-cache-*`): `GET /devices/usb` is kept 5 seconds, since enumerating the devices takes a moment. A USB camera plugged in or pulled out drops it at once (`UsbDeviceCacheEvictor`), so the next read has the new list. A cached answer has an `Age` header.
- **Retries in the clients** (`x-retry-*`): both clients call the generated `AddApiResilience()`. An attempt has two minutes and all attempts together five, since some calls are slow by nature (starting a recording waits for the camera; saving a changed folder layout moves the recordings). A failed read (5xx, 408, a timeout, a 429 that asks for a short wait) is tried up to 3 more times, after the wait a 503 or 429 asks for in `Retry-After`. A change is sent once: POST and PATCH are never repeated, and every PUT and DELETE has `x-retry-enabled: false`. A new PUT or DELETE needs it too. `GET /events` is not repeated either: its reader resumes on its own.

The specification is validated `Strict`. Where a rule does not fit one operation, the operation switches it off with `x-atc-suppress` and says why (`captureSnapshot` also saves the picture; `stopRecording` stops rather than deletes).

---

## 🌊 Events

`GET /events` carries the camera events - connection, recording, motion and USB - as one stream of Server-Sent Events. It is how the server tells the clients what happens. Both clients read them here (the WPF client with `SurveillanceEventService`, the web client with `EventStreamService`):

```bash
curl -N "http://localhost:5000/events?cameraId=47c2d0ad-b1c1-424f-91de-70d7ef99b279"
```

```text
id: 412
data: {"type":"recordingStateChanged","cameraId":"47c2d0ad-...","timestamp":"2026-10-15T09:00:00+00:00","recording":{"cameraId":"47c2d0ad-...","newState":"recording","oldState":"idle","filePath":"...","timestamp":"..."}}
```

- **One model:** `SurveillanceEvent` has a `type` (`connectionStateChanged`, `recordingStateChanged`, `motionDetected`, `usbCameraLifecycleChanged`, `activityChanged`) and the matching part (`connection`, `recording`, `motion`, `usb`, `activity`). `activityChanged` comes from every recorded camera, watched for motion or not: once when something starts to move somewhere (a confirmed track) and once when nothing has moved for five seconds or the recording ends, with the recording and offset to play it from. The timelines of both clients grow by it. The states are written as the REST API writes them (`connected`, `deviceUnplugged`, `recordingMotion`, `replugged`). A client reads the part that is filled; a type it does not know has none it knows, and is read past.
- **Resume:** every event has an id. Send the last one you read as `Last-Event-ID` and the stream goes on after it, from a buffer of the latest 256 events; `X-Events-Missed` says how many the buffer no longer had. The ids start from the time the server started, so an id from before a restart gets the whole buffer and `X-Events-Missed` of at least 1: what happened while the server was down is not known, and a client loads what it shows again (the WPF client's `OnMissedEvents`).
- **Heartbeat:** the response headers come at once; then a named `ping` event after `heartbeatSeconds` (15) without an event, so a proxy does not close the idle connection. The generated clients read past it.
- **A slow reader** is cut off and resumes; it never holds up the others.
- **Filter:** `?cameraId=` for one camera.

The web client's pages read it with `EventStreamService` and the desktop client with `SurveillanceEventService`; both resume by themselves. The desktop client's status bar and its "connection lost" toast follow it too.

---

## 📼 Recordings and thumbnails

`GET /recordings` lists the recordings, newest first. Each entry has:

| Field | Meaning |
|---|---|
| `filePath` | Where the recording is, below the recording folder, with forward slashes |
| `thumbnailPath` | Where its thumbnail is, the same way; `null` when it has none |
| `startedAt` | When it started. Taken from the file name, which is in UTC on the server |
| `duration` | Its length, ISO 8601 (`PT1M5S`). Empty while it is being recorded, when the file cannot be read, or when the server has not read the file yet: lengths are read in the background after a start, so a large archive fills in over the following listings |
| `inProgress` | `true` while it is still being recorded |
| `id` | The same for a file every time it is listed |

The files themselves are fetched from `/recordings-files/{filePath}` and `/recordings-files/{thumbnailPath}`, signed in, with an API key, or with the session's media token as `?media=` (a video player or an `<img>` cannot send a header). Escape each folder and file name of the path on its own (a camera name may contain `#`, `?`, `&`, `+` or `%`). Only recordings (`.mp4`, `.mkv`, `.avi`) and thumbnails (`.png`) are served, from the recording folder that is in the settings at the time of the request. Every camera records below that folder: a recording folder of its own, which a camera imported from CameraWall can carry, is not used by the server (it logs a warning once per camera), so nothing is recorded where the server cannot list, serve or clean it.

`DELETE /recordings?filePath={filePath}` deletes one finished recording, its thumbnail, its activity index, its preview pictures and its review, and the day folder it leaves empty. `filePath` is the path the listing returned. The answers:

| Answer | When |
|---|---|
| `204` | The recording is deleted |
| `409` | It is still being made, by the server or by another program that has the file open |
| `404` | There is no such recording |
| `400` | The path does not name a recording: it leads out of the recording folder, is a full path, or is a thumbnail or any other file. Nothing is touched |

The server makes a thumbnail for every recording file, the way the standalone app does: one picture or a 2 x 2 grid (the *Thumbnail tiles* setting), stored next to the video or in `thumbnails\` (the *Keep thumbnails in a separate folder* setting). Thumbnails are drawn with GDI+, so a server that is not running on Windows records without them.

Beside every recording file the server writes its **activity index**, `{file}.activity.jsonl`: one line for every analysed second, with how much of the picture differed from the learnt scene and how many moving things were followed. A camera that is recorded is analysed (two to four pictures a second) even when it is not watched for motion; that analysis reports no motion, so no badge, webhook or motion recording comes of it. A recording analysed afterwards (*Analyse recording*) is read from its start with no learnt scene, so its first 3 s only teach the scene and show nothing moving (`RecordingAnalysis.WarmUpSeconds`). The index moves, is protected and is deleted with its recording.

When a recording or segment ends, the server makes its **preview pictures** in the background, one recording at a time: `{file}.previews.jpg`, small tiles (the longest side 160 px) evenly over the recording, at most 600, and a tile of the busiest second of each event, with their index `{file}.previews.json`. `GET /recordings/previews?filePath={filePath}` says where each tile is and gives `imagePath`, fetched from `/recordings-files/` with the media token like a thumbnail. A finished recording without previews (one from before them) is queued by that call, and the answer says `isBeingMade`; a recording still being written has none until it ends. An analysed recording gets them again, with its events' tiles. They move, are protected and are deleted with their recording.

`GET /recordings/activity?filePath={filePath}&buckets={n}&durationSeconds={s}` gives a recording's activity for a timeline: `n` equal stretches (1 to 4000, 500 when left out) over `s` seconds (the index's own length when left out), each with the most that happened in it, never the average, so a five-second event is not lost in an hour. A stretch with `isAnalysed` false is unknown, not quiet. A recording made before the index has `hasIndex` false. The index of a recording in progress grows every second, so the answer is not cached.

`POST /recordings/review/verdicts?filePath={filePath}` (operator and admin) adds a person's verdict on a stretch of a finished recording, as a timeline's right-click gives it: `fromSeconds`, `toSeconds` (seconds into the recording), `verdict` (`correct`, `falseBox`, `missed` or `wrongCount`), and optionally `count` and `label`. The server adds it to the review beside the recording, `{file}.review.json`, which a recording without one gets with its camera's motion settings and zones (turned to the recording's rotation). Reviews are the truth real recordings are scored against when the detector changes (see [Motion Detection Storybook](motion-detection-storybook.md)). A recording still being made (`409`), a stretch that does not end after it starts (`400`) and a review that cannot be read (`409`; it is not written over) are refused. The review moves, is protected and is deleted with its recording.

`GET /timeline?from={from}&to={to}&cameraId={id}&buckets={n}` gives one lane per camera over a span of at most seven days: where recordings cover it (from the video files, so a recording without an index still counts as recorded), the span in `n` equal stretches (1 to 4000, 1000 when left out), each recorded or a gap, analysed or not, with the most that happened in it, the gaps with their reason (`offline`, `notRecording`, `diskFull`, `serverDown`, or `unknown` before the server kept a history), and the events to the second with the recording and offset to play each from. `GET /timeline/events?after={moment}` (or `before`) finds the nearest events across the recordings, looking at most seven days away (`limit` 1 to 100). The server reads each activity index once into a short summary and keeps the summaries in memory; a cold day of ten cameras takes under a second.

The reasons come from a history the server keeps in `{data}/availability`: one `{cameraId}.jsonl` per camera with its changes (online, offline, recording started and stopped, stopped by the disk-space guard) and `server.jsonl` with the server's starts and stops (`AvailabilityHistoryService`). Every 30 seconds the server writes the time to `server.alive`; a server that stopped without saying so (a crash, a power cut) is noted at its next start as stopped at that time. A gap takes the state at its start and is cut where the state changes on the way; a stopped server comes before an offline camera, and that before a full disk. Both clients colour a gap by its reason (`TimelineGapColors` in Core, the web client's copy kept equal by a test), name it when the pointer rests on it, and show a legend.

---

## 📺 Live streaming

Both clients watch a camera through HLS, on a **lease**:

| Call | What it does |
|---|---|
| `POST /cameras/{id}/stream` | Takes a lease on the camera's stream. Answers `200` with `leaseId`, `playlistUrl` (below `/streams/`) and `renewSeconds` once the stream can be played (up to a minute for a camera slow to give its first segment); `404` for an unknown camera; `503` with the reason and `Retry-After` when the stream could not be started |
| `PUT /cameras/{id}/stream/{leaseId}` | Renews the lease, every `renewSeconds` (30) while the viewer watches. `404` when the server no longer has it (it ran out, or the server restarted): the viewer starts the stream again |
| `DELETE /cameras/{id}/stream/{leaseId}` | Gives the lease back; one that is already gone is no error |

The stream runs while one lease on it is alive: viewers share it, and it stops with the last lease.

- A network camera's stream is made by the FFmpeg command-line tool. It is taken from the folder the video engine loads its FFmpeg libraries from, so it needs no entry in the `PATH`; without it there, the `PATH` is searched.
- A network camera's video is passed on as it is (no re-encoding), so a rotated camera is **not** rotated in the stream. A client turns the picture itself, from the camera's `rotation`.
- **A USB camera has one pipeline for everything.** Windows lets one program at a time open a USB camera: a second opener connects and gets no pictures. So the server keeps the camera's one pipeline (`ICameraPipelineHost`, implemented by `CameraConnectionService`), and a live view, a recording someone starts, a snapshot, record-on-connect and motion watching all use it. Watching and recording work in either order, and stopping one leaves the other running. The camera is opened for the first of them and closed when nothing needs it any more.
- A USB camera is therefore not streamed by the FFmpeg tool: the server's video engine writes the playlist and segments itself, from the same encoder as the recording (a USB camera sends uncompressed pictures, so both are encoded to H.264; that costs processor time for every USB camera in use). A camera that captures sound is streamed with it (AAC, from the same sound encoder as the recording); the tile starts muted and the viewer turns the sound on.
- A USB camera that cannot be opened (unplugged, or the server is reconnecting to it for its own recording) answers the live view and `POST /cameras/{id}/recording` with the reason (`409 Conflict` for the recording).
- A lease that is not renewed for two intervals (a minute) runs out, so a viewer that goes without a word (a closed tab, a laptop that sleeps) does not keep the camera's transcoder running. `HlsStreamReaperService` sweeps the leases and the streams.
- A camera's user name and password are removed from everything the server logs or sends back about a stream.

---

## 🏃 Motion detection

A camera with *record on motion* on (the application's setting, or the camera's own override) is kept open by the server and watched.

- **What counts as a motion:** from the first picture in which something moved until the scene has been quiet for the post-motion time. Something that pauses for a moment does not end it.
- **Recording:** a motion starts a recording and its end stops it. After a motion recording, the next one waits for the cooldown. A camera that is *recorded on connect* as well is recording anyway; watching it only makes the motion events.
- **Events:** clients get `motionDetected` on `GET /events` for every analysed picture with movement (with the boxes, for overlays) and once when the motion is over. Webhooks get `motion.started` and `motion.stopped`, once each.
- **How it looks:** up to four pictures a second, compared with the same analysis as the desktop app, at the analysis size from the settings. A higher analysis frame rate in the settings is capped at four.
- **Cost:** each analysed picture is captured as a PNG and decoded again. Two USB cameras at 1280x720, both watched and recorded (which for a USB camera includes encoding), used about 1.4 processor cores.
- **Windows only:** the pictures are decoded with GDI+. On another platform the server logs that motion detection is not available and runs without it.
- **Settings are read when a camera starts being watched.** A change to the motion settings reaches a camera the next time it is opened; turning record-on-motion on or off takes effect within half a minute.
- **USB cameras:** a watched USB camera is held by the server; the web client's live view of it is served from the same open pipeline.
- **Starting a recording by hand** for a watched camera opens the camera a second time. That works for a network camera and fails for a USB camera.

---

## 🔔 Webhooks

The server can post what happens to other systems (Home Assistant, Slack, a script of your own). Every receiver under `Webhooks:Endpoints` gets an HTTP `POST` per event.

```json
{
  "Webhooks": {
    "Endpoints": [
      {
        "Url": "https://example.org/hooks/surveillance",
        "Secret": "a-long-random-text",
        "Events": [ "motion.*", "camera.disconnected" ]
      }
    ]
  }
}
```

| Setting | Meaning |
|---|---|
| `Url` | Where to post. `http` or `https` |
| `Secret` | Optional. When set, the body is signed |
| `Events` | Optional. The events this receiver wants, by name or by group (`motion.*`). Left out: all of them |

### Receivers added while the server runs

Receivers can also be added, changed, tested and removed without touching the configuration: on the **Webhooks** page of the web client, or over the API.

| Call | Does |
|---|---|
| `GET /webhooks` | Lists all receivers, those from the configuration first |
| `POST /webhooks` | Adds one (`url`, optional `secret`, optional `events`) |
| `PUT /webhooks/{id}` | Changes one. Without `secret` the secret stays as it is; an empty `secret` removes it |
| `DELETE /webhooks/{id}` | Removes one |
| `POST /webhooks/{id}/test` | Sends a `webhook.test` event to that one receiver and answers whether it took it |

- They are kept in `webhooks.json` next to `settings.json`, secret included, like the camera passwords in `cameras.json`.
- A secret is never sent back: a receiver only says whether it has one.
- A receiver from the configuration is listed, and can be tested, but is changed in the configuration (`409 Conflict` otherwise).

### Events

| Event | When | `data` |
|---|---|---|
| `camera.connected` | A recorded camera connected | `state` |
| `camera.disconnected` | A recorded camera was lost, or stopped being recorded | `state` |
| `recording.started` | A recording file was started | `filePath` |
| `recording.stopped` | A recording file was finished | `filePath` |
| `motion.started` | Motion began | `changePercentage` |
| `motion.stopped` | Motion ended | `changePercentage` |

A recording that is split into segments reports a stop and a start for every file. `filePath` is the path `GET /recordings` lists the file under, so a receiver can fetch it from `/recordings-files/`.

The server follows a camera's connection only while it records it, so the camera events follow the recording. Motion events come when the server detects motion. Server-side motion detection is not built yet (see the roadmap), so today no motion events are sent.

### The request

```http
POST /hooks/surveillance
Content-Type: application/json
X-AtcSoft-Event: recording.stopped
X-AtcSoft-Delivery: 5f1c0e6d6f4b4a0b9d3c0c7a4c1f2e11
X-AtcSoft-Signature: sha256=6d0f...

{
  "event": "recording.stopped",
  "cameraId": "47c2d0ad-b1c1-424f-91de-70d7ef99b279",
  "cameraName": "Front Door",
  "timestamp": "2026-10-13T10:15:00+00:00",
  "data": { "filePath": "Front Door/2026-10-13/Front Door_20261013_101500_000.mp4" }
}
```

- **Signature:** `sha256=` followed by the HMAC-SHA256 of the body, keyed with the secret, in lower-case hex. Compute the same over the raw body you received and compare.
- **Delivery id:** the same for every attempt of one delivery, so a receiver can ignore a repeat.
- **Body:** the `WebhookPayload` schema in `src/VideoSurveillance.yaml`. `data` has `filePath` (recording events), `state` (camera events), `changePercentage` (motion events) or `message` (the test event); a field the event has no value for is left out.

### Delivery

- A receiver that answers with a server error (5xx), `408` or `429`, or cannot be reached, is tried three times.
- A receiver that refuses the request (any other 4xx) is not tried again.
- A receiver has 10 seconds to answer.
- Events wait in a queue of 500. When receivers are slower than events arrive, the oldest are dropped and the server says so in its log.
- The log names a receiver by scheme and host only, and never contains the secret.
- Receivers in the configuration are read when the server starts. Receivers added through the API or the web client take effect at once.
