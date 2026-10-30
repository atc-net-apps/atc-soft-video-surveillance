using CameraConnectionTypeHelper = AtcSoft.VideoSurveillance.Helpers.CameraConnectionTypeHelper;
using CoreCameraConfiguration = AtcSoft.VideoSurveillance.Models.CameraConfiguration;
using CoreCameraConnectionType = AtcSoft.VideoSurveillance.Enums.CameraConnectionType;
using CoreCameraProtocol = AtcSoft.VideoSurveillance.Enums.CameraProtocol;
using CoreCameraRotation = AtcSoft.VideoSurveillance.Enums.CameraRotation;
using CoreCameraSource = AtcSoft.VideoSurveillance.Enums.CameraSource;
using CoreConnectionState = AtcSoft.VideoSurveillance.Enums.ConnectionState;
using CoreMotionZone = AtcSoft.VideoSurveillance.Models.MotionZone;
using CoreMotionZoneKind = AtcSoft.VideoSurveillance.Enums.MotionZoneKind;
using CoreOverlayPosition = AtcSoft.VideoSurveillance.Enums.OverlayPosition;
using CoreRecordingState = AtcSoft.VideoSurveillance.Enums.RecordingState;
using CoreUsbConnectionSettings = AtcSoft.VideoSurveillance.Models.Settings.UsbConnectionSettings;
using CoreUsbStreamFormat = AtcSoft.VideoSurveillance.Models.UsbStreamFormat;
using CoreZonePoint = AtcSoft.VideoSurveillance.Models.ZonePoint;
using MotionZoneGeometry = AtcSoft.VideoSurveillance.Helpers.MotionDetection.MotionZoneGeometry;

namespace AtcSoft.VideoSurveillance.Api.Domain.Mapping;

internal static class CameraMappingExtensions
{
    public static Camera ToApiModel(
        this CoreCameraConfiguration core,
        CoreConnectionState? connectionState = null,
        bool isRecording = false)
    {
        var usb = core.Connection.Usb;
        var format = usb?.Format;

        return new Camera(
            Id: core.Id,
            DisplayName: core.Display.DisplayName,
            Description: core.Display.Description ?? string.Empty,
            Source: core.Connection.Source.ToApiSource(),
            ConnectionType: CameraConnectionTypeHelper.GetEffective(core.Connection).ToApiConnectionType(),
            Rotation: core.Display.Rotation.ToApiRotation(),
            IpAddress: core.Connection.IpAddress,
            Port: core.Connection.Port,
            Protocol: core.Connection.Protocol.ToApiProtocol(),
            Path: core.Connection.Path ?? string.Empty,
            Username: core.Authentication.UserName ?? string.Empty,
            UsbDeviceId: usb?.DeviceId ?? string.Empty,
            UsbFriendlyName: usb?.FriendlyName ?? string.Empty,
            UsbWidth: format?.Width ?? 0,
            UsbHeight: format?.Height ?? 0,
            UsbFrameRate: format?.FrameRate ?? 0,
            UsbPixelFormat: format?.PixelFormat ?? string.Empty,
            UsbCaptureAudio: usb?.PreferAudio ?? false,
            UsbAudioDeviceName: usb?.AudioDeviceName ?? string.Empty,
            OverlayPosition: ToApiOverlayPosition(core.Display.OverlayPosition),
            StreamUseLowLatencyMode: core.Stream.UseLowLatencyMode,
            StreamMaxLatencyMs: core.Stream.MaxLatencyMs,
            StreamRtspTransport: ToApiRtspTransport(core.Stream.RtspTransport),
            StreamBufferDurationMs: core.Stream.BufferDurationMs,
            ConnectionState: connectionState?.ToApiConnectionState(),
            IsRecording: isRecording,
            MotionZones: [.. core.MotionZones.Select(ToApiZone)]);
    }

