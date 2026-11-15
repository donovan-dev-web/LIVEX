using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;

namespace Prism.Viewer;

public enum ViewerInterventionKind
{
    Place,
    Remove,
}

/// <summary>
/// Une intervention du visualiseur (RENDERING_SPEC.md §6) : poser ou retirer un bloc.
/// Désactivée par défaut ; quand elle est activée, elle est journalisée et émise comme
/// commande du contrat, jamais appliquée en silence.
/// </summary>
public sealed record ViewerIntervention(
    long Sequence,
    long IssuedTick,
    ViewerInterventionKind Kind,
    BlockCoordinate Coordinate,
    ushort ItemTypeId,
    ushort MaterialId)
{
    public string KindName => Kind == ViewerInterventionKind.Place ? "Poser" : "Retirer";
}

public static class ViewerInterventionJournal
{
    private const string Header = "{\"schema\":1,\"interventions\":true}";

    public static void Append(string path, ViewerIntervention intervention)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(intervention);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var isNew = !File.Exists(path) || new FileInfo(path).Length == 0;
        using var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
        if (isNew)
        {
            writer.WriteLine(Header);
        }

        writer.WriteLine(JsonSerializer.Serialize(intervention, ProtocolJson.Options));
    }

    public static IReadOnlyList<ViewerIntervention> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var interventions = new List<ViewerIntervention>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.Contains("\"schema\"", StringComparison.Ordinal))
            {
                continue;
            }

            var intervention = JsonSerializer.Deserialize<ViewerIntervention>(line, ProtocolJson.Options);
            if (intervention is not null)
            {
                interventions.Add(intervention);
            }
        }

        return interventions;
    }

    /// <summary>
    /// Commande journalisée correspondante. Le contrat est gelé : l'intervention emprunte les
    /// kinds existants (`Poser`, `Retirer`) au lieu d'en inventer un.
    /// </summary>
    public static CommandEnvelope CreateCommand(
        ViewerIntervention intervention,
        long tick,
        ulong agentId)
    {
        ArgumentNullException.ThrowIfNull(intervention);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        var position = new
        {
            x = intervention.Coordinate.X,
            y = intervention.Coordinate.Y,
            z = intervention.Coordinate.Z,
        };
        var payload = intervention.Kind == ViewerInterventionKind.Place
            ? JsonSerializer.SerializeToElement(
                new
                {
                    position,
                    item = intervention.ItemTypeId,
                    material = intervention.MaterialId,
                },
                ProtocolJson.Options)
            : JsonSerializer.SerializeToElement(new { position }, ProtocolJson.Options);
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            $"viewer-{intervention.Sequence:D6}",
            agentId,
            intervention.KindName,
            payload);
    }
}
