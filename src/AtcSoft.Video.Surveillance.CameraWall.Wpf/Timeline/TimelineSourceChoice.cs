namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Timeline;

/// <summary>
/// Where CameraWall's timeline reads from: this computer, or another CameraWall's folder.
/// </summary>
/// <param name="Folder">The folder, or <see langword="null"/> for this computer.</param>
public sealed record TimelineSourceChoice(string? Folder)
{
    /// <summary>
    /// Gets this computer.
    /// </summary>
    public static TimelineSourceChoice ThisComputer { get; } = new(Folder: null);

    /// <summary>
    /// Gets a value indicating whether this is another CameraWall's folder.
    /// </summary>
    public bool IsFolder
        => Folder is not null;

    /// <inheritdoc />
    public override string ToString()
        => Folder ?? Translations.ThisComputer;
}