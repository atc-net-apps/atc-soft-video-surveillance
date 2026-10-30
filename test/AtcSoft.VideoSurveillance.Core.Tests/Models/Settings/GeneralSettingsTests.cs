namespace AtcSoft.VideoSurveillance.Models.Settings;

public class GeneralSettingsTests
{
    [Fact]
    public void New_Instance_Has_Expected_Defaults()
    {
        // Act
        var settings = new GeneralSettings();

        // Assert
        settings.ThemeBase.Should().Be("Dark");
        settings.ThemeAccent.Should().Be("Blue");
        settings.Language.Should().Be("1033");
        settings.ConnectCamerasOnStartup.Should().BeTrue();
        settings.StartMaximized.Should().BeFalse();
        settings.StartRibbonCollapsed.Should().BeFalse();
    }

    [Fact]
    public void The_Grid_Shows_All_Cameras_Until_A_Size_Is_Chosen_And_A_Copy_Keeps_The_Choice()
    {
        new GeneralSettings().GridSize.Should().Be(0, "zero is the automatic grid");

        new GeneralSettings { GridSize = 3 }.Clone().GridSize.Should().Be(3);
    }

    [Fact]
    public void Clone_Creates_Independent_Copy()
    {
        // Arrange
        var original = new GeneralSettings
        {
            ThemeBase = "Light",
            ThemeAccent = "Red",
            Language = "1030",
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.ThemeBase.Should().Be("Light");
        clone.ThemeAccent.Should().Be("Red");
        clone.Language.Should().Be("1030");

        // Verify independence
        clone.ThemeBase = "Dark";
        original.ThemeBase.Should().Be("Light");
    }
}