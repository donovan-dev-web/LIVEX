using System.Formats.Tar;
using System.IO.Compression;

namespace Setup.Core.Platform.Windows;

public sealed class WindowsArchiveExtractor : IArchiveExtractor
{
    public async Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken ct,
        IProgress<int>? progress = null)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destFull = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var total = zip.Entries.Count;
            var done = 0;
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var target = SafeTarget(destFull, entry.FullName);
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(target);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, overwrite: true);
                }

                progress?.Report(++done * 100 / Math.Max(1, total));
            }
        }
        else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            await using var fs = File.OpenRead(archivePath);
            await using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var tar = new TarReader(gz);
            var done = 0;
            while (await tar.GetNextEntryAsync(true, ct) is { } entry)
            {
                var target = SafeTarget(destFull, entry.Name);
                if (entry.EntryType == TarEntryType.Directory)
                {
                    Directory.CreateDirectory(target);
                }
                else if (entry.EntryType == TarEntryType.RegularFile)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await using var outFs = File.Create(target);
                    if (entry.DataStream is not null) await entry.DataStream.CopyToAsync(outFs, ct);
                }

                progress?.Report(++done);
            }
        }
        else
        {
            throw new NotSupportedException("Format d'archive non supporté.");
        }

        await Task.CompletedTask;
    }

    private static string SafeTarget(string destination, string entryName)
    {
        var target = Path.GetFullPath(Path.Combine(destination, entryName));
        if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(target, destination, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Entrée d'archive refusée (zip-slip).");
        }

        return target;
    }
}
