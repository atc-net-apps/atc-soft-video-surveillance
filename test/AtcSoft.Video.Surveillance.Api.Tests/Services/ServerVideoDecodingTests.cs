namespace AtcSoft.Video.Surveillance.Api.Tests.Services;

/// <summary>
/// The server decodes on the processor unless decoding on the graphics card is asked for.
/// </summary>
public sealed class ServerVideoDecodingTests
{
    [Fact]
    public void Video_Is_Decoded_On_The_Processor_By_Default()
        => ServerVideoDecoding.Choose(Configuration(null), NullLogger.Instance).Should().BeNull();

    [Fact]
    public void Video_Is_Decoded_On_The_Graphics_Card_When_Asked_For()
        => ServerVideoDecoding.Choose(Configuration("true"), NullLogger.Instance)
            .Should().BeOfType<Atc.Video.Engine.DirectX.D3D11AcceleratorFactory>();

    private static IConfiguration Configuration(string? hardwareDecoding)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(hardwareDecoding is null ? [] : [new(ServerVideoDecoding.ConfigurationKey, hardwareDecoding)])
            .Build();
}