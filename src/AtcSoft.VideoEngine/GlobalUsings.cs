global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.IO.Compression;
global using System.Runtime.InteropServices;
global using System.Security.Cryptography;
global using System.Text;

global using AtcSoft.VideoEngine.Capture;
global using AtcSoft.VideoEngine.Decoding;
global using AtcSoft.VideoEngine.Demuxing;
global using AtcSoft.VideoEngine.FFmpeg;
global using AtcSoft.VideoEngine.Helpers;
global using AtcSoft.VideoEngine.Recording;
global using Flyleaf.FFmpeg;
global using Microsoft.Extensions.Logging;

global using static Flyleaf.FFmpeg.Raw;

global using FFmpegLogLevel = Flyleaf.FFmpeg.LogLevel;