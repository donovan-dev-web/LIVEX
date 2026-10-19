// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Identité d'un run (réalignement Phase 4) : hash canonique de la configuration effective et hash canonique
//        de l'état complet, pour comparer et archiver deux runs sans dépendre d'un chemin de sérialisation.

using System.Text;
using System.Text.Json.Nodes;
using Simulation.Core.Configuration;
using Simulation.Core.Loop;
using Simulation.Core.Prng;

namespace Simulation.Core.Persistence;

/// <summary>
/// Identité d'un run : ce qui doit être <b>égal</b> pour que deux exécutions soient
/// comparables, et <b>enregistré</b> pour qu'une archive soit vérifiable (D-17 /
/// S-26).
///
/// <list type="bullet">
/// <item><b><see cref="ConfigHash"/></b> : hash FNV-1a 64 de la configuration
/// <b>canonique</b> — objet JSON aux clés triées récursivement, ordre des tableaux
/// préservé (les tableaux portent du sens : layout d'obstacles, cycle des saisons).
/// Deux configurations équivalentes mais écrites dans un ordre différent donnent le
/// même hash ; deux configurations différentes ne peuvent pas le donner.</item>
/// <item><b><see cref="StateHash"/></b> : hash canonique de l'<b>état complet</b>
/// (monde, gisements, esprits — besoins, mémoire, croyances, confiance,
/// intention et trace de décision —, groupes, réserves, PRNG) au tick courant,
/// via le codec de snapshot. C'est la comparaison <i>exhaustive</i> que le test
/// doré (journal arrondi) ne fournissait pas (D-16).</item>
/// </list>
///
/// <b>Version de code</b> : le hash de commit n'est pas disponible dans un binaire
/// publié — il est explicitement <b>absent</b> (jamais inventé) et l'identité
/// publique reste <c>engineVersion</c> + <c>schemaVersion</c>, portée par le
/// snapshot.
/// </summary>
public static class RunIdentity
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>
    /// Configuration canonique du run : JSON aux clés triées récursivement.
    /// Les valeurs restent celles de la configuration fournie (avant mise à
    /// l'échelle temporelle, qui est une fonction pure de <c>simulatedSecondsPerTick</c>).
    /// </summary>
    public static string CanonicalConfigJson(SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        JsonNode? node = JsonNode.Parse(ConfigLoader.ToJson(options));
        return Canonicalize(node).ToJsonString();
    }

    /// <summary>Hash canonique de la configuration (<see cref="CanonicalConfigJson"/>).</summary>
    public static ulong ConfigHash(SimulationOptions options) =>
        HashUtf8(CanonicalConfigJson(options));

    /// <summary>Hash canonique de l'état complet du run au tick courant.</summary>
    public static ulong StateHash(SimulationLoop loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        return SimulationSnapshotCodec.Hash(SimulationSnapshotCodec.Capture(loop));
    }

    /// <summary>
    /// Empreinte d'identité publiée avec un run : engineVersion, schemaVersion,
    /// hash de configuration et hash d'état. Aucune valeur inventée : le hash de
    /// code absent est signalé comme tel (<c>null</c>).
    /// </summary>
    public static RunIdentityStamp Stamp(SimulationLoop loop) =>
        new(
            Observability.ObservabilityContract.EngineVersion,
            SimulationSnapshotCodec.SchemaVersion,
            loop.Options.Random.Seed,
            ConfigHash(loop.Options),
            StateHash(loop),
            SourceCommit: null);

    /// <summary>Tri récursif des clés d'objet ; l'ordre des tableaux est sémantique et préservé.</summary>
    private static JsonNode Canonicalize(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return JsonValue.Create((string?)null)!;

            case JsonObject jsonObject:
                var sorted = new JsonObject();
                foreach (KeyValuePair<string, JsonNode?> property in jsonObject.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    sorted[property.Key] = Canonicalize(property.Value?.DeepClone());
                }

                return sorted;

            case JsonArray jsonArray:
                var array = new JsonArray();
                foreach (JsonNode? item in jsonArray)
                {
                    array.Add(Canonicalize(item?.DeepClone()));
                }

                return array;

            default:
                return node.DeepClone();
        }
    }

    private static ulong HashUtf8(string value)
    {
        ulong hash = FnvOffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return hash;
    }
}

/// <summary>
/// Empreinte d'identité d'un run : versions, graine, hash de configuration et
/// hash d'état complet. <see cref="SourceCommit"/> reste <c>null</c> quand le
/// binaire ne porte pas cette information — une absence déclarée, jamais une
/// valeur de remplissage.
/// </summary>
public sealed record RunIdentityStamp(
    string EngineVersion,
    int SchemaVersion,
    ulong Seed,
    ulong ConfigHash,
    ulong StateHash,
    string? SourceCommit);
