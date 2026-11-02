# Changelog

## [1.4.0](https://github.com/atc-net-apps/atc-soft-video-surveillance/compare/v1.3.0...v1.4.0) (2026-11-02)


### New features

* **docker:** compose file with optional HTTPS through Caddy, and a health check ([0d43e87](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0d43e87ceed16a9ec6c691dd61bbbfac82a4777f))
* **docker:** the setup code for the first admin in the container's log ([f7b1a37](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/f7b1a370c77158276f2806e82797f61883fddf0d))
* **engine:** find and load FFmpeg 9 on Linux and macOS ([d443a9f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/d443a9f7f6b0f1cd9835772fab119f060f5a8593))
* **iis:** a web.config for IIS as a reverse proxy in front of the server ([4736adf](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/4736adf85341718f6e8bfb951ccfb922c560c459))
* **installer:** open the web client from the server installer's last page ([9f04234](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/9f042340d87379b6b4db51aac1116c28e0a83b34))
* **server:** build and run on Linux, and a Docker image ([3e78a61](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/3e78a61bc60ed22a725a89ffb2832dde82b008e6))
* **server:** create the first admin without the setup code on the server's own machine ([224b199](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/224b199063912f13cfff3b44c3b0075e6a685cb4))
* **server:** recording thumbnails and previews on Linux and macOS ([ee6ddde](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/ee6ddde5ad4a5826e0f6fd6c28c954acee4ed4f5))
* **server:** reset a forgotten password with --reset-password &lt;user&gt; ([717375e](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/717375ec693ba115881184869fd7608df7b20506))
* **server:** run as a Windows service, installed by a server MSI ([5bfaea0](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/5bfaea064cc481816b3429714ff48b054058abac))
* **server:** serve the server and its web client under a path of a proxy's site ([71557da](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/71557da4ec3047dfa289c236a0f9e1a8f2629343))
* **server:** serve the web client from the server's own address, on port 8585 ([df158a5](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/df158a50a532b920ef82f71558b19de29d2685ad))
* **server:** take the client's address from a trusted reverse proxy ([0b5ca18](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0b5ca18b695fe04965c6af9b510aaa8c97794d09))
* **server:** the Windows service writes its log to the data folder ([85770d0](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/85770d04aa092ae75e204f553303a0990db550e5))


### Bug fixes

* **installer:** give the server service's account the files a hand-run server wrote ([103f4c3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/103f4c395c377d019a47d57640fa9dac374168b1))
* **server:** a snapshot of a camera nobody watches waits for its first picture ([8901bb8](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/8901bb82844e60e3a3b1ff7f1bc1ff1296b162dd))
* **server:** first admin without the setup code only at localhost ([cc10279](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/cc10279ef8120429910899fff640ceb08787993d))


### Performance improvements

* **server:** live view shares the connection of a camera the server has open ([da51713](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/da517131a1863ec5832c9c354b8fc9bacaa9138b))


### Documentation

* every place the pinned FFmpeg build is named ([4e115ef](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/4e115ef3a9d75f9b27cd77d8140bb42315f16c43))
* how the server is hosted and installed, and its tests on Linux ([95a4bd4](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/95a4bd44c2f1deb6d379255c5ac55d25b8dc828a))
* **iis:** the server under a path of an existing IIS site ([6193237](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/6193237c95755abfc440b930a71f5e4797c9325c))
* local setup only at localhost in the server rules ([1496157](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/149615727ea4df9314723ce03673695bff3cb89f))
* the first admin and the password reset in the server rules ([fd92278](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/fd922781040e1728bf7af6ac32e3aae91fc56e5b))

## [1.3.0](https://github.com/atc-net-apps/atc-soft-video-surveillance/compare/v1.2.5...v1.3.0) (2026-11-01)


### New features

