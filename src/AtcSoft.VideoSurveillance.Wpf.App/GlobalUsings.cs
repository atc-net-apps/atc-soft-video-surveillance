global using System;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Runtime.InteropServices;
global using System.Security;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Windows;
global using System.Windows.Input;
global using System.Windows.Threading;

global using Atc;
global using Atc.Helpers;
global using Atc.Wpf.Components.Dialogs;
global using Atc.Wpf.Components.Notifications;
global using Atc.Wpf.DependencyObjects;
global using Atc.Wpf.Forms.Dialogs;
global using Atc.Wpf.Notifications;
global using Atc.Wpf.Theming.Helpers;
global using Atc.Wpf.Translation;
global using Atc.XamlToolkit.Controls.Attributes;
global using Atc.XamlToolkit.Mvvm;

global using AtcSoft.VideoEngine;
global using AtcSoft.VideoEngine.DirectX;

global using AtcSoft.VideoSurveillance.Resources;
global using AtcSoft.VideoSurveillance.Services;
global using AtcSoft.VideoSurveillance.Wpf.App.Models;
global using AtcSoft.VideoSurveillance.Wpf.App.Services;
global using AtcSoft.VideoSurveillance.Wpf.Core;
global using AtcSoft.VideoSurveillance.Wpf.Core.Diagnostics;
global using AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;
global using AtcSoft.VideoSurveillance.Wpf.Core.Services;
global using AtcSoft.VideoSurveillance.Wpf.Helpers;
global using AtcSoft.VideoSurveillance.Wpf.Models;
global using AtcSoft.VideoSurveillance.Wpf.Services;
global using AtcSoft.VideoSurveillance.Wpf.ViewModels;

global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Win32;

global using Serilog;
global using Serilog.Events;

global using VideoSurveillance.Generated;
global using VideoSurveillance.Generated.Cameras.Models;
global using VideoSurveillance.Generated.Events.Models;
global using VideoSurveillance.Generated.Models;
global using VideoSurveillance.Generated.Resilience;

global using ApplicationHelper = AtcSoft.VideoSurveillance.Helpers.ApplicationHelper;
global using IApplicationSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;