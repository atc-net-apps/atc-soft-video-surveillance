# Changelog

## [1.2.3](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/compare/v1.2.2...v1.2.3) (2026-09-22)


### Bug fixes

* **ci:** widen the NuGet publish verification window to 15 minutes ([0510c16](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/0510c162fadb192cf85c6540ebf5429c10c1c4ac))


### Documentation

* **nuget:** give every package its own README ([ee9a073](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/ee9a073afd2bb91161ae27ac1c8fd04a8db12f7e))

## [1.2.2](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/compare/v1.2.1...v1.2.2) (2026-09-22)


### Bug fixes

* **ci:** stop producing empty symbol packages ([538bbdc](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/538bbdcf6cdd521c19f0eb42976479bfb0210547))

## [1.2.1](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/compare/v1.2.0...v1.2.1) (2026-09-22)


### Bug fixes

* **ci:** publish installable NuGet packages via Trusted Publishing ([d89b57c](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/d89b57ccd6ed62d16aa5e74a016d7d158ee0ce32))


### Documentation

* describe the published NuGet package set ([76deaa5](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/76deaa5c40306f5c46e619516ab4d31d64d129b2))

## [1.2.0](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/compare/v1.1.1...v1.2.0) (2026-09-18)


### New features

* move data roots from Linksoft to AtcSoft ([80c6a38](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/80c6a388dd8d84876575dca763f99a7ecad035d8))
* rebrand both installers to AtcSoft ([9220cbd](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/9220cbdb64a8d541c0d41928c44855f177a6f215))
* rebrand the remaining user-visible Linksoft strings to AtcSoft ([86b94cc](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/86b94cc43a56af18359e5f3b6bf34957e3a66927))
* rename product and namespaces from Linksoft to AtcSoft ([0668929](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/06689295fe602003e2bef383bb4f762e66e5a4f5))


### Bug fixes

* **ci:** fail the release when the NuGet push publishes nothing ([8b0051b](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/8b0051b674c0492392d8f7f90354bda530766206))


### Documentation

* rename Linksoft to AtcSoft throughout the documentation ([9e2c04e](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/9e2c04e234e627c4b9a5b7ec5ca6b3d93a90cdb2))

## [1.1.1](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/compare/v1.1.0...v1.1.1) (2026-09-17)


### Bug fixes

* **ci:** point the FFmpeg download at a URL that still exists ([d4bfaef](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/d4bfaefbcf0817dc324e1c8a2615603e756b3c4b))

## [1.1.0](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/compare/v1.0.11...v1.1.0) (2026-09-17)


### New features

* add disk space guard to prevent recording drives from filling up ([1a4e8bb](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/1a4e8bb513b9774b357bd57a35e108028faf186c))
* **camera-wall:** integrate USB cameras into standalone wall app ([5fd61b8](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/5fd61b833d9d9bb4fdd872d348d9b7fb3c8eb20b))
* **core:** add USB camera models, services, and lifecycle coordinator ([5d06a00](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/5d06a001054ac4da6efee6370371dd1e0ba6b403))
* **server:** expose USB device endpoint and lifecycle broadcasts ([7f63193](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/7f631931d240c34fb933eba1fab39d15cd755a33))
* **usb:** add USB source pill badge to VS.Wpf camera tiles ([53d8d70](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/53d8d709190cc69d067cf38b84e4ebf62eee8cb9))
* **usb:** add usbAudioDeviceName to API contract and mapping ([c71b387](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/c71b387046f8810b58f52aa7f8e00e127bd89ce9))
* **usb:** close Phase 4 — lock device picker after create and add Test Connection coverage ([ce12bd3](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/ce12bd3df5b3988d9864e553b2aa90289ac1416a))
* **usb:** surface "device in use by another app" as a distinct overlay hint ([25edc48](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/25edc48211c4b0cbdedcc2119267f8dbbb890106))
* **usb:** surface unplugged device indicator in camera overlay across both editions ([ed2a5a0](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/ed2a5a0b926155ed0ed7ab6bac3acee4d2fcd96f))
* **videoengine-windows:** add Media Foundation enumeration and USB hot-plug watcher ([142f8dd](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/142f8dd81cb76db6d00b7b72efafb07b4311e6f3))
* **videoengine:** add input format kind and dshow option support ([2c3f304](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/2c3f30439f6ede445cd1aca3978cd0a05f9c5457))
* **wpf-client:** add Export Diagnostics report for support triage ([79619db](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/79619db8102a0d6e9beaa3d476aa27d548477bff))
* **wpf-client:** add Start-with-Windows toggle via HKCU Run key ([1769b49](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/1769b49d1d6398859ee88572ccd103ea574861d3))
* **wpf-client:** add USB picker UI and remote enumeration over SignalR ([202cc67](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/202cc6778c2b294514659cbea169e9baef8afb66))