* **api:** list recordings for a span or the newest few, and the web page fetches only the newest after an event ([1760f5a](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/1760f5ab80b230ff07d6c6097f1537c80069b5dd))
* **camerawall:** diagnostics for a long test run ([33167e3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/33167e302236e3beba63662a7fb6e95e8142f047))
* **recording:** a new segment starts at the key frame it is named by ([39f2814](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/39f2814eb288138cc6c7a79e5e8e0db4c3235b57))
* **recording:** motion recordings start a few seconds before the motion ([0419bcf](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0419bcf051f84005c7c42cd7ba8def778e7bec8b))
* **server:** decode on the graphics card when Video:HardwareDecoding is on ([88c932c](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/88c932c6ced51eb40b926131d4542c574384926d))
* **web:** the live view starts again the cameras the viewer had playing after a reload ([4e26026](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/4e26026af6863718bc02d1fff21c162597907334))
* **wpf:** add column filters and search to the lists, and pick several cameras for recordings ([fe36e9e](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/fe36e9e94bc40e04dc78200ed1cc5d9905aad6bb))
* **wpf:** filter the notification history by kind ([f24d21f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/f24d21fdc3606a6cb23fdd5d6428d270d8efd81f))
* **wpf:** pick the grid size and the camera source with segments ([0e6dec1](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0e6dec1dd30455ef0439829ea67b084f422ac5e3))
* **wpf:** remember the CameraWall window's size and place, and restore the client's size ([6d15374](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/6d15374f9984a7e960ec3afaf99b4baedc7c8be2))


### Bug fixes

