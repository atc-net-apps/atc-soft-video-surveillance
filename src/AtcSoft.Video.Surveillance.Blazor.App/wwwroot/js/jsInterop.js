window.hlsInterop = {
    _players: {},

    initPlayer: function (elementId, streamUrl) {
        this.destroyPlayer(elementId);

        var video = document.getElementById(elementId);
        if (!video) {
            console.warn('[hlsInterop] Element not found:', elementId);
            return;
        }

        // An element that is marked muted starts muted. The attribute alone
        // does not do it for an element the page adds later, and a browser
        // refuses to start a stream with sound by itself unless it is muted.
        // The viewer turns the sound on with the player's own control.
        if (video.hasAttribute('muted')) {
            video.muted = true;
        }

        if (typeof Hls !== 'undefined' && Hls.isSupported()) {
            var hls = new Hls({
                enableWorker: true,
                lowLatencyMode: true
            });
            hls.loadSource(streamUrl);
            hls.attachMedia(video);
            hls.on(Hls.Events.MANIFEST_PARSED, function () {
                video.play().catch(function () { });
            });
            hls.on(Hls.Events.ERROR, function (event, data) {
                if (data.fatal) {
                    console.error('[hlsInterop] Fatal error:', data.type, data.details);
                    if (data.type === Hls.ErrorTypes.NETWORK_ERROR) {
                        hls.startLoad();
                    } else if (data.type === Hls.ErrorTypes.MEDIA_ERROR) {
                        hls.recoverMediaError();
                    }
                }
            });
            this._players[elementId] = hls;
        } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
            // Native HLS support (Safari)
            video.src = streamUrl;
            video.addEventListener('loadedmetadata', function () {
                video.play().catch(function () { });
            });
            this._players[elementId] = 'native';
        } else {
            console.error('[hlsInterop] HLS is not supported in this browser.');
        }
    },

    // The size of the picture a video element plays: [width, height],
    // zeros until the first picture has arrived.
    videoSize: function (elementId) {
        var video = document.getElementById(elementId);
        return video ? [video.videoWidth || 0, video.videoHeight || 0] : [0, 0];
    },

    destroyPlayer: function (elementId) {
        var player = this._players[elementId];
        if (!player) {
            return;
        }
        if (player !== 'native' && typeof player.destroy === 'function') {
            player.destroy();
        }
        delete this._players[elementId];
    }
};

