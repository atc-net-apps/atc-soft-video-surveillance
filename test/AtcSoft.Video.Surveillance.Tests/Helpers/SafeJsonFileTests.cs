namespace AtcSoft.Video.Surveillance.Helpers;

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
    public void Read_NeitherFileNorBackup_IsMissing()
    {
        var result = SafeJsonFile.Read<Sample>(Path.Combine(tempDir, "missing.json"));

        result.Outcome.Should().Be(JsonFileReadOutcome.Missing);
        result.Value.Should().BeNull();
        result.MayOverwrite.Should().BeTrue();
    }

    [Fact]
    public void Read_GoodFile_IsRead()
    {
        var path = Path.Combine(tempDir, "sample.json");
        SafeJsonFile.TryWrite(path, new Sample(1, "a")).Should().BeTrue();

        var result = SafeJsonFile.Read<Sample>(path);

        result.Outcome.Should().Be(JsonFileReadOutcome.Read);
        result.Value.Should().Be(new Sample(1, "a"));
        result.PreservedCopies.Should().BeEmpty();
    }

    [Fact]
    public void Read_CorruptPrimary_ReadsBackup_AndCopiesThePrimaryAside()
    {
        var path = Path.Combine(tempDir, "sample.json");
        SafeJsonFile.TryWrite(path, new Sample(1, "first")).Should().BeTrue();
        SafeJsonFile.TryWrite(path, new Sample(2, "second")).Should().BeTrue();
        File.WriteAllText(path, "{ this is not valid json");

        var result = SafeJsonFile.Read<Sample>(path);

        result.Outcome.Should().Be(JsonFileReadOutcome.ReadFromBackup);
        result.Value.Should().Be(new Sample(1, "first"));
        result.Error.Should().NotBeNull();
        result.MayOverwrite.Should().BeTrue();
        result.PreservedCopies.Should().ContainSingle();
        File.ReadAllText(result.PreservedCopies[0]).Should().Be("{ this is not valid json");
    }

    [Fact]
    public void Read_ParserThatThrows_CountsAsUnreadable()
    {
        var path = Path.Combine(tempDir, "sample.json");
        File.WriteAllText(path, "{}");

        var result = SafeJsonFile.Read<Sample>(path, _ => throw new JsonException("unknown value"));

        result.Outcome.Should().Be(JsonFileReadOutcome.Unreadable);
        result.Error.Should().BeOfType<JsonException>();
        result.PreservedCopies.Should().ContainSingle();
    }

    [Fact]
    public void Read_EmptyPrimaryWithoutBackup_IsUnreadable_NotMissing()
    {
        var path = Path.Combine(tempDir, "sample.json");
        File.WriteAllText(path, string.Empty);

        var result = SafeJsonFile.Read<Sample>(path);

        result.Outcome.Should().Be(JsonFileReadOutcome.Unreadable);
        result.MayOverwrite.Should().BeTrue();
    }

    [Fact]
    public void Read_LockedFile_IsUnreadable_AndMayNotBeOverwritten()
    {
        var path = Path.Combine(tempDir, "sample.json");
        SafeJsonFile.TryWrite(path, new Sample(1, "a")).Should().BeTrue();

        JsonFileReadResult<Sample> result;
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = SafeJsonFile.Read<Sample>(path, lockedAttempts: 2);
        }

        result.Outcome.Should().Be(JsonFileReadOutcome.Unreadable);
        result.Error.Should().BeAssignableTo<IOException>();
        result.MayOverwrite.Should().BeFalse();
    }

    [Fact]
    public void Read_MissingPrimary_ReadsBackup()
    {
        var path = Path.Combine(tempDir, "sample.json");
        SafeJsonFile.TryWrite(path, new Sample(1, "first")).Should().BeTrue();
        SafeJsonFile.TryWrite(path, new Sample(2, "second")).Should().BeTrue();
        File.Delete(path);

        var result = SafeJsonFile.Read<Sample>(path);

        result.Outcome.Should().Be(JsonFileReadOutcome.ReadFromBackup);
        result.Error.Should().BeOfType<FileNotFoundException>();
        result.Value.Should().Be(new Sample(1, "first"));
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