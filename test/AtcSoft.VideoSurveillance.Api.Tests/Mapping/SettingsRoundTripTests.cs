namespace AtcSoft.VideoSurveillance.Api.Mapping;

/// <summary>
/// Every setting a client can change has to come back the way it was sent.
/// A setting that is added to Core and forgotten in the API contract or the
/// mapping is reset the next time any client saves the settings; this test
/// fails for it by name.
/// </summary>
public sealed class SettingsRoundTripTests
{
    /// <summary>
    /// Settings that are left out of the API on purpose.
    /// </summary>
    private static readonly HashSet<string> NotInTheApi = new(StringComparer.Ordinal)
    {
        // A preference of each desktop client; the API client keeps its own.
        "General.StartRibbonCollapsed",
        "General.GridSize",

        // How the standalone app's recordings browser behaves; the server has no browser.
        "Recording.CloseRecordingsBrowserOnPlay",
        "Recording.RememberRecordingsBrowserState",
    };

    /// <summary>
    /// Text settings only accept certain values, so each gets one that is
    /// valid and differs from its default.
    /// </summary>
    private static readonly Dictionary<string, string> TextValues = new(StringComparer.Ordinal)
    {
        ["General.ThemeBase"] = "Light",
        ["General.ThemeAccent"] = "Green",
        ["General.Language"] = "1030",
        ["CameraDisplay.SnapshotPath"] = @"D:\media\snapshots",
        ["Performance.VideoQuality"] = "720p",
        ["Performance.RtspTransport"] = "udp",
        ["MotionDetection.BoundingBox.Color"] = "Lime",
        ["Recording.RecordingPath"] = @"D:\media\recordings",
        ["Recording.RecordingFormat"] = "mp4",
        ["Recording.TimelapseInterval"] = "10m",
        ["Recording.PlaybackOverlay.FilenameColor"] = "Yellow",
        ["Recording.PlaybackOverlay.TimestampColor"] = "Aqua",
        ["Advanced.LogPath"] = @"D:\media\logs",
    };

    [Fact]
    public void Every_Setting_Survives_The_Trip_Through_The_Api()
    {
        // Arrange — every setting changed from its default
        var sent = new SettingsSet();
        foreach (var (name, section) in sent.Sections)
        {
            ChangeEveryProperty(name, section);
        }

        // Act — to the API model, and back onto untouched settings
        var api = SettingsMappingExtensions.ToApiModel(
            sent.General,
            sent.CameraDisplay,
            sent.Connection,
            sent.Performance,
            sent.MotionDetection,
            sent.Recording,
            sent.Advanced);

        var received = new SettingsSet();
        api.AsUpdateRequest().ApplyToCore(
            received.General,
            received.CameraDisplay,
            received.Connection,
            received.Performance,
            received.MotionDetection,
            received.Recording,
            received.Advanced);

        // Assert
        var sentValues = sent.Flatten();
        var receivedValues = received.Flatten();
        var lost = sentValues
            .Where(kv => !NotInTheApi.Contains(kv.Key))
            .Where(kv => !Equals(kv.Value, receivedValues[kv.Key]))
            .Select(kv => $"{kv.Key}: sent '{kv.Value}', got '{receivedValues[kv.Key]}'")
            .ToList();

        var message = "A setting that does not survive is reset whenever a client saves the settings:" +
                      Environment.NewLine +
                      string.Join(Environment.NewLine, lost);
        Assert.True(lost.Count == 0, message);
    }

    [Fact]
    public void An_Update_That_Leaves_Every_Setting_Out_Changes_Nothing()
    {
        // A client that predates a setting leaves it out of its update.
        var stored = new SettingsSet();
        foreach (var (name, section) in stored.Sections)
        {
            ChangeEveryProperty(name, section);
        }

        var before = stored.Flatten();

        new UpdateSettingsRequest().ApplyToCore(
            stored.General,
            stored.CameraDisplay,
            stored.Connection,
            stored.Performance,
            stored.MotionDetection,
            stored.Recording,
            stored.Advanced);

        var after = stored.Flatten();
        var changed = before
            .Where(kv => !Equals(kv.Value, after[kv.Key]))
            .Select(kv => $"{kv.Key}: was '{kv.Value}', is '{after[kv.Key]}'")
            .ToList();

        var message = "A setting left out of an update must keep its value:" +
                      Environment.NewLine +
                      string.Join(Environment.NewLine, changed);
        Assert.True(changed.Count == 0, message);
    }

