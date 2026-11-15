// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Prism.Contracts;
using Simulation.Core.Cognition;
using Simulation.Core.World;

namespace Simulation.Console.Integration;

public sealed record PrismClientStatus(
    string State,
    long LastCompletedTick,
    ulong? Seed,
    string? ContentHash,
    string? Error);

/// <summary>
/// Compteurs du transport PRISM (opt-in <c>SYNE_COUPLED_PROFILE</c>) : attente des
/// en-têtes, lecture du corps, désérialisation, taille reçue. Le run imprime le
/// bilan ; les tailles disent si un appel lent transporte ou non quelque chose.
/// </summary>
public static class PrismHttpProfile
{
    private static readonly object Gate = new();
    private static readonly List<(string Endpoint, double WaitMs, double BodyMs, double ParseMs, int Chars, long SentEpoch, long HeadersEpoch)> Samples = [];

    public static void Record(
        string endpoint,
        double waitMs,
        double bodyMs,
        double parseMs,
        int chars,
        long sentEpoch = 0,
        long headersEpoch = 0)
    {
        lock (Gate)
        {
            Samples.Add((endpoint, waitMs, bodyMs, parseMs, chars, sentEpoch, headersEpoch));
        }
    }

    public static void Report(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        List<(string Endpoint, double WaitMs, double BodyMs, double ParseMs, int Chars, long SentEpoch, long HeadersEpoch)> samples;
        lock (Gate)
        {
            samples = [.. Samples];
            Samples.Clear();
        }

        if (samples.Count == 0)
        {
            return;
        }

        writer.WriteLine(
            "SYNE — transport PRISM (ms/appel, moyenne) : en-têtes | corps | parse | caractères reçus");
        int window = Math.Min(10, samples.Count);
        foreach ((string label, List<(string Endpoint, double WaitMs, double BodyMs, double ParseMs, int Chars, long SentEpoch, long HeadersEpoch)> part) in
                 new[]
                 {
                     ("début", samples.Take(window).ToList()),
                     ("fin", samples.Skip(samples.Count - window).ToList()),
                 })
        {
            double wait = part.Average(s => s.WaitMs);
            double body = part.Average(s => s.BodyMs);
            double parse = part.Average(s => s.ParseMs);
            double chars = part.Average(s => s.Chars);
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {label,-6} | {wait,8:F1} | {body,6:F1} | {parse,5:F1} | {chars,10:F0} | envoi {part[0].SentEpoch} en-têtes {part[^1].HeadersEpoch}"));
        }

        foreach ((string endpoint, double waitMs, double bodyMs, double parseMs, int chars, long sentEpoch, long headersEpoch) in samples)
        {
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"    {endpoint,-12} attente {waitMs,7:F1} | corps {bodyMs,5:F1} | parse {parseMs,5:F1} | {chars,8} car. | envoi {sentEpoch} en-têtes {headersEpoch}"));
        }
    }

    private static double Ms(long from, long to) =>
        (to - from) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
}

public sealed class PrismClientException(
    string endpoint,
    HttpStatusCode? statusCode,
    string responseBody,
    Exception? innerException = null)
    : Exception(
        $"PRISM request to '{endpoint}' failed{(statusCode is null ? "." : $" with HTTP {(int)statusCode.Value}.")} {responseBody}",
        innerException)
{
    public string Endpoint { get; } = endpoint;
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;
}

public sealed class PrismClient
{
    private const string SessionTokenHeader = "X-LIVEX-Session-Token";
    private readonly HttpClient _httpClient;
    private readonly string _sessionToken;

    public PrismClient(HttpClient httpClient, string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        if (httpClient.BaseAddress is not { IsAbsoluteUri: true })
        {
            throw new ArgumentException("The PRISM HTTP client requires an absolute BaseAddress.", nameof(httpClient));
        }

        _httpClient = httpClient;
        _sessionToken = sessionToken;
    }

    public Task<PrismClientStatus> PrepareAsync(
        ulong seed,
        string contentHash,
        string? contentDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return PostAsync<PrismClientStatus>(
            "/control/prepare",
            new { seed, contentHash, contentDirectory },
            cancellationToken);
    }

