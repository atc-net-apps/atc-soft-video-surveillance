AtcSoft Video Surveillance - web client

The server serves this web client itself, at http://<server>:8585. This zip is for hosting it
somewhere else (another web server, another host name):

1. Serve these files with any web server, and answer every unknown path with index.html.
2. In appsettings.json, set ApiBaseAddress to the server's address, for example
   "http://my-server:8585".
3. On the server, allow the address the web client is opened from:
   Cors__AllowedOrigins__0=https://surveillance.example.com
   and, if it should not serve its own copy, WebClient__Enabled=false.

See https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Server-Reference
