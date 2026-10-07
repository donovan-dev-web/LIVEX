using Launcher.Domain;
using Launcher.Infrastructure;
using Xunit;

namespace Launcher.Tests.Integration.Infrastructure;

/// <summary>
/// Tampon des lignes de composant (USER_INTERFACE.md §9) : les consoles lisent un flux déjà long sans
/// jamais le perdre, et le débordement retient la fin plutôt que la tête.
/// </summary>
public sealed class ComponentLogBufferTests
{
    private const string Instance = "echos-0001";

    [Fact]
    public void Les_lignes_sont_conservees_dans_l_ordre_de_leur_sequence()
    {
        var processes = new FakeProcessManager();
        using var buffer = new ComponentLogBuffer(processes);

        processes.Emit(Instance, "stdout", "premiere");
        processes.Emit(Instance, "stderr", "deuxieme");

        var lines = buffer.ReadSince(Instance, 0);
        Assert.Equal(2, lines.Count);
        Assert.Equal("premiere", lines[0].Text);
        Assert.Equal("stdout", lines[0].Stream);
        Assert.True(lines[0].Sequence < lines[1].Sequence);
        Assert.Equal(lines[^1].Sequence, buffer.LatestSequence(Instance));
    }

    [Fact]
    public void Une_lecture_renvoie_uniquement_ce_qui_a_suivi_le_curseur()
    {
        var processes = new FakeProcessManager();
        using var buffer = new ComponentLogBuffer(processes);
        processes.Emit(Instance, "stdout", "une");
        processes.Emit(Instance, "stdout", "deux");

        var first = buffer.ReadSince(Instance, 0);
        Assert.Equal(2, first.Count);

        var rest = buffer.ReadSince(Instance, first[0].Sequence);
        Assert.Single(rest);
        Assert.Equal("deux", rest[0].Text);
        Assert.Empty(buffer.ReadSince(Instance, rest[0].Sequence));
    }

    [Fact]
    public void Une_instance_inconnue_ne_rend_aucune_ligne()
    {
        var processes = new FakeProcessManager();
        using var buffer = new ComponentLogBuffer(processes);
        processes.Emit(Instance, "stdout", "ici");

        Assert.Empty(buffer.ReadSince("syne-0001", 0));
        Assert.Equal(0, buffer.LatestSequence("syne-0001"));
    }

    [Fact]
    public void Le_debordement_retient_la_fin_du_flux_et_non_la_tete()
    {
        var processes = new FakeProcessManager();
        using var buffer = new ComponentLogBuffer(processes);
        var total = 20000 + 250;
        for (var index = 0; index < total; index++)
        {
            processes.Emit(Instance, "stdout", $"ligne {index}");
        }

        var lines = buffer.ReadSince(Instance, 0);

        Assert.Equal(20000, lines.Count);
        Assert.Equal("ligne 250", lines[0].Text);
        Assert.Equal($"ligne {total - 1}", lines[^1].Text);
        Assert.Equal(lines[^1].Sequence, buffer.LatestSequence(Instance));
    }

    [Fact]
    public void La_desabonnement_arrete_la_collecte()
    {
        var processes = new FakeProcessManager();
        var buffer = new ComponentLogBuffer(processes);
        processes.Emit(Instance, "stdout", "avant");
        buffer.Dispose();

        processes.Emit(Instance, "stdout", "apres");

        Assert.Empty(buffer.ReadSince(Instance, 0));
    }

    /// <summary>Gestionnaire de processus factice : il ne fait que lever les lignes à publier.</summary>
    private sealed class FakeProcessManager : IProcessManager
    {
        private long _sequence;

        public event EventHandler<ProcessExitedEventArgs>? Exited
        {
            add { }
            remove { }
        }

        public event EventHandler<ComponentLogLineEventArgs>? LineEmitted;

        public void Emit(string instanceId, string stream, string text) =>
            LineEmitted?.Invoke(this, new ComponentLogLineEventArgs
            {
                Line = new ComponentLogLine
                {
                    Sequence = ++_sequence,
                    Timestamp = DateTimeOffset.UtcNow,
                    InstanceId = instanceId,
                    Stream = stream,
                    Text = text,
                },
            });

        public Task<int> StartAsync(ProcessLaunchSpec spec, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProcessExit> StopAsync(string instanceId, int processId, Uri controlEndpoint, string sessionToken, TimeSpan graceful, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task KillAsync(string instanceId, int processId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public bool IsAlive(int processId) => false;
    }
}
