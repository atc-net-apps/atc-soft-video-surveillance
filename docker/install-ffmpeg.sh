#!/bin/sh
# Installs the FFmpeg 9 shared libraries (and the ffmpeg tool) the engine needs into /opt/ffmpeg,
# for the image's architecture. The build is pinned by name and SHA-256: a download that differs is
# refused. Keep the version in step with FFmpegRuntime.ReleaseVersion, FFmpegInstaller.Package and
# release-please.yml.
#
# The builds are BtbN's FFmpeg-Builds (GPL, with libx264), the same licence as the Windows build.
# Pin a month-end build (autobuild-YYYY-MM-<last day>-...): BtbN deletes the daily builds after
# about two weeks, but keeps the month-end ones for months.
set -eu

BUILD="autobuild-2026-10-31-12-59"
VERSION="n9.0.2-39-g7adfbe40dc"

# The architecture to install for: the first argument (Docker's TARGETARCH, amd64 or arm64), or
# this machine's.
ARCH="${1:-$(uname -m)}"

case "$ARCH" in
  x86_64|amd64)
    PLATFORM="linux64"
    SHA256="90a42a7d9689c031beacc3d03b139fd8243351d616988e8a74188df949fe5d08"
    ;;
  aarch64|arm64)
    PLATFORM="linuxarm64"
    SHA256="d0a01c8a3c2e31f8613b42c2f7190869e2b87bb3044748702ec4fef71c0bd1b9"
    ;;
  *)
    echo "No FFmpeg build for $ARCH" >&2
    exit 1
    ;;
esac

NAME="ffmpeg-${VERSION}-${PLATFORM}-gpl-shared-9.0"
URL="https://github.com/BtbN/FFmpeg-Builds/releases/download/${BUILD}/${NAME}.tar.xz"

cd /tmp
curl -fsSL -o ffmpeg.tar.xz "$URL"
echo "${SHA256}  ffmpeg.tar.xz" | sha256sum -c -
tar -xJf ffmpeg.tar.xz
mkdir -p /opt/ffmpeg
mv "${NAME}/lib" "${NAME}/bin" "${NAME}/LICENSE.txt" /opt/ffmpeg/
rm -rf "${NAME}" ffmpeg.tar.xz

# The engine looks for the tool next to the libraries.
ln -s ../bin/ffmpeg /opt/ffmpeg/lib/ffmpeg
ln -s ../bin/ffprobe /opt/ffmpeg/lib/ffprobe
