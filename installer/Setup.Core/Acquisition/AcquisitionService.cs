using Setup.Core.Platform;

namespace Setup.Core.Acquisition;

public sealed class AcquisitionService : IAcquisitionService
{
    public async Task<string> AcquireAsync(string cacheDir, string url, string expectedSha256, long expectedSize, string? offlineAssetsDir, bool noNetwork, CancellationToken ct)
    {
        Directory.CreateDirectory(cacheDir);
        var cached = Path.Combine(cacheDir, expectedSha256);
        if (File.Exists(cached))
        {
            // vérification rapide de taille
            var fi = new FileInfo(cached);
            if (fi.Length == expectedSize) return cached;
        }

        // Actif local
        if (!string.IsNullOrEmpty(offlineAssetsDir))
        {
            var name = Path.GetFileName(new Uri(url).LocalPath);
            var local = Path.Combine(offlineAssetsDir, name);
            if (File.Exists(local))
            {
                File.Copy(local, cached, overwrite: true);
                return cached;
            }
        }

        if (noNetwork) throw new InvalidOperationException("Réseau interdit et actif absent du cache.");

        var downloader = PlatformServices.CreateHttpDownloader();
        var part = cached + ".part";
        await downloader.DownloadAsync(url, part, expectedSize, expectedSha256, null, ct);
        File.Move(part, cached, overwrite: true);
        return cached;
    }
}