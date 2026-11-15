using System.Globalization;
using System.Text;
using System.Text.Json;
using Prism.Contracts;
using Prism.Host.Hosting;

namespace Prism.Host.Ticking;

/// <summary>
/// Découpe la réponse d'un tick en réponses bornées par
/// <see cref="TickTransportLimits.MaximumTickResponseBytes"/> (PRISM-142, ADR-014).
/// </summary>
/// <remarks>
/// <para>
/// Le découpage est une <b>tranche contiguë</b> du flux du tick — observations
/// puis événements, dans l'ordre publié — jamais un tri ni un choix : deux
/// exécutions du même tick produisent les mêmes pages, octet pour octet, et la
/// concaténation des pages d'un tick redonne le <see cref="TickResult"/> d'origine.
/// </para>
/// <para>
/// Tant que le tick tient sous le plafond, la réponse reste un
/// <see cref="TickResult"/> inchangé : la pagination ne se manifeste que lorsqu'elle
/// est nécessaire. Un élément seul plus gros que le plafond ne peut pas être paginé :
/// la construction lève alors <see cref="TickPayloadTooLargeException"/>, que la
/// couche HTTP traduit en 413 motivé — jamais en corps tronqué.
/// </para>
/// </remarks>
public sealed class TickPagePlan
{
    private readonly IReadOnlyList<ObservationEnvelope> _observations;
    private readonly IReadOnlyList<EventEnvelope> _events;
    private readonly string _protocolVersion;
    private readonly long _tick;
    private readonly int _maximumPageBytes;
    private readonly byte[]? _single;
    private readonly int[] _itemBytes;
    private readonly int[] _pageStarts;

    private TickPagePlan(TickResult result, int maximumPageBytes)
    {
        _observations = result.Observations;
        _events = result.Events;
        _protocolVersion = result.ProtocolVersion;
        _tick = result.Tick;
        _maximumPageBytes = maximumPageBytes;

        // Mesure exacte du résultat complet : c'est elle qui décide s'il y a lieu de
        // paginer. Elle sert aussi de corps de réponse quand le tick tient en une page.
        // L'hôte l'a déjà produite en octets : la re-sérialiser ici coûtait une seconde
        // passe complète (41 Mo) et une chaîne UTF-16 intermédiaire.
        var whole = result.SerializedUtf8 ?? ProtocolJson.SerializeToUtf8Bytes(result);
        if (whole.Length <= maximumPageBytes)
        {
            _single = whole;
            _itemBytes = [];
            _pageStarts = [0];
            FirstPage = whole;
            return;
        }

        _itemBytes = new int[result.Observations.Count + result.Events.Count];
        var item = 0;
        foreach (var observation in result.Observations)
        {
            _itemBytes[item++] = JsonSerializer.SerializeToUtf8Bytes(observation, ProtocolJson.Options).Length;
        }

        foreach (var eventEnvelope in result.Events)
        {
            _itemBytes[item++] = JsonSerializer.SerializeToUtf8Bytes(eventEnvelope, ProtocolJson.Options).Length;
        }

        _pageStarts = PackPages(_itemBytes, maximumPageBytes);
        FirstPage = RenderPage(0);
    }

    /// <summary>Nombre de pages du tick ; 1 quand le résultat tient sous le plafond.</summary>
    public int PageCount => _pageStarts.Length;

    /// <summary>Vrai dès que le tick a dû être découpé.</summary>
    public bool IsPaged => _single is null;

    public long Tick => _tick;

    /// <summary>Corps de la réponse à la requête de tick (page 1, ou résultat complet).</summary>
    public byte[] FirstPage { get; }

