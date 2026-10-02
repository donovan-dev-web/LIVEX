using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using InfrastructureJournalLine = Launcher.Package.JournalLine;

namespace Launcher.App.Composition;

/// <summary>
/// Service de paquets concret : réalise le port IPackageService de l'application par
/// Launcher.Package (composition de App). Un seul écrivain par paquet.
/// </summary>
public sealed class FileSystemPackageService : IPackageService
{
    private readonly string _packagesRoot;
    private readonly IClock _clock;
    private const string LauncherVersion = "0.1.0";

    /// <summary>Initialise le service sur la racine des paquets vivants.</summary>
    public FileSystemPackageService(string packagesRoot, IClock clock)
    {
        _packagesRoot = packagesRoot;
        _clock = clock;
        Directory.CreateDirectory(packagesRoot);
    }

    /// <inheritdoc />
    public string Create(ExperimentDefinition experiment)
    {
        var path = Path.Combine(_packagesRoot, $"{experiment.Id}.livexp");
        using var writer = LivexPackageWriter.CreateNew(path, experiment, LauncherVersion, _clock.UtcNow);
        return path;
    }

    /// <inheritdoc />
    public void CompleteRun(string packagePath, RunCompletion completion)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        writer.CompleteRun(
            completion.RunId,
            completion.IndexEntry,
            completion.RunJson,
            completion.ConfigResolvedJson,
            completion.DataFiles,
            completion.LogFiles,
            completion.CompletionEvent is { } @event
                ? InfrastructureJournalLine.Pack(@event.At, @event.Event, @event.Message, @event.RunId)
                : InfrastructureJournalLine.Pack(_clock.UtcNow, "run_completed", $"run {completion.RunId} clôturé", completion.RunId),
            completion.AnalysisFiles);
    }

    /// <inheritdoc />
    public string Seal(string packagePath)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        return writer.Seal(_clock.UtcNow);
    }

    /// <inheritdoc />
    public void MarkRecoverable(string packagePath)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        writer.MarkRecoverable();
    }

    /// <inheritdoc />
    public void WriteAnalysisReport(string packagePath, string emergenceReport, IReadOnlyDictionary<string, byte[]> aggregateFiles)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        writer.WriteEntry(PackageConstants.EmergenceReportEntry, emergenceReport);
        foreach (var (entryName, content) in aggregateFiles)
        {
            writer.WriteEntry($"analysis/aggregate/{entryName}", content);
        }
    }

    /// <inheritdoc />
    public void RegisterComponents(string packagePath, IReadOnlyList<JsonComponentRef> components)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        writer.RegisterComponents(components);
    }

    /// <inheritdoc />
    public (string State, RunIndex Index) ReadState(string packagePath)
    {
        using var reader = new LivexPackageReader(packagePath);
        return (reader.Manifest.State, reader.RunIndex);
    }
}