    public static CoreCameraConfiguration ToCoreModel(
        this CreateCameraRequest request)
    {
        var source = request.Source.ToCoreSource();

        var camera = new CoreCameraConfiguration
        {
            Connection =
            {
                Source = source,
                IpAddress = request.IpAddress ?? string.Empty,
                Port = request.Port,
                Protocol = request.Protocol.ToCoreProtocol(),
                Path = request.Path,
                Usb = source == CoreCameraSource.Usb ? BuildUsbSettings(request) : null,
                ConnectionType = request.ConnectionType?.ToCoreConnectionType() ?? CoreCameraConnectionType.None,
            },
            Authentication =
            {
                UserName = request.Username,
                Password = request.Password,
            },
            Display =
            {
                DisplayName = request.DisplayName,
                Description = request.Description,
                Rotation = request.Rotation?.ToCoreRotation() ?? CoreCameraRotation.None,
            },
        };

        // USB cameras are always USB; a network camera never keeps a stored USB value.
        camera.Connection.ConnectionType = CameraConnectionTypeHelper.GetEffective(camera.Connection);

        if (request.OverlayPosition is not null)
        {
            camera.Display.OverlayPosition = ToCoreOverlayPosition(request.OverlayPosition.Value);
        }

        camera.Stream.UseLowLatencyMode = request.StreamUseLowLatencyMode ?? false;

        if (request.StreamMaxLatencyMs > 0)
        {
            camera.Stream.MaxLatencyMs = request.StreamMaxLatencyMs.Value;
        }

        if (request.StreamRtspTransport is not null)
        {
            camera.Stream.RtspTransport = request.StreamRtspTransport.Value.ToString().ToLowerInvariant();
        }

        if (request.StreamBufferDurationMs > 0)
        {
            camera.Stream.BufferDurationMs = request.StreamBufferDurationMs.Value;
        }

        if (request.MotionZones is { } zones)
        {
            camera.MotionZones = [.. zones.Select(ToCoreZone)];
        }

        return camera;
    }

    public static void ApplyUpdate(
        this CoreCameraConfiguration core,
        UpdateCameraRequest request)
    {
        if (!string.IsNullOrEmpty(request.DisplayName))
        {
            core.Display.DisplayName = request.DisplayName;
        }

        if (request.Description is not null)
        {
            core.Display.Description = request.Description;
        }

        if (request.Source is not null)
        {
            core.Connection.Source = request.Source.Value.ToCoreSource();
        }

        if (!string.IsNullOrEmpty(request.IpAddress))
        {
            core.Connection.IpAddress = request.IpAddress;
        }

        if (request.Port > 0)
        {
            core.Connection.Port = request.Port.Value;
        }

        if (request.Protocol is not null)
        {
            core.Connection.Protocol = request.Protocol.Value.ToCoreProtocol();
        }

        if (request.Path is not null)
        {
            core.Connection.Path = request.Path;
        }

        if (request.ConnectionType is not null)
        {
            core.Connection.ConnectionType = request.ConnectionType.Value.ToCoreConnectionType();
        }

        if (request.Username is not null)
        {
            core.Authentication.UserName = request.Username;
        }

        if (request.Password is not null)
        {
            core.Authentication.Password = request.Password;
        }

        ApplyUsbUpdate(core, request);

        // USB cameras are always USB; a network camera never keeps a stored USB value.
        core.Connection.ConnectionType = CameraConnectionTypeHelper.GetEffective(core.Connection);

        if (request.OverlayPosition is not null)
        {
            core.Display.OverlayPosition = ToCoreOverlayPosition(request.OverlayPosition.Value);
        }

        if (request.Rotation is not null)
        {
            var rotation = request.Rotation.Value.ToCoreRotation();

            // Zones left out stay on the same part of the scene; zones sent are drawn on the new picture.
            if (request.MotionZones is null && rotation != core.Display.Rotation)
            {
                core.MotionZones = [.. MotionZoneGeometry.Turn(core.MotionZones, core.Display.Rotation, rotation)];
            }

            core.Display.Rotation = rotation;
        }

        if (request.MotionZones is { } zones)
        {
            core.MotionZones = [.. zones.Select(ToCoreZone)];
        }

        if (request.StreamUseLowLatencyMode is { } useLowLatencyMode)
        {
            core.Stream.UseLowLatencyMode = useLowLatencyMode;
        }

        if (request.StreamMaxLatencyMs > 0)
        {
            core.Stream.MaxLatencyMs = request.StreamMaxLatencyMs.Value;
        }

        if (request.StreamRtspTransport is not null)
        {
            core.Stream.RtspTransport = request.StreamRtspTransport.Value.ToString().ToLowerInvariant();
        }

        if (request.StreamBufferDurationMs > 0)
        {
            core.Stream.BufferDurationMs = request.StreamBufferDurationMs.Value;
        }
    }

