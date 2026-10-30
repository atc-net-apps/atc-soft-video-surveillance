using ApiCamera = VideoSurveillance.Generated.Cameras.Models.Camera;
using ApiCameraConnectionType = VideoSurveillance.Generated.Cameras.Models.CameraConnectionType;
using ApiCameraOverlayPosition = VideoSurveillance.Generated.Cameras.Models.CameraOverlayPosition;
using ApiCameraProtocol = VideoSurveillance.Generated.Cameras.Models.CameraProtocol;
using ApiCameraRotation = VideoSurveillance.Generated.Cameras.Models.CameraRotation;
using ApiCameraSource = VideoSurveillance.Generated.Cameras.Models.CameraSource;
using ApiCameraStreamRtspTransport = VideoSurveillance.Generated.Cameras.Models.CameraStreamRtspTransport;
using ApiMotionZone = VideoSurveillance.Generated.Cameras.Models.MotionZone;
using ApiMotionZoneKind = VideoSurveillance.Generated.Cameras.Models.MotionZoneKind;
using ApiZonePoint = VideoSurveillance.Generated.Cameras.Models.ZonePoint;

using CoreCameraConnectionType = AtcSoft.VideoSurveillance.Enums.CameraConnectionType;
using CoreCameraProtocol = AtcSoft.VideoSurveillance.Enums.CameraProtocol;
using CoreCameraRotation = AtcSoft.VideoSurveillance.Enums.CameraRotation;
using CoreMotionZone = AtcSoft.VideoSurveillance.Models.MotionZone;
using CoreMotionZoneKind = AtcSoft.VideoSurveillance.Enums.MotionZoneKind;
using CoreOverlayPosition = AtcSoft.VideoSurveillance.Enums.OverlayPosition;
using CoreZonePoint = AtcSoft.VideoSurveillance.Models.ZonePoint;

using WpfCoreModels = AtcSoft.VideoSurveillance.Wpf.Core.Models;

namespace AtcSoft.VideoSurveillance.Wpf.Extensions;

/// <summary>
/// Extension methods for mapping between API camera models and Wpf.Core CameraConfiguration models.
/// </summary>
public static class CameraModelMappingExtensions
{
    /// <summary>
    /// Converts an API <see cref="ApiCamera"/> to a Wpf.Core <see cref="WpfCoreModels.CameraConfiguration"/>.
    /// </summary>
    public static WpfCoreModels.CameraConfiguration ToCameraConfiguration(
        this ApiCamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var config = new WpfCoreModels.CameraConfiguration
        {
            Id = camera.Id,
        };

        config.Connection.IpAddress = camera.IpAddress ?? string.Empty;
        config.Connection.Port = camera.Port;
        config.Connection.Protocol = camera.Protocol.ToCoreProtocol();
        config.Connection.Path = camera.Path;
        config.Connection.ConnectionType = camera.ConnectionType.ToCoreConnectionType();

        config.Authentication.UserName = camera.Username ?? string.Empty;

        config.Display.DisplayName = camera.DisplayName;
        config.Display.Description = camera.Description ?? string.Empty;
        config.Display.OverlayPosition = camera.OverlayPosition.ToCoreOverlayPosition();
        config.Display.Rotation = camera.Rotation.ToCoreRotation();
        config.MotionZones = [.. (camera.MotionZones ?? []).Select(ToCoreZone)];

        config.Stream.UseLowLatencyMode = camera.StreamUseLowLatencyMode;
        config.Stream.MaxLatencyMs = camera.StreamMaxLatencyMs;
        config.Stream.RtspTransport = camera.StreamRtspTransport.ToString().ToLowerInvariant();
        config.Stream.BufferDurationMs = camera.StreamBufferDurationMs;

        return config;
    }

    /// <summary>
    /// Converts a Wpf.Core <see cref="WpfCoreModels.CameraConfiguration"/> to an API <see cref="CreateCameraRequest"/>.
    /// </summary>
    public static CreateCameraRequest ToCreateCameraRequest(
        this WpfCoreModels.CameraConfiguration config,
        string? password = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var username = string.IsNullOrEmpty(config.Authentication.UserName)
            ? string.Empty
            : config.Authentication.UserName;

        return new CreateCameraRequest(
            DisplayName: config.Display.DisplayName,
            Description: config.Display.Description ?? string.Empty,
            Source: ApiCameraSource.Network,
            ConnectionType: config.Connection.ConnectionType.ToApiConnectionType(),
            Rotation: config.Display.Rotation.ToApiRotation(),
            IpAddress: config.Connection.IpAddress,
            Protocol: config.Connection.Protocol.ToApiProtocol(),
            Path: config.Connection.Path,
            Username: username,
            Password: string.IsNullOrEmpty(password) ? null : password,
            OverlayPosition: config.Display.OverlayPosition.ToApiOverlayPosition(),
            StreamUseLowLatencyMode: config.Stream.UseLowLatencyMode,
            StreamMaxLatencyMs: config.Stream.MaxLatencyMs,
            StreamRtspTransport: config.Stream.RtspTransport.ToApiRtspTransport(),
            StreamBufferDurationMs: config.Stream.BufferDurationMs,
            Port: config.Connection.Port);
    }

