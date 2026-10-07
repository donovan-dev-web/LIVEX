using Launcher.Application;
using Launcher.Domain;
using Launcher.Presentation.ViewModel;
using Xunit;

namespace Launcher.Tests.Unit.Presentation;

/// <summary>
/// Avancement d'un run dans l'interface (EXPERIMENTS.md §8). La règle qui compte :
/// n'afficher un pourcentage que si le moteur a rapporté un horizon — sinon la barre
/// afficherait une progression qui n'existe pas.
/// </summary>
public sealed class RunTickProgressViewModelTests
{
    private static MainWindowViewModel ViewModel() => new(new FakeOrchestrationFacade());

    private static CampaignProgress Progress(RunTickProgress? tick) => new()
    {
        ExperimentId = "EXP-2026-001",
        CurrentRunId = "RUN-0001",
        CurrentSeed = 500,
        RunsDone = 0,
        RunsFailed = 0,
        RunsTotal = 1,
        CurrentRunProgress = tick,
    };

    [Fact]
    public void Un_horizon_reporte_permet_le_pourcentage_et_le_texte()
    {
        var viewModel = ViewModel();

        viewModel.UpdateCampaignProgress(Progress(new RunTickProgress(250, 1000, 50, "running")));

        Assert.True(viewModel.HasRunTickProgress);
        Assert.Equal(25d, viewModel.RunTickPercent);
        Assert.Contains("250", viewModel.RunTickText, StringComparison.Ordinal);
        Assert.Contains("1000", viewModel.RunTickText, StringComparison.Ordinal);
        Assert.Contains("50", viewModel.RunTickText, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_moteur_muets_n_affiche_aucun_pourcentage()
    {
        var viewModel = ViewModel();

        viewModel.UpdateCampaignProgress(Progress(tick: null));

        // La progression de campagne, elle, reste affichée.
        Assert.Contains("RUN-0001", viewModel.CampaignProgressText, StringComparison.Ordinal);
        Assert.False(viewModel.HasRunTickProgress);
        Assert.Equal(0d, viewModel.RunTickPercent);
        Assert.Equal(string.Empty, viewModel.RunTickText);
    }

    [Fact]
    public void Un_horizon_absent_n_invente_pas_de_pourcentage()
    {
        var viewModel = ViewModel();

        // Le moteur ne dit pas jusqu'où il va : l'interface montre le tic, pas un ratio.
        viewModel.UpdateCampaignProgress(Progress(new RunTickProgress(250, null, 50, "running")));

        Assert.False(viewModel.HasRunTickProgress);
        Assert.Equal(0d, viewModel.RunTickPercent);
        Assert.Contains("250", viewModel.RunTickText, StringComparison.Ordinal);
        Assert.DoesNotContain("/", viewModel.RunTickText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 0d)]
    [InlineData(1000, 100d)]
    [InlineData(5000, 100d)] // Un moteur qui dépasse son horizon ne doit pas afficher plus de 100 %.
    [InlineData(-5, 0d)]
    public void Le_pourcentage_reste_borne_entre_0_et_100(long tick, double expected)
    {
        var viewModel = ViewModel();

        viewModel.UpdateCampaignProgress(Progress(new RunTickProgress(tick, 1000, 1, "running")));

        Assert.Equal(expected, viewModel.RunTickPercent);
    }

    [Fact]
    public void Un_horizon_nul_est_traité_comme_absent()
    {
        var viewModel = ViewModel();

        // Diviser par un horizon nul donnerait NaN ou l'infini dans la barre.
        viewModel.UpdateCampaignProgress(Progress(new RunTickProgress(10, 0, 1, "running")));

        Assert.False(viewModel.HasRunTickProgress);
        Assert.Equal(0d, viewModel.RunTickPercent);
    }
}