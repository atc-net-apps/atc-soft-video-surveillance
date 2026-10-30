namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// The live streams' files, under the lease they are watched on:
/// <c>/{leaseId}/{file}</c> is the file in the folder of the lease's stream,
/// while the lease lives. Without a lease, or for any other path, there is no file.
/// </summary>
public sealed partial class LeaseStreamFileProvider(StreamingService streams) : IFileProvider
{
    public IFileInfo GetFileInfo(string subpath)
    {
        var parts = (subpath ?? string.Empty).Trim('/').Split('/');
        if (parts.Length != 2 ||
            !Guid.TryParse(parts[0], out var leaseId) ||
            !FileName().IsMatch(parts[1]) ||
            !streams.TryGetLeaseFolder(leaseId, out var folder))
        {
            return new NotFoundFileInfo(subpath ?? string.Empty);
        }

        return new Microsoft.Extensions.FileProviders.Physical.PhysicalFileInfo(new FileInfo(Path.Combine(folder, parts[1])));
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
        => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter)
        => NullChangeToken.Singleton;

    // A playlist or a segment: a plain file name, never a path.
    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9_-]+\\.(m3u8|ts|m4s|mp4)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex FileName();
}