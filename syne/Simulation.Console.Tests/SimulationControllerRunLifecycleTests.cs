using Simulation.Console.Control;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Cycle de vie des runs du <see cref="SimulationController"/> (jalon
/// review/refactor, engineVersion 0.12.0).
///
/// <para>
/// La boucle de tick était lancée en « feu et forget » (<c>_ = Task.Run(...)</c>)
/// et jamais conservée. Conséquences : un second <c>StartAsync</c> superimposait
/// une deuxième boucle sur le même monde ; <c>Stop</c> n'annulait que la dernière
        /// <c>Stop</c> n'annulait que la dernière
/// celle-ci pouvait ensuite repasser l'état à <c>Finished</c> par-dessus un run
/// plus récent ; et <c>DisposeAsync</c> simulait l'attente avec
/// <c>Task.Delay(20)</c>.
/// </para>
/// </summary>
public sealed class SimulationControllerRunLifecycleTests
{
    private static string SlowProfile()
    {
        // ticksPerSecond bas : la boucle vit longtemps assez pour être observée.
        SimulationOptions options = SimulationProfiles.Reference();
        options.Simulation.TicksPerSecond = 2;
        return ConfigLoader.ToJson(options);
    }

    [Fact]
    public async Task SecondStart_OnAnActiveRun_IsRejected()
    {
        await using var controller = new SimulationController();
        await controller.StartAsync(seed: 1, configJson: SlowProfile(), maxTicks: null);

        // Deuxième boucle sur le même monde : elle mutilerait l'état partagé.
        // On réutilise volontairement le monde déjà construit (seed et config
        // absents) pour que le seul motif de refus possible soit bien la
        // détection d'un run actif, et non la règle de Prepare/ready explicite.
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.StartAsync(seed: null, configJson: null, maxTicks: null));

        Assert.Contains("déjà actif", error.Message, StringComparison.Ordinal);
        await controller.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ActuallyStopsTheLoop()
    {
        await using var controller = new SimulationController();
        await controller.StartAsync(seed: 1, configJson: SlowProfile(), maxTicks: null);
        await Task.Delay(200);

        await controller.StopAsync();
        ulong tickAtStop = controller.Status().Tick;

        // Aucun tick ne doit plus \u00eatre produit apr\u00e8s l'attente de l'arr\u00eat.
        await Task.Delay(250);
        Assert.Equal(tickAtStop, controller.Status().Tick);
        Assert.Equal(SimulationControlState.Idle, controller.State);
    }

    [Fact]
    public async Task RunId_CarriesTheEffectiveSeed_WithAUniqueSuffix()
    {
        // A1 (format canonique run-<seed>-<12hex>) : ECHOS peut dériver le seed du
        // run_id en repli (flux V0.2.0) et deux runs de même seed restent distincts.
        await using var controller = new SimulationController();
        string runId = await controller.StartAsync(seed: 12345, configJson: SlowProfile(), maxTicks: 1);

        Assert.StartsWith("run-12345-", runId);
        Assert.Matches("^run-12345-[0-9a-f]{12}$", runId);
        await controller.StopAsync();

        // Deux runs de même seed → identifiants distincts (suffixe unique).
        string second = await controller.StartAsync(seed: 12345, configJson: SlowProfile(), maxTicks: 1);
        Assert.NotEqual(runId, second);
        await controller.StopAsync();
    }

    [Fact]
    public async Task Start_IsPossibleAgain_AfterStop()
    {
        await using var controller = new SimulationController();
        await controller.StartAsync(seed: 1, configJson: SlowProfile(), maxTicks: null);
        await controller.StopAsync();

        // L'absence de fuite doit lib\u00e9rer le contr\u00f4le : le refus ne doit pas
        // \u00eatre d\u00e9finitif.
        string runId = await controller.StartAsync(seed: 1, configJson: SlowProfile(), maxTicks: 1);

        Assert.NotEmpty(runId);
        await controller.StopAsync();
    }

    [Fact]
    public async Task Start_IsPossibleAgain_AfterTheRunFinishesNaturally()
    {
        await using var controller = new SimulationController();
        await controller.StartAsync(seed: 1, configJson: SlowProfile(), maxTicks: 1);
        await WaitUntilAsync(() => controller.State == SimulationControlState.Finished, TimeSpan.FromSeconds(10));

        // Redémarrage sur le monde déjà construit (ni seed ni config) : c'est le
        // seul chemin qui n'exige pas un Prepare/ready explicite.
        string runId = await controller.StartAsync(seed: null, configJson: null, maxTicks: 1);

        Assert.NotEmpty(runId);
        await controller.StopAsync();
    }

    [Fact]
    public async Task Reset_StopsThePreviousRun_BeforeStartingTheNext()
    {
        await using var controller = new SimulationController();
        await controller.StartAsync(seed: 1, configJson: SlowProfile(), maxTicks: null);
        await Task.Delay(150);

        string runId = await controller.ResetAsync(seed: 2, maxTicks: 1);

        Assert.NotEmpty(runId);
        await WaitUntilAsync(() => controller.State == SimulationControlState.Finished, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task DisposeAsync_WaitsForTheLoop_AndIsIdempotent()
    {
        var controller = new SimulationController();
        await controller.StartAsync(seed: 1, configJson: SlowProfile(), maxTicks: null);
        await Task.Delay(150);

        await controller.DisposeAsync();
        // ne doit pas échouer définitivement.
        // d\u00e9j\u00e0 lib\u00e9r\u00e9e.
        await controller.DisposeAsync();
    }

    [Fact]
    public async Task MaxTicks_ReachesFinished_AndStopsAdvancing()
    {
        await using var controller = new SimulationController();
        await controller.StartAsync(seed: 5, configJson: SlowProfile(), maxTicks: 2);
        await WaitUntilAsync(() => controller.State == SimulationControlState.Finished, TimeSpan.FromSeconds(10));

        ulong tick = controller.Status().Tick;
        await Task.Delay(200);

        Assert.Equal(tick, controller.Status().Tick);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition(), "condition non satisfaite avant l'\u00e9ch\u00e9ance.");
    }
}
