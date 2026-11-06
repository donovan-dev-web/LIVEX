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

    public PrismCoupledRunner(
        Func<TickRequest, CancellationToken, Task<TickResult>> stepAsync,
        Func<long, IReadOnlyList<ObservationEnvelope>, IReadOnlyList<CommandEnvelope>>? decide = null,
        Action<ulong, IReadOnlyList<Observation>, long>? onObservations = null)
    {
        ArgumentNullException.ThrowIfNull(stepAsync);
        _stepAsync = stepAsync;
        _decide = decide ?? ((_, _) => Array.Empty<CommandEnvelope>());
        _onObservations = onObservations;
    }

    public async Task<PrismCoupledRunResult> RunAsync(
        long ticks,
        long startTick = 0,
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

        var result = new PrismCoupledRunResult { StartTick = startTick };
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

            foreach (var envelope in tickResult.Observations)
            {
                var prismObservation = new PrismAgentObservation(
                    envelope.ProtocolVersion,
                    envelope.Tick,
                    envelope.AgentId,
                    envelope.Changes,
                    envelope.Self);
                var mapped = PrismObservationMapper.ToObservations(prismObservation);
                _onObservations?.Invoke(envelope.AgentId, mapped, tick);
            }

            previousObservations = tickResult.Observations;
            result.TicksCompleted++;
        }

        return result;
    }
}

public sealed class PrismCoupledRunResult
{
    public long StartTick { get; set; }
    public long TicksCompleted { get; set; }
    public Exception? Failure { get; set; }
    public long? FailedAtTick { get; set; }

    public bool Succeeded => Failure is null;
}
