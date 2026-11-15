// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Prism.Contracts;

namespace Simulation.Console.Integration;

/// <summary>
/// Réponse d'un tick côté client : soit le <see cref="TickResult"/> complet, soit sa
/// première page quand le résultat dépasse le plafond de transport (PRISM-142).
/// </summary>
internal sealed record PrismTickResponse(TickResult? Result, TickPageEnvelope? FirstPage)
{
    public bool IsPaged => FirstPage is not null;
}

/// <summary>
/// Lecture des réponses de tick paginées (PRISM-142, ADR-014) : reconnaître la forme,
/// drainer les pages et reconstruire le flux que le monde a produit.
/// </summary>
/// <remarks>
/// Le chaînage est vérifié, jamais supposé : une page manquante, dupliquée, hors ordre
/// ou d'un autre tick lève <see cref="PrismTickPageSequenceException"/>. Le contraire
/// — assembler ce qu'on a reçu et continuer — serait exactement la troncature
/// silencieuse que le plafond est censé interdire.
/// </remarks>
internal static class PrismTickPages
{
    public static PrismTickResponse Read(string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A tick response must be a JSON object.");
        }

        // La pagination s'annonce par `page` à la racine. Une réponse complète n'en a
        // pas et se relit exactement comme avant ce chantier.
        if (!root.TryGetProperty("page", out _))
        {
            return new PrismTickResponse(ProtocolJson.Deserialize<TickResult>(root), null);
        }

        var page = ProtocolJson.Deserialize<TickPageEnvelope>(root);
        if (page.Page.Complete)
        {
            throw new PrismTickPageSequenceException(
                $"Tick {page.Tick} was served as a complete page (1/1) instead of a full tick result.");
        }

        return new PrismTickResponse(null, page);
    }

    public static TickPageEnvelope ReadPage(string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("page", out _))
        {
            throw new PrismTickPageSequenceException(
                "A paged tick asked for its next page but received a response without page metadata.");
        }

        return ProtocolJson.Deserialize<TickPageEnvelope>(root);
    }

    public static void Append(List<TickPageEnvelope> pages, TickPageEnvelope next)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(next);
        if (pages.Count == 0)
        {
            pages.Add(next);
            return;
        }

        var previous = pages[^1];
        if (next.Tick != previous.Tick)
        {
            throw new PrismTickPageSequenceException(
                $"Page {next.Page.Index + 1} belongs to tick {next.Tick}, not to tick {previous.Tick}.");
        }

        if (next.Page.PageCount != previous.Page.PageCount)
        {
            throw new PrismTickPageSequenceException(
                $"Page count changed mid-sequence ({previous.Page.PageCount} then {next.Page.PageCount}) on tick {next.Tick}.");
        }

        if (next.Page.Index != previous.Page.Index + 1)
        {
            throw new PrismTickPageSequenceException(
                $"Expected page {previous.Page.Index + 2}/{previous.Page.PageCount} of tick {next.Tick}, " +
                $"received page {next.Page.Index + 1}/{next.Page.PageCount}.");
        }

        pages.Add(next);
    }

    public static TickResult Assemble(long expectedTick, List<TickPageEnvelope> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0)
        {
            throw new PrismTickPageSequenceException("No tick page was received.");
        }

        var first = pages[0];
        if (first.Tick != expectedTick)
        {
            throw new PrismTickPageSequenceException(
                $"Tick {expectedTick} was requested but the pages describe tick {first.Tick}.");
        }

        if (pages.Count != first.Page.PageCount || !pages[^1].Page.Complete)
        {
            throw new PrismTickPageSequenceException(
                $"Tick {first.Tick} announced {first.Page.PageCount} page(s) but {pages.Count} were received; " +
                "the tick is incomplete and is not applied.");
        }

        for (var index = 0; index < pages.Count; index++)
        {
            if (pages[index].Page.Index != index)
            {
                throw new PrismTickPageSequenceException(
                    $"Tick {first.Tick} page {index + 1} is missing: page order is {string.Join(",", pages.Select(static page => page.Page.Index + 1))}.");
            }
        }

        var observationCount = pages.Sum(static page => page.Chunk.Observations.Count);
        var eventCount = pages.Sum(static page => page.Chunk.Events.Count);
        var observations = new List<ObservationEnvelope>(observationCount);
        var events = new List<EventEnvelope>(eventCount);
        foreach (var page in pages)
        {
            observations.AddRange(page.Chunk.Observations);
            events.AddRange(page.Chunk.Events);
        }

        return new TickResult(first.ProtocolVersion, first.Tick, observations, events);
    }
}

/// <summary>
/// Une séquence de pages de tick est incomplète, désordonnée ou incohérente : le tick
/// n'est pas appliqué (PRISM-142).
/// </summary>
public sealed class PrismTickPageSequenceException(string detail) : Exception(detail);