### Bug fixes

* **recording:** hard-gate recording start when disk reclaim fails ([8e991ee](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/8e991ee59ef497d9529811243faa44e96d7f5458))
* **reliability:** add mid-recording disk-space guard (Bug 5) ([3f999fc](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/3f999fcf4e5bf0eb6164a1897bb7d9c349d7fd75))
* **reliability:** address concurrency, IPv6, and reconnect-backoff bugs ([7064487](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/706448780b7bd35fb617d66110a6710e8c72fd40))
* **reliability:** fix motion detection bugs — swallowed exceptions, wrong FPS default, diagnostic leaks ([1b9dd72](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/1b9dd72584c4867e1e57b10c2d0cba1c2a2f5683))
* **reliability:** fix WPF SegmentRecording TOCTOU race — use TryUpdate instead of indexer ([dfda2f9](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/dfda2f954fb21c531c498e72937076a79b0af0b8))
* **security:** pin Microsoft.OpenApi above vulnerable transitive 2.0.0 ([5ffaacf](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/5ffaacf5c0c02eb87ad362f82dafb2aab24eb969))
* **settings:** wire SelectedKey on MaxRecordingDuration combobox ([7d20be6](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/7d20be61477536a8e25c6f8a463caa21f6fe690f))
* **theme:** replace hardcoded colors that break in light mode ([963d470](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/963d470f84e2790333ea7f2de0bf33cf69607afa))
* **usb:** broaden device-busy classifier — match any read-error burst on dshow, not just EAGAIN ([6b4c4de](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/6b4c4de8e5c8ab7a033a620ebfa378a7bb389c6f))
* **usb:** clear coordinator unplugged-state on camera delete ([71ba3be](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/71ba3bed60b54c6eea58a3afd42db1434a728b22))
* **usb:** make standalone USB cameras connect and render ([34f78ef](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/34f78efc1340d5383f31fbab5f00f96ba510da74))


### Refactorings

* drop the AVERROR_EAGAIN equality and treat any error burst on a dshow input with zero frames received as DeviceBusy. The existing gates already exclude the false-positive cases — inputFormat == Dshow rules out network cameras, and anyFramesReceived rules out mid-stream hardware hiccups. lastErrorCode is still logged via VideoPlayer.LogReadErrorCode for diagnostic purposes; the discarding underscore parameter makes its retained-for-logging role explicit. The expanded comment block captures the empirical reasoning so a future reader doesn't re-narrow the heuristic without seeing the data that broadened it. ([6b4c4de](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/6b4c4de8e5c8ab7a033a620ebfa378a7bb389c6f))
* **hosting:** adopt Atc.Hosting BackgroundServiceBase for periodic workers ([83a287f](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/83a287f93d6cb7a53252ecb26362412ada6f7ec2))
* **hosting:** move SurveillanceEventBroadcaster onto BackgroundServiceBase ([4dc909b](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/4dc909bddf45aa8004e10ca6b3d498b6dafd46f1))
* **wpf:** swap BooleanToVisibilityConverter for singleton converter ([fe93c60](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/fe93c60657a402d7082fe267279cca68b7b88a22))


### Documentation

* add release-please automation design spec ([14afc40](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/14afc408e17d1a1bdb5fc7a93590119ab1a69d27))
* add release-please implementation plan ([5b0907f](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/5b0907f24e5ede4714c55aca9d9d6a1b6a0da2a7))
* add roadmap-suggestions.md — codebase audit + competitive analysis ([1073dda](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/1073dda8881e74c8b9ce3c8bcd3e8ce221267b1f))
* correct the plan's verification steps to what actually worked ([d7b99f9](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/d7b99f9b74814a7a2e27d163fed6fedd3dfacd1e))
* correct the versioning row to release-please ([b079917](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/b07991746b767dabcc6cc9bb49a204163dfa1918))
* update OpenAPI version references to 3.2 ([2ed5f78](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/2ed5f78ee3c3d41b1cbccc8a0830c9f5786185ec))
* **usb:** add USB camera roadmap and design notes ([e73f47e](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/e73f47eac11d6d24dce306f28b42727f1429aed5))
* **usb:** close §8 storage hardening with finding — no code change needed ([d99a230](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/d99a230063f8c46dce426c0b57cb0ee986acbe26))
* **usb:** close Phase 12 — settings reference + OpenAPI sanity check ([3151a51](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/3151a51f54d65f4c943d6ead0a13ecba75047dc2))
* **usb:** close Phase 4 and rewrite Phase 8 overlay design ([826742e](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/commit/826742ed63391f3f935038f906dd19d5cdc2c6f3))
