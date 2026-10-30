namespace AtcSoft.VideoSurveillance.Resources;

/// <summary>
/// An app that uses the Wpf.Core package's controls may never touch
/// Translations itself, and Atc.Wpf finds the texts only in assemblies already
/// loaded. So each of those controls that binds a text in XAML loads the texts
/// in its static constructor, before its XAML is read.
/// </summary>
public sealed class PackageControlTests
{
    [Fact]
    public void A_Package_Control_Loads_The_Texts_Before_Its_Xaml()
        => RepositoryFiles.Find("*.xaml", "src/AtcSoft.VideoSurveillance.Wpf.Core/UserControls")
            .Where(xaml => File.ReadAllText(xaml).Contains("atcTranslation:Resx", StringComparison.Ordinal))
            .Where(xaml => !File.ReadAllText(xaml + ".cs").Contains("_ = Translations.ResourceManager;", StringComparison.Ordinal))
            .Select(RepositoryFiles.Relative)
            .Should().BeEmpty("a static constructor touches Translations.ResourceManager");
}