namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class ColorChoicesTests
{
    [Fact]
    public void The_Choices_Are_The_Basic_Colours_The_Desktop_Apps_Offer()
        => ColorChoices.Names.Should().Equal("Aqua", "Black", "Blue", "Fuchsia", "Gray", "Green", "Lime", "Maroon", "Navy", "Olive", "Purple", "Red", "Silver", "Teal", "White", "Yellow");

    [Theory]
    [InlineData("Red", "Red")]
    [InlineData("red", "Red")]
    [InlineData(" WHITE ", "White")]
    public void A_Colour_Name_Gets_Its_Canonical_Spelling(
        string value,
        string expected)
        => ColorChoices.Normalize(value, "White").Should().Be(expected);

    [Theory]
    [InlineData("#FF0000", "Red")]
    [InlineData("#ffffff", "White")]
    [InlineData("#00FF00", "Lime")]
    [InlineData("#FFFF0000", "Red")]
    public void A_Hex_Value_Of_A_Basic_Colour_Becomes_Its_Name(
        string value,
        string expected)
    {
        // The web client used to save hex values; the desktop apps select by name.
        ColorChoices.Normalize(value, "White").Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_Missing_Value_Becomes_The_Fallback(string? value)
        => ColorChoices.Normalize(value, "White").Should().Be("White");

    [Theory]
    [InlineData("#123456")]
    [InlineData("Chartreuse")]
    public void A_Colour_That_Is_Not_A_Basic_One_Is_Kept_As_It_Is(string value)
        => ColorChoices.Normalize(value, "White").Should().Be(value);

    [Theory]
    [InlineData("Red", "#FF0000")]
    [InlineData("lime", "#00FF00")]
    [InlineData("#123456", "#123456")]
    [InlineData("Chartreuse", "Chartreuse")]
    public void ToCss_Gives_A_Value_A_Browser_Understands(
        string value,
        string expected)
        => ColorChoices.ToCss(value).Should().Be(expected);

    [Fact]
    public void Every_Colour_Has_A_Name_In_The_Chosen_Language()
        => ColorChoices.Names.Select(ColorChoices.Label).Should().OnlyHaveUniqueItems().And.NotContainNulls();

    [Fact]
    public void A_Colour_Is_Named_In_Danish()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("da-DK");

            ColorChoices.Label("Red").Should().Be("Rød");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void A_Colour_Set_By_Hand_Is_Shown_As_It_Is()
        => ColorChoices.Label("#123456").Should().Be("#123456");
}