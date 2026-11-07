// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using Prism.Contracts;
using Simulation.Core.Perception;

namespace Simulation.Console.Integration;

/// <summary>
/// Boucle couplée SYNE ↔ PRISM (SYNE-220/225, R2) : SYNE pilote PRISM tick par tick
/// via un délégué d'exécution et applique les observations
/// reçues à la cognition via <see cref="PrismObservationMapper"/>.
/// </summary>
/// <remarks>
/// Le délégué d'exécution est injectable pour rester testable sans hôte PRISM réel
/// (tests unitaires) ; en production il s'agit de <c>PrismClient.StepAsync</c>.
/// </remarks>
public sealed class PrismCoupledRunner
{
    private readonly Func<TickRequest, CancellationToken, Task<TickResult>> _stepAsync;
    private readonly Func<long, IReadOnlyList<ObservationEnvelope>, IReadOnlyList<CommandEnvelope>> _decide;
    private readonly Action<ulong, IReadOnlyList<Observation>, long>? _onObservations;
    private readonly Func<string, long, CancellationToken, Task>? _saveCheckpointAsync;
    private readonly Func<string, long, CancellationToken, Task>? _restoreCheckpointAsync;
    private readonly Action<ObservationEnvelope, long>? _onEnvelope;
    private readonly Action<TickResult>? _onTickResult;
    private readonly PrismResourceSites? _resourceSites;
    private readonly PrismItemCatalog? _itemCatalog;
    private readonly Action<ulong, string, string, string, long>? _onCommandFailed;

    public PrismCoupledRunner(
        Func<TickRequest, CancellationToken, Task<TickResult>> stepAsync,
        Func<long, IReadOnlyList<ObservationEnvelope>, IReadOnlyList<CommandEnvelope>>? decide = null,
        Action<ulong, IReadOnlyList<Observation>, long>? onObservations = null,
        Func<string, long, CancellationToken, Task>? saveCheckpointAsync = null,
        Func<string, long, CancellationToken, Task>? restoreCheckpointAsync = null,
        Action<ObservationEnvelope, long>? onEnvelope = null,
        PrismResourceSites? resourceSites = null,
        Action<TickResult>? onTickResult = null,
        PrismItemCatalog? itemCatalog = null,
        Action<ulong, string, string, string, long>? onCommandFailed = null)
    {
        ArgumentNullException.ThrowIfNull(stepAsync);
        _stepAsync = stepAsync;
        _decide = decide ?? ((_, _) => Array.Empty<CommandEnvelope>());
        _onObservations = onObservations;
        _saveCheckpointAsync = saveCheckpointAsync;
        _restoreCheckpointAsync = restoreCheckpointAsync;
        _onEnvelope = onEnvelope;
        _resourceSites = resourceSites;
        _onTickResult = onTickResult;
        _itemCatalog = itemCatalog;
        _onCommandFailed = onCommandFailed;
    }

