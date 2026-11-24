namespace AtcSoft.Video.Surveillance.Models;

/// <summary>
/// A network camera's sub-stream: a second path (and port) on the same camera, for the live
/// grid and motion detection. A camera without one opens its main stream for every role.
/// </summary>
public sealed class ConnectionSettingsSubStreamTests
{
    [Fact]
    public void A_Network_Camera_With_A_Path_Has_A_Sub_Stream()
    {
        var connection = new ConnectionSettings { SubStreamPath = "/stream2" };

        connection.HasSubStream.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_Camera_Without_A_Path_Has_No_Sub_Stream(string? path)
        => new ConnectionSettings { SubStreamPath = path }.HasSubStream.Should().BeFalse();

    [Fact]
    public void A_Usb_Camera_Has_No_Sub_Stream()
        => new ConnectionSettings { Source = CameraSource.Usb, SubStreamPath = "/stream2" }.HasSubStream.Should().BeFalse();

    [Fact]
    public void Clone_Copy_And_Equality_Include_The_Sub_Stream()
    {
        var connection = new ConnectionSettings { IpAddress = "192.0.2.10", SubStreamPath = "/stream2", SubStreamPort = 8554 };

        var clone = connection.Clone();
        var copy = new ConnectionSettings();
        copy.CopyFrom(connection);

        (clone.SubStreamPath, clone.SubStreamPort).Should().Be(("/stream2", 8554));
        (copy.SubStreamPath, copy.SubStreamPort).Should().Be(("/stream2", 8554));
        clone.ValueEquals(connection).Should().BeTrue();
        new ConnectionSettings { IpAddress = "192.0.2.10", SubStreamPath = "/stream3", SubStreamPort = 8554 }.ValueEquals(connection).Should().BeFalse();
        new ConnectionSettings { IpAddress = "192.0.2.10", SubStreamPath = "/stream2" }.ValueEquals(connection).Should().BeFalse();
    }

    [Fact]
    public void A_Camera_Stored_Before_Sub_Streams_Loads_Without_One()
    {
        // As 1.7.0 wrote a camera's connection: camel case, enums as strings.
        const string json = """{"source":"Network","ipAddress":"192.0.2.10","protocol":"Rtsp","port":554,"path":"/stream1","connectionType":"None"}""";

        var connection = JsonSerializer.Deserialize<ConnectionSettings>(json, StorageOptions)!;

        connection.SubStreamPath.Should().BeNull();
        connection.SubStreamPort.Should().BeNull();
        connection.HasSubStream.Should().BeFalse();
    }

    [Fact]
    public void The_File_Keeps_The_Sub_Stream_But_Not_Whether_There_Is_One()
    {
        var connection = new ConnectionSettings { IpAddress = "192.0.2.10", SubStreamPath = "/stream2", SubStreamPort = 8554 };

        var json = JsonSerializer.Serialize(connection, StorageOptions);
        var back = JsonSerializer.Deserialize<ConnectionSettings>(json, StorageOptions)!;

        json.Should().Contain("\"subStreamPath\":\"/stream2\"").And.NotContain("hasSubStream");
        back.ValueEquals(connection).Should().BeTrue();
    }

    private static JsonSerializerOptions StorageOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
