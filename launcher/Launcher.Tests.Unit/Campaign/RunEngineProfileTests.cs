using System.Text.Json;
using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Xunit;

namespace Launcher.Tests.Unit.Campaign;

/// <summary>
/// Le profil moteur effectif (EXPERIMENTS.md §12) : la surcouche remise au moteur et
/// le document qui en atteste doivent décrire la même configuration, sans valeur
/// implicite, et rester reproductibles d'un run à l'autre.
/// </summary>
public sealed class RunEngineProfileTests
{
    private static RunSpec Spec(string runId = "RUN-0001", long seed = 500, int attempt = 1) => new()
    {
        ExperimentId = "EXP-2026-001",
        RunId = runId,
        Seed = seed,
        Ticks = 1000,
        AgentCount = 50,
        Simulation = "reference",
        Attempt = attempt,
        WorkDirectory = "/tmp/run",
        SessionToken = "jeton-aleatoire",
    };

    private static ExperimentDefinition Definition() => new()
    {
        Id = "EXP-2026-001",
        Title = "Campagne de test",
        Profile = WellKnownProfiles.SimulationSeule,
        Simulation = "reference",
        RunCount = 1,
        Ticks = 1000,
        AgentCount = 50,
        SeedStrategy = SeedStrategy.Derived,
        BaseSeed = 500,
        FailurePolicy = FailurePolicy.Stop,
        MaxRetries = 1,
    };

    [Fact]
    public void La_surcouche_declare_le_nombre_d_agents_et_la_cadence_batch()
    {
        using var overlay = JsonDocument.Parse(
            JsonSerializer.Serialize(RunEngineProfile.ConfigOverlay(Spec())));

        Assert.Equal(50, overlay.RootElement.GetProperty("agents").GetProperty("initialCount").GetInt32());
        Assert.Equal(
            RunEngineProfile.BatchTicksPerSecond,
            overlay.RootElement.GetProperty("simulation").GetProperty("ticksPerSecond").GetInt32());
    }

    [Fact]
    public void La_surcouche_ne_declare_que_la_cadence_et_les_agents()
    {
        // Une surcouche plus large écraserait le profil par défaut du moteur : le
        // chargeur fusionne le JSON brut, donc chaque clé présente est une décision.
        using var overlay = JsonDocument.Parse(
            JsonSerializer.Serialize(RunEngineProfile.ConfigOverlay(Spec())));

        Assert.Equal(
            new[] { "agents", "simulation" },
            overlay.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "initialCount" },
            overlay.RootElement.GetProperty("agents").EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            new[] { "ticksPerSecond" },
            overlay.RootElement.GetProperty("simulation").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Le_document_resolu_atteste_la_configuration_du_run()
    {
        using var resolved = JsonDocument.Parse(RunEngineProfile.SerializeResolved(Spec(), Definition()));

        var run = resolved.RootElement.GetProperty("run");
        Assert.Equal("EXP-2026-001", run.GetProperty("experimentId").GetString());
        Assert.Equal("RUN-0001", run.GetProperty("runId").GetString());
        Assert.Equal(1, run.GetProperty("attempt").GetInt32());
        Assert.Equal("reference", run.GetProperty("simulation").GetString());
        // La graine dérivée, pas la graine de base de la campagne.
        Assert.Equal(500, run.GetProperty("seed").GetInt64());
        Assert.Equal(1000, run.GetProperty("ticks").GetInt64());
        Assert.Equal(50, run.GetProperty("agentCount").GetInt32());

        var engine = resolved.RootElement.GetProperty("engine");
        Assert.Equal("syne", engine.GetProperty("component").GetString());
        Assert.True(engine.GetProperty("headless").GetBoolean());
        Assert.True(engine.GetProperty("autoStart").GetBoolean());
        Assert.True(engine.GetProperty("exportStream").GetBoolean());
        Assert.Equal("EXP-2026-001-RUN-0001", engine.GetProperty("analyticsRunId").GetString());

        // La cadence est archivée : sans elle, le paquet ne prouverait pas que le run
        // n'a pas été rejoué en temps réel.
        Assert.Equal(
            RunEngineProfile.BatchTicksPerSecond,
            engine.GetProperty("configOverlay").GetProperty("simulation").GetProperty("ticksPerSecond").GetInt32());
        Assert.Equal(
            50,
            engine.GetProperty("configOverlay").GetProperty("agents").GetProperty("initialCount").GetInt32());
    }

    [Fact]
    public void Le_document_resolu_ne_contient_aucune_valeur_de_transport()
    {
        // Port, jeton de session et corrélation sont propres à une exécution : les
        // inscrire rendrait le document différent à chaque run, or un paquet scellé se
        // compare octet pour octet (PACKAGE_FORMAT.md §6).
        var json = RunEngineProfile.SerializeResolved(Spec(), Definition());

        Assert.DoesNotContain("jeton-aleatoire", json, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "controlPort", "sessionToken", "correlationId", "workDirectory" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Le_document_resolu_est_reproductible_pour_un_meme_run()
    {
        var first = RunEngineProfile.SerializeResolved(Spec(), Definition());
        var second = RunEngineProfile.SerializeResolved(Spec(), Definition());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Le_document_resolu_distingue_les_graines_derivees_de_deux_runs()
    {
        var first = RunEngineProfile.SerializeResolved(Spec("RUN-0001", seed: 500), Definition());
        var second = RunEngineProfile.SerializeResolved(Spec("RUN-0002", seed: 501), Definition());

        Assert.NotEqual(first, second);
        using var resolved = JsonDocument.Parse(second);
        Assert.Equal(501, resolved.RootElement.GetProperty("run").GetProperty("seed").GetInt64());
    }
}