namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// Every release carries one installer per desktop app. Each app must offer its own installer
/// as its update, never the other app's.
/// </summary>
public sealed class GitHubReleaseServiceTests
{
    [Theory]
    [InlineData("AtcSoft.Video.Surveillance.Wpf.App", "AtcSoft.Video.Surveillance.Installer.msi")]
    [InlineData("AtcSoft.Video.Surveillance.CameraWall.Wpf.App", "AtcSoft.Video.Surveillance.CameraWall.Installer.msi")]
    public void Each_App_Is_Updated_With_Its_Own_Installer(
        string appAssemblyName,
        string installer)
        => GitHubReleaseService.GetInstallerAssetName(appAssemblyName).Should().Be(installer);

    [Theory]
    [InlineData(null)]
    [InlineData("testhost")]
    [InlineData("AtcSoft.Video.Surveillance.Api")]
    public void Anything_But_A_Desktop_App_Has_No_Installer(
        string? appAssemblyName)
        => GitHubReleaseService.GetInstallerAssetName(appAssemblyName).Should().BeNull();

    [Fact]
    public void Every_Desktop_App_Has_An_Installer_Project_With_The_Expected_Name()
    {
        // The release uploads each installer under its project name, so a renamed app or
        // installer project would silently stop the app from finding its update.
        var root = FindRepositoryRoot();
        var apps = System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(root, "AtcSoft.Video.Surveillance.slnx"))
            .Descendants("Project")
            .Select(p => System.IO.Path.GetFileNameWithoutExtension((string?)p.Attribute("Path")))
            .Where(name => name is not null && name.EndsWith(".Wpf.App", StringComparison.Ordinal))
            .ToList();

        apps.Should().HaveCount(2);
        foreach (var app in apps)
        {
            var installer = System.IO.Path.GetFileNameWithoutExtension(GitHubReleaseService.GetInstallerAssetName(app));
            System.IO.File.Exists(System.IO.Path.Combine(root, "setup", installer!, installer + ".wixproj"))
                .Should().BeTrue($"{app} is updated with {installer}.msi");
        }
    }

    private static string FindRepositoryRoot()
    {
        var folder = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(System.IO.Path.Combine(folder.FullName, "AtcSoft.Video.Surveillance.slnx")))
        {
            folder = folder.Parent;
        }

        folder.Should().NotBeNull("the test runs from below the repository");
        return folder!.FullName;
    }
}