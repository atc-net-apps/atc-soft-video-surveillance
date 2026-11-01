namespace AtcSoft.Video.Surveillance.Windows.Diagnostics;

/// <summary>
/// The report of the machine a long test run is measured on: every section is there, a probe that
/// fails is reported instead of failing the report, and the text reads section by section.
/// </summary>
public sealed class SystemReportTests
{
    [Fact]
    public void The_Report_Has_Every_Section_And_The_Apps_Version()
    {
        var sections = SystemReport.Collect([System.IO.Path.GetTempPath()]);

        sections.Select(s => s.Name).Should().Equal(
            "Application",
            "Operating system",
            "Processor and memory",
            "Graphics",
            "Disks",
            "Power",
            "FFmpeg");
        sections[0].Entries.Should().Contain(e => e.Key == ".NET" && e.Value.Contains(".NET", StringComparison.Ordinal));
        sections.Single(s => s.Name == "Disks").Entries.Should().ContainSingle();
    }

    [Fact]
    public void The_Text_Has_A_Heading_Per_Section_And_A_Line_Per_Entry()
    {
        var text = SystemReport.Format(
        [
            new SystemReportSection("Power", [new KeyValuePair<string, string>("Sleep after (plugged in)", "never")]),
        ]);

        text.Should().Contain("[Power]").And.Contain("  Sleep after (plugged in): never");
    }
}