    public static TickPagePlan Create(
        TickResult result,
        int maximumPageBytes = TickTransportLimits.MaximumTickResponseBytes)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPageBytes);
        return new TickPagePlan(result, maximumPageBytes);
    }

    /// <summary>
    /// Rend la page désignée par un curseur. Un curseur étranger au tick courant,
    /// tronqué ou d'une autre séquence est refusé explicitement : jamais servi de
    /// travers.
    /// </summary>
    public byte[] Render(string cursor)
    {
        if (!TryParseCursor(cursor, out var tick, out var index, out var pageCount))
        {
            throw new HostLifecycleException("page_invalid", $"Cursor '{cursor}' is not a tick page cursor.");
        }

        if (tick != _tick || pageCount != PageCount)
        {
            throw new HostLifecycleException(
                "page_expired",
                $"Cursor '{cursor}' targets tick {tick} ({pageCount} pages); the current tick is {_tick} ({PageCount} pages).");
        }

        if (_single is not null)
        {
            throw new HostLifecycleException(
                "page_invalid",
                $"Tick {_tick} is served as a single response; it has no pages to page through.");
        }

        if (index < 0 || index >= PageCount)
        {
            throw new HostLifecycleException(
                "page_invalid",
                $"Cursor '{cursor}' points to page {index + 1}; tick {_tick} has {PageCount} pages.");
        }

        return RenderPage(index);
    }

    /// <summary>
    /// Empile les éléments dans l'ordre jusqu'au budget d'une page. Un élément plus
    /// gros que le budget occupe une page à lui seul : la construction de cette page
    /// échouera franchement, ce qui est le seul comportement honnête (on ne peut pas
    /// couper une observation en deux).
    /// </summary>
    private static int[] PackPages(int[] itemBytes, int maximumPageBytes)
    {
        var budget = Math.Max(1, maximumPageBytes - TickTransportLimits.PageEnvelopeReserveBytes);
        var starts = new List<int>();
        var pageStart = 0;
        long used = 0;
        for (var item = 0; item < itemBytes.Length; item++)
        {
            long cost = itemBytes[item] + (item > pageStart ? 1 : 0);
            if (used + cost > budget && item > pageStart)
            {
                starts.Add(pageStart);
                pageStart = item;
                used = 0;
                cost = itemBytes[item];
            }

            used += cost;
        }

        starts.Add(pageStart);
        return starts.ToArray();
    }

    private byte[] RenderPage(int index)
    {
        if (_single is not null)
        {
            return _single;
        }

        var start = _pageStarts[index];
        var end = index + 1 < _pageStarts.Length ? _pageStarts[index + 1] : _itemBytes.Length;
        var observationStart = Math.Min(start, _observations.Count);
        var observationEnd = Math.Min(end, _observations.Count);
        var eventStart = Math.Clamp(start - _observations.Count, 0, _events.Count);
        var eventEnd = Math.Clamp(end - _observations.Count, 0, _events.Count);
        var observations = new ObservationEnvelope[observationEnd - observationStart];
        for (var item = observationStart; item < observationEnd; item++)
        {
            observations[item - observationStart] = _observations[item];
        }

        var events = new EventEnvelope[eventEnd - eventStart];
        for (var item = eventStart; item < eventEnd; item++)
        {
            events[item - eventStart] = _events[item];
        }

        var complete = index == PageCount - 1;
        var page = new TickPageEnvelope(
            _protocolVersion,
            _tick,
            new TickPageInfo(index, PageCount, complete, complete ? null : CreateCursor(index + 1)),
            new TickPageChunk(observations, events));
        var bytes = Encoding.UTF8.GetBytes(ProtocolJson.Serialize(page));
        if (bytes.Length > _maximumPageBytes)
        {
            // Ni troncature, ni page hors plafond : on nomme ce qui ne peut pas tenir.
            var largest = 0;
            for (var item = start; item < end; item++)
            {
                largest = Math.Max(largest, _itemBytes[item]);
            }

            throw new TickPayloadTooLargeException(
                $"Tick {_tick} page {index + 1}/{PageCount} would be {bytes.Length} bytes, above the " +
                $"{_maximumPageBytes}-byte ceiling: one of its {end - start} item(s) is {largest} bytes alone, " +
                "so it cannot be split across pages. No partial response was sent.");
        }

        return bytes;
    }

    private string CreateCursor(int index) => string.Create(
        CultureInfo.InvariantCulture,
        $"{_tick}.{index}.{PageCount}.{Checksum(_tick, index, PageCount)}");

    private static bool TryParseCursor(string cursor, out long tick, out int index, out int pageCount)
    {
        tick = -1;
        index = -1;
        pageCount = -1;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        var parts = cursor.Split('.');
        return parts.Length == 4 &&
               long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out tick) &&
               int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out index) &&
               int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out pageCount) &&
               string.Equals(parts[3], Checksum(tick, index, pageCount), StringComparison.Ordinal);
    }

    /// <summary>
    /// Empreinte FNV-1a 64 du triplet (tick, page, nombre de pages) : un curseur
    /// altéré est refusé au lieu d'être interprété comme une autre page.
    /// </summary>
    private static string Checksum(long tick, int index, int pageCount)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{tick}.{index}.{pageCount}");
        var hash = 14695981039346656037UL;
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Une réponse de tick ne peut pas être produite sous le plafond sans troncature
/// (PRISM-142) : la couche HTTP répond 413 avec ce motif, et le run s'arrête
/// franchement plutôt que de laisser SYNE travailler sur un monde partiel.
/// </summary>
public sealed class TickPayloadTooLargeException(string detail) : InvalidOperationException(detail)
{
    public string ErrorCode => "payload_too_large";
}
