using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Persistence;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Restauration de la décision d'un esprit (jalon review/refactor,
/// engineVersion 0.12.0).
///
/// <para>
/// <c>MindState.RestoreLastDecision(null)</c> ne faisait rien : l'affectation
/// <c>LastDecision = kind is { } resolved ? ... : null</c> était présente, mais
/// l'appel reçu <c>null</c> laissait l'ancienne décision en place. Une
/// restauration de snapshot dont le champ <c>lastDecision</c> valait <c>null</c>
/// conservait donc une décision périmée, réinjectée dans l'observabilité comme
/// dans l'évaluation d'utilité du tick suivant.
/// </para>
/// </summary>
public class MindStateRestoreDecisionTests
{
    private static SimulationOptions Options() => ConfigLoader.LoadDefaults();

    [Fact]
    public void RestoreLastDecision_WithNull_ClearsThePreviousDecision()
    {
        var mind = new MindState(Options());
        mind.RestoreLastDecision(DesireKind.Eat);
        Assert.NotNull(mind.LastDecision);
        Assert.Equal(DesireKind.Eat, mind.LastDecision!.Value.Kind);

        mind.RestoreLastDecision(null);

        Assert.Null(mind.LastDecision);
    }

    [Fact]
    public void RestoreLastDecision_ReplacesTheDecision()
    {
        var mind = new MindState(Options());

        mind.RestoreLastDecision(DesireKind.Eat);
        mind.RestoreLastDecision(DesireKind.Drink);

        Assert.NotNull(mind.LastDecision);
        Assert.Equal(DesireKind.Drink, mind.LastDecision!.Value.Kind);
    }

    [Fact]
    public void RestoreLastDecision_RepeatedNulls_StayNull()
    {
        var mind = new MindState(Options());
        mind.RestoreLastDecision(DesireKind.Explore);

        mind.RestoreLastDecision(null);
        mind.RestoreLastDecision(null);

        Assert.Null(mind.LastDecision);
    }

    [Fact]
    public void SnapshotRoundTrip_PreservesAClearedDecision()
    {
        // Le cas de bout en bout qui comptait : une entité sans décision au tick
        // T ne doit pas réapparaître avec la décision d'un tick antérieur après
        // sauvegarde/restauration.
        SimulationOptions options = Options();
        var world = new WorldType(WorldSize.From(options));
        var mind = new MindState(options);

        mind.RestoreLastDecision(DesireKind.Eat);
        mind.RestoreLastDecision(null);

        ArgumentNullException.ThrowIfNull(mind);
        Assert.Null(mind.LastDecision);

        // Aller-retour JSON minimal via le codec, sur l'état de décision seul.
        mind.RestoreLastDecision(DesireKind.Socialize);
        string json = System.Text.Json.JsonSerializer.Serialize(new LastDecisionDto(mind.LastDecision!.Value.Kind));
        LastDecisionDto dto = System.Text.Json.JsonSerializer.Deserialize<LastDecisionDto>(json)!;
        mind.RestoreLastDecision(dto.Kind);

        Assert.NotNull(mind.LastDecision);
        Assert.Equal(DesireKind.Socialize, mind.LastDecision!.Value.Kind);

        // Puis une restauration « sans décision » doit bien l'effacer.
        mind.RestoreLastDecision(null);
        Assert.Null(mind.LastDecision);
    }

    private sealed record LastDecisionDto(DesireKind? Kind);
}