    /// <summary>
    /// Converts a Wpf.Core <see cref="WpfCoreModels.CameraConfiguration"/> to an API <see cref="UpdateCameraRequest"/>.
    /// </summary>
    public static UpdateCameraRequest ToUpdateCameraRequest(
        this WpfCoreModels.CameraConfiguration config,
        string? password = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var username = string.IsNullOrEmpty(config.Authentication.UserName)
            ? string.Empty
            : config.Authentication.UserName;

        return new UpdateCameraRequest(
            DisplayName: config.Display.DisplayName,
            Description: config.Display.Description ?? string.Empty,
            ConnectionType: config.Connection.ConnectionType.ToApiConnectionType(),
            Rotation: config.Display.Rotation.ToApiRotation(),
            IpAddress: config.Connection.IpAddress,
            Port: config.Connection.Port,
            Protocol: config.Connection.Protocol.ToApiProtocol(),
            Path: config.Connection.Path,
            Username: username,
            Password: string.IsNullOrEmpty(password) ? null : password,
            OverlayPosition: config.Display.OverlayPosition.ToApiOverlayPosition(),
            StreamUseLowLatencyMode: config.Stream.UseLowLatencyMode,
            StreamMaxLatencyMs: config.Stream.MaxLatencyMs,
            StreamRtspTransport: config.Stream.RtspTransport.ToApiRtspTransport(),
            StreamBufferDurationMs: config.Stream.BufferDurationMs);
    }

    /// <summary>
    /// Converts an API <see cref="ApiCameraConnectionType"/> to a Core <see cref="CoreCameraConnectionType"/>.
    /// </summary>
    public static CoreCameraConnectionType ToCoreConnectionType(
        this ApiCameraConnectionType connectionType)
        => connectionType.Value switch
        {
            ApiCameraConnectionType.Names.Lan => CoreCameraConnectionType.Lan,
            ApiCameraConnectionType.Names.Wifi => CoreCameraConnectionType.Wifi,
            ApiCameraConnectionType.Names.Usb => CoreCameraConnectionType.Usb,
            _ => CoreCameraConnectionType.None,
        };

    /// <summary>
    /// Converts a Core <see cref="CoreCameraConnectionType"/> to an API <see cref="ApiCameraConnectionType"/>.
    /// </summary>
    public static ApiCameraConnectionType ToApiConnectionType(
        this CoreCameraConnectionType connectionType)
        => connectionType switch
        {
            CoreCameraConnectionType.Lan => ApiCameraConnectionType.Lan,
            CoreCameraConnectionType.Wifi => ApiCameraConnectionType.Wifi,
            CoreCameraConnectionType.Usb => ApiCameraConnectionType.Usb,
            _ => ApiCameraConnectionType.None,
        };

    /// <summary>
    /// Converts an API <see cref="ApiCameraProtocol"/> to a Core <see cref="CoreCameraProtocol"/>.
    /// </summary>
    /// <summary>
    /// Converts an API <see cref="ApiCameraRotation"/> to a Core <see cref="CoreCameraRotation"/>.
    /// </summary>
    public static CoreCameraRotation ToCoreRotation(
        this ApiCameraRotation rotation)
        => rotation.Value switch
        {
            ApiCameraRotation.Names.Rotate90 => CoreCameraRotation.Rotate90,
            ApiCameraRotation.Names.Rotate180 => CoreCameraRotation.Rotate180,
            ApiCameraRotation.Names.Rotate270 => CoreCameraRotation.Rotate270,
            _ => CoreCameraRotation.None,
        };

    /// <summary>
    /// Converts an API motion zone to a Core one, for the zone editor. A kind the client does not
    /// know yet is read as ignore, the only kind the detector uses now.
    /// </summary>
    public static CoreMotionZone ToCoreZone(this ApiMotionZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return new CoreMotionZone
        {
            Name = zone.Name,
            Kind = zone.Kind.Value == ApiMotionZoneKind.Names.Watch ? CoreMotionZoneKind.Watch : CoreMotionZoneKind.Ignore,
            Points = [.. (zone.Points ?? []).Select(p => new CoreZonePoint(p.X, p.Y))],
            Color = zone.Color,
            Sensitivity = zone.Sensitivity,
            SmallestObjectPercent = zone.SmallestObjectPercent,
        };
    }