* a dead event stream connection is noticed and opened again ([e339f8e](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/e339f8e66e34ed74861edaadef969cc3fbead14f))
* **api:** answer file errors in snapshots and deletes with the reason ([32ca53f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/32ca53f65fb01a24f40455ec15ae02c6fc878e67))
* **camerawall:** a camera keeps its player and recording when tiles swap or layouts change ([120e1ce](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/120e1ce00caad5e45b7eb0c36f068a2253019391))
* **camerawall:** a camera that records on motion and never had a tile is watched ([6db48dd](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/6db48dde278af825b9b8c57b7e5a64a548f5e52a))
* **camerawall:** a camera that records on motion is watched and recorded while no layout shows it ([63d14e2](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/63d14e216fc46e563b2af4ed3cfc17bbd0c31818))
* **camerawall:** a camera whose address cannot be read fails to connect instead of ending the app ([33c3085](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/33c3085df6afc64b7475e61a69d3b7a51dda0c92))
* **camerawall:** a cameras or settings file that cannot be read is never replaced by an empty one ([6f7990f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/6f7990fa8f506dfc7cf72976693a7f52b3174ff8))
* **camerawall:** a motion recording ends after the post-motion time whichever thread started it ([776464f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/776464ff3f4d8c640d616756edfa4a69252cb4e0))
* **camerawall:** a recording camera that no layout shows is opened again when its stream drops ([a420cd0](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/a420cd00fb7d51aa8c8e077fc0ce452d28c96c7c))
* **camerawall:** a recording played in full screen is let go when its window closes ([1cfbfca](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/1cfbfca20631e3eb705f40006d6e52f907f3e2ce))
* **camerawall:** a stop during a segment switch no longer leaves a recording nobody owns ([2bf8433](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/2bf84337df1858326b06ece2c50ea28da5ca28b4))
* **camerawall:** closing the settings while recordings are moved no longer crashes the app ([af4bb47](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/af4bb47a8dcc7e15e3bbee732be55c92e93dd23b))
* **camerawall:** keep recording after a camera drops out, a write fails or the disk fills ([99cf0df](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/99cf0dfaefc62a4a1841fa50f84e11b300aaafea))
* **camerawall:** keep recordings going when the layout changes ([449447b](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/449447b56a5e377b07b4c4267447786d59c07d18))
* **camerawall:** keep running and recording after an error in the UI ([92536b3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/92536b3e14496a24d79a63f0d4431b9a6768171e))
* **camerawall:** notice a camera that stops sending by the time since its last packet ([3389e7d](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/3389e7dc0835690f5e438b9b4b0742d9bbf95f58))
* **camerawall:** one failing step no longer stops the recording worker's pass, and a stuck worker is reported ([218301d](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/218301d3722dfed862eea40245deead30f68076c))
* **camerawall:** open the recordings browser at once with a large archive ([8fcea14](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/8fcea14430de0b4ee79953f912525bbe5b04bf21))
* **camerawall:** read the cameras safely from the recording workers ([c77d2ea](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/c77d2eae60b8fbf9e8aaeedd02414e0227e268d8))
* **camerawall:** read the recordings browser's list in the background, and skip what cannot be read ([c64a66d](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/c64a66dce960ee6d978a96681d7aa3c1499e5c2d))
* **camerawall:** saving an unchanged camera no longer restarts its stream and recording ([422f161](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/422f1613302d4b3affccf52fc015bd265a786f7c))
* **camerawall:** show a camera in full screen without taking its player from the tile ([0308e79](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0308e79e43cec996eae45c0899d5928a49b11754))
* **camerawall:** Test connection no longer closes its player on the player's own thread ([1a08e93](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/1a08e93b17602d5588b4f1a57df040922798c902))
* **camerawall:** the app always ends when it closes ([883bb1f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/883bb1fcd3cd5303b276d1c6822976bbd2634a40))
* **camerawall:** try a lost camera again at least every two minutes ([3a15744](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/3a157443a430141ecf5a58b18d5474cad562e905))
* **camerawall:** unlocking the desktop no longer splits every recording, and a lost graphics device is recovered ([115e8f0](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/115e8f0ce57719c84268c5a0165c6053f4b2e024))
* **engine:** a failing decoder no longer ends the recording ([0bb846b](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0bb846bb716499cef944c92beedea064ff0a61f4))
* **engine:** a failing state subscriber no longer ends the app ([7c02aac](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/7c02aac796f3a9e1bf962bcf176e160d4cc0b30c))
* **engine:** a jump in a camera's clock no longer squeezes or stretches the rest of a recording ([62d778d](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/62d778d2815518b1c132b31f020a65edb04fae6e))
* **engine:** a player's streams never overlap, and opening or disposing it no longer waits on the caller's thread ([73b6706](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/73b6706bebf62d1d1b51299ed2a5ed9e664a0b6a))
* **engine:** a probe that cannot start FFmpeg returns no length again ([3230611](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/3230611a8f90f4f91366863b782cf70401188168))
* **engine:** a removed graphics device no longer logs an error for every picture ([4a7f9ac](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/4a7f9ac4c931da2aba0b81edbf677a57617302c6))
* **engine:** a slow recording output no longer stops the camera being read ([9c588b8](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/9c588b811a7c53781f62517aa4b06f565c7f96ce))
* **engine:** dispose a player's graphics accelerator with the player ([4900238](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/49002387d3f3f6bc3098fe8d4c13194750b22063))
* **engine:** FFmpeg's decoder and our picture work take the same device lock ([c0610f1](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/c0610f1c5bb9681feea8bea92966fdca893405c5))
* **engine:** no pictures are lost when a recording moves on to its next file ([3c9b77e](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/3c9b77edae088665fabf9045a4af5c740e71f785))
* **engine:** packets the disk cannot take are counted, and the recording goes on from a key frame ([88a0f0a](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/88a0f0a7dfb946dac6970547efbbfef72c7535ea))
* **engine:** pictures after a camera clock jump are still encoded ([3fd8639](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/3fd8639dd1f7ebde7335f3fc499d76a37f489488))
* **engine:** reading a recording from a share that stops answering gives up ([8c54a91](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/8c54a91118a397366ca6c446cd6cdd3c4312208b))
* **player:** showing a picture no longer holds up reading the camera ([6155496](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/6155496f4208eaa61f9bcfc815b0ffbc0e0eb325))
* **recording:** a recording that ended before its first picture gets no thumbnail ([e823c20](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/e823c202d94bd131849691e6ffe5dccaefb8aef4))
* **recording:** a thumbnail's pictures no longer depend on a closed stream ([c4166b5](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/c4166b5d28b71a7bd9b2a05a239e5716f622482b))
* **recording:** the activity index of a video that was never made is removed ([4f5c418](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/4f5c4185ea5657b594d6a71f1e3332c77ebb70f0))
* **recording:** the queue of recordings waiting for previews is bounded ([ea24b20](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/ea24b20aee5857ec9d06a71a8bd1594a0b98c02b))
* **security:** end a session's tokens when the session ends ([0e5233f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0e5233f10fdccec02432db66c0bc2863821c3822))
* **security:** keep the server's secrets from the machine's other users ([07fba93](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/07fba933a870ba30f74114cf432478c614865314))
* **server:** a camera that drops while it plays is opened again after 5 seconds ([724657c](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/724657cba1dfb2f35362c3fc33f96218e4cb613a))
* **server:** a motion end during a segment switch no longer leaves a recording nobody owns ([dbc2b84](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/dbc2b84a960d103e0f60dc9f1424b0c8fc627a55))
* **server:** a motion end raised while a camera is closed ends its motion recording ([49680c5](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/49680c58bf4361b51930ad9f7faabf5c69f8de05))
* **server:** a motion that begins in the cooldown is recorded when the cooldown ends ([409d950](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/409d9501d51dc47b7cdcf8ed225e3bef21d30c24))
* **server:** a picture that cannot be analysed no longer ends a camera's motion watching ([12c8480](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/12c84802998fb18e80343c19ef6b4651c8d766f6))
* **server:** a watched camera whose pictures stopped is opened again ([5ac1abe](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/5ac1abed9ddd146e8f39cbd4101c034c8d4af402))
* **server:** an event stream abandoned before it is read leaves the feed ([f4d4061](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/f4d4061dd93712e6ffc37a0dd23e1d887927c636))
* **server:** apply a camera edit as a whole, and to what runs for it ([caa76a2](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/caa76a24334ca9aaa95bcf7a32eb12d63ef30d38))
* **server:** ask a stopped live-stream process nothing more ([8b3045c](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/8b3045cbfd90a0dd675820f525c6d4175f7d38c3))
* **server:** back off a recording that cannot write, and share a watched camera's connection ([b2090c3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/b2090c3fa64424d3c54ff2a0d570b85702fd314b))
* **server:** changed motion settings reach a camera that is already watched ([315c636](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/315c636875298b028f45f3c836b6761e2d525174))
* **server:** end live-stream FFmpeg processes with the server ([ca4dd42](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/ca4dd423271ed1588d4cb40e170070f89a40bfbe))
* **server:** keep a failing event subscriber out of recording code ([e0ff09f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/e0ff09f5f0d98f3b3a564009b1e682407f542fdd))
* **server:** keep a started recording going, and stop one the settings no longer want ([ef03f6a](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/ef03f6a94dd451614891b9a18c1f49bb000d0831))
* **server:** keep data files through a power cut, and FFmpeg's chatter out of the log ([38ba0f6](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/38ba0f6a443e82bc1bbf7396f0a3d87400e6a7a3))
* **server:** keep recording starts off the camera's reading thread ([174dcd9](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/174dcd9a55f34e8727624da629f21348450473bb))
* **server:** keep webhook receivers that share an address apart ([b8b5c51](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/b8b5c5175cf10e194ab1bfa8ea032859c200fc02))
* **server:** never save over a data file that was locked at start-up ([98353cb](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/98353cb038cdee6be4a15e297ec98900f2b3c40a))
* **server:** never take an unreadable data file for a missing one ([b0b8945](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/b0b8945189c79f0397ff3ea496de5561b5c75123))
* **server:** one slow webhook receiver no longer holds up the others ([c22dd8c](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/c22dd8c48156eefeb5e5bb79b7dfcee26a955d79))
* **server:** one unreadable index or vanished folder no longer fails a listing ([cff7900](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/cff7900ede1cbeae3d4ab93c6635d2c7b6f50e13))
* **server:** open a camera as it is now, not as a running check read it ([67b64f3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/67b64f391fbabe52526ebcc56facf0308a7772c2))
* **server:** stop what runs for a camera when it is deleted ([009f124](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/009f12448b8d485010ea699176f2e8d2e642f098))
* **server:** take a camera's recording slot before it starts recording ([04416dd](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/04416dd85cba3bc2cbb09015533e990943688b88))
* **server:** the boxes of a motion going on no longer fill the event replay buffer ([f004cd3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/f004cd374a007911cf08952d5a1cdfb43718bb59))
* **server:** try a recorded camera that dropped out at least every two minutes ([0ec3065](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0ec3065ad232d9bee1d836e1e979ca703de3ff68))
* **server:** wait for a locked data file, and keep what could not be read ([d893531](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/d89353155b66cd70b9d84d1924f9d7ef3ce7ac28))
* **streaming:** pass the FFmpeg live-stream arguments one value each ([7366b89](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/7366b89f2f153cfd9ac2244a2bb68d2d39d7b297))
* **streaming:** restart live streams that end by themselves ([dc9428e](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/dc9428e826b9df337d771e1480e5e7c3843f9fd1))
* the event stream starts again after a 401, after the last event read ([280cf93](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/280cf9359edcc9c7c28071a0c9c25b81bcda0a30))
* **web:** a live player that stands still or keeps failing starts again ([c870cc2](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/c870cc2928c360d9b109d500741063f0ab56df15))
* **web:** the live view renews its leases when its tab is shown again ([163d169](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/163d169a80132c78c00514fcfe00f756e16bed15))
* **web:** the live view starts a stream again after it broke ([b8654cb](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/b8654cb540a8825e188f6786416f875cb6c2187f))
* **web:** the timeline no longer holds up its event stream ([bbd52dc](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/bbd52dc81b79accf75ebab9a133a436a54483e8e))
* **wpf:** a live tile keeps starting its stream until it plays ([f3d5380](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/f3d5380dddd99a7c6461588267c6e53a1dc2139c))
* **wpf:** a live tile plays again after its player fails ([53fa9ab](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/53fa9ab274bcd8f2f6563dd2f98007dd0e1ccab6))
* **wpf:** a network change no longer ends the event stream ([12da3ed](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/12da3edbbba2174304dabb6887efc0c9532990c0))
* **wpf:** a recordings list open for hours still plays its recordings ([ea7083a](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/ea7083a07c9db088cb867cb0da1f9b0426650f22))
* **wpf:** loading the live view again keeps the tiles of cameras that stay ([6f287b2](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/6f287b2a83654f85ae65fd6e8a988f20de11b3c9))
* **wpf:** notifications no longer hold up the event stream ([1f0f73c](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/1f0f73c51237a6ac8f54e4de05f0c239a49c3316))
* **wpf:** the connected and recording counts no longer drift ([80721ed](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/80721ed409ee5bccb11b9e97e95cbad8409ffb24))
* **wpf:** the desktop client asks to sign in again when the session ends ([7fbe970](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/7fbe9701b2d573a7c8879bb50629b367ee7dab9d))
* **wpf:** the desktop client's settings no longer show a diagnostics switch it does not have ([2b8f7af](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/2b8f7af0a9b7f03c76e538c0998c49a02d7d119e))
* **wpf:** timeouts and refused calls no longer escape the desktop client ([9d1fa3b](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/9d1fa3b13a70610a0293b274582c24e7b4c92646))


