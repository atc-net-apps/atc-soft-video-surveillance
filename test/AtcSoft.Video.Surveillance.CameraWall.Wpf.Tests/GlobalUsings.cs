global using System.Globalization;
global using System.IO;
global using System.Reflection;
global using System.Runtime.CompilerServices;

global using Atc.Video.Engine;
global using Atc.Wpf.Notifications;
global using AtcSoft.Video.Surveillance.Enums;
global using AtcSoft.Video.Surveillance.Events;
global using AtcSoft.Video.Surveillance.Models.Settings;
global using AtcSoft.Video.Surveillance.Wpf.Core.Models;
global using AtcSoft.Video.Surveillance.Wpf.Core.Services;
global using Microsoft.Extensions.Logging.Abstractions;
global using DiskSpaceGuard = AtcSoft.Video.Surveillance.Helpers.DiskSpaceGuard;
global using IMediaPipeline = AtcSoft.Video.Surveillance.Services.IMediaPipeline;
global using IMotionFrameReader = AtcSoft.Video.Surveillance.Services.IMotionFrameReader;
global using IThumbnailGeneratorService = AtcSoft.Video.Surveillance.Services.IThumbnailGeneratorService;
global using UsbDeviceDescriptor = AtcSoft.Video.Surveillance.Models.UsbDeviceDescriptor;