namespace AtcSoft.VideoSurveillance.Wpf.Core.ValueConverters;

/// <summary>
/// JSON converter for <see cref="CameraConfiguration"/> that supports migration from flat to nested structure.
/// Reads both legacy flat format and new nested format, always writes new nested format.
/// </summary>
public sealed class CameraConfigurationJsonValueConverter : JsonConverter<CameraConfiguration>
{
    /// <inheritdoc />
    public override CameraConfiguration? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var config = new CameraConfiguration();

        // Read Id
        if (root.TryGetProperty("id", out var idElement) ||
            root.TryGetProperty("Id", out idElement))
        {
            config.Id = idElement.GetGuid();
        }

        // Determine if this is the new nested format or legacy flat format
        var hasConnectionObject = root.TryGetProperty("connection", out var connectionElement) ||
                                  root.TryGetProperty("Connection", out connectionElement);

        if (hasConnectionObject && connectionElement.ValueKind == JsonValueKind.Object)
        {
            // New nested format
            ReadNestedFormat(root, config, connectionElement, options);
        }
        else
        {
            // Legacy flat format
            ReadLegacyFormat(root, config);
        }

        return config;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        CameraConfiguration value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        writer.WriteStartObject();

        // Id
        writer.WriteString("id", value.Id);

        // Connection
        writer.WritePropertyName("connection");
        WriteConnectionSettings(writer, value.Connection);

        // Authentication
        writer.WritePropertyName("authentication");
        WriteAuthenticationSettings(writer, value.Authentication);

        // Display
        writer.WritePropertyName("display");
        WriteDisplaySettings(writer, value.Display);

        // Stream
        writer.WritePropertyName("stream");
        WriteStreamSettings(writer, value.Stream);

        // Overrides - only write if any override is set
        if (value.Overrides is not null && value.Overrides.HasAnyOverride())
        {
            writer.WritePropertyName("overrides");
            WriteOverridesSettings(writer, value.Overrides);
        }

        // Motion zones - only written when the camera has any
        if (value.MotionZones.Count > 0)
        {
            writer.WritePropertyName("motionZones");
            JsonSerializer.Serialize(writer, value.MotionZones, options);
        }

