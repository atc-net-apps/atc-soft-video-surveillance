namespace AtcSoft.Video.Surveillance.Helpers;

public class UniqueFilenameTests
{
    [Fact]
    public void NoCollision_ReturnsOriginalPath()
    {
        // Arrange
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var result = UniqueFilename.EnsureUnique(@"C:\rec\Cam_20260428_010203.mkv", existing.Contains);

        // Assert
        result.Should().Be(@"C:\rec\Cam_20260428_010203.mkv");
    }

    [Fact]
    public void Collision_AppendsUnderscoreTwo()
    {
        // Arrange
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\rec\Cam_20260428_010203.mkv",
        };

        // Act
        var result = UniqueFilename.EnsureUnique(@"C:\rec\Cam_20260428_010203.mkv", existing.Contains);

        // Assert
        result.Should().Be(@"C:\rec\Cam_20260428_010203_2.mkv");
    }

    [Fact]
    public void MultipleCollisions_KeepIncrementingSuffix()
    {
        // Arrange — first three slots taken
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\rec\Cam_20260428_010203.mkv",
            @"C:\rec\Cam_20260428_010203_2.mkv",
            @"C:\rec\Cam_20260428_010203_3.mkv",
        };

        // Act
        var result = UniqueFilename.EnsureUnique(@"C:\rec\Cam_20260428_010203.mkv", existing.Contains);

        // Assert
        result.Should().Be(@"C:\rec\Cam_20260428_010203_4.mkv");
    }

    [Fact]
    public void PreservesExtension()
    {
        // Arrange
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\snap\Cam.png",
        };

        // Act
        var result = UniqueFilename.EnsureUnique(@"C:\snap\Cam.png", existing.Contains);

        // Assert
        result.Should().EndWith(".png");
        result.Should().Be(@"C:\snap\Cam_2.png");
    }

    [Fact]
    public void NoExtension_SuffixGoesAtEnd()
    {
        // Arrange
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\rec\NoExt",
        };

        // Act
        var result = UniqueFilename.EnsureUnique(@"C:\rec\NoExt", existing.Contains);

        // Assert
        result.Should().Be(@"C:\rec\NoExt_2");
    }

    [Fact]
    public void MaxSuffixExceeded_FallsBackToMillisecondStamp()
    {
        // Arrange — fill suffixes _2 through _999 plus the base
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\rec\Cam.mkv",
        };
        for (var i = 2; i <= 999; i++)
        {
            existing.Add(string.Create(CultureInfo.InvariantCulture, $@"C:\rec\Cam_{i}.mkv"));
        }

        // Act
        var result = UniqueFilename.EnsureUnique(@"C:\rec\Cam.mkv", existing.Contains);

        // Assert — fallback path is dir/Cam_<9-digit-time>.mkv and is not
        // any of the pre-occupied paths
        result.Should().StartWith(@"C:\rec\Cam_");
        result.Should().EndWith(".mkv");
        existing.Should().NotContain(result);
    }

    [Fact]
    public void NullOrEmptyPath_Throws()
    {
        // Act
        var actNull = () => UniqueFilename.EnsureUnique(desiredPath: null!);
        var actEmpty = () => UniqueFilename.EnsureUnique(desiredPath: string.Empty);

        // Assert
        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_New_Segment_Never_Takes_The_Name_Of_The_File_Being_Written()
    {
        // The current file is not on disk yet: the recorder writes it on the first picture.
        var result = UniqueFilename.EnsureDifferentFrom(Rec("Cam.mkv"), Rec("Cam.mkv"), _ => false);

        result.Should().Be(Rec("Cam_2.mkv"));
    }

    [Fact]
    public void A_New_Segment_With_Another_Name_Keeps_It()
    {
        UniqueFilename.EnsureDifferentFrom(Rec("Cam_1.mkv"), Rec("Cam.mkv"), _ => false).Should().Be(Rec("Cam_1.mkv"));
        UniqueFilename.EnsureDifferentFrom(Rec("Cam.mkv"), currentPath: null, _ => false).Should().Be(Rec("Cam.mkv"));
    }

    [Fact]
    public void A_New_Segment_Skips_Both_The_Current_File_And_Files_On_Disk()
    {
        var onDisk = new HashSet<string>(StringComparer.Ordinal) { Rec("Cam_2.mkv") };

        var result = UniqueFilename.EnsureDifferentFrom(Rec("Cam.mkv"), Rec("Cam.mkv"), onDisk.Contains);

        result.Should().Be(Rec("Cam_3.mkv"));
    }

    // Built with the platform's separator, so the tests mean the same on Windows and Linux.
    private static string Rec(string fileName)
        => Path.Combine(Path.GetTempPath(), "rec", fileName);
}