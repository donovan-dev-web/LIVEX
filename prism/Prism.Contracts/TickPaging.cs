using System.Text.Json.Serialization;

namespace Prism.Contracts;

/// <summary>
/// Plafond dur d'une réponse de tick (PRISM-142, ADR-014). Au-delà, une réponse
/// n'est jamais tronquée : le résultat est découpé en pages à curseur, ou refusé
/// explicitement quand un élément seul dépasse déjà le plafond.
/// </summary>
public static class TickTransportLimits
{
    /// <summary>
    /// 8 Mio (8 388 608 octets) : taille maximale d'une réponse de tick, toutes
    /// enveloppes comprises. Le plafond ne dépend ni du nombre d'agents ni du
    /// nombre d'écritures d'un tick — c'est la taille d'<b>une</b> réponse.
    /// </summary>
    public const int MaximumTickResponseBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Marge réservée à l'enveloppe de page (<c>protocolVersion</c>, <c>tick</c>,
    /// <c>page</c>, <c>chunk</c> et séparateurs) avant de placer du contenu utile.
    /// </summary>
    public const int PageEnvelopeReserveBytes = 1024;
}

/// <summary>
/// Position d'une page dans le résultat d'un tick. <see cref="PageCount"/> et
/// <see cref="Index"/> sont connus de l'hôte dès la fin du tick : un lecteur peut
/// donc prouver qu'il a reçu la séquence complète, et un chaînage incomplet est
/// une erreur, jamais une lecture partielle silencieuse.
/// </summary>
/// <param name="Index">Index de la page, à partir de 0.</param>
/// <param name="PageCount">Nombre total de pages de ce tick, au moins 1.</param>
/// <param name="Complete">Vrai pour la dernière page du tick.</param>
/// <param name="Cursor">Curseur opaque de la page suivante ; absent quand <paramref name="Complete"/>.</param>
public sealed record TickPageInfo(
    int Index,
    int PageCount,
    bool Complete,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Cursor);

/// <summary>
/// Contenu d'une page : une tranche contiguë du flux du tick, observations puis
/// événements, dans l'ordre où le <see cref="TickResult"/> complet les publie.
/// </summary>
/// <remarks>
/// Les tableaux s'appellent <c>observations</c>/<c>events</c> <b>à l'intérieur</b> de
/// <c>chunk</c>, et non à la racine : une page ne peut donc pas être désérialisée
/// comme un <see cref="TickResult"/>. C'est voulu — un client qui ne connaît pas la
/// pagination échoue franchement au lieu d'accepter un tick partiel.
/// </remarks>
public sealed record TickPageChunk(
    IReadOnlyList<ObservationEnvelope> Observations,
    IReadOnlyList<EventEnvelope> Events);

/// <summary>
/// Réponse d'un tick découpée en pages (PRISM-142). Les pages d'un même tick
/// partagent <see cref="Tick"/> et la forme du flux : les concaténer dans l'ordre
/// des <see cref="TickPageInfo.Index"/> redonne exactement le <see cref="TickResult"/>
/// que le monde a produit.
/// </summary>
public sealed record TickPageEnvelope(
    string ProtocolVersion,
    long Tick,
    TickPageInfo Page,
    TickPageChunk Chunk) : IProtocolMessage;
