# syntax=docker/dockerfile:1
#
# The AtcSoft Video Surveillance server with its web client, for linux/amd64 and linux/arm64.
#   docker build -t atc-video-surveillance .
#   docker run -d -p 8585:8585 -v ./data:/data atc-video-surveillance
# See compose.yaml, and the wiki's Docker guide.

# The sources are built on the machine's own platform, for the image's architecture.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet publish src/AtcSoft.Video.Surveillance.Api \
      --configuration Release \
      --arch "$TARGETARCH" \
      --self-contained false \
      --output /app \
      -p:UseAppHost=false

# FFmpeg 9 for the image's architecture, pinned and checked (docker/install-ffmpeg.sh).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS ffmpeg
RUN apt-get update \
 && apt-get install -y --no-install-recommends xz-utils \
 && rm -rf /var/lib/apt/lists/*
COPY docker/install-ffmpeg.sh /tmp/install-ffmpeg.sh
RUN sh /tmp/install-ffmpeg.sh

FROM mcr.microsoft.com/dotnet/aspnet:10.0
LABEL org.opencontainers.image.title="AtcSoft Video Surveillance" \
      org.opencontainers.image.description="Video surveillance server with its web client: cameras, recording, motion detection, timeline" \
      org.opencontainers.image.source="https://github.com/atc-net-apps/atc-soft-video-surveillance" \
      org.opencontainers.image.licenses="MIT"

COPY --from=ffmpeg /opt/ffmpeg /opt/ffmpeg
COPY --from=build /app /app
COPY --chmod=755 docker/healthcheck.sh /usr/local/bin/healthcheck

# /data holds the settings, users, keys and (by default) the recordings: keep it in a volume.
RUN mkdir -p /data && chown app:app /data

ENV ASPNETCORE_URLS=http://+:8585 \
    ASPNETCORE_ENVIRONMENT=Production \
    Storage__DataPath=/data \
    ATCSOFT_FFMPEG_PATH=/opt/ffmpeg/lib \
    LD_LIBRARY_PATH=/opt/ffmpeg/lib \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

USER app
WORKDIR /app
VOLUME ["/data"]
EXPOSE 8585

# Healthy while /health answers 200: every always-on worker of the server is running.
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 CMD ["healthcheck"]

ENTRYPOINT ["dotnet", "AtcSoft.Video.Surveillance.Api.dll"]
