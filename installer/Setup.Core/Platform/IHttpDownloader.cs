namespace Setup.Core.Platform;

public interface IHttpDownloader
{
    Task DownloadAsync(string url, string destination, long? expectedSize, string? expectedSha256, IProgress<(long downloaded, long? total)>? progress, CancellationToken ct);
}