    public async Task<PrismCoupledRunResult> RunAsync(
        long ticks,
        long startTick = 0,
        string? checkpointId = null,
        long checkpointEvery = 0,
        CancellationToken cancellationToken = default)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }

        if (startTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startTick));
        }

        if (checkpointEvery < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpointEvery));
        }

        var result = new PrismCoupledRunResult { StartTick = startTick };

        if (startTick > 0 && !string.IsNullOrWhiteSpace(checkpointId) && _restoreCheckpointAsync is not null)
        {
            await _restoreCheckpointAsync(checkpointId!, startTick, cancellationToken).ConfigureAwait(false);
            result.RestoredCheckpointId = checkpointId;
            result.RestoredCheckpointTick = startTick;
        }

        IReadOnlyList<ObservationEnvelope> previousObservations = [];
        for (long i = 0; i < ticks; i++)
        {
            long tick = startTick + i;
            cancellationToken.ThrowIfCancellationRequested();

            var commands = _decide(tick, previousObservations);
            TickResult tickResult;
            try
            {
                tickResult = await _stepAsync(
                    new TickRequest(ProtocolVersion.Current.ToString(), tick, commands),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                result.Failure = exception;
                result.FailedAtTick = tick;
                return result;
            }

            if (tickResult.Tick != tick)
            {
                result.Failure = new InvalidOperationException(
                    $"PRISM returned tick {tickResult.Tick} for requested tick {tick}.");
                result.FailedAtTick = tick;
                return result;
            }

            // Trace des issues de commandes (`action_completed` / `action_failed`) :
            // sans elle, une commande refusée par PRISM est invisible côté SYNE.
            _onTickResult?.Invoke(tickResult);

            // Un refus est **attribué** à la consigne envoyée (agent, nature,
            // payload) : c'est ce qui permet au write-back de cesser de redemander
            // une cible que PRISM vient de déclarer inatteignable. Sans cette
            // attribution, l'événement ne porte qu'un `commandId` que SYNE jette.
            if (_onCommandFailed is not null)
            {
                AttributeFailures(commands, tickResult, tick);
            }

            foreach (var envelope in tickResult.Observations)
            {
                // Le monde externe est mis à jour avant la cognition : la boucle
                // couplée lit ainsi l'état du tick qu'elle vient de recevoir.
                _onEnvelope?.Invoke(envelope, tick);

                var prismObservation = new PrismAgentObservation(
                    envelope.ProtocolVersion,
                    envelope.Tick,
                    envelope.AgentId,
                    envelope.Changes,
                    envelope.Self);
                var mapped = PrismObservationMapper.ToObservations(prismObservation, _resourceSites, _itemCatalog);
                _onObservations?.Invoke(envelope.AgentId, mapped, tick);
            }

            previousObservations = tickResult.Observations;
            result.TicksCompleted++;

            bool shouldSave = checkpointEvery > 0
                && _saveCheckpointAsync is not null
                && (result.TicksCompleted % checkpointEvery == 0);
            if (shouldSave)
            {
                string id = string.IsNullOrWhiteSpace(checkpointId)
                    ? $"coupled-{tick}"
                    : $"{checkpointId}-{tick}";
                await _saveCheckpointAsync!(id, tick, cancellationToken).ConfigureAwait(false);
                result.LastCheckpointId = id;
                result.LastCheckpointTick = tick;
            }
        }

        return result;
    }

    private void AttributeFailures(
        IReadOnlyList<CommandEnvelope> commands,
        TickResult tickResult,
        long tick)
    {
        Dictionary<string, CommandEnvelope> byCommandId = new(StringComparer.Ordinal);
        foreach (CommandEnvelope command in commands)
        {
            byCommandId[command.CommandId] = command;
        }

        if (byCommandId.Count == 0 || tickResult.Events.Count == 0)
        {
            return;
        }

        foreach (EventEnvelope entry in tickResult.Events)
        {
            if (!StringComparer.Ordinal.Equals(entry.Kind, "action_failed") ||
                entry.Payload.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !entry.Payload.TryGetProperty("commandId", out System.Text.Json.JsonElement idElement) ||
                idElement.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                continue;
            }

            string? commandId = idElement.GetString();
            if (commandId is null || !byCommandId.TryGetValue(commandId, out CommandEnvelope? command))
            {
                continue;
            }

            string reason = entry.Payload.TryGetProperty("reason", out System.Text.Json.JsonElement reasonElement) &&
                            reasonElement.ValueKind == System.Text.Json.JsonValueKind.String
                ? reasonElement.GetString()!
                : string.Empty;
            _onCommandFailed!(
                command.AgentId,
                command.Kind,
                command.Payload.GetRawText(),
                reason,
                tick);
        }
    }
}

public sealed class PrismCoupledRunResult
{
    public long StartTick { get; set; }
    public long TicksCompleted { get; set; }
    public Exception? Failure { get; set; }
    public long? FailedAtTick { get; set; }

    /// <summary>Identifiant du checkpoint restauré avant le démarrage (le cas échéant).</summary>
    public string? RestoredCheckpointId { get; set; }

    /// <summary>Tick associé au checkpoint restauré (le cas échéant).</summary>
    public long? RestoredCheckpointTick { get; set; }

    /// <summary>Identifiant du dernier checkpoint sauvegardé pendant le run (le cas échéant).</summary>
    public string? LastCheckpointId { get; set; }

    /// <summary>Tick associé au dernier checkpoint sauvegardé (le cas échéant).</summary>
    public long? LastCheckpointTick { get; set; }

    public bool Succeeded => Failure is null;
}
