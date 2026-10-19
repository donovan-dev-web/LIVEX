// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Souvenirs : catégories, mémoire court/long terme et décroissance de la salience.

using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// Catégorie d'un souvenir —
/// Chaque catégorie a sa propre vitesse de décroissance.
/// </summary>
public enum MemoryCategory
{
    Observation = 0,
    Event = 1,
    Interaction = 2,
}

/// <summary>
/// Souvenir : source, catégorie, contenu, confiance, horodatage.
/// Le contenu est une chaîne canonique (papier déterministe, pas d'objet mutable).
/// </summary>
public sealed record MemoryEntry
{
    public MemoryEntry(ulong sequence, MemoryCategory category, string source, string content, double confidence, ulong storedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (confidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "La confiance doit être dans [0, 1].");
        }

        Sequence = sequence;
        Category = category;
        Source = source;
        Content = content;
        Confidence = confidence;
        StoredAt = storedAt;
    }

    /// <summary>Séquence mono-ente (0, 1, 2, …) — garantit un ordre déterministe.</summary>
    public ulong Sequence { get; }

    public MemoryCategory Category { get; }

    public string Source { get; }

    public string Content { get; }

    public double Confidence { get; }

    /// <summary>Tick de stockage.</summary>
    public ulong StoredAt { get; }
}

/// <summary>Élément rappelé avec sa salience courante.</summary>
public readonly record struct MemoryRecall(MemoryEntry Entry, double Salience);

/// <summary>
/// Mémoire (court/long terme) : décroissance exponentielle de la salience
/// <c>salience(t) = salience(0) × exp(−decayRate × (t − storedAt))</c>, seuil
/// d'oubli (défaut 0.01), capacité (défaut 1000) avec éviction de l'élément le
/// moins saillant, decay par catégorie 0.01/0.005/0.002 (SYNE-013).
/// </summary>
public sealed class Memory
{
    private readonly MemorySettings _settings;
    private readonly List<MemoryEntry> _entries = [];
    private ulong _sequence;

    public Memory(MemorySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.MaxCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "La capacité mémoire doit être &gt; 0.");
        }

        _settings = settings;
    }

    public int Count => _entries.Count;

    /// <summary>Séquence suivante — persistance (le compteur doit être restauré pour rester bit-à-bit).</summary>
    internal ulong NextSequence => _sequence;

    /// <summary>Copie de tous les souvenirs stockés (ordre d'insertion) — accès pour l'héritage (SYNE-020) et l'observabilité.</summary>
    public IReadOnlyList<MemoryEntry> AllEntries => new List<MemoryEntry>(_entries);

    public MemorySettings Settings => _settings;

    /// <summary>Vitesse de décroissance de la catégorie.</summary>
    public double DecayRateFor(MemoryCategory category) => category switch
    {
        MemoryCategory.Observation => _settings.ObservationDecayRate,
        MemoryCategory.Event => _settings.EventDecayRate,
        MemoryCategory.Interaction => _settings.InteractionDecayRate,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    /// <summary>Stocke un souvenir ; évince le moins saillant si la capacité est atteinte (SYNE-013).</summary>
    public void Store(MemoryCategory category, string source, string content, double confidence, ulong storedAt)
    {
        _entries.Add(new MemoryEntry(_sequence++, category, source, content, confidence, storedAt));
        if (_entries.Count > _settings.MaxCapacity)
        {
            EvictLeastSalient(storedAt);
        }
    }

    /// <summary>
    /// Rappel des souvenirs dont la salience dépasse le seuil d'oubli, ordre
    /// déterministe (StoredAt croissant, puis Sequence croissante).
    /// </summary>
    public IReadOnlyList<MemoryRecall> Recall(ulong currentTick)
    {
        var result = new List<MemoryRecall>();
        foreach (MemoryEntry entry in _entries)
        {
            double salience = SalienceOf(entry, currentTick);
            if (salience > _settings.RecallThreshold)
            {
                result.Add(new MemoryRecall(entry, salience));
            }
        }

        result.Sort(static (a, b) =>
        {
            int byTick = a.Entry.StoredAt.CompareTo(b.Entry.StoredAt);
            return byTick != 0 ? byTick : a.Entry.Sequence.CompareTo(b.Entry.Sequence);
        });

        return result;
    }

    public IReadOnlyList<MemoryRecall> Recall(MemoryCategory category, ulong currentTick)
    {
        var result = new List<MemoryRecall>();
        foreach (MemoryRecall recall in Recall(currentTick))
        {
            if (recall.Entry.Category == category)
            {
                result.Add(recall);
            }
        }

        return result;
    }

    /// <summary>
    /// Derniers souvenirs stockés (au plus <paramref name="maxCount"/>), sans copie
    /// de la collection : parcours chaud pour la déduplication d'observation.
    /// </summary>
    public IReadOnlyList<MemoryEntry> RecentEntries(int maxCount)
    {
        if (maxCount <= 0 || _entries.Count == 0)
        {
            return [];
        }

        int start = Math.Max(0, _entries.Count - maxCount);
        var recent = new List<MemoryEntry>(_entries.Count - start);
        for (int i = start; i < _entries.Count; i++)
        {
            recent.Add(_entries[i]);
        }

        return recent;
    }

    public double SalienceOf(MemoryEntry entry, ulong currentTick)
    {
        double age = currentTick >= entry.StoredAt ? (double)(currentTick - entry.StoredAt) : 0.0;
        double exponent = -DecayRateFor(entry.Category) * age;

        // Chemin court : au-delà de -20, l'exponentielle vaut < 2e-9 — sous tout
        // seuil de rappel utile et sous toute tolérance de comparaison. Renvoyer
        // zéro évite un <c>Math.Exp</c> par souvenir (ce calcul est parcouru par
        // l'éviction de capacité, donc pour chaque écriture mémoire d'un esprit plein).
        return exponent < -20.0 ? 0.0 : Math.Exp(exponent);
    }

    /// <summary>
    /// Éviction : retire le souvenir de plus faible salience ; à salience égale,
    /// le plus ancien (StoredAt puis Sequence croissantes, ordre déterministe).
    /// </summary>
    private void EvictLeastSalient(ulong currentTick)
    {
        MemoryEntry least = _entries[0];
        double leastSalience = double.MaxValue;
        for (int i = 0; i < _entries.Count; i++)
        {
            double salience = SalienceOf(_entries[i], currentTick);
            if (salience < leastSalience ||
                (Math.Abs(salience - leastSalience) < 1e-12 &&
                 (_entries[i].StoredAt < least.StoredAt ||
                  (_entries[i].StoredAt == least.StoredAt && _entries[i].Sequence < least.Sequence))))
            {
                leastSalience = salience;
                least = _entries[i];
            }
        }

        _entries.Remove(least);
    }

    /// <summary>Restauration d'état complet (persistance bit-à-bit) : séquence + entrées.</summary>
    internal void RestoreState(IEnumerable<MemoryEntry> entries, ulong nextSequence)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries.Clear();
        _entries.AddRange(entries);
        _sequence = nextSequence;
    }
}
