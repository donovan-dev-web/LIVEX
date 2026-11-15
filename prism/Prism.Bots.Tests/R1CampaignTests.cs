using Prism.Content;
using Prism.Host.Hosting;

namespace Prism.Bots.Tests;

/// <summary>
/// Reduced run R1 (PRISM-102): five planner agents, one per production chain, played through the
/// ordinary contract against the real host. The test pins the gate harness rather than the chain
/// completion: what it proves today is that the campaign runs, that the matter audit stays green
/// and that the run is reproducible. Chain outcomes are reported by the harness and tracked in
/// <c>docs/prism/ISSUES.md</c>; a full five-chain pass is still the G4 blocker.
/// <para>
/// The pinned value is a <b>byte-level</b> fingerprint (PRISM-107): the serialized stream in its
/// published order. A performance change is judged on the trajectory level instead — see
/// <see cref="TwoLevelFingerprintTests"/> and <c>docs/prism/TESTING.md</c> §3.1.
/// </para>
/// </summary>
public sealed class R1CampaignTests
{
    [Fact]
    public void FiveChainCampaignRunsReproduciblyWithAGreenConservationAudit()
    {
        var root = Path.Combine(BotRunnerTests.FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;
        var content = BotContentMap.FromContent(pack);

        var first = RunCampaign(root, pack, content, seed: 1UL, agents: 5, ticks: 200);
        var second = RunCampaign(root, pack, content, seed: 1UL, agents: 5, ticks: 200);

        // The audit is the gate: what the ledger says was created or destroyed must match what
        // agents, containers and the ground actually hold.
        Assert.True(first.ConservationGreen, Describe(first));
        Assert.Equal(first.Run.FingerprintHex, second.Run.FingerprintHex);

        // Pinned like the golden campaign: a changed fingerprint means the campaign changed, so
        // the reason belongs in docs/prism/ISSUES.md (PRISM-102) before this constant moves.
        // 9 novembre 2026 : le graphe de déplacement accepte la cellule d'un trou miné (un établi
        // ne finit plus sur un pilier), une station se pose sur le sol de la colonne voisine, et
        // une source de minerai ne compte que si son approche est dans la composante du spawn —
        // la chaîne outils termine, 5/5 — voir §13 du rapport G4.
        Assert.Equal("70455f3b3b4068e6", first.Run.FingerprintHex);
        Assert.Equal(R1Campaign.Chains.Count, first.Agents.Count);
        Assert.Contains(first.Agents, agent => agent.Chain == "tools");
        Assert.Contains(first.Agents, agent => agent.Chain == "agriculture");
        Assert.Contains(first.Agents, agent => agent.Chain == "water");
        Assert.Contains(first.Agents, agent => agent.Chain == "cooking");
        Assert.Contains(first.Agents, agent => agent.Chain == "storage");
        Assert.All(first.Agents, agent => Assert.True(agent.PlanSteps > 0));

        // Two chains must never start around the same tree: the second one used to answer
        // `block_already_mined` at tick 36 while the first was still cutting it.
        Assert.Equal(
            first.Agents.Count,
            first.Agents.Select(agent => (agent.Spawn.X, agent.Spawn.Y, agent.Spawn.Z)).Distinct().Count());
        for (var left = 0; left < first.Agents.Count; left++)
        {
            for (var right = left + 1; right < first.Agents.Count; right++)
            {
                var a = first.Agents[left].Spawn;
                var b = first.Agents[right].Spawn;
                Assert.True(
                    Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) >= R1Campaign.MinimumSpawnSeparation,
                    $"agents {left + 1} and {right + 1} start too close: ({a.X},{a.Y}) and ({b.X},{b.Y})");
            }
        }
    }

    private static R1RunResult RunCampaign(
        string root,
        CompiledContentPack pack,
        BotContentMap content,
        ulong seed,
        int agents,
        long ticks)
    {
        var runtime = new PrismHostRuntime(checkpointOnStop: false);
        runtime.Prepare(new PrepareWorldRequest(seed, pack.ContentHash, root));
        runtime.Start(new StartWorldRequest());
        return R1Campaign.Run(runtime, content, agents, ticks);
    }

    private static string Describe(R1RunResult result) =>
        string.Join(
            " | ",
            result.Agents.Select(agent =>
                $"{agent.Chain}: {agent.PlanSteps - agent.RejectedSteps.Count}/{agent.PlanSteps} steps, rejected={string.Join(",", agent.RejectedSteps)}")) +
        $" | violations={string.Join(",", result.Violations.Select(violation => $"{violation.ItemTypeId}:{violation.Difference}"))}";
}
