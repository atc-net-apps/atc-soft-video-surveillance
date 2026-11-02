# Runs the cross-platform test projects on Linux with FFmpeg 9, as CI and the image use it:
#   docker build -f docker/tests.Dockerfile -t atc-video-surveillance-tests .
#   docker run --rm atc-video-surveillance-tests
FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
 && apt-get install -y --no-install-recommends xz-utils \
 && rm -rf /var/lib/apt/lists/*

COPY docker/install-ffmpeg.sh /tmp/install-ffmpeg.sh
RUN sh /tmp/install-ffmpeg.sh

ENV ATCSOFT_FFMPEG_PATH=/opt/ffmpeg/lib \
    LD_LIBRARY_PATH=/opt/ffmpeg/lib \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

WORKDIR /src
COPY . .
RUN dotnet build test/Atc.Video.Engine.Tests -c Release

CMD ["dotnet", "test", "--no-build", "-c", "Release", "--project", "test/Atc.Video.Engine.Tests"]
