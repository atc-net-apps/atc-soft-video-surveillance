namespace AtcSoft.VideoSurveillance.Helpers;

public sealed class SafeJsonFileTests : IDisposable
{
    private readonly string tempDir;

    public SafeJsonFileTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "SafeJsonFileTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }

    private sealed record Sample(int Number, string Text);

    [Fact]
    public void TryWrite_FirstTime_CreatesFile_AndCanBeReadBack()
    {
        // Arrange
        var path = Path.Combine(tempDir, "sample.json");
        var value = new Sample(42, "hello");

        // Act
        var ok = SafeJsonFile.TryWrite(path, value);

        // Assert
        ok.Should().BeTrue();
        File.Exists(path).Should().BeTrue();
        File.Exists(path + ".tmp").Should().BeFalse();
        File.Exists(path + ".bak").Should().BeFalse();

        var roundTrip = SafeJsonFile.TryRead<Sample>(path);
        roundTrip.Should().Be(value);
    }

    [Fact]
    public void TryWrite_OverExistingFile_PreservesPreviousAsBackup()
    {
        // Arrange
        var path = Path.Combine(tempDir, "sample.json");
        var first = new Sample(1, "first");
        var second = new Sample(2, "second");

        SafeJsonFile.TryWrite(path, first).Should().BeTrue();

        // Act
        var ok = SafeJsonFile.TryWrite(path, second);

        // Assert
        ok.Should().BeTrue();
        File.Exists(path + ".bak").Should().BeTrue();

        SafeJsonFile.TryRead<Sample>(path).Should().Be(second);

        // The backup contains the previous version
        var backupJson = File.ReadAllText(path + ".bak");
        var backup = JsonSerializer.Deserialize<Sample>(backupJson);
        backup.Should().Be(first);
    }

    [Fact]
    public void TryWrite_DoesNotLeaveTempFile_OnSuccess()
    {
        // Arrange
        var path = Path.Combine(tempDir, "sample.json");

        // Act
        SafeJsonFile.TryWrite(path, new Sample(1, "a")).Should().BeTrue();
        SafeJsonFile.TryWrite(path, new Sample(2, "b")).Should().BeTrue();
        SafeJsonFile.TryWrite(path, new Sample(3, "c")).Should().BeTrue();

        // Assert
        File.Exists(path + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void TryWrite_CreatesDirectory_WhenItDoesNotExist()
    {
        // Arrange
        var nestedDir = Path.Combine(tempDir, "deep", "nested", "path");
        var path = Path.Combine(nestedDir, "sample.json");

        // Act
        var ok = SafeJsonFile.TryWrite(path, new Sample(1, "a"));

        // Assert
        ok.Should().BeTrue();
        Directory.Exists(nestedDir).Should().BeTrue();
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void TryWrite_DoesNotCorruptExistingFile_WhenWriteFails()
    {
        // Arrange
        var path = Path.Combine(tempDir, "sample.json");
        var good = new Sample(1, "good");
        SafeJsonFile.TryWrite(path, good).Should().BeTrue();

        // Pre-create the temp file as a directory so File.WriteAllText fails
        var tempPath = path + ".tmp";
        Directory.CreateDirectory(tempPath);

        // Act
        var ok = SafeJsonFile.TryWrite(path, new Sample(2, "should not land"));

        // Assert
        ok.Should().BeFalse();
        SafeJsonFile.TryRead<Sample>(path).Should().Be(good);

        // Cleanup so disposing the temp dir succeeds
        Directory.Delete(tempPath);
    }

    [Fact]
    public void TryRead_MissingFile_ReturnsDefault()
    {
        // Arrange
        var path = Path.Combine(tempDir, "missing.json");

        // Act
        var value = SafeJsonFile.TryRead<Sample>(path);

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void TryRead_EmptyFile_ReturnsDefault()
    {
        // Arrange
        var path = Path.Combine(tempDir, "empty.json");
        File.WriteAllText(path, string.Empty);

        // Act
        var value = SafeJsonFile.TryRead<Sample>(path);

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void TryRead_CorruptPrimary_FallsBackToBackup()
    {
        // Arrange — produce a primary + backup pair via two writes,
        // then truncate the primary to simulate a power-loss-corrupted file
        var path = Path.Combine(tempDir, "sample.json");
        var first = new Sample(1, "first");
        var second = new Sample(2, "second");
        SafeJsonFile.TryWrite(path, first).Should().BeTrue();
        SafeJsonFile.TryWrite(path, second).Should().BeTrue();

        File.WriteAllText(path, "{ this is not valid json");

        // Act
        var value = SafeJsonFile.TryRead<Sample>(path);

        // Assert — falls back to .bak which holds the previous version
        value.Should().Be(first);
    }

    [Fact]
    public void TryRead_BothPrimaryAndBackupCorrupt_ReturnsDefault()
    {
        // Arrange
        var path = Path.Combine(tempDir, "sample.json");
        File.WriteAllText(path, "garbage");
        File.WriteAllText(path + ".bak", "also garbage");

        // Act
        var value = SafeJsonFile.TryRead<Sample>(path);

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void TryWrite_NullPath_Throws()
    {
        // Act
        var act = () => SafeJsonFile.TryWrite<Sample>(path: null!, new Sample(1, "a"));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryRead_NullPath_Throws()
    {
        // Act
        var act = () => SafeJsonFile.TryRead<Sample>(path: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}