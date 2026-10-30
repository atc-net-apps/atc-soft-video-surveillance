namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

using UiCameraConfiguration = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraConfiguration;

/// <summary>
/// CameraWall keeps camera passwords encrypted in cameras.json (Windows data protection for
/// the machine: every user of the computer can use the cameras, a copy of the file elsewhere
/// cannot). In memory a camera keeps its plain password; a file written before is taken over.
/// </summary>
public sealed class CameraStorageServicePasswordTests : IDisposable
{
    private const string Secret = "camera-password-123";

    private readonly string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-storage-" + Guid.NewGuid().ToString("N"));

    private string FilePath
        => System.IO.Path.Combine(folder, "cameras.json");

    public void Dispose()
    {
        if (System.IO.Directory.Exists(folder))
        {
            TestFolders.Delete(folder);
        }
    }

    [Fact]
    public void A_Saved_Password_Is_Not_In_The_File_And_Comes_Back_On_Load()
    {
        System.IO.Directory.CreateDirectory(folder);
        var storage = new CameraStorageService(FilePath);
        var camera = new UiCameraConfiguration();
        camera.Authentication.Password = Secret;
        storage.AddOrUpdateCamera(camera);
        storage.Save();

        System.IO.File.ReadAllText(FilePath).Should().NotContain(Secret);
        camera.Authentication.Password.Should().Be(Secret, "the camera in memory keeps its plain password");

        var reloaded = new CameraStorageService(FilePath);
        reloaded.GetCameraById(camera.Id)!.Authentication.Password.Should().Be(Secret);
    }

    [Fact]
    public void A_File_With_Plain_Passwords_Is_Read_And_Written_Again_Encrypted()
    {
        System.IO.Directory.CreateDirectory(folder);
        var id = Guid.NewGuid();
        var json = $$"""
            {
              "cameras": [
                {
                  "id": "{{id}}",
                  "connection": { "ipAddress": "192.0.2.10", "protocol": "Rtsp", "port": 554 },
                  "authentication": { "userName": "admin", "password": "{{Secret}}" },
                  "display": { "displayName": "Old" }
                }
              ]
            }
            """;
        System.IO.File.WriteAllText(FilePath, json);

        var storage = new CameraStorageService(FilePath);

        storage.GetCameraById(id)!.Authentication.Password.Should().Be(Secret);
        System.IO.File.ReadAllText(FilePath).Should().NotContain(Secret);
        System.IO.File.ReadAllText(FilePath + ".bak").Should().NotContain(Secret, "the backup copy must not keep the plain password either");
    }

    [Fact]
    public void A_Password_That_Cannot_Be_Decrypted_Is_Left_Empty()
    {
        System.IO.Directory.CreateDirectory(folder);
        var id = Guid.NewGuid();
        var json = $$"""
            {
              "cameras": [
                {
                  "id": "{{id}}",
                  "connection": { "ipAddress": "192.0.2.10", "protocol": "Rtsp", "port": 554 },
                  "authentication": { "userName": "admin", "password": "dpapi:v1:bm90LWVuY3J5cHRlZA==" },
                  "display": { "displayName": "Moved" }
                }
              ]
            }
            """;
        System.IO.File.WriteAllText(FilePath, json);

        var storage = new CameraStorageService(FilePath);

        var camera = storage.GetCameraById(id);
        camera.Should().NotBeNull("one unreadable password must not lose the camera");
        camera!.Authentication.Password.Should().BeNullOrEmpty();
    }
}