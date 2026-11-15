using System.Text.Json;
using Prism.Contracts;

namespace Prism.Contracts.Tests;

/// <summary>
/// Contrat des réponses de tick bornées (PRISM-142, ADR-014) : une page annonce sa
/// position dans une séquence dont le nombre de pages est connu, et elle ne peut pas
/// se faire passer pour un tick complet.
/// </summary>
public sealed class TickPagingContractTests
{
    [Fact]
    public void PageCarriesItsPositionAndTheNextCursor()
    {
        var json = ProtocolJson.Serialize(Page(index: 0, pageCount: 3, agents: [1UL, 2UL]));

        using var document = JsonDocument.Parse(json);
        var page = document.RootElement.GetProperty("page");
        Assert.Equal(0, page.GetProperty("index").GetInt32());
        Assert.Equal(3, page.GetProperty("pageCount").GetInt32());
        Assert.False(page.GetProperty("complete").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(page.GetProperty("cursor").GetString()));

        // Le contenu est sous `chunk`, pas à la racine : c'est ce qui distingue une
        // page d'un TickResult et interdit de la lire comme un tick complet.
        Assert.Equal(2, document.RootElement.GetProperty("chunk")
            .GetProperty("observations").GetArrayLength());
        Assert.False(document.RootElement.TryGetProperty("observations", out _));

        var roundTrip = ProtocolJson.Deserialize<TickPageEnvelope>(json);
        Assert.Equal(3, roundTrip.Page.PageCount);
        Assert.Equal(2, roundTrip.Chunk.Observations.Count);
    }

    [Fact]
    public void LastPageIsCompleteAndCarriesNoCursor()
    {
        var json = ProtocolJson.Serialize(Page(index: 2, pageCount: 3, agents: [7UL]));

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("page").GetProperty("complete").GetBoolean());
        Assert.False(document.RootElement.GetProperty("page").TryGetProperty("cursor", out _));
        Assert.Equal(7UL, Assert.Single(
            ProtocolJson.Deserialize<TickPageEnvelope>(json).Chunk.Observations).AgentId);
    }

    [Fact]
    public void RejectsAPageThatClaimsCompletionTooEarly()
    {
        var page = Page(index: 0, pageCount: 3, agents: [1UL]) with
        {
            Page = new TickPageInfo(0, 3, Complete: true, Cursor: null),
        };

        var exception = Assert.Throws<JsonException>(() => ProtocolJson.Serialize(page));
        Assert.Contains("Only the last tick page", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnIncompletePageWithoutCursor()
    {
        var page = Page(index: 1, pageCount: 3, agents: [1UL]) with
        {
            Page = new TickPageInfo(1, 3, Complete: false, Cursor: null),
        };

        var exception = Assert.Throws<JsonException>(() => ProtocolJson.Serialize(page));
        Assert.Contains("must carry the next cursor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsObservationsFromAnotherTick()
    {
        var page = Page(index: 0, pageCount: 2, agents: [1UL]) with
        {
            Chunk = new TickPageChunk([Observation(tick: 8, agentId: 1)], []),
        };

        var exception = Assert.Throws<JsonException>(() => ProtocolJson.Serialize(page));
        Assert.Contains("must match the page tick", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAPageOutsideTheAnnouncedSequence()
    {
        // Sans cette borne, un hôte qui n'annonce jamais la dernière page ferait
        // drainer SYNE indéfiniment (PRISM-142).
        var page = Page(index: 2, pageCount: 2, agents: [1UL]) with
        {
            Page = new TickPageInfo(2, 2, Complete: false, Cursor: "7.3.2.0123456789abcdef"),
        };

        var exception = Assert.Throws<JsonException>(() => ProtocolJson.Serialize(page));
        Assert.Contains("index and page count are inconsistent", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APageCannotBeReadAsACompleteTickResult()
    {
        var json = ProtocolJson.Serialize(Page(index: 0, pageCount: 2, agents: [1UL, 2UL]));

        // Silently accepting a page as a tick would apply a partial world: the
        // contract must fail loudly here (PRISM-142).
        var exception = Assert.Throws<JsonException>(() => ProtocolJson.Deserialize<TickResult>(json));
        Assert.Contains("observations and events are required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CeilingIsEightMebibytes()
    {
        Assert.Equal(8 * 1024 * 1024, TickTransportLimits.MaximumTickResponseBytes);
        Assert.True(TickTransportLimits.PageEnvelopeReserveBytes < TickTransportLimits.MaximumTickResponseBytes / 8);
    }

    private static TickPageEnvelope Page(int index, int pageCount, ulong[] agents)
    {
        var complete = index == pageCount - 1;
        return new TickPageEnvelope(
            ProtocolVersion.Current.ToString(),
            7,
            new TickPageInfo(index, pageCount, complete, complete ? null : $"7.{index + 1}.{pageCount}.0123456789abcdef"),
            new TickPageChunk(
                agents.Select(agentId => Observation(7, agentId)).ToArray(),
                []));
    }

    private static ObservationEnvelope Observation(long tick, ulong agentId) =>
        new(
            ProtocolVersion.Current.ToString(),
            tick,
            agentId,
            JsonSerializer.SerializeToElement(new { changes = Array.Empty<object>() }, ProtocolJson.Options),
            JsonSerializer.SerializeToElement(new { position = new { x = 1, y = 2, z = 3 } }, ProtocolJson.Options));
}
