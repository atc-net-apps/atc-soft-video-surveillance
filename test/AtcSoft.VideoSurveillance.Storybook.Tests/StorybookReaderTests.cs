namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// A storybook is read with errors that say where they are, so a scene written by hand is
/// fixed quickly.
/// </summary>
public sealed class StorybookReaderTests
{
    [Fact]
    public void Every_Scene_In_The_Catalogue_Reads()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Scenes"), "*.storybook.json"))
        {
            var scene = StorybookReader.ReadFile(file);

            scene.Name.Should().NotBeNullOrWhiteSpace(Path.GetFileName(file));
        }
    }

    [Fact]
    public void Key_Points_Out_Of_Order_Are_Named()
    {
        const string json = """
            { "version": 1, "name": "x",
              "objects": [ { "id": "car", "kind": "car", "size": [0.2, 0.1],
                             "path": [ { "t": 5, "at": [0, 0.5] }, { "t": 3, "at": [1, 0.5] } ] } ] }
            """;

        var read = () => StorybookReader.Read(json);

        read.Should().Throw<StorybookException>().WithMessage("*objects[0].path[1].t*");
    }

    [Fact]
    public void An_Unknown_Kind_Is_Named()
    {
        const string json = """
            { "version": 1, "name": "x",
              "objects": [ { "id": "u", "kind": "unicorn", "size": [0.2, 0.1], "path": [ { "t": 1, "at": [0.5, 0.5] } ] } ] }
            """;

        var read = () => StorybookReader.Read(json);

        read.Should().Throw<StorybookException>().WithMessage("*objects[0].kind*unicorn*");
    }

    [Fact]
    public void A_Bitrate_Of_Nothing_Is_Named()
    {
        const string json = """{ "version": 1, "name": "x", "video": { "bitrateKbps": 0 } }""";

        var read = () => StorybookReader.Read(json);

        read.Should().Throw<StorybookException>().WithMessage("*video.bitrateKbps*");
    }

    [Fact]
    public void A_Clock_Start_That_Is_No_Time_Is_Named()
    {
        const string json = """{ "version": 1, "name": "x", "background": { "distractors": [ { "id": "clock", "kind": "clockOverlay", "startsAt": "25:00" } ] } }""";

        var read = () => StorybookReader.Read(json);

        read.Should().Throw<StorybookException>().WithMessage("*distractors[0].startsAt*25:00*");
    }

    [Fact]
    public void A_Misspelt_Property_Is_Named()
    {
        const string json = """{ "version": 1, "name": "x", "vidoe": { } }""";

        var read = () => StorybookReader.Read(json);

        read.Should().Throw<StorybookException>().WithMessage("*vidoe*");
    }

    [Theory]
    [InlineData(2.2, true)]
    [InlineData(3, true)]
    [InlineData(3.5, false)]
    public void An_Object_May_Be_Larger_Than_The_Picture_Up_To_Three_Times(
        double height,
        bool reads)
    {
        var size = height.ToString(CultureInfo.InvariantCulture);
        var json = $$"""
            { "version": 1, "name": "x",
              "objects": [ { "id": "near", "kind": "human", "size": [1.3, {{size}}], "path": [ { "t": 1, "at": [0.5, 0.9] } ] } ] }
            """;

        var read = () => StorybookReader.Read(json);

        if (reads)
        {
            read.Should().NotThrow();
        }
        else
        {
            read.Should().Throw<StorybookException>().WithMessage("*objects[0].size*at most 3*");
        }
    }

    [Fact]
    public void The_Detector_Settings_A_Scene_Is_Meant_For_Are_Read()
    {
        const string json = """{ "version": 1, "name": "x", "detector": { "ignoreLightingChanges": false } }""";

        var scene = StorybookReader.Read(json);

        scene.Detector.Settings().IgnoreLightingChanges.Should().BeFalse();
        StorybookReader.Read("""{ "version": 1, "name": "x" }""").Detector.Settings().IgnoreLightingChanges.Should().BeTrue("the default's");
    }

    [Fact]
    public void A_Span_About_An_Object_That_Does_Not_Exist_Is_Named()
    {
        const string json = """
            { "version": 1, "name": "x", "expect": { "spans": [ { "object": "ghost", "state": "dontCare" } ] } }
            """;

        var read = () => StorybookReader.Read(json);

        read.Should().Throw<StorybookException>().WithMessage("*expect.spans[0].object*ghost*");
    }
}