// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Mémoire spatiale par chunk (SYNE-221) : masque de tags cru, confiance et oubli.

using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// Souvenir d'un chunk (région) : les <b>types de ressources exposées</b> qu'on y
/// a vus, sous forme de masque de tags, avec une confiance et le tick de la
/// dernière observation directe.
/// </summary>
/// <remarks>
/// Le masque est <b>cumulatif</b> (OU des observations) : la mémoire dit « ce
/// chunk contient au moins ces tags », elle ne prétend pas connaître son contenu
/// exact. Une ressource épuisée n'est donc pas désapprise par le masque — c'est
/// la confiance qui décroît, et l'agent doit retourner voir.
/// </remarks>
public sealed class SpatialChunkMemory
{
    public SpatialChunkMemory(int chunkX, int chunkY, uint tagMask, double confidence, ulong lastSeenTick)
    {
        if (confidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "La confiance doit être dans [0, 1].");
        }

        ChunkX = chunkX;
        ChunkY = chunkY;
        TagMask = tagMask;
        Confidence = confidence;
        LastSeenTick = lastSeenTick;
    }

    public int ChunkX { get; }

    public int ChunkY { get; }

    /// <summary>Masque de tags exposés crus pour ce chunk (OU cumulé des observations).</summary>
    public uint TagMask { get; private set; }

    public double Confidence { get; private set; }

    /// <summary>Tick de la dernière observation <b>directe</b> de ce chunk.</summary>
    public ulong LastSeenTick { get; private set; }

    internal void Merge(uint tagMask, double confidence, ulong tick)
    {
        TagMask |= tagMask;
        Confidence = Math.Clamp(confidence, 0.0, 1.0);
        LastSeenTick = tick;
    }

    internal void SetConfidence(double value) => Confidence = Math.Clamp(value, 0.0, 1.0);
}

/// <summary>
/// Mémoire spatiale d'une entité : un souvenir par chunk, indexé par coordonnées
/// de chunk (celles que PRISM publie dans ses résumés de région).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Observation</b> : le masque est fusionné (OU) et la confiance révisée
/// par la formule des croyances (<c>belief + (signal − belief) × UpdateStrength</c>,
/// plafonnée par <c>MaxChangePerSnap</c>) — un chunk vu une fois est faiblement
/// cru, un chunk revu converge vers 1.</item>
/// <item><b>Temps</b> : la confiance décroît de <c>SpatialMemorySettings.DecayPerTick</c>
/// à chaque tick, et le chunk est oublié sous <c>ForgetThreshold</c>.</item>
/// <item><b>Capacité</b> : au-delà de <c>MaxChunksPerAgent</c>, le chunk le moins
/// cru (puis le plus ancien, puis l'ordre des coordonnées) est évincé — ordre
/// total, donc déterministe.</item>
/// </list>
/// </remarks>
public sealed class SpatialMemory
{
    private readonly Dictionary<(int X, int Y), SpatialChunkMemory> _chunks = [];

    /// <summary>Nombre de chunks crus.</summary>
    public int Count => _chunks.Count;

    /// <summary>Chunks crus, ordonnés par (X, Y) — ordre canonique pour l'émission et la persistance.</summary>
    public IReadOnlyList<SpatialChunkMemory> Chunks =>
        _chunks.Values
            .OrderBy(chunk => chunk.ChunkX)
            .ThenBy(chunk => chunk.ChunkY)
            .ToList();

    public bool TryGet(int chunkX, int chunkY, out SpatialChunkMemory chunk) =>
        _chunks.TryGetValue((chunkX, chunkY), out chunk!);

    /// <summary>Masque de tags cru pour ce chunk, <c>0</c> s'il n'est pas mémorisé.</summary>
    public uint TagMaskAt(int chunkX, int chunkY) =>
        _chunks.TryGetValue((chunkX, chunkY), out SpatialChunkMemory? chunk) ? chunk.TagMask : 0u;

    /// <summary>
    /// Enregistre une observation directe d'un chunk : fusion du masque, révision
    /// de confiance depuis la croyance antérieure (0 si le chunk est inconnu),
    /// horodatage. Le signal vaut 1 : un résumé de région est une observation
    /// directe, pas un témoignage.
    /// </summary>
    public void Observe(int chunkX, int chunkY, uint tagMask, BeliefSettings settings, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (_chunks.TryGetValue((chunkX, chunkY), out SpatialChunkMemory? known))
        {
            known.Merge(
                tagMask,
                BeliefRevision.Revise(known.Confidence, 1.0, settings.UpdateStrength, settings.MaxChangePerSnap),
                tick);
            return;
        }

        _chunks[(chunkX, chunkY)] = new SpatialChunkMemory(
            chunkX,
            chunkY,
            tagMask,
            BeliefRevision.Revise(0.0, 1.0, settings.UpdateStrength, settings.MaxChangePerSnap),
            tick);
        EvictBeyondCapacity(settings);
    }

    /// <summary>
    /// Décroissance temporelle : la confiance des chunks <b>non observés ce
    /// tick-ci</b> est multipliée par le taux de décroissance, et un chunk passe
    /// sous le seuil d'oubli est retiré.
    /// </summary>
    public void Tick(ulong currentTick, BeliefSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SpatialMemorySettings spatial = settings.SpatialMemory;

        List<(int X, int Y)>? forgotten = null;
        foreach (SpatialChunkMemory chunk in _chunks.Values)
        {
            if (chunk.LastSeenTick == currentTick)
            {
                continue;
            }

            chunk.SetConfidence(chunk.Confidence * spatial.DecayPerTick);
            if (chunk.Confidence < spatial.ForgetThreshold)
            {
                (forgotten ??= []).Add((chunk.ChunkX, chunk.ChunkY));
            }
        }

        if (forgotten is null)
        {
            return;
        }

        foreach ((int x, int y) in forgotten)
        {
            _chunks.Remove((x, y));
        }
    }

    /// <summary>Restauration complète (persistance bit-à-bit).</summary>
    internal void RestoreState(IEnumerable<SpatialChunkMemory> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        _chunks.Clear();
        foreach (SpatialChunkMemory chunk in chunks)
        {
            _chunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
        }
    }

    private void EvictBeyondCapacity(BeliefSettings settings)
    {
        int capacity = settings.SpatialMemory.MaxChunksPerAgent;
        if (capacity <= 0)
        {
            _chunks.Clear();
            return;
        }

        while (_chunks.Count > capacity)
        {
            (int X, int Y) victim = default;
            bool hasVictim = false;
            foreach (SpatialChunkMemory chunk in _chunks.Values)
            {
                if (!hasVictim || IsWeaker(chunk, _chunks[victim]))
                {
                    victim = (chunk.ChunkX, chunk.ChunkY);
                    hasVictim = true;
                }
            }

            _chunks.Remove(victim);
        }
    }

    /// <summary>Ordre d'éviction : confiance la plus faible, puis souvenir le plus ancien, puis coordonnées.</summary>
    private static bool IsWeaker(SpatialChunkMemory candidate, SpatialChunkMemory reference)
    {
        if (candidate.Confidence != reference.Confidence)
        {
            return candidate.Confidence < reference.Confidence;
        }

        if (candidate.LastSeenTick != reference.LastSeenTick)
        {
            return candidate.LastSeenTick < reference.LastSeenTick;
        }

        if (candidate.ChunkX != reference.ChunkX)
        {
            return candidate.ChunkX < reference.ChunkX;
        }

        return candidate.ChunkY < reference.ChunkY;
    }
}
