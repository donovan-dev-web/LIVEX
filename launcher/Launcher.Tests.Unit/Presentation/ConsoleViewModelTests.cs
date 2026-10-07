using Launcher.Domain;
using Launcher.Presentation.ViewModel;
using Xunit;

namespace Launcher.Tests.Unit.Presentation;

/// <summary>
/// Console de composant (USER_INTERFACE.md §9). Les règles qui comptent : rien ne s'affiche sans
/// relevé, la pause retient sans perdre, un filtre ne fabrique jamais de retard, et
/// l'effacement ne rejoue pas un flux déjà lu.
/// </summary>
public sealed class ConsoleViewModelTests
{
    private const string Instance = "echos-0001";

    [Fact]
    public void Les_lignes_recues_sont_affichees_au_releve_suivant()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "Console — ECHOS", string.Empty);
        source.Publish(Instance, "stdout", "démarrage");

        Assert.True(console.Poll());
        Assert.Single(console.Lines);
        Assert.Equal("démarrage", console.Lines[0].Text);
        Assert.False(console.Lines[0].IsError);
    }

    [Fact]
    public void Une_ligne_d_erreur_est_marquee_comme_telle()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);
        source.Publish(Instance, "stderr", "boom");

        console.Poll();

        Assert.True(console.Lines[0].IsError);
    }

    [Fact]
    public void Un_releve_sans_nouvelle_ligne_ne_rend_vrai_que_si_ajout()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);

        Assert.False(console.Poll());
        source.Publish(Instance, "stdout", "a");
        Assert.True(console.Poll());
        Assert.False(console.Poll());
    }

    [Fact]
    public void La_pause_retient_les_lignes_jusqu_a_la_reprise()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);
        console.ActionCommand.Execute("pause");
        source.Publish(Instance, "stdout", "pendant la pause");

        Assert.False(console.Poll());
        Assert.Empty(console.Lines);

        console.ActionCommand.Execute("pause");
        Assert.True(console.Poll());
        Assert.Single(console.Lines);
        Assert.Equal("pendant la pause", console.Lines[0].Text);
    }

    [Fact]
    public void Le_filtre_standard_masque_stdout_sans_perdre_la_suite()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);
        source.Publish(Instance, "stdout", "info");
        console.Poll();

        console.ShowStdout = false;
        Assert.Empty(console.Lines);

        source.Publish(Instance, "stderr", "erreur");
        Assert.True(console.Poll());
        Assert.Single(console.Lines);
        Assert.Equal("erreur", console.Lines[0].Text);
    }

    [Fact]
    public void Un_filtre_retabli_restitue_l_historique_retenu()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);
        source.Publish(Instance, "stdout", "avant");
        source.Publish(Instance, "stderr", "après");
        console.ShowStdout = false;
        console.Poll();
        Assert.Single(console.Lines);

        console.ShowStdout = true;

        Assert.Equal(2, console.Lines.Count);
        Assert.Equal("avant", console.Lines[0].Text);
    }

    [Fact]
    public void Effacer_repart_de_zero_sans_rejouer_le_flux_deja_lu()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);
        source.Publish(Instance, "stdout", "lue");
        console.Poll();

        console.ActionCommand.Execute("clear");
        Assert.Empty(console.Lines);

        Assert.False(console.Poll());
        Assert.Empty(console.Lines);

        source.Publish(Instance, "stdout", "suivante");
        Assert.True(console.Poll());
        Assert.Single(console.Lines);
        Assert.Equal("suivante", console.Lines[0].Text);
    }

    [Fact]
    public void Une_autre_instance_ne_paraît_pas_dans_la_console()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);
        source.Publish("syne-0001", "stdout", "ailleurs");

        Assert.False(console.Poll());
        Assert.Empty(console.Lines);
    }

    [Fact]
    public void Le_releve_est_plafonne_pour_ne_pas_figer_la_vue()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);
        for (var index = 0; index < 2500; index++)
        {
            source.Publish(Instance, "stdout", $"ligne {index}");
        }

        console.Poll();
        Assert.Equal(2000, console.Lines.Count);

        console.Poll();
        Assert.Equal(2500, console.Lines.Count);
    }

    [Fact]
    public void Les_lignes_affichees_reste_bornees_meme_sur_un_flux_long()
    {
        var source = new FakeLogSource();
        var console = new ConsoleViewModel(source, Instance, "t", string.Empty);
        for (var index = 0; index < 5600; index++)
        {
            source.Publish(Instance, "stdout", $"ligne {index}");
        }

        while (console.Poll())
        {
        }

        Assert.Equal(5000, console.Lines.Count);
        // Les dernières lignes sont conservées, les premières sont retirées.
        Assert.Equal("ligne 600", console.Lines[0].Text);
    }

    /// <summary>Source mémoire de test : publication explicite, lecture filtrée par instance.</summary>
    private sealed class FakeLogSource : IComponentLogSource
    {
        private readonly List<ComponentLogLine> _lines = new();
        private long _sequence;

        public void Publish(string instanceId, string stream, string text) => _lines.Add(new ComponentLogLine
        {
            Sequence = ++_sequence,
            Timestamp = DateTimeOffset.UtcNow,
            InstanceId = instanceId,
            Stream = stream,
            Text = text,
        });

        public IReadOnlyList<ComponentLogLine> ReadSince(string instanceId, long afterSequence) =>
            _lines.Where(line => line.InstanceId == instanceId && line.Sequence > afterSequence).ToList();

        public long LatestSequence(string instanceId) =>
            _lines.Where(line => line.InstanceId == instanceId).Select(line => line.Sequence).DefaultIfEmpty(0).Max();
    }
}
