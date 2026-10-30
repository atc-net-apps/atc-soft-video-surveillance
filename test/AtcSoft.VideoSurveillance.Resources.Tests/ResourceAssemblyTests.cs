namespace AtcSoft.VideoSurveillance.Resources;

/// <summary>
/// Atc.Wpf finds a .resx by its base name in the loaded assemblies, so the
/// name must be the class's, and every culture must come from a satellite.
/// </summary>
public sealed class ResourceAssemblyTests
{
    [Fact]
    public void The_Resources_Are_Found_By_The_Name_The_Views_Use()
        => typeof(Translations).Assembly.GetManifestResourceNames()
            .Should().Contain("AtcSoft.VideoSurveillance.Resources.Translations.resources");

    [Theory]
    [InlineData("da-DK", "Annuller")]
    [InlineData("de-DE", "Abbrechen")]
    [InlineData("en-US", "Cancel")]
    public void A_Culture_Gets_Its_Own_Text(
        string culture,
        string cancel)
        => Translations.ResourceManager.GetString(nameof(Translations.Cancel), new CultureInfo(culture))
            .Should().Be(cancel);

    [Theory]
    [InlineData("en-GB", "Bounding box colour")]
    [InlineData("en-US", "Bounding box color")]
    public void British_English_Has_Its_Own_Spelling(
        string culture,
        string text)
        => Translations.ResourceManager.GetString(nameof(Translations.BoundingBoxColor), new CultureInfo(culture))
            .Should().Be(text);
}