using System.Diagnostics;
using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;

namespace Prism.Host.Ticking;

public sealed class AgentPerceptionTickPhase(
    AgentLifecycleSystem? lifecycle,
    StructureConstructionSystem? construction = null) : IWorldTickPhaseHandler
{
    private readonly StructureEffectConsumer? _structureEffects =
        construction is null ? null : new StructureEffectConsumer(construction);

    // TEMPORARY PRISM-141 probe; removed once the allocation split is known.
    private static readonly bool AllocationProbe =
        Environment.GetEnvironmentVariable("PRISM_ALLOC_PROBE") == "1";

    public WorldTickPhase Phase => WorldTickPhase.Perception;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (lifecycle is null)
        {
            return;
        }

        long observeStart = AllocationProbe ? GC.GetAllocatedBytesForCurrentThread() : 0;
        long observeStamp = AllocationProbe ? Stopwatch.GetTimestamp() : 0;
        // Le tick ne sert qu'aux diagnostics de vérification des caches (PRISM-108) : il permet
        // de nommer le tick où une fenêtre mémorisée a menti, sans entrer dans la trame observée.
        var observations = lifecycle.ObserveAgents(
            construction?.Sites, _structureEffects, groupMembership: null, context.Tick);
        long observeAllocated = AllocationProbe
            ? GC.GetAllocatedBytesForCurrentThread() - observeStart
            : 0;
        double observeMs = AllocationProbe
            ? (Stopwatch.GetTimestamp() - observeStamp) * 1000.0 / Stopwatch.Frequency
            : 0;
        int gc0Count = AllocationProbe ? GC.CollectionCount(0) : 0;
        int gc2Count = AllocationProbe ? GC.CollectionCount(2) : 0;
        double gcPauseMs = AllocationProbe ? GC.GetTotalPauseDuration().TotalMilliseconds : 0;
        long loopStamp = AllocationProbe ? Stopwatch.GetTimestamp() : 0;
        long envelopeStart = AllocationProbe ? GC.GetAllocatedBytesForCurrentThread() : 0;
        foreach (var observation in observations)
        {
            // Charges utiles typées, jamais de document JSON : l'enveloppe les écrit
            // directement dans le flux (ObservationEnvelopeConverter). Passer par
            // SerializeToElement matérialisait un DOM par agent et par tick — 164 Mo
            // au premier tick, 24,7 Mo ensuite à 1 000 agents — pour un JSON identique.
            context.AddObservation(new ObservationEnvelope(
                ProtocolVersion.Current.ToString(),
                context.Tick,
                observation.AgentId,
                new { changes = observation.Frame.Changes },
                observation.Frame.Self));
        }

        if (AllocationProbe)
        {
            var p = typeof(Prism.Core.Perception.PerceptionSystem);
            long Get(string name) => (long)p.GetField(name)!.GetValue(null)!;
            void Reset(string name) => p.GetField(name)!.SetValue(null, 0L);

            long envelopeAllocated = GC.GetAllocatedBytesForCurrentThread() - envelopeStart;
            const double Mb = 1024 * 1024.0;
            double Ms(string name) => Get(name) * 1000.0 / Stopwatch.Frequency;
            Console.Error.WriteLine(
                $"[probe] tick {context.Tick}: observe={observeAllocated / Mb:F1}Mo/{observeMs:F1}ms " +
                $"pause=+{(GC.GetTotalPauseDuration().TotalMilliseconds - gcPauseMs):F1}ms " +
                $"gc0=+{(GC.CollectionCount(0) - gc0Count)} gc2=+{(GC.CollectionCount(2) - gc2Count)} " +
                $"env={envelopeAllocated / Mb:F1}Mo/{(Stopwatch.GetTimestamp() - loopStamp) * 1000.0 / Stopwatch.Frequency:F1}ms | " +
                $"window={Get("ProbeWindowBytes") / Mb:F1} ground={Get("ProbeGroundBytes") / Mb:F1} " +
                $"agents={Get("ProbeAgentsBytes") / Mb:F1} summaries={Get("ProbeSummariesBytes") / Mb:F1} | " +
                $"collectChanges={Get("ProbeCollectChangesBytes") / Mb:F1} disappear={Get("ProbeDisappearBytes") / Mb:F1} " +
                $"sort={Get("ProbeSortBytes") / Mb:F1} copy={Get("ProbeCopyBytes") / Mb:F1} update={Get("ProbeUpdateBytes") / Mb:F1} " +
                $"tail={Get("ProbeTailBytes") / Mb:F1} | counts blocks={Get("ProbeBlocksCount")} extras={Get("ProbeExtrasCount")} " +
                $"entries={Get("ProbeEntriesCount")} pending={Get("ProbePendingCount")} selected={Get("ProbeSelectedCount")} compares={Get("ProbeCompareCount")} " +
                $"| win builds={lifecycle.PerceptionWindowCounters.Builds} slides={lifecycle.PerceptionWindowCounters.Slides} " +
                $"patches={lifecycle.PerceptionWindowCounters.Patches} moveRebuilds={lifecycle.PerceptionWindowCounters.MoveRebuilds} " +
                $"| cand agents={lifecycle.PerceptionCandidates.Agents} items={lifecycle.PerceptionCandidates.Items} " +
                $"| ms window={Ms("ProbeWindowTicks") / 1000.0:F1} extras={Ms("ProbeExtrasTicks") / 1000.0:F1} " +
                $"diff={Ms("ProbeDiffTicks") / 1000.0:F1} tail={Ms("ProbeTailTicks") / 1000.0:F1} " +
                $"collect={Ms("ProbeCollectTicks") / 1000.0:F1} sort={Ms("ProbeSortTicks") / 1000.0:F1} " +
                $"copy={Ms("ProbeCopyTicks") / 1000.0:F1} update={Ms("ProbeUpdateTicks") / 1000.0:F1} " +
                $"setup={Ms("ProbeSetupTicks") / 1000.0:F1} shelter={Ms("ProbeShelterTicks") / 1000.0:F1} " +
                $"obs={Ms("ProbeObserveCallTicks") / 1000.0:F1} snap={Ms("ProbeSnapshotTicks") / 1000.0:F1}");

            foreach (var name in new[]
                     {
                         "ProbeWindowBytes", "ProbeGroundBytes", "ProbeAgentsBytes", "ProbeSitesBytes",
                         "ProbeSummariesBytes", "ProbeCollectBytes", "ProbeTailBytes", "ProbeCollectChangesBytes",
                         "ProbeDisappearBytes", "ProbeSortBytes", "ProbeCopyBytes", "ProbeUpdateBytes",
                         "ProbePendingCount", "ProbeEntriesCount", "ProbeBlocksCount", "ProbeExtrasCount",
                         "ProbeSelectedCount", "ProbeCompareCount", "ProbeWindowTicks",
                         "ProbeExtrasTicks", "ProbeDiffTicks", "ProbeTailTicks",
                         "ProbeCollectTicks", "ProbeSortTicks", "ProbeCopyTicks", "ProbeUpdateTicks",
                         "ProbeSetupTicks", "ProbeShelterTicks", "ProbeObserveCallTicks", "ProbeSnapshotTicks",
                     })
            {
                Reset(name);
            }
        }
    }
}