### Performance improvements

* **camerawall:** less file work on the UI thread ([2b049e2](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/2b049e274d17a530cfe69a4c96c5b104a12cabf7))
* **camerawall:** start the USB camera watcher in the background ([46b7a5b](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/46b7a5b2abe187ccc9628db427887a11204a1f07))
* **camerawall:** starting a recording reads the free space once instead of running the disk guard ([85b6f48](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/85b6f48b179421858e81c263ada29a57f6548261))
* **camerawall:** stopping a recording from its tile no longer closes the file on the UI thread ([047e615](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/047e615357b0aa9d6ddbb7d1c5ebe6fa8f57f4b0))
* **camerawall:** write the log file on a thread of its own ([06691da](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/06691daed729e01603faa30e8d224894b6843a68))
* **engine:** a GPU snapshot encodes its PNG after letting go of the picture ([4d7a5f3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/4d7a5f30bf995f9a17932c2fbd1926e3a50c5124))
* **engine:** no GPU objects or arrays made for every picture ([90ee284](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/90ee2848d5b25a30867152c73497542421cc406c))
* **engine:** pictures for motion analysis copy back from the graphics card into one reused frame ([23efa98](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/23efa9880a9e5f365ce51ab7021d4099dac31608))
* **engine:** pictures no view shows are not processed on the graphics card ([e5cde34](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/e5cde34a4d434aca9aef1f75f95ec56a4cab8caf))
* **engine:** preview pictures jump to their place instead of decoding a whole recording ([c4905ce](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/c4905cebde78272498cec146d2b789e84a1d69f5))
* **events:** keep half a minute of events so busy clients are not cut off ([617bb0f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/617bb0fc8cd946c30a015cf8288b57084789f9c2))
* motion events are throttled and hidden desktop tiles rest ([04e3d1d](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/04e3d1d7e9504665760f2e323282298fe19dc1dd))
* **recording:** a stopped recording's thumbnail is drawn and saved on a pool thread ([cbc1a96](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/cbc1a965923b229c6753526bc62d99eee2dbba09))
* **recordings:** walk only what a listing needs, and look at each file once ([d452a15](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/d452a15f4836d94fd53647b7ba8ade65d1a9c6ab))
* **server:** bound the recording caches, and stop re-reading damaged files ([cf34ce1](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/cf34ce1d5d30692f9567deabb1a4f54139b6072b))
* **server:** check passwords outside the accounts lock, and cap sessions ([80b33d9](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/80b33d947f33dd1c07a6dead573364e913e3c410))
* **server:** keep the availability history small and off the camera threads ([f454840](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/f454840c51029375a8e120363e4c1cf395a6eabc))
* **server:** look only at the deleted recording's folders for empty ones ([207912f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/207912f58dc9698265b5b6510dd2ab9178fe8d6c))
* **server:** take a watched camera's snapshot from the pipeline that watches it ([5bd5105](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/5bd5105f1c5f0d61c8fe28d71daf03cd237012b0))
* **storage:** stop walking the archive at every recording start on a full disk ([0f004f3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0f004f34f8a314bf4c7688b18b44dd01454f840e))
* **timeline:** keep every summary a timeline reads, and trim in batches ([5988f27](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/5988f27e4b05f282c446f905af6ef980fca9eed0))


