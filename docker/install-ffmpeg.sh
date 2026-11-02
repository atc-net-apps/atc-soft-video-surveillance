#!/bin/sh
# Installs the FFmpeg 9 shared libraries (and the ffmpeg tool) the engine needs into /opt/ffmpeg,
# for the image's architecture. The build is pinned by name and SHA-256: a download that differs is
# refused. Keep the version in step with FFmpegRuntime.ReleaseVersion, FFmpegInstaller.Package and
# release-please.yml.
#
# The builds are BtbN's FFmpeg-Builds (GPL, with libx264), the same licence as the Windows build.
set -eu

BUILD="autobuild-2026-11-01-12-49"
VERSION="n9.0.2-42-gd7643a0fe3"

case "$(uname -m)" in
  x86_64)
    PLATFORM="linux64"
    SHA256="16b7be9d17e4b91e661e88c50f4f42f7e5c3928a7c15529e6759e96a66cb786c"
    ;;
  aarch64|arm64)
    PLATFORM="linuxarm64"
    SHA256="5b9bf5bad16462c3ff9896e60c623d31d7778ae13c469be765cfcd29173c956e"
    ;;
  *)
    echo "No FFmpeg build for $(uname -m)" >&2
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
