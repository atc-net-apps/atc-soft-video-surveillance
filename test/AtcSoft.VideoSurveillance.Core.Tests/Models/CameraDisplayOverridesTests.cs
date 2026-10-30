namespace AtcSoft.VideoSurveillance.Models;

public class CameraDisplayOverridesTests
{
    // Every member of CameraDisplayOverrides is kept in sync by hand across
    // HasAnyOverride / Clone / CopyFrom / ValueEquals. These tests set each
    // property in turn, so a new member that is missed in one method fails.
    public static TheoryData<string> PropertyNames()
    {
        var data = new TheoryData<string>();
        foreach (var property in typeof(CameraDisplayOverrides).GetProperties())
        {
            data.Add(property.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void HasAnyOverride_Detects_Each_Property(string propertyName)
    {
        var overrides = CreateWith(propertyName);

        overrides.HasAnyOverride().Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void Clone_Copies_Each_Property(string propertyName)
    {
        var overrides = CreateWith(propertyName);

        overrides.Clone().ValueEquals(overrides).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void CopyFrom_Copies_Each_Property(string propertyName)
    {
        var source = CreateWith(propertyName);
        var target = new CameraDisplayOverrides();

        target.CopyFrom(source);

        target.ValueEquals(source).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void ValueEquals_Detects_Difference_In_Each_Property(
        string propertyName)
    {
        var overrides = CreateWith(propertyName);

        overrides.ValueEquals(new CameraDisplayOverrides()).Should().BeFalse();
    }

    [Fact]
    public void ConnectionTypeIcon_Members_Default_To_Inherit()
    {
        var overrides = new CameraDisplayOverrides();

        overrides.ShowOverlayConnectionTypeIcon.Should().BeNull();
        overrides.ConnectionTypeIconPlacement.Should().BeNull();
    }

    private static CameraDisplayOverrides CreateWith(string propertyName)
    {
        var overrides = new CameraDisplayOverrides();
        var property = typeof(CameraDisplayOverrides).GetProperty(propertyName)!;
        var type = Nullable.GetUnderlyingType(property.PropertyType)!;

        object value = type switch
        {
            _ when type == typeof(bool) => true,
            _ when type == typeof(double) => 0.42,
            _ when type.IsEnum => Enum.GetValues(type).GetValue(1)!,
            _ => throw new NotSupportedException($"Add a test value for {type.Name}."),
        };

        property.SetValue(overrides, value);
        return overrides;
    }
}