    public static CameraProtocol ToApiProtocol(this CoreCameraProtocol protocol)
        => protocol switch
        {
            CoreCameraProtocol.Http => CameraProtocol.Http,
            CoreCameraProtocol.Https => CameraProtocol.Https,
            _ => CameraProtocol.Rtsp,
        };

    public static CoreCameraProtocol ToCoreProtocol(
        this CameraProtocol protocol)
        => protocol switch
        {
            CameraProtocol.Http => CoreCameraProtocol.Http,
            CameraProtocol.Https => CoreCameraProtocol.Https,
            _ => CoreCameraProtocol.Rtsp,
        };

    public static CameraSource ToApiSource(this CoreCameraSource source)
        => source switch
        {
            CoreCameraSource.Usb => CameraSource.Usb,
            _ => CameraSource.Network,
        };

    public static CoreCameraSource ToCoreSource(this CameraSource source)
        => source switch
        {
            CameraSource.Usb => CoreCameraSource.Usb,
            _ => CoreCameraSource.Network,
        };

    public static CameraConnectionType ToApiConnectionType(
        this CoreCameraConnectionType connectionType)
        => connectionType switch
        {
            CoreCameraConnectionType.Lan => CameraConnectionType.Lan,
            CoreCameraConnectionType.Wifi => CameraConnectionType.Wifi,
            CoreCameraConnectionType.Usb => CameraConnectionType.Usb,
            _ => CameraConnectionType.None,
        };

    public static CoreCameraConnectionType ToCoreConnectionType(
        this CameraConnectionType connectionType)
        => connectionType switch
        {
            CameraConnectionType.Lan => CoreCameraConnectionType.Lan,
            CameraConnectionType.Wifi => CoreCameraConnectionType.Wifi,
            CameraConnectionType.Usb => CoreCameraConnectionType.Usb,
            _ => CoreCameraConnectionType.None,
        };

    public static CameraRotation ToApiRotation(this CoreCameraRotation rotation)
        => rotation switch
        {
            CoreCameraRotation.Rotate90 => CameraRotation.Rotate90,
            CoreCameraRotation.Rotate180 => CameraRotation.Rotate180,
            CoreCameraRotation.Rotate270 => CameraRotation.Rotate270,
            _ => CameraRotation.None,
        };

    public static CoreCameraRotation ToCoreRotation(
        this CameraRotation rotation)
        => rotation switch
        {
            CameraRotation.Rotate90 => CoreCameraRotation.Rotate90,
            CameraRotation.Rotate180 => CoreCameraRotation.Rotate180,
            CameraRotation.Rotate270 => CoreCameraRotation.Rotate270,
            _ => CoreCameraRotation.None,
        };

    public static CameraConnectionState ToApiConnectionState(
        this CoreConnectionState state)
        => state switch
        {
            CoreConnectionState.Connecting => CameraConnectionState.Connecting,
            CoreConnectionState.Connected => CameraConnectionState.Connected,
            CoreConnectionState.Reconnecting => CameraConnectionState.Reconnecting,
            CoreConnectionState.Error => CameraConnectionState.Error,
            CoreConnectionState.DeviceUnplugged => CameraConnectionState.DeviceUnplugged,
            _ => CameraConnectionState.Disconnected,
        };

    public static RecordingStatusState ToApiRecordingState(
        this CoreRecordingState state)
        => state switch
        {
            CoreRecordingState.Recording => RecordingStatusState.Recording,
            CoreRecordingState.RecordingMotion => RecordingStatusState.RecordingMotion,
            CoreRecordingState.RecordingPostMotion => RecordingStatusState.RecordingPostMotion,
            _ => RecordingStatusState.Idle,
        };

    private static CameraOverlayPosition ToApiOverlayPosition(
        CoreOverlayPosition position)
        => position switch
        {
            CoreOverlayPosition.TopRight => CameraOverlayPosition.TopRight,
            CoreOverlayPosition.BottomLeft => CameraOverlayPosition.BottomLeft,
            CoreOverlayPosition.BottomRight => CameraOverlayPosition.BottomRight,
            _ => CameraOverlayPosition.TopLeft,
        };

    private static CoreOverlayPosition ToCoreOverlayPosition(
        CameraOverlayPosition position)
        => position switch
        {
            CameraOverlayPosition.TopRight => CoreOverlayPosition.TopRight,
            CameraOverlayPosition.BottomLeft => CoreOverlayPosition.BottomLeft,
            CameraOverlayPosition.BottomRight => CoreOverlayPosition.BottomRight,
            _ => CoreOverlayPosition.TopLeft,
        };