    public Task<PrismClientStatus> StartAsync(
        long? maxTicks = null,
        CancellationToken cancellationToken = default)
    {
        if (maxTicks is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTicks));
        }

        return PostAsync<PrismClientStatus>(
            "/control/start",
            new { maxTicks },
            cancellationToken);
    }

    public async Task<TickResult> StepAsync(
        TickRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = ProtocolJson.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var first = await SendForBodyAsync("/data/tick", content, cancellationToken).ConfigureAwait(false);
        var response = ReadBody("/data/tick", first, PrismTickPages.Read);
        if (!response.IsPaged)
        {
            return response.Result!;
        }

        // Réponse bornée (PRISM-142) : les pages restantes sont lues à la suite du même
        // tick, sans que le monde avance, puis concaténées, dans l'ordre annoncé.
        var pages = new List<TickPageEnvelope> { response.FirstPage! };
        while (!pages[^1].Page.Complete)
        {
            var cursor = pages[^1].Page.Cursor
                         ?? throw new PrismTickPageSequenceException(
                             $"Tick {pages[^1].Tick} page {pages[^1].Page.Index + 1}/{pages[^1].Page.PageCount} announces no next cursor.");
            var exchange = await SendForBodyAsync(
                $"/data/tick/result?cursor={Uri.EscapeDataString(cursor)}",
                content: null,
                cancellationToken).ConfigureAwait(false);
            PrismTickPages.Append(pages, ReadBody("/data/tick/result", exchange, PrismTickPages.ReadPage));
        }

        return PrismTickPages.Assemble(request.Tick, pages);
    }

    public Task<TickResult> StepDecisionsAsync(
        long tick,
        IEnumerable<DecisionRecord> decisions,
        Func<Position, PrismBlockPosition> mapPosition,
        CancellationToken cancellationToken = default)
    {
        var commands = PrismDecisionCommandMapper.Map(tick, decisions, mapPosition);
        return StepAsync(
            new TickRequest(ProtocolVersion.Current.ToString(), tick, commands),
            cancellationToken);
    }

    public Task<PrismClientStatus> StopAsync(CancellationToken cancellationToken = default) =>
        PostAsync<PrismClientStatus>("/control/stop", new { }, cancellationToken);

    /// <summary>État courant du monde, lu sans le modifier (attente d'un run gaté).</summary>
    public async Task<PrismClientStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var exchange = await SendForBodyAsync("/control/status", content: null, cancellationToken)
            .ConfigureAwait(false);
        return ReadBody("/control/status", exchange, body => ProtocolJson.Deserialize<PrismClientStatus>(body));
    }

    public Task<PrismClientStatus> SaveCheckpointAsync(
        string checkpointId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        return PostAsync<PrismClientStatus>(
            "/control/checkpoint",
            new { checkpointId },
            cancellationToken);
    }

    public Task<PrismClientStatus> RestoreCheckpointAsync(
        string checkpointId,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return PostAsync<PrismClientStatus>(
            "/control/restore",
            new { checkpointId, contentHash },
            cancellationToken);
    }

    private Task<TResponse> PostAsync<TResponse>(
        string endpoint,
        object payload,
        CancellationToken cancellationToken)
    {
        var content = JsonContent.Create(payload, options: ProtocolJson.Options);
        return SendAsync<TResponse>(endpoint, content, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        string endpoint,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        var exchange = await SendForBodyAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
        return ReadBody(endpoint, exchange, body => ProtocolJson.Deserialize<TResponse>(body));
    }

    /// <summary>
    /// Lit le corps reçu avec le contrat fourni par l'appelant et relève le profil
    /// (attente, corps, parse, taille) : un tick peut être un résultat complet ou une
    /// page (PRISM-142), donc la lecture est choisie par l'appelant, la mesure reste ici.
    /// </summary>
    private static T ReadBody<T>(string endpoint, PrismHttpExchange exchange, Func<string, T> read)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var value = read(exchange.Body);
            if (Profiling)
            {
                double parseMs = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                PrismHttpProfile.Record(
                    endpoint,
                    exchange.WaitMs,
                    exchange.BodyMs,
                    parseMs,
                    exchange.Chars,
                    exchange.SentEpoch,
                    exchange.HeadersEpoch);
            }

            return value;
        }
        catch (JsonException exception)
        {
            throw new PrismClientException(endpoint, HttpStatusCode.OK, exchange.Body, exception);
        }
    }

    private static bool Profiling => PrismCoupledProfile.FromEnvironment() is not null;

    /// <summary>Réponse brute et ses temps de transport, relevés opt-in.</summary>
    private readonly record struct PrismHttpExchange(
        string Body,
        double WaitMs,
        double BodyMs,
        int Chars,
        long SentEpoch,
        long HeadersEpoch);

    /// <summary>
    /// Envoie une requête et rend le corps de la réponse tel quel (GET quand
    /// <paramref name="content"/> est nul). Le corps n'est pas analysé ici pour que
    /// l'appelant décide lui-même du contrat qui s'applique — un tick peut être un
    /// résultat complet ou une page (PRISM-142).
    /// </summary>
    private async Task<PrismHttpExchange> SendForBodyAsync(
        string endpoint,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            content is null ? HttpMethod.Get : HttpMethod.Post,
            endpoint)
        {
            Content = content,
        };
        request.Headers.TryAddWithoutValidation(SessionTokenHeader, _sessionToken);
        // Profilage opt-in : sépare l'attente du serveur, la lecture du corps et
        // la désérialisation. Sans cela, un appel lent ne dit pas s'il attend
        // l'hôte, le réseau ou le parseur — trois chantiers différents.
        bool profiled = Profiling;
        long sentEpoch = profiled ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : 0;
        long started = profiled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        long headersRead = profiled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        long bodyRead = profiled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        if (!response.IsSuccessStatusCode)
        {
            throw new PrismClientException(endpoint, response.StatusCode, responseBody);
        }

        double frequency = System.Diagnostics.Stopwatch.Frequency;
        return new PrismHttpExchange(
            responseBody,
            (headersRead - started) * 1000.0 / frequency,
            (bodyRead - headersRead) * 1000.0 / frequency,
            responseBody.Length,
            sentEpoch,
            sentEpoch + (long)((headersRead - started) * 1000.0 / frequency));
    }
}
