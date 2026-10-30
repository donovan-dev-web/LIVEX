using System.Formats.Tar;
using System.IO.Compression;

namespace Setup.Core.Platform.Windows;

public sealed class WindowsArchiveExtractor : IArchiveExtractor
{
    public async Task ExtractAsync(string archivePath, string destinationDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destFull = Path.GetFullPath(destinationDirectory);
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var target = Path.GetFullPath(Path.Combine(destFull, entry.FullName));
                if (!target.StartsWith(destFull, StringComparison.Ordinal)) throw new InvalidOperationException("Entrée d'archive refusée (zip-slip).");
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
        }
        else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            await using var fs = File.OpenRead(archivePath);
            await using var gz = new System.IO.Compression.GZipStream(fs, CompressionMode.Decompress);
            using var tar = new TarReader(gz);
            while (await tar.GetNextEntryAsync(true, ct) is { } entry)
            {
                var target = Path.GetFullPath(Path.Combine(destFull, entry.Name));
                if (!target.StartsWith(destFull, StringComparison.Ordinal)) throw new InvalidOperationException("Entrée d'archive refusée (zip-slip).");
                if (entry.EntryType == TarEntryType.Directory) { Directory.CreateDirectory(target); continue; }
                if (entry.EntryType == TarEntryType.RegularFile)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await using var outFs = File.Create(target);
                    if (entry.DataStream is not null) await entry.DataStream.CopyToAsync(outFs, ct);
                }
            }
        }
        else
        {
            throw new NotSupportedException("Format d'archive non supporté.");
        }
        await Task.CompletedTask;
    }
}