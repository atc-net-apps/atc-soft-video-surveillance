namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// A camera can record into a folder of its own. Everything that reads recordings (the
/// browser, the clean-up, the layout reorganiser) reads every root, each folder once.
/// </summary>
public sealed class RecordingRootsTests
{
    private static readonly string Global = Path.Combine(Path.GetTempPath(), "rec-global");
    private static readonly string Own = Path.Combine(Path.GetTempPath(), "rec-own");

    [Fact]
    public void The_Global_Folder_Comes_First_Then_Each_Cameras_Own_Folder()
        => RecordingRoots.Collect(Global, [Own, null, string.Empty, "  "])
            .Should().Equal(Path.GetFullPath(Global), Path.GetFullPath(Own));

    [Fact]
    public void A_Folder_Used_By_Several_Cameras_Is_Listed_Once()
        => RecordingRoots.Collect(Global, [Own, Own + Path.DirectorySeparatorChar, Own.ToUpperInvariant()])
            .Should().Equal(Path.GetFullPath(Global), Path.GetFullPath(Own));

    [Fact]
    public void A_Cameras_Folder_Inside_The_Global_One_Is_Not_Read_Twice()
        => RecordingRoots.Collect(Global, [Path.Combine(Global, "Front door")])
            .Should().Equal(Path.GetFullPath(Global));

    [Fact]
    public void A_Global_Folder_Inside_A_Cameras_Folder_Gives_Way_To_It()
        => RecordingRoots.Collect(Path.Combine(Own, "all"), [Own])
            .Should().Equal(Path.GetFullPath(Own));

    [Fact]
    public void A_Folder_That_Only_Starts_Like_Another_Is_Its_Own()
        => RecordingRoots.Collect(Global, [Global + "-extra"])
            .Should().Equal(Path.GetFullPath(Global), Path.GetFullPath(Global + "-extra"));

    [Fact]
    public void Without_A_Global_Folder_Only_The_Cameras_Folders_Are_Read()
        => RecordingRoots.Collect(null, [Own])
            .Should().Equal(Path.GetFullPath(Own));
}