window.fileInterop = {
    downloadBlob: function (base64Data, fileName, contentType) {
        var byteCharacters = atob(base64Data);
        var byteNumbers = new Array(byteCharacters.length);
        for (var i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        var byteArray = new Uint8Array(byteNumbers);
        var blob = new Blob([byteArray], { type: contentType });

        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    downloadFromUrl: function (url, fileName) {
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }
};
window.recordingTimeline = {
    // Where the player is and how long the recording is, in seconds.
    position: function (videoId) {
        var video = document.getElementById(videoId);
        return video && isFinite(video.duration) ? [video.currentTime, video.duration] : [0, 0];
    },

    seekTo: function (videoId, seconds) {
        var video = document.getElementById(videoId);
        if (video && isFinite(video.duration)) {
            video.currentTime = Math.max(0, Math.min(seconds, video.duration));
        }
    },

    pause: function (videoId) {
        var video = document.getElementById(videoId);
        if (video) {
            video.pause();
        }
    },

    // Tells the dialog where the player is as it plays, for the playhead and *play events only*.
    onTime: function (videoId, dotNetRef) {
        var video = document.getElementById(videoId);
        if (video) {
            video.addEventListener('timeupdate', function () {
                // The dialog may be closed already: its reference is gone, and the call is let go.
                dotNetRef.invokeMethodAsync('OnTimeAsync', video.currentTime, isFinite(video.duration) ? video.duration : 0)
                    .catch(function () { });
            });
        }
    },

    // A click on the timeline jumps to the same place in the recording; the timeline shows the
    // part of it from start to end (fractions of the recording).
    seekAt: function (videoId, element, clientX, start, end) {
        var video = document.getElementById(videoId);
        if (!video || !element || !isFinite(video.duration)) {
            return;
        }

        var box = element.getBoundingClientRect();
        if (box.width > 0) {
            var share = Math.max(0, Math.min(1, (clientX - box.left) / box.width));
            video.currentTime = (start + share * (end - start)) * video.duration;
        }
    },

    // The mouse over the timeline: the wheel zooms it around the place under the mouse (listened
    // to here, not in Blazor: its wheel listener is passive, so the page would scroll as well), and
    // a move tells the place, at most once a frame, so the timeline can say what happened there.
    listen: function (element, dotNetRef) {
        if (!element) {
            return;
        }

        var pending = null;
        var shareAt = function (clientX) {
            var box = element.getBoundingClientRect();
            return box.width > 0 ? Math.max(0, Math.min(1, (clientX - box.left) / box.width)) : -1;
        };
        var tell = function (share) {
            dotNetRef.invokeMethodAsync('OnTimelineHover', share).catch(function () { });
        };

        element.addEventListener('mousemove', function (e) {
            var first = pending === null;
            pending = shareAt(e.clientX);
            if (first) {
                window.requestAnimationFrame(function () {
                    var share = pending;
                    pending = null;
                    tell(share);
                });
            }
        });
        element.addEventListener('mouseleave', function () {
            pending = null;
            tell(-1);
        });

        // A right-click offers verdicts on the place, in the page's own menu.
        element.addEventListener('contextmenu', function (e) {
            var share = shareAt(e.clientX);
            if (share < 0) {
                return;
            }

            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnTimelineContextMenu', share).catch(function () { });
        });

        element.addEventListener('wheel', function (e) {
            var box = element.getBoundingClientRect();
            if (box.width <= 0 || e.deltaY === 0) {
                return;
            }

            e.preventDefault();
            var share = Math.max(0, Math.min(1, (e.clientX - box.left) / box.width));
            dotNetRef.invokeMethodAsync('OnTimelineWheel', share, e.deltaY < 0)
                .catch(function () { });
        }, { passive: false });
    }
};

window.timelineInterop = {
    // Where on the element a click landed, as a fraction of its width.
    fractionAt: function (elementId, clientX) {
        var element = document.getElementById(elementId);
        if (!element) {
            return 0;
        }

        var box = element.getBoundingClientRect();
        return box.width > 0 ? Math.max(0, Math.min(1, (clientX - box.left) / box.width)) : 0;
    },

    // Tells the dialog when a recording has played to its end, so it can move on to the next.
    onEnded: function (videoId, dotNetRef) {
        var video = document.getElementById(videoId);
        if (video) {
            video.addEventListener('ended', function () { dotNetRef.invokeMethodAsync('OnEndedAsync').catch(function () { }); });
        }
    },

    // Starts a recording this far in, once the player knows its length.
    startAt: function (videoId, seconds) {
        var video = document.getElementById(videoId);
        if (!video) {
            return;
        }

        var seek = function () { video.currentTime = Math.min(seconds, isFinite(video.duration) ? video.duration : seconds); };
        if (video.readyState >= 1) {
            seek();
        } else {
            video.addEventListener('loadedmetadata', seek, { once: true });
        }
    }
};

// Play all from here: several muted video elements the page keeps in step. Each element notes
// what happened since the page last read it (opened, ended, failed), so the page reads every tile
// in one call.
window.playAllInterop = {
    flags: {},

    open: function (videoId, url, seconds) {
        var video = document.getElementById(videoId);
        if (!video) {
            return;
        }

        var flags = this.flags[videoId] = { opened: false, ended: false, failed: false };
        video.onloadedmetadata = function () {
            video.currentTime = Math.min(seconds, isFinite(video.duration) ? video.duration : seconds);
        };
        video.onplaying = function () { flags.opened = true; };
        video.onended = function () { flags.ended = true; };
        video.onerror = function () { flags.failed = true; };
        video.muted = true;
        video.src = url;
        var started = video.play();
        if (started) {
            started.catch(function () { });
        }
    },

    play: function (videoId) {
        var video = document.getElementById(videoId);
        if (video) {
            var started = video.play();
            if (started) {
                started.catch(function () { });
            }
        }
    },

    pause: function (videoId) {
        var video = document.getElementById(videoId);
        if (video) {
            video.pause();
        }
    },

    // Lets go of the recording, without the element reporting the empty source as an error.
    close: function (videoId) {
        var video = document.getElementById(videoId);
        delete this.flags[videoId];
        if (!video) {
            return;
        }

        video.onloadedmetadata = null;
        video.onplaying = null;
        video.onended = null;
        video.onerror = null;
        video.pause();
        video.removeAttribute('src');
        video.load();
    },

    read: function (videoIds) {
        var all = this.flags;
        return videoIds.map(function (videoId) {
            var video = document.getElementById(videoId);
            var flags = all[videoId] || {};
            var reading = {
                time: video ? video.currentTime : 0,
                opened: !!flags.opened,
                ended: !!flags.ended,
                failed: !!flags.failed
            };
            flags.opened = false;
            flags.ended = false;
            flags.failed = false;
            return reading;
        });
    }
};

window.zoneEditor = {
    // Where the zone drawing is on the page: [left, top, width, height]. Given a pointer, the
    // drawing keeps it until it is let go, so a drag goes on outside the drawing.
    box: function (element, pointerId) {
        if (!element) {
            return [0, 0, 0, 0];
        }

        if (pointerId !== null && pointerId !== undefined && element.setPointerCapture) {
            try {
                element.setPointerCapture(pointerId);
            } catch (e) {
                // The pointer is gone already.
            }
        }

        var box = element.getBoundingClientRect();
        return [box.left, box.top, box.width, box.height];
    }
};
