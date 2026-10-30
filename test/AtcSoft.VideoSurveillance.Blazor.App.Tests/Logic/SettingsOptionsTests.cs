namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

using CoreItems = AtcSoft.VideoSurveillance.Factories.DropDownItemsFactory;

public sealed class SettingsOptionsTests
{
    [Fact]
    public void A_Value_That_Is_A_Choice_Leaves_The_Choices_As_They_Are()
        => SettingsOptions.WithCurrent(SettingsOptions.RetentionPeriods, 30, d => $"{d} days")
            .Should().BeSameAs(SettingsOptions.RetentionPeriods);

    [Fact]
    public void A_Value_Set_Elsewhere_Is_Added_So_It_Can_Be_Shown()
    {
        var options = SettingsOptions.WithCurrent(SettingsOptions.RetentionPeriods, 45, d => $"{d} days");

        options.Should().HaveCount(SettingsOptions.RetentionPeriods.Count + 1);
        options[^1].Should().Be((45, "45 days"));
    }

    [Fact]
    public void The_Video_Qualities_Are_The_Ones_The_Server_Knows()
        => SettingsOptions.VideoQualities.Select(o => o.Value).Should().Equal(CoreItems.VideoQualityItems.Keys);

    [Fact]
    public void The_Defaults_Are_Among_The_Choices()
    {
        var state = new Pages.SettingsTabs.SettingsState();

        SettingsOptions.VideoQualities.Should().Contain(o => o.Value == state.VideoQuality);
        SettingsOptions.MotionSensitivities.Should().Contain(o => o.Value == state.MotionSensitivity);
        SettingsOptions.AnalysisWidths.Should().Contain(o => o.Value == state.AnalysisWidth);
        SettingsOptions.PostMotionDurations.Should().Contain(o => o.Value == state.PostMotionDurationSeconds);
        SettingsOptions.BoundingBoxThicknesses.Should().Contain(o => o.Value == state.BoundingBoxThickness);
        SettingsOptions.ThumbnailTiles.Should().Contain(o => o.Value == state.ThumbnailTileCount);
        SettingsOptions.MaxRecordingDurations.Should().Contain(o => o.Value == state.MaxRecordingDurationMinutes);
        SettingsOptions.RetentionPeriods.Should().Contain(o => o.Value == state.RecordingRetentionDays);
        SettingsOptions.RetentionPeriods.Should().Contain(o => o.Value == state.SnapshotRetentionDays);
        SettingsOptions.MinFreeSpaces.Should().Contain(o => o.Value == state.MinFreeSpaceMb);
        SettingsOptions.TimelapseIntervals.Should().Contain(o => o.Value == state.TimelapseInterval);
        ColorChoices.Names.Should().Contain(state.BoundingBoxColor);
        ColorChoices.Names.Should().Contain(state.PlaybackFilenameColor);
    }
}