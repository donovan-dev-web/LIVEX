// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Point de reprise couplé (SYNE-225) : snapshot SYNE aligné sur un checkpoint PRISM.

using System.Globalization;
using System.Text.Json;
using Simulation.Core.Loop;
using Simulation.Core.Persistence;

namespace Simulation.Console.Integration;

/// <summary>
/// Enveloppe d'un snapshot SYNE couplé (SYNE-225) : le tick <b>couplé</b> (celui de
/// PRISM), l'identifiant du checkpoint PRISM correspondant, l'empreinte de l'état
/// SYNE et le snapshot lui-même dans son format canonique.
/// </summary>
/// <remarks>
/// Le tick est stocké ici et non lu dans le snapshot : en mode couplé, SYNE ne
/// fait pas avancer sa propre horloge (c'est PRISM qui pilote), donc
/// <c>snapshot.Tick</c> vaut 0 alors que le run est au tick N. C'est l'enveloppe
/// qui porte l'alignement des deux côtés.
/// </remarks>
public sealed record CoupledCheckpointFile(
    int Schema,
    string CheckpointId,
    long Tick,
    string Fingerprint,
    ulong SnapshotHash,
    string SnapshotJson);

/// <summary>Résultat d'un chargement : le checkpoint et son chemin, ou l'erreur motivée.</summary>
public sealed record CoupledCheckpointLoad(CoupledCheckpointFile? Checkpoint, string Path, string? Error)
{
    public bool Succeeded => Checkpoint is not null;
}

/// <summary>
/// Magasin des snapshots SYNE couplés (SYNE-225). Écriture <b>atomique</b> (fichier
/// temporaire puis remplacement) : un arrêt brutal ne laisse jamais un snapshot
/// tronqué à la place d'un point de reprise valide.
/// </summary>
/// <remarks>
/// Le chargement refuse un snapshot dont l'empreinte de contenu ne correspond pas
/// (fichier corrompu ou édité) et un snapshot dont le tick ne correspond pas au
/// checkpoint PRISM restauré : mieux vaut un refus explicite qu'une cognition
/// silencieusement désalignée du monde.
/// </remarks>
public static class CoupledCheckpointStore
{
    /// <summary>Version du format d'enveloppe.</summary>
    public const int Schema = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>Répertoire par défaut (relatif au répertoire courant).</summary>
    public const string DefaultDirectory = ".livex/coupled-checkpoints";

    /// <summary>
    /// Premier tick à demander après une reprise : le checkpoint contient le
    /// dernier tick <b>achevé</b>, que PRISM refuse de rejouer
    /// (<c>unexpected_tick</c> : « Expected tick 2500, received 2499 »).
    /// </summary>
    public static long NextTickAfterRestore(long lastCompletedTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lastCompletedTick);
        return checked(lastCompletedTick + 1);
    }

    public static string PathFor(string directory, string checkpointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        return Path.Combine(directory, string.Concat(checkpointId, ".syne.json"));
    }

    /// <summary>
    /// Écrit le snapshot <b>cognitif</b> de la boucle et rend le chemin du fichier.
    /// </summary>
    /// <remarks>
    /// Étape 3a de SYNE-220 : seul l'état que PRISM ne peut pas reconstruire est
    /// persisté ici — esprits, groupes, PRNG, tick et identité de configuration.
    /// Le monde appartient à l'hôte et vit dans <c>SaveCheckpointAsync</c> ; en
    /// embarquer une copie laissait croire qu'une reprise locale était possible, et
    /// c'est cette copie que la restauration aurait pu écraser l'état autoritaire.
    /// Le fichier porte une <see cref="WorldReferenceDto"/> à la place.
    /// </remarks>
    public static string Save(string directory, string checkpointId, long tick, SimulationLoop loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        SimulationSnapshot snapshot = SimulationSnapshotCodec.CaptureCognition(loop);
        var file = new CoupledCheckpointFile(
            Schema,
            checkpointId,
            tick,
            CoupledFingerprint.Compute(loop, (ulong)tick),
            SimulationSnapshotCodec.Hash(snapshot),
            SimulationSnapshotCodec.ToJson(snapshot));

        string path = PathFor(directory, checkpointId);
        Directory.CreateDirectory(directory);
        string temporary = string.Concat(path, ".tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(file, SerializerOptions));
        File.Move(temporary, path, overwrite: true);
        return path;
    }

    /// <summary>
    /// Charge un snapshot et vérifie qu'il correspond au checkpoint PRISM restauré.
    /// </summary>
    public static CoupledCheckpointLoad Load(string directory, string checkpointId, long expectedTick)
    {
        string path = PathFor(directory, checkpointId);
        if (!File.Exists(path))
        {
            return new CoupledCheckpointLoad(
                null,
                path,
                $"aucun snapshot SYNE couplé pour « {checkpointId} » ({path})");
        }

        CoupledCheckpointFile? file;
        try
        {
            file = JsonSerializer.Deserialize<CoupledCheckpointFile>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException exception)
        {
            return new CoupledCheckpointLoad(null, path, $"snapshot illisible : {exception.Message}");
        }

        if (file is null)
        {
            return new CoupledCheckpointLoad(null, path, "snapshot vide");
        }

        if (file.Schema != Schema)
        {
            return new CoupledCheckpointLoad(
                null,
                path,
                $"format de snapshot {file.Schema} inconnu (attendu : {Schema})");
        }

        if (file.Tick != expectedTick)
        {
            return new CoupledCheckpointLoad(
                null,
                path,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"snapshot au tick {file.Tick} alors que PRISM reprend au tick {expectedTick} — reprise refusée"));
        }

        SimulationSnapshot snapshot;
        try
        {
            snapshot = SimulationSnapshotCodec.FromJson(file.SnapshotJson);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException or InvalidOperationException)
        {
            return new CoupledCheckpointLoad(null, path, $"snapshot SYNE invalide : {exception.Message}");
        }

        ulong hash = SimulationSnapshotCodec.Hash(snapshot);
        if (hash != file.SnapshotHash)
        {
            return new CoupledCheckpointLoad(
                null,
                path,
                string.Create(CultureInfo.InvariantCulture, $"empreinte de snapshot divergente ({hash:x16} ≠ {file.SnapshotHash:x16})"));
        }

        return new CoupledCheckpointLoad(file, path, null);
    }
}
