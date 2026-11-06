// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

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
        return await SendAsync<TickResult>(
            "/data/tick",
            content,
            cancellationToken).ConfigureAwait(false);
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
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content,
        };
        request.Headers.TryAddWithoutValidation(SessionTokenHeader, _sessionToken);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new PrismClientException(endpoint, response.StatusCode, responseBody);
        }

        try
        {
            return ProtocolJson.Deserialize<TResponse>(responseBody);
        }
        catch (JsonException exception)
        {
            throw new PrismClientException(endpoint, response.StatusCode, responseBody, exception);
        }
    }
}
