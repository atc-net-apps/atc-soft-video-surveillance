namespace AtcSoft.VideoSurveillance.Helpers;

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
}