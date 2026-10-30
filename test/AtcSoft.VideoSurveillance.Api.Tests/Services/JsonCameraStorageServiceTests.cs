namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// A camera's password is kept encrypted in cameras.json; the cameras in
/// memory keep it as it is, since the stream needs it.
/// </summary>
public sealed class JsonCameraStorageServiceTests : IDisposable
{
    private const string Password = "camera-pass-1234";

    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-cameras-" + Guid.NewGuid().ToString("N"));
    private readonly EphemeralDataProtectionProvider keys = new();

    public JsonCameraStorageServiceTests()
        => Directory.CreateDirectory(folder);

    private string FilePath
        => Path.Combine(folder, "cameras.json");

    public void Dispose()
        => Directory.Delete(folder, recursive: true);

    [Fact]
    public void The_Password_Is_Encrypted_In_The_File_And_Read_Back()
    {
        var sut = CreateSut();
        var camera = CreateCamera();

        sut.AddOrUpdateCamera(camera);

        var raw = File.ReadAllText(FilePath);
        raw.Should().NotContain(Password);
        raw.Should().Contain(SecretProtector.Prefix);
        camera.Authentication.Password.Should().Be(Password);
        sut.GetCameraById(camera.Id)!.Authentication.Password.Should().Be(Password);

        CreateSut().GetCameraById(camera.Id)!.Authentication.Password.Should().Be(Password);
    }

    [Fact]
    public void A_Password_Written_Before_Encryption_Is_Encrypted_When_The_File_Is_Read()
    {
        var camera = CreateCamera();
        WritePlainFile(camera);

        var sut = CreateSut();

        sut.GetCameraById(camera.Id)!.Authentication.Password.Should().Be(Password);
        File.ReadAllText(FilePath).Should().NotContain(Password);
        File.ReadAllText(FilePath + ".bak").Should().NotContain(Password);
    }

    [Fact]
    public void A_Password_From_Other_Keys_Is_Read_As_None()
    {
        var camera = CreateCamera();
        new JsonCameraStorageService(FilePath, new SecretProtector(new EphemeralDataProtectionProvider()), NullLogger<JsonCameraStorageService>.Instance)
            .AddOrUpdateCamera(camera);

        var sut = CreateSut();

        var read = sut.GetCameraById(camera.Id)!;
        read.Authentication.UserName.Should().Be("admin");
        read.Authentication.Password.Should().BeNull();
    }

    [Fact]
    public void A_Camera_Without_A_Password_Is_Kept_As_It_Is()
    {
        var sut = CreateSut();
        var camera = CreateCamera();
        camera.Authentication.Password = null;

        sut.AddOrUpdateCamera(camera);

        CreateSut().GetCameraById(camera.Id)!.Authentication.Password.Should().BeNull();
    }

    [Fact]
    public void The_Motion_Zones_Are_Kept_In_The_File()
    {
        var camera = CreateCamera();
        camera.MotionZones =
        [
            new() { Name = "Tree", Kind = CoreMotionZoneKind.Ignore, Points = [new(0.1, 0.2), new(0.4, 0.2), new(0.4, 0.9)], Color = "Green" },
        ];
        CreateSut().AddOrUpdateCamera(camera);

        var read = CreateSut().GetCameraById(camera.Id)!;

        read.MotionZones.Should().ContainSingle().Which.ValueEquals(camera.MotionZones[0]).Should().BeTrue();
        File.ReadAllText(FilePath).Should().Contain("\"motionZones\"").And.Contain("\"Ignore\"");
    }

    private static CameraConfiguration CreateCamera()
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = "Front door";
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = Password;
        return camera;
    }

    private JsonCameraStorageService CreateSut()
        => new(FilePath, new SecretProtector(keys), NullLogger<JsonCameraStorageService>.Instance);

    // A file as a server before encryption wrote it.
    private void WritePlainFile(CameraConfiguration camera)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        var data = new CameraStorageData();
        data.Cameras.Add(camera);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, options));
    }
}