        writer.WriteEndObject();
    }

    private static void ReadNestedFormat(
        JsonElement root,
        CameraConfiguration config,
        JsonElement connectionElement,
        JsonSerializerOptions options)
    {
        // Connection
        ReadConnectionSettings(connectionElement, config.Connection);

        // Authentication
        if ((root.TryGetProperty("authentication", out var authElement) ||
             root.TryGetProperty("Authentication", out authElement)) &&
            authElement.ValueKind == JsonValueKind.Object)
        {
            ReadAuthenticationSettings(authElement, config.Authentication);
        }

        // Display
        if ((root.TryGetProperty("display", out var displayElement) ||
             root.TryGetProperty("Display", out displayElement)) &&
            displayElement.ValueKind == JsonValueKind.Object)
        {
            ReadDisplaySettings(displayElement, config.Display);
        }

        // Stream
        if ((root.TryGetProperty("stream", out var streamElement) ||
             root.TryGetProperty("Stream", out streamElement)) &&
            streamElement.ValueKind == JsonValueKind.Object)
        {
            ReadStreamSettings(streamElement, config.Stream);
        }

        // Overrides
        if ((root.TryGetProperty("overrides", out var overridesElement) ||
             root.TryGetProperty("Overrides", out overridesElement)) &&
            overridesElement.ValueKind == JsonValueKind.Object)
        {
            config.Overrides = ReadOverridesSettings(overridesElement);
        }

        // Motion zones
        if ((root.TryGetProperty("motionZones", out var zonesElement) ||
             root.TryGetProperty("MotionZones", out zonesElement)) &&
            zonesElement.ValueKind == JsonValueKind.Array)
        {
            config.MotionZones = zonesElement.Deserialize<List<MotionZone>>(options) ?? [];
        }
    }

    private static void ReadLegacyFormat(
        JsonElement root,
        CameraConfiguration config)
    {
        // Connection properties (flat)
        if (TryGetStringProperty(root, "ipAddress", "IpAddress", out var ipAddress))
        {
            config.Connection.IpAddress = ipAddress;
        }

        if (TryGetStringProperty(root, "protocol", "Protocol", out var protocol) &&
            Enum.TryParse<CameraProtocol>(protocol, ignoreCase: true, out var protocolEnum))
        {
            config.Connection.Protocol = protocolEnum;
        }

        if (TryGetInt32Property(root, "port", "Port", out var port))
        {
            config.Connection.Port = port;
        }

        if (TryGetStringProperty(root, "path", "Path", out var path))
        {
            config.Connection.Path = path;
        }

        // Authentication properties (flat)
        if (TryGetStringProperty(root, "userName", "UserName", out var userName))
        {
            config.Authentication.UserName = userName;
        }

        if (TryGetStringProperty(root, "password", "Password", out var password))
        {
            config.Authentication.Password = password;
        }

        // Display properties (flat)
        if (TryGetStringProperty(root, "displayName", "DisplayName", out var displayName))
        {
            config.Display.DisplayName = displayName;
        }

        if (TryGetStringProperty(root, "description", "Description", out var description))
        {
            config.Display.Description = description;
        }

        if (TryGetStringProperty(root, "overlayPosition", "OverlayPosition", out var overlayPosition) &&
            Enum.TryParse<OverlayPosition>(overlayPosition, ignoreCase: true, out var positionEnum))
        {
            config.Display.OverlayPosition = positionEnum;
        }

        if (TryGetStringProperty(root, "rotation", "Rotation", out var rotation) &&
            Enum.TryParse<CameraRotation>(rotation, ignoreCase: true, out var rotationEnum))
        {
            config.Display.Rotation = rotationEnum;
        }

        // Stream properties (flat)
        if (TryGetBoolProperty(root, "useLowLatencyMode", "UseLowLatencyMode", out var useLowLatencyMode))
        {
            config.Stream.UseLowLatencyMode = useLowLatencyMode;
        }

        if (TryGetInt32Property(root, "maxLatencyMs", "MaxLatencyMs", out var maxLatencyMs))
        {
            config.Stream.MaxLatencyMs = maxLatencyMs;
        }

        if (TryGetStringProperty(root, "rtspTransport", "RtspTransport", out var rtspTransport))
        {
            config.Stream.RtspTransport = rtspTransport;
        }

        if (TryGetInt32Property(root, "bufferDurationMs", "BufferDurationMs", out var bufferDurationMs))
        {
            config.Stream.BufferDurationMs = bufferDurationMs;
        }
    }

    private static void ReadConnectionSettings(
        JsonElement element,
        ConnectionSettings settings)
    {
        if (TryGetStringProperty(element, "ipAddress", "IpAddress", out var ipAddress))
        {
            settings.IpAddress = ipAddress;
        }

        if (TryGetStringProperty(element, "protocol", "Protocol", out var protocol) &&
            Enum.TryParse<CameraProtocol>(protocol, ignoreCase: true, out var protocolEnum))
        {
            settings.Protocol = protocolEnum;
        }

        if (TryGetInt32Property(element, "port", "Port", out var port))
        {
            settings.Port = port;
        }

        if (TryGetStringProperty(element, "path", "Path", out var path))
        {
            settings.Path = path;
        }

        if (TryGetStringProperty(element, "source", "Source", out var source) &&
            Enum.TryParse<CameraSource>(source, ignoreCase: true, out var sourceEnum))
        {
            settings.Source = sourceEnum;
        }

        if (TryGetStringProperty(element, "connectionType", "ConnectionType", out var connectionType) &&
            Enum.TryParse<CameraConnectionType>(connectionType, ignoreCase: true, out var connectionTypeEnum))
        {
            settings.ConnectionType = connectionTypeEnum;
        }

        if ((element.TryGetProperty("usb", out var usbElement) ||
             element.TryGetProperty("Usb", out usbElement)) &&
            usbElement.ValueKind == JsonValueKind.Object)
        {
            settings.Usb = ReadUsbConnectionSettings(usbElement);
        }
    }

    private static UsbConnectionSettings ReadUsbConnectionSettings(
        JsonElement element)
    {
        var usb = new UsbConnectionSettings();

        if (TryGetStringProperty(element, "deviceId", "DeviceId", out var deviceId))
        {
            usb.DeviceId = deviceId;
        }

        if (TryGetStringProperty(element, "friendlyName", "FriendlyName", out var friendlyName))
        {
            usb.FriendlyName = friendlyName;
        }

        if (TryGetBoolProperty(element, "preferAudio", "PreferAudio", out var preferAudio))
        {
            usb.PreferAudio = preferAudio;
        }

        if (TryGetStringProperty(element, "audioDeviceName", "AudioDeviceName", out var audioDeviceName))
        {
            usb.AudioDeviceName = audioDeviceName;
        }

        if ((element.TryGetProperty("format", out var formatElement) ||
             element.TryGetProperty("Format", out formatElement)) &&
            formatElement.ValueKind == JsonValueKind.Object)
        {
            usb.Format = ReadUsbStreamFormat(formatElement);
        }

        return usb;
    }

    private static AtcSoft.VideoSurveillance.Models.UsbStreamFormat ReadUsbStreamFormat(
        JsonElement element)
    {
        var fmt = new AtcSoft.VideoSurveillance.Models.UsbStreamFormat();

        if (TryGetInt32Property(element, "width", "Width", out var width))
        {
            fmt.Width = width;
        }

        if (TryGetInt32Property(element, "height", "Height", out var height))
        {
            fmt.Height = height;
        }

        if ((element.TryGetProperty("frameRate", out var fpsProp) ||
             element.TryGetProperty("FrameRate", out fpsProp)) &&
            fpsProp.ValueKind == JsonValueKind.Number &&
            fpsProp.TryGetDouble(out var fps))
        {
            fmt.FrameRate = fps;
        }

        if (TryGetStringProperty(element, "pixelFormat", "PixelFormat", out var pixelFormat))
        {
            fmt.PixelFormat = pixelFormat;
        }

        return fmt;
    }

    private static void ReadAuthenticationSettings(
        JsonElement element,
        AuthenticationSettings settings)
    {
        if (TryGetStringProperty(element, "userName", "UserName", out var userName))
        {
            settings.UserName = userName;
        }

        if (TryGetStringProperty(element, "password", "Password", out var password))
        {
            settings.Password = password;
        }
    }

    private static void ReadDisplaySettings(
        JsonElement element,
        CameraDisplaySettings settings)
    {
        if (TryGetStringProperty(element, "displayName", "DisplayName", out var displayName))
        {
            settings.DisplayName = displayName;
        }

        if (TryGetStringProperty(element, "description", "Description", out var description))
        {
            settings.Description = description;
        }

        if (TryGetStringProperty(element, "overlayPosition", "OverlayPosition", out var overlayPosition) &&
            Enum.TryParse<OverlayPosition>(overlayPosition, ignoreCase: true, out var positionEnum))
        {
            settings.OverlayPosition = positionEnum;
        }

        if (TryGetStringProperty(element, "rotation", "Rotation", out var rotation) &&
            Enum.TryParse<CameraRotation>(rotation, ignoreCase: true, out var rotationEnum))
        {
            settings.Rotation = rotationEnum;
        }
    }

    private static void ReadStreamSettings(
        JsonElement element,
        StreamSettings settings)
    {
        if (TryGetBoolProperty(element, "useLowLatencyMode", "UseLowLatencyMode", out var useLowLatencyMode))
        {
            settings.UseLowLatencyMode = useLowLatencyMode;
        }

        if (TryGetInt32Property(element, "maxLatencyMs", "MaxLatencyMs", out var maxLatencyMs))
        {
            settings.MaxLatencyMs = maxLatencyMs;
        }

        if (TryGetStringProperty(element, "rtspTransport", "RtspTransport", out var rtspTransport))
        {
            settings.RtspTransport = rtspTransport;
        }

        if (TryGetInt32Property(element, "bufferDurationMs", "BufferDurationMs", out var bufferDurationMs))
        {
            settings.BufferDurationMs = bufferDurationMs;
        }
    }

    private static CameraOverrides ReadOverridesSettings(JsonElement element)
    {
        var overrides = new CameraOverrides();

        // Detect nested vs legacy flat format by checking for any known sub-object
        var hasNestedFormat = HasObjectProperty(element, "connection", "Connection") ||
                              HasObjectProperty(element, "cameraDisplay", "CameraDisplay") ||
                              HasObjectProperty(element, "performance", "Performance") ||
                              HasObjectProperty(element, "recording", "Recording") ||
                              HasObjectProperty(element, "motionDetection", "MotionDetection");

        if (hasNestedFormat)
        {
            ReadNestedOverrides(element, overrides);
        }
        else
        {
            ReadLegacyFlatOverrides(element, overrides);
        }

        return overrides;
    }

    private static void ReadNestedOverrides(
        JsonElement element,
        CameraOverrides overrides)
    {
        // Connection sub-object
        if ((element.TryGetProperty("connection", out var connEl) || element.TryGetProperty("Connection", out connEl)) &&
            connEl.ValueKind == JsonValueKind.Object)
        {
            ReadConnectionOverrides(connEl, overrides.Connection);
        }

        // CameraDisplay sub-object
        if ((element.TryGetProperty("cameraDisplay", out var dispEl) || element.TryGetProperty("CameraDisplay", out dispEl)) &&
            dispEl.ValueKind == JsonValueKind.Object)
        {
            ReadCameraDisplayOverrides(dispEl, overrides.CameraDisplay);
        }

        // Performance sub-object
        if ((element.TryGetProperty("performance", out var perfEl) || element.TryGetProperty("Performance", out perfEl)) &&
            perfEl.ValueKind == JsonValueKind.Object)
        {
            ReadPerformanceOverrides(perfEl, overrides.Performance);
        }

        // Recording sub-object
        if ((element.TryGetProperty("recording", out var recEl) || element.TryGetProperty("Recording", out recEl)) &&
            recEl.ValueKind == JsonValueKind.Object)
        {
            ReadRecordingOverrides(recEl, overrides.Recording);
        }

        // MotionDetection sub-object
        if ((element.TryGetProperty("motionDetection", out var motionEl) || element.TryGetProperty("MotionDetection", out motionEl)) &&
            motionEl.ValueKind == JsonValueKind.Object)
        {
            ReadMotionDetectionOverrides(motionEl, overrides.MotionDetection);
        }
    }

    private static void ReadConnectionOverrides(
        JsonElement element,
        ConnectionOverrides target)
    {
        if (TryGetNullableInt32Property(element, "connectionTimeoutSeconds", "ConnectionTimeoutSeconds", out var connectionTimeout))
        {
            target.ConnectionTimeoutSeconds = connectionTimeout;
        }

        if (TryGetNullableInt32Property(element, "reconnectDelaySeconds", "ReconnectDelaySeconds", out var reconnectDelay))
        {
            target.ReconnectDelaySeconds = reconnectDelay;
        }

        if (TryGetNullableBoolProperty(element, "autoReconnectOnFailure", "AutoReconnectOnFailure", out var autoReconnect))
        {
            target.AutoReconnectOnFailure = autoReconnect;
        }

        if (TryGetNullableBoolProperty(element, "showNotificationOnDisconnect", "ShowNotificationOnDisconnect", out var notifyDisconnect))
        {
            target.ShowNotificationOnDisconnect = notifyDisconnect;
        }

        if (TryGetNullableBoolProperty(element, "showNotificationOnReconnect", "ShowNotificationOnReconnect", out var notifyReconnect))
        {
            target.ShowNotificationOnReconnect = notifyReconnect;
        }

        if (TryGetNullableBoolProperty(element, "playNotificationSound", "PlayNotificationSound", out var playSound))
        {
            target.PlayNotificationSound = playSound;
        }
    }

    private static void ReadCameraDisplayOverrides(
        JsonElement element,
        CameraDisplayOverrides target)
    {
        if (TryGetNullableBoolProperty(element, "showOverlayTitle", "ShowOverlayTitle", out var showTitle))
        {
            target.ShowOverlayTitle = showTitle;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayDescription", "ShowOverlayDescription", out var showDescription))
        {
            target.ShowOverlayDescription = showDescription;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayTime", "ShowOverlayTime", out var showTime))
        {
            target.ShowOverlayTime = showTime;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayConnectionStatus", "ShowOverlayConnectionStatus", out var showStatus))
        {
            target.ShowOverlayConnectionStatus = showStatus;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayQuickActions", "ShowOverlayQuickActions", out var showQuickActions))
        {
            target.ShowOverlayQuickActions = showQuickActions;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayConnectionTypeIcon", "ShowOverlayConnectionTypeIcon", out var showConnectionTypeIcon))
        {
            target.ShowOverlayConnectionTypeIcon = showConnectionTypeIcon;
        }

        if (TryGetStringProperty(element, "connectionTypeIconPlacement", "ConnectionTypeIconPlacement", out var iconPlacement) &&
            Enum.TryParse<ConnectionTypeIconPlacement>(iconPlacement, ignoreCase: true, out var iconPlacementEnum))
        {
            target.ConnectionTypeIconPlacement = iconPlacementEnum;
        }

        if (TryGetNullableDoubleProperty(element, "overlayOpacity", "OverlayOpacity", out var opacity))
        {
            target.OverlayOpacity = opacity;
        }

        if (TryGetStringProperty(element, "overlayPosition", "OverlayPosition", out var overlayPosition) &&
            Enum.TryParse<OverlayPosition>(overlayPosition, ignoreCase: true, out var positionEnum))
        {
            target.OverlayPosition = positionEnum;
        }
    }

    private static void ReadPerformanceOverrides(
        JsonElement element,
        PerformanceOverrides target)
    {
        if (TryGetStringProperty(element, "videoQuality", "VideoQuality", out var videoQuality))
        {
            target.VideoQuality = videoQuality;
        }

        if (TryGetNullableBoolProperty(element, "hardwareAcceleration", "HardwareAcceleration", out var hardwareAccel))
        {
            target.HardwareAcceleration = hardwareAccel;
        }
    }

    private static void ReadRecordingOverrides(
        JsonElement element,
        RecordingOverrides target)
    {
        if (TryGetStringProperty(element, "recordingPath", "RecordingPath", out var recordingPath))
        {
            target.RecordingPath = recordingPath;
        }

        if (TryGetStringProperty(element, "recordingFormat", "RecordingFormat", out var recordingFormat))
        {
            target.RecordingFormat = recordingFormat;
        }

        if (TryGetNullableBoolProperty(element, "enableRecordingOnMotion", "EnableRecordingOnMotion", out var recordOnMotion))
        {
            target.EnableRecordingOnMotion = recordOnMotion;
        }

        if (TryGetNullableBoolProperty(element, "enableRecordingOnConnect", "EnableRecordingOnConnect", out var recordOnConnect))
        {
            target.EnableRecordingOnConnect = recordOnConnect;
        }

        if (TryGetNullableInt32Property(element, "thumbnailTileCount", "ThumbnailTileCount", out var tileCount))
        {
            target.ThumbnailTileCount = tileCount;
        }

        if (TryGetNullableBoolProperty(element, "enableTimelapse", "EnableTimelapse", out var enableTimelapse))
        {
            target.EnableTimelapse = enableTimelapse;
        }

        if (TryGetStringProperty(element, "timelapseInterval", "TimelapseInterval", out var timelapseInterval))
        {
            target.TimelapseInterval = timelapseInterval;
        }
    }

    private static void ReadMotionDetectionOverrides(
        JsonElement element,
        MotionDetectionOverrides target)
    {
        if (TryGetNullableInt32Property(element, "sensitivity", "Sensitivity", out var sensitivity))
        {
            target.Sensitivity = sensitivity;
        }

        if (TryGetNullableDoubleProperty(element, "smallestObjectPercent", "SmallestObjectPercent", out var smallestObject))
        {
            target.SmallestObjectPercent = smallestObject;
        }

        if (TryGetNullableInt32Property(element, "stoppedObjectSeconds", "StoppedObjectSeconds", out var stoppedSeconds))
        {
            target.StoppedObjectSeconds = stoppedSeconds;
        }

        if (TryGetNullableBoolProperty(element, "ignoreLightingChanges", "IgnoreLightingChanges", out var ignoreLighting))
        {
            target.IgnoreLightingChanges = ignoreLighting;
        }

        if (TryGetNullableDoubleProperty(element, "minimumChangePercent", "MinimumChangePercent", out var minChange))
        {
            target.MinimumChangePercent = minChange;
        }

        if (TryGetNullableInt32Property(element, "analysisFrameRate", "AnalysisFrameRate", out var frameRate))
        {
            target.AnalysisFrameRate = frameRate;
        }

        if (TryGetNullableInt32Property(element, "analysisWidth", "AnalysisWidth", out var width))
        {
            target.AnalysisWidth = width;
        }

        if (TryGetNullableInt32Property(element, "analysisHeight", "AnalysisHeight", out var height))
        {
            target.AnalysisHeight = height;
        }

        if (TryGetNullableInt32Property(element, "postMotionDurationSeconds", "PostMotionDurationSeconds", out var postMotion))
        {
            target.PostMotionDurationSeconds = postMotion;
        }

        if (TryGetNullableInt32Property(element, "cooldownSeconds", "CooldownSeconds", out var cooldown))
        {
            target.CooldownSeconds = cooldown;
        }

        // BoundingBox sub-object
        if ((element.TryGetProperty("boundingBox", out var bbEl) || element.TryGetProperty("BoundingBox", out bbEl)) &&
            bbEl.ValueKind == JsonValueKind.Object)
        {
            ReadBoundingBoxOverrides(bbEl, target.BoundingBox);
        }
    }

    private static void ReadBoundingBoxOverrides(
        JsonElement element,
        BoundingBoxOverrides target)
    {
        if (TryGetNullableBoolProperty(element, "showInGrid", "ShowInGrid", out var showInGrid))
        {
            target.ShowInGrid = showInGrid;
        }

        if (TryGetNullableBoolProperty(element, "showInFullScreen", "ShowInFullScreen", out var showInFullScreen))
        {
            target.ShowInFullScreen = showInFullScreen;
        }

        if (TryGetStringProperty(element, "color", "Color", out var color))
        {
            target.Color = color;
        }

        if (TryGetNullableInt32Property(element, "thickness", "Thickness", out var thickness))
        {
            target.Thickness = thickness;
        }

        if (TryGetNullableDoubleProperty(element, "smoothing", "Smoothing", out var smoothing))
        {
            target.Smoothing = smoothing;
        }

        if (TryGetNullableInt32Property(element, "minArea", "MinArea", out var minArea))
        {
            target.MinArea = minArea;
        }

        if (TryGetNullableInt32Property(element, "padding", "Padding", out var padding))
        {
            target.Padding = padding;
        }
    }

    private static void ReadLegacyFlatOverrides(
        JsonElement element,
        CameraOverrides overrides)
    {
        // Connection overrides
        if (TryGetNullableInt32Property(element, "connectionTimeoutSeconds", "ConnectionTimeoutSeconds", out var connectionTimeout))
        {
            overrides.Connection.ConnectionTimeoutSeconds = connectionTimeout;
        }

        if (TryGetNullableInt32Property(element, "reconnectDelaySeconds", "ReconnectDelaySeconds", out var reconnectDelay))
        {
            overrides.Connection.ReconnectDelaySeconds = reconnectDelay;
        }

        if (TryGetNullableBoolProperty(element, "autoReconnectOnFailure", "AutoReconnectOnFailure", out var autoReconnect))
        {
            overrides.Connection.AutoReconnectOnFailure = autoReconnect;
        }

        if (TryGetNullableBoolProperty(element, "showNotificationOnDisconnect", "ShowNotificationOnDisconnect", out var notifyDisconnect))
        {
            overrides.Connection.ShowNotificationOnDisconnect = notifyDisconnect;
        }

        if (TryGetNullableBoolProperty(element, "showNotificationOnReconnect", "ShowNotificationOnReconnect", out var notifyReconnect))
        {
            overrides.Connection.ShowNotificationOnReconnect = notifyReconnect;
        }

        if (TryGetNullableBoolProperty(element, "playNotificationSound", "PlayNotificationSound", out var playSound))
        {
            overrides.Connection.PlayNotificationSound = playSound;
        }

        // Performance overrides
        if (TryGetStringProperty(element, "videoQuality", "VideoQuality", out var videoQuality))
        {
            overrides.Performance.VideoQuality = videoQuality;
        }

        if (TryGetNullableBoolProperty(element, "hardwareAcceleration", "HardwareAcceleration", out var hardwareAccel))
        {
            overrides.Performance.HardwareAcceleration = hardwareAccel;
        }

        // Display overrides
        if (TryGetNullableBoolProperty(element, "showOverlayTitle", "ShowOverlayTitle", out var showTitle))
        {
            overrides.CameraDisplay.ShowOverlayTitle = showTitle;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayDescription", "ShowOverlayDescription", out var showDescription))
        {
            overrides.CameraDisplay.ShowOverlayDescription = showDescription;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayTime", "ShowOverlayTime", out var showTime))
        {
            overrides.CameraDisplay.ShowOverlayTime = showTime;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayConnectionStatus", "ShowOverlayConnectionStatus", out var showStatus))
        {
            overrides.CameraDisplay.ShowOverlayConnectionStatus = showStatus;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayQuickActions", "ShowOverlayQuickActions", out var showQuickActions))
        {
            overrides.CameraDisplay.ShowOverlayQuickActions = showQuickActions;
        }

        if (TryGetNullableBoolProperty(element, "showOverlayConnectionTypeIcon", "ShowOverlayConnectionTypeIcon", out var showConnectionTypeIcon))
        {
            overrides.CameraDisplay.ShowOverlayConnectionTypeIcon = showConnectionTypeIcon;
        }

        if (TryGetStringProperty(element, "connectionTypeIconPlacement", "ConnectionTypeIconPlacement", out var iconPlacement) &&
            Enum.TryParse<ConnectionTypeIconPlacement>(iconPlacement, ignoreCase: true, out var iconPlacementEnum))
        {
            overrides.CameraDisplay.ConnectionTypeIconPlacement = iconPlacementEnum;
        }

        if (TryGetNullableDoubleProperty(element, "overlayOpacity", "OverlayOpacity", out var opacity))
        {
            overrides.CameraDisplay.OverlayOpacity = opacity;
        }

        if (TryGetStringProperty(element, "overlayPosition", "OverlayPosition", out var overlayPosition) &&
            Enum.TryParse<OverlayPosition>(overlayPosition, ignoreCase: true, out var positionEnum))
        {
            overrides.CameraDisplay.OverlayPosition = positionEnum;
        }

        // Recording overrides
        if (TryGetStringProperty(element, "recordingPath", "RecordingPath", out var recordingPath))
        {
            overrides.Recording.RecordingPath = recordingPath;
        }

        if (TryGetStringProperty(element, "recordingFormat", "RecordingFormat", out var recordingFormat))
        {
            overrides.Recording.RecordingFormat = recordingFormat;
        }

        if (TryGetNullableBoolProperty(element, "enableRecordingOnMotion", "EnableRecordingOnMotion", out var recordOnMotion))
        {
            overrides.Recording.EnableRecordingOnMotion = recordOnMotion;
        }

        if (TryGetNullableBoolProperty(element, "enableRecordingOnConnect", "EnableRecordingOnConnect", out var recordOnConnect))
        {
            overrides.Recording.EnableRecordingOnConnect = recordOnConnect;
        }

        if (TryGetNullableInt32Property(element, "thumbnailTileCount", "ThumbnailTileCount", out var tileCount))
        {
            overrides.Recording.ThumbnailTileCount = tileCount;
        }

        if (TryGetNullableBoolProperty(element, "enableTimelapse", "EnableTimelapse", out var enableTimelapse))
        {
            overrides.Recording.EnableTimelapse = enableTimelapse;
        }

        if (TryGetStringProperty(element, "timelapseInterval", "TimelapseInterval", out var timelapseInterval))
        {
            overrides.Recording.TimelapseInterval = timelapseInterval;
        }

        // Motion detection overrides
        if (TryGetNullableInt32Property(element, "motionSensitivity", "MotionSensitivity", out var motionSensitivity))
        {
            overrides.MotionDetection.Sensitivity = motionSensitivity;
        }

        if (TryGetNullableDoubleProperty(element, "motionMinimumChangePercent", "MotionMinimumChangePercent", out var minChangePercent))
        {
            overrides.MotionDetection.MinimumChangePercent = minChangePercent;
        }

        if (TryGetNullableInt32Property(element, "motionAnalysisFrameRate", "MotionAnalysisFrameRate", out var analysisFrameRate))
        {
            overrides.MotionDetection.AnalysisFrameRate = analysisFrameRate;
        }

        if (TryGetNullableInt32Property(element, "motionAnalysisWidth", "MotionAnalysisWidth", out var analysisWidth))
        {
            overrides.MotionDetection.AnalysisWidth = analysisWidth;
        }

        if (TryGetNullableInt32Property(element, "motionAnalysisHeight", "MotionAnalysisHeight", out var analysisHeight))
        {
            overrides.MotionDetection.AnalysisHeight = analysisHeight;
        }

        if (TryGetNullableInt32Property(element, "motionCooldownSeconds", "MotionCooldownSeconds", out var cooldownSeconds))
        {
            overrides.MotionDetection.CooldownSeconds = cooldownSeconds;
        }

        if (TryGetNullableInt32Property(element, "postMotionDurationSeconds", "PostMotionDurationSeconds", out var postMotionDuration))
        {
            overrides.MotionDetection.PostMotionDurationSeconds = postMotionDuration;
        }

        // Bounding box overrides
        if (TryGetNullableBoolProperty(element, "showBoundingBoxInGrid", "ShowBoundingBoxInGrid", out var showBoundingBoxInGrid))
        {
            overrides.MotionDetection.BoundingBox.ShowInGrid = showBoundingBoxInGrid;
        }

        if (TryGetNullableBoolProperty(element, "showBoundingBoxInFullScreen", "ShowBoundingBoxInFullScreen", out var showBoundingBoxInFullScreen))
        {
            overrides.MotionDetection.BoundingBox.ShowInFullScreen = showBoundingBoxInFullScreen;
        }

        if (TryGetStringProperty(element, "boundingBoxColor", "BoundingBoxColor", out var boundingBoxColor))
        {
            overrides.MotionDetection.BoundingBox.Color = boundingBoxColor;
        }

        if (TryGetNullableInt32Property(element, "boundingBoxThickness", "BoundingBoxThickness", out var boundingBoxThickness))
        {
            overrides.MotionDetection.BoundingBox.Thickness = boundingBoxThickness;
        }

        if (TryGetNullableDoubleProperty(element, "boundingBoxSmoothing", "BoundingBoxSmoothing", out var boundingBoxSmoothing))
        {
            overrides.MotionDetection.BoundingBox.Smoothing = boundingBoxSmoothing;
        }

        if (TryGetNullableInt32Property(element, "boundingBoxMinArea", "BoundingBoxMinArea", out var boundingBoxMinArea))
        {
            overrides.MotionDetection.BoundingBox.MinArea = boundingBoxMinArea;
        }

        if (TryGetNullableInt32Property(element, "boundingBoxPadding", "BoundingBoxPadding", out var boundingBoxPadding))
        {
            overrides.MotionDetection.BoundingBox.Padding = boundingBoxPadding;
        }
    }

    private static void WriteConnectionSettings(
        Utf8JsonWriter writer,
        ConnectionSettings settings)
    {
        writer.WriteStartObject();
        writer.WriteString("ipAddress", settings.IpAddress);
        writer.WriteString("protocol", settings.Protocol.ToString());
        writer.WriteNumber("port", settings.Port);
        if (settings.Path is not null)
        {
            writer.WriteString("path", settings.Path);
        }
        else
        {
            writer.WriteNull("path");
        }

        // Only emit the USB-specific fields when this is actually a USB
        // camera. Network cameras stay byte-identical to the v1 on-disk
        // shape so we don't churn the file for the existing user-base.
        if (settings.Source != CameraSource.Network)
        {
            writer.WriteString("source", settings.Source.ToString());
        }

        if (settings.ConnectionType != CameraConnectionType.None)
        {
            writer.WriteString("connectionType", settings.ConnectionType.ToString());
        }

        if (settings.Usb is not null)
        {
            writer.WritePropertyName("usb");
            WriteUsbConnectionSettings(writer, settings.Usb);
        }

        writer.WriteEndObject();
    }

    private static void WriteUsbConnectionSettings(
        Utf8JsonWriter writer,
        UsbConnectionSettings usb)
    {
        writer.WriteStartObject();
        writer.WriteString("deviceId", usb.DeviceId);
        writer.WriteString("friendlyName", usb.FriendlyName);
        writer.WriteBoolean("preferAudio", usb.PreferAudio);
        if (!string.IsNullOrEmpty(usb.AudioDeviceName))
        {
            writer.WriteString("audioDeviceName", usb.AudioDeviceName);
        }

        if (usb.Format is not null)
        {
            writer.WritePropertyName("format");
            writer.WriteStartObject();
            writer.WriteNumber("width", usb.Format.Width);
            writer.WriteNumber("height", usb.Format.Height);
            writer.WriteNumber("frameRate", usb.Format.FrameRate);
            writer.WriteString("pixelFormat", usb.Format.PixelFormat ?? string.Empty);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteAuthenticationSettings(
        Utf8JsonWriter writer,
        AuthenticationSettings settings)
    {
        writer.WriteStartObject();
        if (settings.UserName is not null)
        {
            writer.WriteString("userName", settings.UserName);
        }
        else
        {
            writer.WriteNull("userName");
        }

        if (settings.Password is not null)
        {
            writer.WriteString("password", settings.Password);
        }
        else
        {
            writer.WriteNull("password");
        }

        writer.WriteEndObject();
    }

    private static void WriteDisplaySettings(
        Utf8JsonWriter writer,
        CameraDisplaySettings settings)
    {
        writer.WriteStartObject();
        writer.WriteString("displayName", settings.DisplayName);
        if (settings.Description is not null)
        {
            writer.WriteString("description", settings.Description);
        }
        else
        {
            writer.WriteNull("description");
        }

        writer.WriteString("overlayPosition", settings.OverlayPosition.ToString());
        writer.WriteString("rotation", settings.Rotation.ToString());
        writer.WriteEndObject();
    }

    private static void WriteStreamSettings(
        Utf8JsonWriter writer,
        StreamSettings settings)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("useLowLatencyMode", settings.UseLowLatencyMode);
        writer.WriteNumber("maxLatencyMs", settings.MaxLatencyMs);
        writer.WriteString("rtspTransport", settings.RtspTransport);
        writer.WriteNumber("bufferDurationMs", settings.BufferDurationMs);
        writer.WriteEndObject();
    }

    private static void WriteOverridesSettings(
        Utf8JsonWriter writer,
        CameraOverrides overrides)
    {
        writer.WriteStartObject();

        // Connection sub-section
        if (overrides.Connection.HasAnyOverride())
        {
            writer.WritePropertyName("connection");
            WriteConnectionOverrides(writer, overrides.Connection);
        }

        // CameraDisplay sub-section
        if (overrides.CameraDisplay.HasAnyOverride())
        {
            writer.WritePropertyName("cameraDisplay");
            WriteCameraDisplayOverrides(writer, overrides.CameraDisplay);
        }

        // Performance sub-section
        if (overrides.Performance.HasAnyOverride())
        {
            writer.WritePropertyName("performance");
            WritePerformanceOverrides(writer, overrides.Performance);
        }

        // Recording sub-section
        if (overrides.Recording.HasAnyOverride())
        {
            writer.WritePropertyName("recording");
            WriteRecordingOverrides(writer, overrides.Recording);
        }

        // MotionDetection sub-section
        if (overrides.MotionDetection.HasAnyOverride())
        {
            writer.WritePropertyName("motionDetection");
            WriteMotionDetectionOverrides(writer, overrides.MotionDetection);
        }

        writer.WriteEndObject();
    }

    private static void WriteConnectionOverrides(
        Utf8JsonWriter writer,
        ConnectionOverrides conn)
    {
        writer.WriteStartObject();

        if (conn.ConnectionTimeoutSeconds.HasValue)
        {
            writer.WriteNumber("connectionTimeoutSeconds", conn.ConnectionTimeoutSeconds.Value);
        }

        if (conn.ReconnectDelaySeconds.HasValue)
        {
            writer.WriteNumber("reconnectDelaySeconds", conn.ReconnectDelaySeconds.Value);
        }

        if (conn.AutoReconnectOnFailure.HasValue)
        {
            writer.WriteBoolean("autoReconnectOnFailure", conn.AutoReconnectOnFailure.Value);
        }

        if (conn.ShowNotificationOnDisconnect.HasValue)
        {
            writer.WriteBoolean("showNotificationOnDisconnect", conn.ShowNotificationOnDisconnect.Value);
        }

        if (conn.ShowNotificationOnReconnect.HasValue)
        {
            writer.WriteBoolean("showNotificationOnReconnect", conn.ShowNotificationOnReconnect.Value);
        }

        if (conn.PlayNotificationSound.HasValue)
        {
            writer.WriteBoolean("playNotificationSound", conn.PlayNotificationSound.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteCameraDisplayOverrides(
        Utf8JsonWriter writer,
        CameraDisplayOverrides display)
    {
        writer.WriteStartObject();

        if (display.ShowOverlayTitle.HasValue)
        {
            writer.WriteBoolean("showOverlayTitle", display.ShowOverlayTitle.Value);
        }

        if (display.ShowOverlayDescription.HasValue)
        {
            writer.WriteBoolean("showOverlayDescription", display.ShowOverlayDescription.Value);
        }

        if (display.ShowOverlayTime.HasValue)
        {
            writer.WriteBoolean("showOverlayTime", display.ShowOverlayTime.Value);
        }

        if (display.ShowOverlayConnectionStatus.HasValue)
        {
            writer.WriteBoolean("showOverlayConnectionStatus", display.ShowOverlayConnectionStatus.Value);
        }

        if (display.ShowOverlayQuickActions.HasValue)
        {
            writer.WriteBoolean("showOverlayQuickActions", display.ShowOverlayQuickActions.Value);
        }

        if (display.ShowOverlayConnectionTypeIcon.HasValue)
        {
            writer.WriteBoolean("showOverlayConnectionTypeIcon", display.ShowOverlayConnectionTypeIcon.Value);
        }

        if (display.ConnectionTypeIconPlacement.HasValue)
        {
            writer.WriteString("connectionTypeIconPlacement", display.ConnectionTypeIconPlacement.Value.ToString());
        }

        if (display.OverlayOpacity.HasValue)
        {
            writer.WriteNumber("overlayOpacity", display.OverlayOpacity.Value);
        }

        if (display.OverlayPosition.HasValue)
        {
            writer.WriteString("overlayPosition", display.OverlayPosition.Value.ToString());
        }

        writer.WriteEndObject();
    }

    private static void WritePerformanceOverrides(
        Utf8JsonWriter writer,
        PerformanceOverrides perf)
    {
        writer.WriteStartObject();

        if (perf.VideoQuality is not null)
        {
            writer.WriteString("videoQuality", perf.VideoQuality);
        }

        if (perf.HardwareAcceleration.HasValue)
        {
            writer.WriteBoolean("hardwareAcceleration", perf.HardwareAcceleration.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteRecordingOverrides(
        Utf8JsonWriter writer,
        RecordingOverrides rec)
    {
        writer.WriteStartObject();

        if (rec.RecordingPath is not null)
        {
            writer.WriteString("recordingPath", rec.RecordingPath);
        }

        if (rec.RecordingFormat is not null)
        {
            writer.WriteString("recordingFormat", rec.RecordingFormat);
        }

        if (rec.EnableRecordingOnMotion.HasValue)
        {
            writer.WriteBoolean("enableRecordingOnMotion", rec.EnableRecordingOnMotion.Value);
        }

        if (rec.EnableRecordingOnConnect.HasValue)
        {
            writer.WriteBoolean("enableRecordingOnConnect", rec.EnableRecordingOnConnect.Value);
        }

        if (rec.ThumbnailTileCount.HasValue)
        {
            writer.WriteNumber("thumbnailTileCount", rec.ThumbnailTileCount.Value);
        }

        if (rec.EnableTimelapse.HasValue)
        {
            writer.WriteBoolean("enableTimelapse", rec.EnableTimelapse.Value);
        }

        if (rec.TimelapseInterval is not null)
        {
            writer.WriteString("timelapseInterval", rec.TimelapseInterval);
        }

        writer.WriteEndObject();
    }

    private static void WriteMotionDetectionOverrides(
        Utf8JsonWriter writer,
        MotionDetectionOverrides motion)
    {
        writer.WriteStartObject();

        if (motion.Sensitivity.HasValue)
        {
            writer.WriteNumber("sensitivity", motion.Sensitivity.Value);
        }

        if (motion.SmallestObjectPercent.HasValue)
        {
            writer.WriteNumber("smallestObjectPercent", motion.SmallestObjectPercent.Value);
        }

        if (motion.StoppedObjectSeconds.HasValue)
        {
            writer.WriteNumber("stoppedObjectSeconds", motion.StoppedObjectSeconds.Value);
        }

        if (motion.IgnoreLightingChanges.HasValue)
        {
            writer.WriteBoolean("ignoreLightingChanges", motion.IgnoreLightingChanges.Value);
        }

        if (motion.MinimumChangePercent.HasValue)
        {
            writer.WriteNumber("minimumChangePercent", motion.MinimumChangePercent.Value);
        }

        if (motion.AnalysisFrameRate.HasValue)
        {
            writer.WriteNumber("analysisFrameRate", motion.AnalysisFrameRate.Value);
        }

        if (motion.AnalysisWidth.HasValue)
        {
            writer.WriteNumber("analysisWidth", motion.AnalysisWidth.Value);
        }

        if (motion.AnalysisHeight.HasValue)
        {
            writer.WriteNumber("analysisHeight", motion.AnalysisHeight.Value);
        }

        if (motion.PostMotionDurationSeconds.HasValue)
        {
            writer.WriteNumber("postMotionDurationSeconds", motion.PostMotionDurationSeconds.Value);
        }

        if (motion.CooldownSeconds.HasValue)
        {
            writer.WriteNumber("cooldownSeconds", motion.CooldownSeconds.Value);
        }

        if (motion.BoundingBox.HasAnyOverride())
        {
            writer.WritePropertyName("boundingBox");
            WriteBoundingBoxOverrides(writer, motion.BoundingBox);
        }

        writer.WriteEndObject();
    }

    private static void WriteBoundingBoxOverrides(
        Utf8JsonWriter writer,
        BoundingBoxOverrides bb)
    {
        writer.WriteStartObject();

        if (bb.ShowInGrid.HasValue)
        {
            writer.WriteBoolean("showInGrid", bb.ShowInGrid.Value);
        }

        if (bb.ShowInFullScreen.HasValue)
        {
            writer.WriteBoolean("showInFullScreen", bb.ShowInFullScreen.Value);
        }

        if (bb.Color is not null)
        {
            writer.WriteString("color", bb.Color);
        }

        if (bb.Thickness.HasValue)
        {
            writer.WriteNumber("thickness", bb.Thickness.Value);
        }

        if (bb.Smoothing.HasValue)
        {
            writer.WriteNumber("smoothing", bb.Smoothing.Value);
        }

        if (bb.MinArea.HasValue)
        {
            writer.WriteNumber("minArea", bb.MinArea.Value);
        }

        if (bb.Padding.HasValue)
        {
            writer.WriteNumber("padding", bb.Padding.Value);
        }

        writer.WriteEndObject();
    }

    private static bool TryGetStringProperty(
        JsonElement element,
        string camelName,
        string pascalName,
        [NotNullWhen(true)] out string? value)
    {
        if ((element.TryGetProperty(camelName, out var prop) ||
             element.TryGetProperty(pascalName, out prop)) &&
            prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return value is not null;
        }

        value = null;
        return false;
    }

    private static bool TryGetInt32Property(
        JsonElement element,
        string camelName,
        string pascalName,
        out int value)
    {
        if ((element.TryGetProperty(camelName, out var prop) ||
             element.TryGetProperty(pascalName, out prop)) &&
            prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetBoolProperty(
        JsonElement element,
        string camelName,
        string pascalName,
        out bool value)
    {
        if ((element.TryGetProperty(camelName, out var prop) ||
             element.TryGetProperty(pascalName, out prop)) &&
            prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = prop.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetNullableInt32Property(
        JsonElement element,
        string camelName,
        string pascalName,
        out int? value)
    {
        if ((element.TryGetProperty(camelName, out var prop) ||
             element.TryGetProperty(pascalName, out prop)) &&
            prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var intValue))
        {
            value = intValue;
            return true;
        }

        value = null;
        return false;
    }

    private static bool HasObjectProperty(
        JsonElement element,
        string camelName,
        string pascalName)
        => (element.TryGetProperty(camelName, out var prop) || element.TryGetProperty(pascalName, out prop)) &&
           prop.ValueKind == JsonValueKind.Object;

    private static bool TryGetNullableBoolProperty(
        JsonElement element,
        string camelName,
        string pascalName,
        out bool? value)
    {
        if ((element.TryGetProperty(camelName, out var prop) ||
             element.TryGetProperty(pascalName, out prop)) &&
            prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = prop.GetBoolean();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetNullableDoubleProperty(
        JsonElement element,
        string camelName,
        string pascalName,
        out double? value)
    {
        if ((element.TryGetProperty(camelName, out var prop) ||
             element.TryGetProperty(pascalName, out prop)) &&
            prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var doubleValue))
        {
            value = doubleValue;
            return true;
        }

        value = null;
        return false;
    }
}