    /// <summary>
    /// Converts a Core motion zone to an API one, to send.
    /// </summary>
    public static ApiMotionZone ToApiZone(this CoreMotionZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return new ApiMotionZone(
            Name: zone.Name,
            Kind: zone.Kind == CoreMotionZoneKind.Watch ? ApiMotionZoneKind.Watch : ApiMotionZoneKind.Ignore,
            Points: [.. zone.Points.Select(p => new ApiZonePoint(p.X, p.Y))],
            Color: string.IsNullOrEmpty(zone.Color) ? null : zone.Color,
            Sensitivity: zone.Sensitivity,
            SmallestObjectPercent: zone.SmallestObjectPercent);
    }

    /// <summary>
    /// Converts a Core <see cref="CoreCameraRotation"/> to an API <see cref="ApiCameraRotation"/>.
    /// </summary>
    public static ApiCameraRotation ToApiRotation(
        this CoreCameraRotation rotation)
        => rotation switch
        {
            CoreCameraRotation.Rotate90 => ApiCameraRotation.Rotate90,
            CoreCameraRotation.Rotate180 => ApiCameraRotation.Rotate180,
            CoreCameraRotation.Rotate270 => ApiCameraRotation.Rotate270,
            _ => ApiCameraRotation.None,
        };

    public static CoreCameraProtocol ToCoreProtocol(
        this ApiCameraProtocol protocol)
        => protocol.Value switch
        {
            ApiCameraProtocol.Names.Rtsp => CoreCameraProtocol.Rtsp,
            ApiCameraProtocol.Names.Http => CoreCameraProtocol.Http,
            ApiCameraProtocol.Names.Https => CoreCameraProtocol.Https,
            _ => CoreCameraProtocol.Rtsp,
        };

    /// <summary>
    /// Converts a Core <see cref="CoreCameraProtocol"/> to an API <see cref="ApiCameraProtocol"/>.
    /// </summary>
    public static ApiCameraProtocol ToApiProtocol(
        this CoreCameraProtocol protocol)
        => protocol switch
        {
            CoreCameraProtocol.Rtsp => ApiCameraProtocol.Rtsp,
            CoreCameraProtocol.Http => ApiCameraProtocol.Http,
            CoreCameraProtocol.Https => ApiCameraProtocol.Https,
            _ => ApiCameraProtocol.Rtsp,
        };

    /// <summary>
    /// Converts an API <see cref="ApiCameraOverlayPosition"/> to a Core <see cref="CoreOverlayPosition"/>.
    /// </summary>
    public static CoreOverlayPosition ToCoreOverlayPosition(
        this ApiCameraOverlayPosition position)
        => position.Value switch
        {
            ApiCameraOverlayPosition.Names.TopLeft => CoreOverlayPosition.TopLeft,
            ApiCameraOverlayPosition.Names.TopRight => CoreOverlayPosition.TopRight,
            ApiCameraOverlayPosition.Names.BottomLeft => CoreOverlayPosition.BottomLeft,
            ApiCameraOverlayPosition.Names.BottomRight => CoreOverlayPosition.BottomRight,
            _ => CoreOverlayPosition.TopLeft,
        };

    /// <summary>
    /// Converts a Core <see cref="CoreOverlayPosition"/> to an API <see cref="ApiCameraOverlayPosition"/>.
    /// </summary>
    public static ApiCameraOverlayPosition ToApiOverlayPosition(
        this CoreOverlayPosition position)
        => position switch
        {
            CoreOverlayPosition.TopLeft => ApiCameraOverlayPosition.TopLeft,
            CoreOverlayPosition.TopRight => ApiCameraOverlayPosition.TopRight,
            CoreOverlayPosition.BottomLeft => ApiCameraOverlayPosition.BottomLeft,
            CoreOverlayPosition.BottomRight => ApiCameraOverlayPosition.BottomRight,
            _ => ApiCameraOverlayPosition.TopLeft,
        };

    /// <summary>
    /// Converts a string RTSP transport value to an API <see cref="ApiCameraStreamRtspTransport"/>.
    /// </summary>
    public static ApiCameraStreamRtspTransport ToApiRtspTransport(
        this string? transport)
        => string.Equals(transport, "udp", StringComparison.OrdinalIgnoreCase)
            ? ApiCameraStreamRtspTransport.Udp
            : ApiCameraStreamRtspTransport.Tcp;
}