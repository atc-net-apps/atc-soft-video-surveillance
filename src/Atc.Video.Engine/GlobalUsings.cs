global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.IO.Compression;
global using System.Runtime.InteropServices;
global using System.Security.Cryptography;
global using System.Text;

global using Atc.Video.Engine.Capture;
global using Atc.Video.Engine.Decoding;
global using Atc.Video.Engine.Demuxing;
global using Atc.Video.Engine.FFmpeg;
global using Atc.Video.Engine.Helpers;
global using Atc.Video.Engine.Recording;
global using Flyleaf.FFmpeg;
global using Microsoft.Extensions.Logging;

global using static Flyleaf.FFmpeg.Raw;

global using FFmpegLogLevel = Flyleaf.FFmpeg.LogLevel;