namespace Setup.Core.Acquisition;

public interface IAcquisitionService
{
    Task<string> AcquireAsync(string cacheDir, string url, string expectedSha256, long expectedSize, string? offlineAssetsDir, bool noNetwork, CancellationToken ct);
}