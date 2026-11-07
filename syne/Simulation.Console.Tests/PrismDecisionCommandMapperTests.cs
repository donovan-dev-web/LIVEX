// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Prism.Contracts;
using Simulation.Console.Integration;
using Simulation.Core.Cognition;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismDecisionCommandMapperTests
{
    private static DecisionRecord Decision(
        ulong entityId,
        DesireKind chosen,
        double x = 10.5,
        double y = 20.5,
        bool withTarget = true,
        ulong tick = 7,
        DecisionState state = DecisionState.New,
        bool missing = false) =>
        new(
            tick,
            entityId,
            Deliberated: true,
            Interrupted: false,
            chosen,
            Utility: 0.5,
            Scores: [],
            State: state,
            Missing: missing,
            Target: withTarget
                ? new GoalTarget("node", 42UL, "resource-water-1", new Position(x, y), tick, 1.0)
                : null);

    private static PrismBlockPosition Map(Position position) => new(
        WorldSpace.BlockOf(position.X),
        WorldSpace.BlockOf(position.Y),
        0);

    [Fact]
    public void Map_WritesTheCoordinateDirectlyUnderTarget()
    {
        // Le contrat PRISM attend `target: {x,y,z}` numériques
        // (CommandPayloadValidator.TryRequireCoordinate) : l'enveloppe
        // { type, id, position } était refusée `invalid_payload`.
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [Decision(5, DesireKind.SeekWater)],
            Map);

        CommandEnvelope command = Assert.Single(commands);
        Assert.Equal("Aller", command.Kind);
        Assert.Equal(5UL, command.AgentId);
        Assert.Equal(7, command.Tick);
        Assert.Equal("syne:7:5:Aller", command.CommandId);
        Assert.True(CommandPayloadValidator.TryValidate(command.Kind, command.Payload, out string? detail), detail);
        Assert.Equal(10, command.Payload.GetProperty("target").GetProperty("x").GetInt32());
        Assert.Equal(20, command.Payload.GetProperty("target").GetProperty("y").GetInt32());
        Assert.Equal(0, command.Payload.GetProperty("target").GetProperty("z").GetInt32());
    }

    [Fact]
    public void Map_DrinkTargetsTheSourceBlockResolvedByTheCaller()
    {
        // La cible cognitive est la case d'approche ; le bloc buvable est à côté.
        DecisionRecord decision = Decision(9, DesireKind.Drink);
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [decision],
            Map,
            _ => new PrismBlockPosition(3, 4, 11));

        CommandEnvelope command = Assert.Single(commands);
        Assert.Equal("Consommer", command.Kind);
        Assert.True(CommandPayloadValidator.TryValidate(command.Kind, command.Payload, out string? detail), detail);
        Assert.Equal(3, command.Payload.GetProperty("target").GetProperty("x").GetInt32());
        Assert.Equal(11, command.Payload.GetProperty("target").GetProperty("z").GetInt32());
    }

    [Fact]
    public void Map_FallsBackToTheTargetPositionWhenNoSourceIsKnown()
    {
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [Decision(9, DesireKind.Drink)],
            Map);

        CommandEnvelope command = Assert.Single(commands);
        Assert.Equal("Consommer", command.Kind);
        Assert.Equal(10, command.Payload.GetProperty("target").GetProperty("x").GetInt32());
    }

    [Theory]
    [InlineData(DesireKind.Idle)]
    [InlineData(DesireKind.Rest)]
    [InlineData(DesireKind.Eat)]
    [InlineData(DesireKind.Give)]
    [InlineData(DesireKind.Take)]
    public void Map_SkipsActionsWithoutPrismEquivalent(DesireKind chosen)
    {
        Assert.Empty(PrismDecisionCommandMapper.Map(7, [Decision(3, chosen)], Map));
    }

    private static PrismCommandVocabulary Vocabulary(
        ushort[] edible,
        ushort[]? carried = null) =>
        new(PrismItemCatalog.FromIds(edible), carried);

    [Fact]
    public void Map_AcquiresFoodThroughTheMacroAction_WhenTheContentIsKnown()
    {
        // Étape 3b : avec le contenu lisible, la faim devient une **acquisition**
        // (PRISM cherche, rejoint, prélève) au lieu d'un simple déplacement vers
        // une cible que SYNE ne peut pas faire aboutir.
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [Decision(5, DesireKind.SeekFood)],
            Map,
            mapSource: null,
            vocabulary: Vocabulary([3]));

        CommandEnvelope command = Assert.Single(commands);
        Assert.Equal("ObtenirObjet", command.Kind);
        Assert.Equal("syne:7:5:ObtenirObjet", command.CommandId);
        Assert.True(CommandPayloadValidator.TryValidate(command.Kind, command.Payload, out string? detail), detail);
        Assert.Equal("edible", command.Payload.GetProperty("tag").GetString());
        Assert.Equal(1, command.Payload.GetProperty("quantity").GetInt32());
        Assert.Equal(PrismDecisionCommandMapper.AcquisitionHintRadius, command.Payload.GetProperty("hintRadius").GetInt32());
        Assert.Equal(10, command.Payload.GetProperty("hint").GetProperty("x").GetInt32());
        Assert.Equal(20, command.Payload.GetProperty("hint").GetProperty("y").GetInt32());
    }

    [Fact]
    public void Map_AcquiresFoodForSecureFoodToo()
    {
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [Decision(5, DesireKind.SecureFood)],
            Map,
            mapSource: null,
            vocabulary: Vocabulary([3]));

        Assert.Equal("ObtenirObjet", Assert.Single(commands).Kind);
    }

    [Fact]
    public void Map_KeepsThePlainMove_WhenTheContentIsNotReadable()
    {
        // Sans catalogue, aucune commande nouvelle : le pont retombe sur le
        // vocabulaire minimal, comportement identique à avant l'étape 3b.
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [Decision(5, DesireKind.SeekFood)],
            Map,
            mapSource: null,
            vocabulary: new PrismCommandVocabulary(PrismItemCatalog.Empty));

        Assert.Equal("Aller", Assert.Single(commands).Kind);
    }

    [Fact]
    public void Map_EatsACarriedItem_WhenTheCatalogIdentifiesIt()
    {
        // Manger ne vise aucun lieu : c'est l'objet porté qui est consommé, et le
        // plus petit identifiant comestible rend le choix déterministe.
        DecisionRecord decision = Decision(4, DesireKind.Eat, withTarget: false);
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [decision],
            Map,
            mapSource: null,
            vocabulary: Vocabulary([2, 9], carried: [7, 2, 9]));

        CommandEnvelope command = Assert.Single(commands);
        Assert.Equal("Consommer", command.Kind);
        Assert.Equal(2, command.Payload.GetProperty("item").GetInt32());
        Assert.True(CommandPayloadValidator.TryValidate(command.Kind, command.Payload, out string? detail), detail);
    }

    [Fact]
    public void Map_SkipsEat_WhenNoCarriedItemIsEdible()
    {
        DecisionRecord decision = Decision(4, DesireKind.Eat, withTarget: false);

        Assert.Empty(PrismDecisionCommandMapper.Map(
            7,
            [decision],
            Map,
            mapSource: null,
            vocabulary: Vocabulary([9], carried: [1, 5])));
        Assert.False(PrismDecisionCommandMapper.IsMappable(decision, Vocabulary([9], carried: [1])));
    }

    [Fact]
    public void Map_StillTargetsTheWaterBlock_WhenAVocabularyIsPresent()
    {
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [Decision(9, DesireKind.Drink)],
            Map,
            _ => new PrismBlockPosition(3, 4, 11),
            Vocabulary([2], carried: [2]));

        CommandEnvelope command = Assert.Single(commands);
        Assert.Equal("Consommer", command.Kind);
        Assert.Equal(11, command.Payload.GetProperty("target").GetProperty("z").GetInt32());
    }

    [Fact]
    public void Map_SkipsADecisionWithoutTarget()
    {
        // Sans destination, aucune commande n'a de sens : ce n'est pas une erreur
        // du pont, c'est un agent qui ne sait pas encore où aller.
        Assert.Empty(PrismDecisionCommandMapper.Map(7, [Decision(3, DesireKind.SeekWater, withTarget: false)], Map));
    }

    [Fact]
    public void Map_SkipsMissingDecisions()
    {
        DecisionRecord missing = Decision(3, DesireKind.SeekWater, state: DecisionState.None, missing: true);

        Assert.Empty(PrismDecisionCommandMapper.Map(7, [missing], Map));
    }

    [Fact]
    public void Map_RejectsADecisionFromAnotherTick()
    {
        Assert.Throws<ArgumentException>(() =>
            PrismDecisionCommandMapper.Map(8, [Decision(3, DesireKind.SeekWater, tick: 7)], Map));
    }

    [Fact]
    public void Map_RejectsTwoDecisionsForTheSameAgent()
    {
        Assert.Throws<ArgumentException>(() => PrismDecisionCommandMapper.Map(
            7,
            [Decision(3, DesireKind.SeekWater), Decision(3, DesireKind.Explore)],
            Map));
    }

    [Fact]
    public void Map_OrdersCommandsByAgent()
    {
        IReadOnlyList<CommandEnvelope> commands = PrismDecisionCommandMapper.Map(
            7,
            [Decision(9, DesireKind.SeekWater), Decision(2, DesireKind.Explore)],
            Map);

        Assert.Equal([2UL, 9UL], commands.Select(command => command.AgentId).ToArray());
    }
}