    [Fact]
    public void The_Update_Request_Has_Every_Setting_Of_The_Response()
    {
        static IEnumerable<string> Names(Type type)
            => type
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .Order(StringComparer.Ordinal);

        Names(typeof(UpdateSettingsRequest)).Should().Equal(Names(typeof(AppSettings)));
    }

    [Fact]
    public void Every_Setting_Is_Changed_By_The_Test()
    {
        // Guards the guard: a value that equals its default proves nothing.
        var defaults = new SettingsSet().Flatten();
        var changed = new SettingsSet();
        foreach (var (name, section) in changed.Sections)
        {
            ChangeEveryProperty(name, section);
        }

        var unchanged = changed.Flatten()
            .Where(kv => Equals(kv.Value, defaults[kv.Key]))
            .Select(kv => kv.Key)
            .ToList();

        unchanged.Should().BeEmpty();
    }

    private static void ChangeEveryProperty(
        string path,
        object target)
    {
        foreach (var property in SettingsSet.SettableProperties(target))
        {
            var name = $"{path}.{property.Name}";
            var value = property.GetValue(target);
            var type = property.PropertyType;

            if (type == typeof(bool))
            {
                property.SetValue(target, !(bool)value!);
            }
            else if (type == typeof(int))
            {
                // Stays inside the range every numeric setting accepts.
                property.SetValue(target, (int)value! == 4 ? 1 : (int)value! + 1);
            }
            else if (type == typeof(double))
            {
                property.SetValue(target, ((double)value! / 2) + 0.05);
            }
            else if (type == typeof(string))
            {
                TextValues.TryGetValue(name, out var text).Should().BeTrue($"the test needs a valid value for the text setting '{name}'");
                property.SetValue(target, text);
            }
            else if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                var index = Array.IndexOf(values, value);
                property.SetValue(target, values.GetValue((index + 1) % values.Length));
            }
            else if (type.IsClass)
            {
                ChangeEveryProperty(name, value!);
            }
            else
            {
                Assert.Fail($"The test does not know how to change '{name}' of type {type.Name}.");
            }
        }
    }

    private sealed class SettingsSet
    {
        public GeneralSettings General { get; } = new();

        public CameraDisplayAppSettings CameraDisplay { get; } = new();

        public ConnectionAppSettings Connection { get; } = new();

        public PerformanceSettings Performance { get; } = new();

        public MotionDetectionSettings MotionDetection { get; } = new();

        public RecordingSettings Recording { get; } = new();

        public AdvancedSettings Advanced { get; } = new();

        public IEnumerable<(string Name, object Section)> Sections
            =>
            [
                (nameof(General), General),
                (nameof(CameraDisplay), CameraDisplay),
                (nameof(Connection), Connection),
                (nameof(Performance), Performance),
                (nameof(MotionDetection), MotionDetection),
                (nameof(Recording), Recording),
                (nameof(Advanced), Advanced),
            ];

        public static IEnumerable<System.Reflection.PropertyInfo> SettableProperties(
            object target)
            => target
                .GetType()
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

        public Dictionary<string, object?> Flatten()
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (name, section) in Sections)
            {
                Flatten(name, section, values);
            }

            return values;
        }

        private static void Flatten(
            string path,
            object target,
            Dictionary<string, object?> values)
        {
            foreach (var property in SettableProperties(target))
            {
                var name = $"{path}.{property.Name}";
                var value = property.GetValue(target);
                if (value is not null && property.PropertyType.IsClass && property.PropertyType != typeof(string))
                {
                    Flatten(name, value, values);
                }
                else
                {
                    values[name] = value;
                }
            }
        }
    }
}