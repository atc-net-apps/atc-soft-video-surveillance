global using System.Globalization;
global using System.IO;
global using System.Reflection;
global using System.Runtime.CompilerServices;

global using Atc.Wpf.Notifications;
global using AtcSoft.VideoEngine;
global using AtcSoft.VideoSurveillance.Enums;
global using AtcSoft.VideoSurveillance.Events;
global using AtcSoft.VideoSurveillance.Models.Settings;
global using AtcSoft.VideoSurveillance.Wpf.Core.Models;
global using AtcSoft.VideoSurveillance.Wpf.Core.Services;
global using Microsoft.Extensions.Logging.Abstractions;
global using DiskSpaceGuard = AtcSoft.VideoSurveillance.Helpers.DiskSpaceGuard;
global using IMediaPipeline = AtcSoft.VideoSurveillance.Services.IMediaPipeline;
global using IMotionFrameReader = AtcSoft.VideoSurveillance.Services.IMotionFrameReader;
global using IThumbnailGeneratorService = AtcSoft.VideoSurveillance.Services.IThumbnailGeneratorService;
global using UsbDeviceDescriptor = AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor;