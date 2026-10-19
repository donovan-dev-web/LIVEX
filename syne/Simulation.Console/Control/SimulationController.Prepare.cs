// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : SimulationController - partie Prepare.

using System;
using System.Threading;
using System.Threading.Tasks;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Console.Control;

using System;
using System.Threading;
using System.Threading.Tasks;
using Simulation.Core.Configuration;
using Simulation.Core.World;

public sealed partial class SimulationController : IAsyncDisposable
{

            /// <summary>Construit le monde déterministe et le laisse en état Ready.</summary>
        public async Task<Simulation.Core.World.WorldDescription> PrepareAsync(
                ulong? seed, string? configJson, int? ticksPerSecond = null,
                CancellationToken cancellationToken = default)
            => await PrepareCoreAsync(seed, configJson, ticksPerSecond, autoReady: false, cancellationToken);


        private async Task<Simulation.Core.World.WorldDescription> PrepareCoreAsync(
                ulong? seed, string? configJson, int? ticksPerSecond, bool autoReady,
                CancellationToken cancellationToken)
            {
                await CancelCurrentRunSafeAsync();
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    _state = SimulationControlState.WorldPreparing;
                    _worldDescription = null;
                    _preparedOptions = null;
                    _worldReadyAcknowledged = false;
                    _explicitPreparation = !autoReady;
                }
                // La surcouche reste en JSON brut jusqu'ici : la désérialiser en
                // SimulationOptions la rendrait complète et écraserait le profil de
                // référence avec les valeurs par défaut du type (cf. MergeJson).
                // Sans surcouche, le profil de référence est appliqué explicitement :
                // le chemin reset (configJson null) doit produire exactement le même
                // monde que prepare, sans jamais dépendre des défauts intégrés
                // (doctrine).
                string effectiveConfig = string.IsNullOrWhiteSpace(configJson)
                    ? SimulationProfiles.ReferenceJson()
                    : configJson;
                (SimulationOptions options, ulong effectiveSeed) = SimulationFactory.ResolveOptions(
                    ConfigLoader.LoadDefaults(), effectiveConfig, seed);
                if (ticksPerSecond is not null)
                {
                    if (ticksPerSecond <= 0)
                        throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), "ticksPerSecond doit être strictement positif.");
                    options.Simulation.TicksPerSecond = ticksPerSecond.Value;
                }
                var (_, loop) = SimulationFactory.Build(options, effectiveSeed);
                var description = Simulation.Core.World.WorldDescriptionBuilder.Build(
                    loop.World, effectiveSeed, options.Simulation.WorldCellSize, options.Simulation.TicksPerSecond,
                    options.Simulation.SimulatedSecondsPerTick, options.World.MetersPerUnit);
                lock (_gate)
                {
                    _loop = loop;
                    _preparedOptions = options;
                    _seed = effectiveSeed;
                    _runId = $"run-{effectiveSeed}";
                    _maxTicks = null;
                    _worldDescription = description;
                    _state = SimulationControlState.Ready;
                    _worldReadyAcknowledged = autoReady;
                }
                if (_observabilitySink is not null)
                {
                    await _observabilitySink.BroadcastAsync(System.Text.Json.JsonSerializer.Serialize(
                        new { type = "world_initialized", version = description.Version, seed = effectiveSeed, world = description },
                        new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
                }
                return description;
            }

            /// <summary>Accusé de réception Unreal. L'accusé est idempotent.</summary>
        public bool AcknowledgeReady(string? version)
            {
                lock (_gate)
                {
                    if (_state != SimulationControlState.Ready
                        || (!string.IsNullOrWhiteSpace(version) && version != _worldDescription?.Version))
                        return false;
                    _worldReadyAcknowledged = true;
                    return true;
                }
    }

    /// <summary>
    /// Format canonique du run_id piloté : <c>run-&lt;seed&gt;-&lt;12hex&gt;</c>.
    /// Le seed reste lisible dans l'identifiant (contrat ECHOS, repli ``_seed_of``
    /// sur les flux V0.2.0) et le suffixe aléatoire conserve l'unicité entre deux
    /// runs de même seed — l'ancien format <c>run-&lt;seed&gt;</c> du monde préparé
    /// collisionnait les runs successifs d'un même seed côté stockage ECHOS.
    /// </summary>
    internal static string RunIdFor(ulong seed)
    {
        string hex = Guid.NewGuid().ToString("N")[..12];
        return $"run-{seed}-{hex}";
    }

    /// <summary>
    /// Identité du run à archiver dans le flux d'observabilité. Un identifiant
    /// demandé par l'appelant est retenu tel quel : il est alors déterministe,
    /// ce qu'exige l'export batch (deux runs de même graine doivent produire
    /// le même fichier <c>stream.jsonl</c>). Sans demande, on retombe sur le
    /// format généré, qui garantit l'unicité des runs observés en direct.
    /// </summary>
    private static string ResolveRunId(ulong seed, string? requestedRunId) =>
        string.IsNullOrWhiteSpace(requestedRunId) ? RunIdFor(seed) : requestedRunId;
}