    private static MotionZone ToApiZone(CoreMotionZone zone)
        => new(
            Name: zone.Name,
            Kind: zone.Kind == CoreMotionZoneKind.Watch ? MotionZoneKind.Watch : MotionZoneKind.Ignore,
            Points: [.. zone.Points.Select(p => new ZonePoint(p.X, p.Y))],
            Color: zone.Color,
            Sensitivity: zone.Sensitivity,
            SmallestObjectPercent: zone.SmallestObjectPercent);

    private static CoreMotionZone ToCoreZone(MotionZone zone)
        => new()
        {
            Name = zone.Name,
            Kind = zone.Kind == MotionZoneKind.Watch ? CoreMotionZoneKind.Watch : CoreMotionZoneKind.Ignore,
            Points = [.. zone.Points.Select(p => new CoreZonePoint(p.X, p.Y))],
            Color = string.IsNullOrEmpty(zone.Color) ? null : zone.Color,
            Sensitivity = zone.Sensitivity,
            SmallestObjectPercent = zone.SmallestObjectPercent,
        };

    private static CameraStreamRtspTransport ToApiRtspTransport(
        string? transport)
        => string.Equals(transport, "udp", StringComparison.OrdinalIgnoreCase)
            ? CameraStreamRtspTransport.Udp
            : CameraStreamRtspTransport.Tcp;

    private static CoreUsbConnectionSettings BuildUsbSettings(
        CreateCameraRequest request)
        => new()
        {
            DeviceId = request.UsbDeviceId ?? string.Empty,
            FriendlyName = request.UsbFriendlyName ?? string.Empty,
            PreferAudio = request.UsbCaptureAudio ?? false,
            AudioDeviceName = request.UsbAudioDeviceName ?? string.Empty,
            Format = (request.UsbWidth > 0 && request.UsbHeight > 0)
                ? new CoreUsbStreamFormat
                {
                    Width = request.UsbWidth.Value,
                    Height = request.UsbHeight.Value,
                    FrameRate = request.UsbFrameRate ?? 0,
                    PixelFormat = request.UsbPixelFormat ?? string.Empty,
                }
                : null,
        };

    private static void ApplyUsbUpdate(
        CoreCameraConfiguration core,
        UpdateCameraRequest request)
    {
        var hasUsbField = request.UsbDeviceId is not null
            || request.UsbFriendlyName is not null
            || request.UsbWidth > 0
            || request.UsbHeight > 0
            || request.UsbFrameRate > 0
            || request.UsbPixelFormat is not null
            || request.UsbCaptureAudio is not null
            || request.UsbAudioDeviceName is not null;

        if (!hasUsbField && core.Connection.Source != CoreCameraSource.Usb)
        {
            return;
        }

        core.Connection.Usb ??= new CoreUsbConnectionSettings();

        if (request.UsbDeviceId is not null)
        {
            core.Connection.Usb.DeviceId = request.UsbDeviceId;
        }

        if (request.UsbFriendlyName is not null)
        {
            core.Connection.Usb.FriendlyName = request.UsbFriendlyName;
        }

        if (request.UsbCaptureAudio is { } captureAudio)
        {
            core.Connection.Usb.PreferAudio = captureAudio;
        }

        if (request.UsbAudioDeviceName is not null)
        {
            core.Connection.Usb.AudioDeviceName = request.UsbAudioDeviceName;
        }

        if (request.UsbWidth > 0 || request.UsbHeight > 0 ||
            request.UsbFrameRate > 0 || request.UsbPixelFormat is not null)
        {
            core.Connection.Usb.Format ??= new CoreUsbStreamFormat();

            if (request.UsbWidth > 0)
            {
                core.Connection.Usb.Format.Width = request.UsbWidth.Value;
            }

            if (request.UsbHeight > 0)
            {
                core.Connection.Usb.Format.Height = request.UsbHeight.Value;
            }

            if (request.UsbFrameRate > 0)
            {
                core.Connection.Usb.Format.FrameRate = request.UsbFrameRate.Value;
            }

            if (request.UsbPixelFormat is not null)
            {
                core.Connection.Usb.Format.PixelFormat = request.UsbPixelFormat;
            }
        }
    }
}