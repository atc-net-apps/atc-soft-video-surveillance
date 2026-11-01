namespace AtcSoft.Video.Surveillance.Api.Services;

using ILogger = Microsoft.Extensions.Logging.ILogger;

/// <summary>
/// Where the server decodes its cameras' video: on the processor (the default), or on the graphics
/// card (D3D11VA) when <c>Video:HardwareDecoding</c> is true. The server decodes every picture of a
/// recorded camera for its activity index, so decoding on the card takes most of that work off the
/// processor. A machine without a graphics card that decodes falls back to the processor by itself.
/// </summary>
public static partial class ServerVideoDecoding
{
    /// <summary>
    /// The configuration key: <c>Video:HardwareDecoding</c> (environment variable
    /// <c>Video__HardwareDecoding</c>).
    /// </summary>
    public const string ConfigurationKey = "Video:HardwareDecoding";

    /// <summary>
    /// Gets the graphics accelerator factory for the server's players, or <see langword="null"/>
    /// to decode on the processor.
    /// </summary>
    /// <param name="configuration">The server's configuration.</param>
    /// <param name="logger">Where the choice is logged.</param>
    /// <returns>The factory, or <see langword="null"/>.</returns>
    public static IGpuAcceleratorFactory? Choose(
        IConfiguration configuration,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        if (!configuration.GetValue(ConfigurationKey, defaultValue: false))
        {
            LogProcessorDecoding(logger, ConfigurationKey);
            return null;
        }

        LogHardwareDecoding(logger);
        return new Atc.Video.Engine.DirectX.D3D11AcceleratorFactory();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Video is decoded on the processor; set {Key} to true to decode on the graphics card")]
    private static partial void LogProcessorDecoding(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "Video is decoded on the graphics card (D3D11VA) where a camera's player can open it, otherwise on the processor")]
    private static partial void LogHardwareDecoding(ILogger logger);
}