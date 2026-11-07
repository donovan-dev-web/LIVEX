// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Simulation.Console.Integration;

/// <summary>
/// Mémoire des commandes que PRISM a **refusées** (SYNE-220, étape 3b).
/// </summary>
/// <remarks>
/// <para>
/// La boucle couplée n'émet une commande que si sa cible change, ou après
/// <c>PrismCommandRefreshTicks</c>. Sans mémoire des refus, une cible
/// inatteignable est donc redemandée indéfiniment : PRISM recalculait un A*
/// impossible à chaque fenêtre de rafraîchissement, et le compte
/// d'<c>action_failed</c> ne servait qu'à constater les dégâts.
/// </para>
/// <para>
/// Seuls les refus **durables** sont mémorisés : <c>path_not_found</c> (aucun
/// chemin depuis la position courante) et <c>source_not_found</c> (la source
/// n'est pas là où l'agent le croyait). <c>not_adjacent</c> est transitoire (l'agent
/// bouge) et <c>invalid_payload</c> est un défaut de notre write-back : les
/// retenter est justement ce qui les rend visibles.
/// </para>
/// <para>
/// La mémoire est **purement côté SYNE et non persistée** : un refus décrit une
/// position relative au monde observé, pas un savoir durable. Après reprise, un
/// agent redemande — au pire, PRISM refuse une fois de plus.
/// </para>
/// </remarks>
public sealed class PrismCommandFailureMemory
{
    /// <summary>Fenêtre par défaut avant de redemander une cible refusée.</summary>
    public const int DefaultRetryAfterTicks = 300;

    private readonly int _retryAfterTicks;
    private readonly Dictionary<(ulong AgentId, string Kind, string Payload), long> _failedAt = [];
    private readonly Dictionary<string, int> _countsByReason = new(StringComparer.Ordinal);
    private int _failureCount;

    public PrismCommandFailureMemory(int retryAfterTicks = DefaultRetryAfterTicks)
    {
        if (retryAfterTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfterTicks));
        }

        _retryAfterTicks = retryAfterTicks;
    }

    /// <summary>Refus mémorisés (toutes raisons durables confondues).</summary>
    public int FailureCount => _failureCount;

    /// <summary>Cibles distinctes actuellement écartées.</summary>
    public int SuppressedCount => _failedAt.Count;

    /// <summary>Refus par raison, dans l'ordre ordinal (diagnostic de fin de run).</summary>
    public IReadOnlyList<KeyValuePair<string, int>> CountsByReason
    {
        get
        {
            var ordered = new List<KeyValuePair<string, int>>(_countsByReason);
            ordered.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));
            return ordered;
        }
    }

    /// <summary>
    /// Vrai si la raison décrit un refus qui ne se corrigera pas en redemandant
    /// la même consigne au même endroit.
    /// </summary>
    public static bool IsDurableFailure(string reason) =>
        reason is "path_not_found" or "source_not_found";

    /// <summary>Enregistre un refus ; les raisons transitoires sont ignorées.</summary>
    /// <returns>Vrai si le refus a été mémorisé.</returns>
    public bool Record(ulong agentId, string kind, string payload, string reason, long tick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(payload);
        if (!IsDurableFailure(reason))
        {
            return false;
        }

        _failedAt[(agentId, kind, payload)] = tick;
        _failureCount++;
        string key = string.IsNullOrEmpty(reason) ? kind : reason;
        _countsByReason[key] = _countsByReason.GetValueOrDefault(key) + 1;
        return true;
    }

    /// <summary>
    /// Vrai si la consigne a été refusée récemment et ne doit pas être réémise.
    /// Les entrées expirées sont oubliées au passage : une cible inatteignable un
    /// jour peut le redevenir après un déplacement ou un chantier.
    /// </summary>
    public bool IsSuppressed(ulong agentId, string kind, string payload, long tick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(payload);
        if (!_failedAt.TryGetValue((agentId, kind, payload), out long failedAt))
        {
            return false;
        }

        if (tick - failedAt >= _retryAfterTicks)
        {
            _failedAt.Remove((agentId, kind, payload));
            return false;
        }

        return true;
    }

    /// <summary>Description lisible pour le journal de fin de run.</summary>
    public string Describe()
    {
        if (_failureCount == 0)
        {
            return "aucun refus durable";
        }

        var parts = new List<string>(_countsByReason.Count);
        foreach (KeyValuePair<string, int> entry in CountsByReason)
        {
            parts.Add(string.Concat(
                entry.Key,
                "=",
                entry.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return string.Concat(
            string.Join(",", parts),
            " ; cibles écartées ",
            SuppressedCount.ToString(CultureInfo.InvariantCulture));
    }
}
