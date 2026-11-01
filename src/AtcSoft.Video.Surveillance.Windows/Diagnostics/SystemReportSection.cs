namespace AtcSoft.Video.Surveillance.Windows.Diagnostics;

/// <summary>
/// One part of a <see cref="SystemReport"/>: a heading and its lines, in order.
/// </summary>
/// <param name="Name">The heading, for example <c>Processor and memory</c>.</param>
/// <param name="Entries">The lines: what, and its value.</param>
public sealed record SystemReportSection(
    string Name,
    IReadOnlyList<KeyValuePair<string, string>> Entries);