### Refactorings

* **server:** take the cameras' reconnect ceiling from ReconnectBackoff ([523a3ee](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/523a3eec8da28660e7721f527311dd9024df7c07))


### Documentation

* keep the wiki in step with every change ([48d20b3](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/48d20b335d18ae89040b2fe38313d5febfe97f43))
* move the documentation to the wiki ([e6d0eb8](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/e6d0eb8bffb384803e76bff96101ee13dede58b8))
* name the .NET Desktop Runtime the installers need, and FFmpeg 9's library versions ([0091396](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0091396d0aa3bc2972dc39b1cd1c07091b2fe67c))
* note that a recording outlives its tile ([e259cfb](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/e259cfbfbcd9f264e0bc0d1cfe3a06d06462ee96))
* rewrite the README around the two editions, features and the wiki ([091e3b0](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/091e3b0dc0571833e906dae9fd98badf97fd0907))

## [1.2.5](https://github.com/atc-net-apps/atc-soft-video-surveillance/compare/v1.2.4...v1.2.5) (2026-10-30)


### Bug fixes

* **installer:** create the data folder so every user can write to it ([c89d5a0](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/c89d5a0a1da8b44a2fcbf497745887a5a4e3d861))
* **updates:** offer each app its own installer ([a170d9f](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/a170d9f3e2fb0efe818fedc821276fc299db8b8e))

## [1.2.4](https://github.com/atc-net-apps/atc-soft-video-surveillance/releases/tag/v1.2.4) (2026-10-30)


### Bug fixes

* **recording:** never give a new segment the name of the file being written ([ff7d240](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/ff7d2406fc738c82e12f305d7800457079d406c6))


### Refactorings

* rename to AtcSoft.Video.Surveillance ([0110d77](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/0110d778816968cdb5604d7444f2e22190d4c49b))


### Documentation

* add the no-NuGet-packages rule to CLAUDE.md ([4fdc837](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/4fdc837892c5caddb31ee86aab018f4a890d1bf8))
* use the new project names in short references ([db4a987](https://github.com/atc-net-apps/atc-soft-video-surveillance/commit/db4a9874e621c395a920574291a68a4fa2dd4d36))

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
