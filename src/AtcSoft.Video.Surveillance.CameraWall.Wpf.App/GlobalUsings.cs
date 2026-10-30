global using System;
global using System.Collections.ObjectModel;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Text.Json;
global using System.Windows;
global using System.Windows.Input;
global using System.Windows.Threading;

global using Atc;
global using Atc.DependencyInjection;
global using Atc.Helpers;
global using Atc.Video.Engine;
global using Atc.Video.Engine.DirectX;
global using Atc.Wpf.Components.Dialogs;
global using Atc.Wpf.Components.Notifications;
global using Atc.Wpf.Forms.Dialogs;
global using Atc.Wpf.Notifications;
global using Atc.Wpf.Theming.Helpers;
global using Atc.Wpf.Translation;
global using Atc.XamlToolkit.Mvvm;

global using AtcSoft.Video.Surveillance.CameraWall.Wpf.SplashScreens;

global using AtcSoft.Video.Surveillance.Models.Settings;

global using AtcSoft.Video.Surveillance.Resources;
global using AtcSoft.Video.Surveillance.Wpf.Core;
global using AtcSoft.Video.Surveillance.Wpf.Core.Diagnostics;
global using AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;
global using AtcSoft.Video.Surveillance.Wpf.Core.Events;
global using AtcSoft.Video.Surveillance.Wpf.Core.Helpers;
global using AtcSoft.Video.Surveillance.Wpf.Core.Models;
global using AtcSoft.Video.Surveillance.Wpf.Core.Services;
global using AtcSoft.Video.Surveillance.Wpf.Core.UserControls;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

global using Serilog;
global using Serilog.Events;

global using ActivityIndexService = AtcSoft.Video.Surveillance.CameraWall.Wpf.Services.ActivityIndexService;
global using ApplicationHelper = AtcSoft.Video.Surveillance.Helpers.ApplicationHelper;
global using DiskSpaceGuard = AtcSoft.Video.Surveillance.Helpers.DiskSpaceGuard;
global using FreeSpaceReader = AtcSoft.Video.Surveillance.Helpers.FreeSpaceReader;
global using IMediaCleanupService = AtcSoft.Video.Surveillance.Services.IMediaCleanupService;
global using IRecordingPreviews = AtcSoft.Video.Surveillance.Services.IRecordingPreviews;
global using IRecordingSegmentationService = AtcSoft.Video.Surveillance.Services.IRecordingSegmentationService;
global using IUsbCameraEnumerator = AtcSoft.Video.Surveillance.Services.IUsbCameraEnumerator;
global using IUsbCameraWatcher = AtcSoft.Video.Surveillance.Services.IUsbCameraWatcher;
global using MediaCleanupService = AtcSoft.Video.Surveillance.CameraWall.Wpf.Services.MediaCleanupService;
global using MediaCleanupServiceOptions = AtcSoft.Video.Surveillance.CameraWall.Wpf.Services.MediaCleanupServiceOptions;
global using NullUsbCameraEnumerator = AtcSoft.Video.Surveillance.Services.NullUsbCameraEnumerator;
global using NullUsbCameraWatcher = AtcSoft.Video.Surveillance.Services.NullUsbCameraWatcher;
global using RecordingSegmentationService = AtcSoft.Video.Surveillance.CameraWall.Wpf.Services.RecordingSegmentationService;