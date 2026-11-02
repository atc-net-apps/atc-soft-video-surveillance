#!/bin/bash
# The container's health: the server's /health answers 200 (every always-on worker is running).
# The runtime image has no curl or wget; bash talks HTTP itself.
port="${ATCSOFT_HEALTH_PORT:-8585}"
exec 3<>"/dev/tcp/127.0.0.1/${port}" || exit 1
printf 'GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n' >&3
read -r -t 5 status <&3 || exit 1
case "$status" in
  *" 200 "*) exit 0 ;;
  *) exit 1 ;;
esac
