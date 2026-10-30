namespace Setup.Core.Platform;

public interface IArchiveExtractor
{
    Task ExtractAsync(string archivePath, string destinationDirectory, CancellationToken ct);
}