// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Gate de la chaîne de transmission (S-11/S-21/D-13) : un message compris devient une croyance de plein
//        droit et un souvenir, avec une confiance dégradée par la fiabilité de la source — la chaîne
//        perception → mémoire → message → croyance est remontable de bout en bout.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class TransmissionChainTests
{
    private const ulong SharerId = 1;
    private const ulong ReceiverId = 2;

    private static Entity MakeEntity(ulong id, Position position)
    {
        var traits = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["bravery"] = 1.0,
            ["curiosity"] = 1.0,
            ["sociability"] = 1.0, // ≥ 0.5 : le partage automatique s'applique
            ["greed"] = 1.0,
            ["pessimism"] = 1.0,
            ["aggression"] = 1.0,
            ["strength"] = 1.0,
            ["speed"] = 0.0, // les deux agents restent sur place : la portée de transmission ne varie pas
        };
        return new Entity(new EntityId(id), "Entité A", name: null, position, new TraitSet(traits), bornAt: 0);
    }

    /// <summary>Deux agents à portée l'un de l'autre, un monde pourvu de gisements.</summary>
    private static SimulationLoop Build()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        var world = new Simulation.Core.World.World(WorldSize.From(options), spatialCellSize: options.Agents.Perception.Radius);
        world.ApplyResourceLayout(options.Resources, seed: 12345, cellSize: options.Agents.Perception.Radius);
        world.AddEntity(MakeEntity(SharerId, new Position(250, 250)));
        world.AddEntity(MakeEntity(ReceiverId, new Position(255, 250)));

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(12345);
        return new SimulationLoop(world, rng, options);
    }

    private static (MindState Sharer, MindState Receiver, Belief Belief, MemoryEntry Memory) RunUntilTransmission(
        SimulationLoop loop,
        int maxTicks = 40)
    {
        for (int tick = 1; tick <= maxTicks; tick++)
        {
            loop.AdvanceOneTick();

            MindState sharer = loop.Cognition.MindOf(SharerId);
            MindState receiver = loop.Cognition.MindOf(ReceiverId);
            foreach (Belief belief in receiver.Beliefs.OrderedByFact())
            {
                if (!belief.Source.StartsWith("message-", StringComparison.Ordinal))
                {
                    continue;
                }

                // Le souvenir du récepteur porte la provenance du message ET le
                // contenu reçu ; l'émetteur doit porter ce même contenu comme
                // observation — sinon la chaîne n'est pas remontable jusqu'à la
                // perception et le témoignage n'a pas d'origine locale.
                foreach (MemoryEntry received in receiver.Memory.AllEntries)
                {
                    if (received.Category != MemoryCategory.Interaction ||
                        !string.Equals(received.Source, belief.Source, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool witnessed = sharer.Memory.AllEntries.Any(entry =>
                        entry.Category == MemoryCategory.Observation &&
                        string.Equals(entry.Content, received.Content, StringComparison.Ordinal));
                    if (witnessed)
                    {
                        return (sharer, receiver, belief, received);
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Aucune transmission complète (perception → mémoire → message → croyance) en 40 ticks.");
    }

    [Fact]
    public void ReceivedMessage_BecomesABelief_AndAMemory_WithTheSameCanonicalContent()
    {
        (MindState sharer, MindState _, Belief belief, MemoryEntry received) = RunUntilTransmission(Build());

        // La croyance du récepteur vient bien du message : sa provenance nomme l'émetteur.
        Assert.Equal($"message-{SharerId}", belief.Source);
        Assert.Equal(MemoryCategory.Interaction, received.Category);
        Assert.True(belief.Fact.Subject.StartsWith("node-", StringComparison.Ordinal) ||
                    belief.Fact.Subject.StartsWith("entity-", StringComparison.Ordinal));

        // Même contenu canonique des deux côtés : le message transporte le témoignage
        // de l'émetteur, et le récepteur le stocke tel quel (une seule grammaire).
        MemoryEntry? witnessed = sharer.Memory.AllEntries
            .FirstOrDefault(entry => string.Equals(entry.Content, received.Content, StringComparison.Ordinal));
        Assert.NotNull(witnessed);

        // La chaîne est causale : l'émetteur a d'abord *perçu* (mémoire d'observation),
        // le récepteur n'a ni perçu ni mémorisé autrement que par le message.
        Assert.Equal(MemoryCategory.Observation, witnessed!.Category);
        // Convention de la mémoire : un souvenir d'observation porte pour source
        // l'<b>observateur</b> lui-même (« entity-<id> »), le contenu canonique
        // portant l'objet observé. Le message, lui, porte la source de sa provenance
        // (« message-<émetteur> ») — les deux provenances restent donc distinctes.
        Assert.Equal($"entity-{SharerId}", witnessed.Source);

        // Le témoignage a une seule grammaire : la croyance du récepteur décrit bien
        // le fait transmis (sujet dérivé du contenu reçu), pas un fait local.
        string subject = belief.Fact.Subject;
        string identity = subject.StartsWith("node-", StringComparison.Ordinal)
            ? subject["node-".Length..]
            : subject["entity-".Length..];
        Assert.Contains(identity, received.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void TransmittedBelief_IsDegradedByTheReliabilityOfTheSource()
    {
        (MindState _, MindState _, Belief belief, MemoryEntry received) = RunUntilTransmission(Build());

        // Le contenu du témoignage porte la confiance de la perception d'origine
        // (dernier champ, grammaire partagée mémoire/message) : la croyance reçue
        // doit être *dégradée* — fiabilité de la source et confiance du témoignage —
        // jamais installée à la confiance de celui qui a vu.
        Assert.True(
            KnowledgeBuilder.TryParseNodeMemory(received.Content, out string _, out Position _, out double _, out double payloadConfidence) ||
            TryParseEntityConfidence(received.Content, out payloadConfidence),
            $"Contenu de témoignage inattendu : « {received.Content} ».");

        Assert.True(belief.Confidence > 0.0, "Une transmission comprise produit une croyance non nulle.");
        Assert.True(
            belief.Confidence < payloadConfidence,
            $"La croyance reçue ({belief.Confidence:F3}) doit être dégradée par rapport au témoignage ({payloadConfidence:F3}).");
        Assert.Equal(received.Confidence, belief.Confidence, 10);
    }

    /// <summary>Confiance portée par un témoignage de position d'entité (« entity|id|x|y|conf »).</summary>
    private static bool TryParseEntityConfidence(string content, out double confidence)
    {
        confidence = 0.0;
        string[] parts = content.Split('|');
        return parts.Length >= 5 &&
               string.Equals(parts[0], "entity", StringComparison.Ordinal) &&
               double.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out confidence);
    }
}
