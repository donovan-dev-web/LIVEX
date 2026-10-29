namespace Setup.Core.Platform.Windows;

public sealed class WindowsHttpDownloader : IHttpDownloader
{
    public async Task DownloadAsync(string url, string destination, long? expectedSize, string? expectedSha256, IProgress<(long downloaded, long? total)>? progress, CancellationToken ct)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            progress?.Report((downloaded, total));
        }
        await dst.FlushAsync(ct);
    }
}