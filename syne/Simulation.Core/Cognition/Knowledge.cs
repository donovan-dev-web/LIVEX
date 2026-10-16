// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Connaissance active d'un agent à l'instant de la décision : cibles concrètes dérivées des croyances
//        (et, en repli, de la mémoire rappelée). C'est le maillon « objectif ← besoin ← croyance ← mémoire ←
//        perception » : la décision ne consomme aucune collection globale du monde, seulement ce que l'agent sait.

using System.Globalization;
using Simulation.Core.Configuration;
using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Cible concrète d'un objectif : <b>quel</b> objet, <b>où</b>, <b>connu à quel
/// tick</b> et <b>avec quelle confiance</b> — « de l'eau en P, connue à t »
/// (B-02). Un <see cref="Goal"/> qui porte une cible est falsifiable : arrivé à la
/// position sans trouver la ressource, l'objectif n'est pas accompli et la cible
/// est révisée.
/// </summary>
/// <param name="TargetType">« node » (gisement), « entity » (pair), « point » (position dérivée).</param>
/// <param name="TargetId">Identité numérique stable de l'objet visé.</param>
/// <param name="Label">Identifiant canonique (« resource-food-1 », « entity-7 »).</param>
/// <param name="Position">Position visée <b>telle qu'observée</b> — jamais la position vraie actuelle.</param>
/// <param name="ObservedTick">Tick d'observation de cette position (âge de la connaissance).</param>
/// <param name="Confidence">Confiance épistémique de la cible (0-1) issue de la croyance.</param>
public readonly record struct GoalTarget(
    string TargetType,
    ulong TargetId,
    string Label,
    Position Position,
    ulong ObservedTick,
    double Confidence)
{
    /// <summary>Âge de la connaissance en ticks.</summary>
    public ulong Age(ulong currentTick) => currentTick >= ObservedTick ? currentTick - ObservedTick : 0;

    /// <summary>Identité canonique de la cible (« node:resource-food-1 ») — traçabilité et persistance.</summary>
    public string Canonical => string.Concat(TargetType, ":", Label);
}

/// <summary>
/// Connaissance active d'un agent au moment de délibérer : les cibles concrètes
/// qu'il peut viser, dérivées de ses <b>croyances</b> et — en repli — de sa
/// <b>mémoire rappelée</b>. Aucune de ces valeurs ne lit le monde : une cible
/// inconnue de l'agent n'existe pas ici, même si l'objet existe dans le monde.
/// </summary>
public readonly record struct CognitionKnowledge(
    GoalTarget? FoodSource,
    GoalTarget? WaterSource,
    GoalTarget? Peer,
    GoalTarget? Landmark,
    bool AtFoodSource,
    bool AtWaterSource)
{
    /// <summary>Connaissance vide (agent sans aucune observation utile).</summary>
    public static readonly CognitionKnowledge Empty = new(null, null, null, null, false, false);

    public bool KnowsFood => AtFoodSource || FoodSource is not null;

    public bool KnowsWater => AtWaterSource || WaterSource is not null;

    public bool KnowsPeer => Peer is not null;

    public bool KnowsLandmark => Landmark is not null;

    /// <summary>Cible d'une action : la source pour un besoin physiologique, le pair pour un besoin social.</summary>
    public GoalTarget? TargetFor(DesireKind kind) => kind switch
    {
        DesireKind.Eat or DesireKind.SeekFood => FoodSource,
        DesireKind.Drink or DesireKind.SeekWater => WaterSource,
        DesireKind.Socialize or DesireKind.Give or DesireKind.Trade => Peer,
        DesireKind.Explore => Landmark,
        DesireKind.Flee => Peer,
        _ => null,
    };

    /// <summary>
    /// Confiance épistémique à faire entrer dans l'utilité du candidat
    /// (<c>null</c> = aucune connaissance spécifique : terme neutre, formule
    /// inchangée par rapport à l'historique). « Être sur place » est une
    /// connaissance certaine : elle vaut 1.
    /// </summary>
    public double? EpistemicFor(DesireKind kind) => kind switch
    {
        DesireKind.Eat => AtFoodSource ? 1.0 : null,
        DesireKind.Drink => AtWaterSource ? 1.0 : null,
        DesireKind.SeekFood => FoodSource?.Confidence,
        DesireKind.SeekWater => WaterSource?.Confidence,
        DesireKind.Socialize or DesireKind.Give or DesireKind.Trade => Peer?.Confidence,
        DesireKind.Explore => Landmark?.Confidence,
        DesireKind.Flee => Peer?.Confidence,
        _ => null,
    };

    /// <summary>
    /// Destination de fuite : point opposé à la menace connue, à
    /// <paramref name="distance"/> — l'agent s'éloigne d'une <b>menace crue</b>,
    /// jamais d'une entité qu'il ne sait pas là.
    /// </summary>
    public GoalTarget? FleeDestination(Position self, double distance, WorldSize size)
    {
        if (Peer is not { } threat || !double.IsFinite(distance) || distance <= 0.0)
        {
            return null;
        }

        double dx = self.X - threat.Position.X;
        double dy = self.Y - threat.Position.Y;
        double norm = Math.Sqrt((dx * dx) + (dy * dy));
        if (norm <= 1e-9)
        {
            // Menace confondue avec soi (observation datée) : fuite dans une
            // direction stable dérivée de l'identité de la menace.
            double angle = ((threat.TargetId % 360) / 360.0) * 2.0 * Math.PI;
            dx = Math.Cos(angle);
            dy = Math.Sin(angle);
            norm = 1.0;
        }

        Position destination = Position.Clamp(
            new Position(self.X + ((dx / norm) * distance), self.Y + ((dy / norm) * distance)),
            size);
        return new GoalTarget("point", threat.TargetId, $"away-from-{threat.Label}", destination, threat.ObservedTick, threat.Confidence);
    }
}

/// <summary>
/// Construit la connaissance active d'un agent à partir de ses croyances
/// (<b>source primaire</b>) et, en repli, du <b>rappel mémoire</b> : la mémoire
/// n'est plus un stockage inerte, elle alimente la cible quand la croyance a
/// expiré ou a été évincée (S-07 / B-05).
///
/// <b>Déterminisme</b> : aucune consommation du PRNG, itérations sur des
/// collections triées (<c>BeliefSet.All</c> suit l'ordre du dictionnaire des
/// faits, <c>Memory.Recall</c> est trié par (StoredAt, Sequence)), départages par
/// identifiant canonique.
/// </summary>
public static class KnowledgeBuilder
{
    /// <summary>Confiance minimale d'une croyance pour servir de cible (sous ce seuil : connaissance négligeable).</summary>
    public const double MinConfidence = 0.2;

    /// <summary>Nom du fait d'état d'un gisement (« x,y,amount »), sujet <c>node-&lt;label&gt;</c>.</summary>
    public const string NodeStatePredicate = "state";

    /// <summary>Prédicat de position d'une entité perçue (<c>entity-&lt;id&gt;</c>).</summary>
    public const string EntityPositionPredicate = "position";

    public static CognitionKnowledge Build(
        MindState mind,
        Position self,
        ulong currentTick,
        BeliefSettings settings,
        double interactionRadius)
    {
        ArgumentNullException.ThrowIfNull(mind);
        ArgumentNullException.ThrowIfNull(settings);

        GoalTarget? food = null;
        double foodDistance = double.MaxValue;
        GoalTarget? water = null;
        double waterDistance = double.MaxValue;
        GoalTarget? landmark = null;
        double landmarkDistance = -1.0;
        GoalTarget? peer = null;
        double peerDistance = double.MaxValue;
        bool atFood = false;
        bool atWater = false;

        foreach (Belief belief in mind.Beliefs.AllInPlace)
        {
            if (belief.IsExpired(currentTick) || belief.Confidence < MinConfidence)
            {
                continue;
            }

            Fact fact = belief.Fact;
            if (string.Equals(fact.Predicate, NodeStatePredicate, StringComparison.Ordinal) &&
                fact.Subject.StartsWith("node-", StringComparison.Ordinal))
            {
                if (!TryParseNodeState(fact.Value, out Position position, out double amount))
                {
                    continue;
                }

                string label = fact.Subject["node-".Length..];
                double distance = self.DistanceTo(position);
                bool atHand = distance <= interactionRadius && amount > 0.0;

                // Un gisement dont on croit la quantité épuisée n'est pas une
                // destination : la cible est invalide et l'objectif doit être révisé.
                if (amount <= 0.0)
                {
                    continue;
                }

                var target = new GoalTarget("node", Fnv1a64.HashUtf8(label), label, position, belief.UpdatedTick, belief.Confidence);
                ResourceKind? kind = KindFromLabel(label);
                if (kind == ResourceKind.Food && distance < foodDistance)
                {
                    food = target;
                    foodDistance = distance;
                    atFood = atHand;
                }
                else if (kind == ResourceKind.Water && distance < waterDistance)
                {
                    water = target;
                    waterDistance = distance;
                    atWater = atHand;
                }

                if (distance > landmarkDistance)
                {
                    landmark = target;
                    landmarkDistance = distance;
                }
            }
            else if (string.Equals(fact.Predicate, EntityPositionPredicate, StringComparison.Ordinal) &&
                     fact.Subject.StartsWith("entity-", StringComparison.Ordinal))
            {
                if (!TryParsePosition(fact.Value, out Position position))
                {
                    continue;
                }

                double distance = self.DistanceTo(position);
                if (distance < peerDistance)
                {
                    peer = new GoalTarget(
                        "entity",
                        ParseEntityId(fact.Subject),
                        fact.Subject,
                        position,
                        belief.UpdatedTick,
                        belief.Confidence);
                    peerDistance = distance;
                }
            }
        }

        if (food is null || water is null || landmark is null)
        {
            FillFromMemory(mind, self, currentTick, interactionRadius, ref food, ref water, ref landmark, ref atFood, ref atWater);
        }

        return new CognitionKnowledge(food, water, peer, landmark, atFood, atWater);
    }

    /// <summary>
    /// Repli mémoire : les observations de gisements encore assez saillantes
    /// (<c>Memory.Recall</c>) fournissent des cibles que la croyance n'a pas
    /// conservées (expiration, éviction de capacité). La plus récente par
    /// identifiant gagne — <c>Recall</c> est trié par (StoredAt, Sequence)
    /// croissants, la dernière vue écrase donc les précédentes.
    /// </summary>
    private static void FillFromMemory(
        MindState mind,
        Position self,
        ulong currentTick,
        double interactionRadius,
        ref GoalTarget? food,
        ref GoalTarget? water,
        ref GoalTarget? landmark,
        ref bool atFood,
        ref bool atWater)
    {
        if (food is not null && water is not null && landmark is not null)
        {
            return;
        }

        var remembered = new Dictionary<string, MemoryRecall>(StringComparer.Ordinal);
        foreach (MemoryRecall recall in mind.Memory.Recall(MemoryCategory.Observation, currentTick))
        {
            if (!TryParseNodeMemory(recall.Entry.Content, out string label, out Position _, out double amount, out double _) || amount <= 0.0)
            {
                continue;
            }

            remembered[label] = recall;
        }

        foreach ((string label, MemoryRecall recall) in remembered.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            MemoryEntry entry = recall.Entry;
            if (!TryParseNodeMemory(entry.Content, out string _, out Position position, out double _, out double confidence))
            {
                continue;
            }

            double distance = self.DistanceTo(position);
            var target = new GoalTarget(
                "node",
                Fnv1a64.HashUtf8(label),
                label,
                position,
                entry.StoredAt,
                // La confiance d'un souvenir rappelé est bornée par sa salience :
                // une mémoire qui s'efface ne peut pas produire une croyance ferme.
                Math.Min(confidence, recall.Salience));

            ResourceKind? kind = KindFromLabel(label);
            if (kind == ResourceKind.Food && food is null)
            {
                food = target;
                atFood = distance <= interactionRadius;
            }
            else if (kind == ResourceKind.Water && water is null)
            {
                water = target;
                atWater = distance <= interactionRadius;
            }
            else if (landmark is null)
            {
                landmark = target;
            }
        }
    }

    /// <summary>« x.xx,y.yy,amount » → position + quantité courante du gisement.</summary>
    public static bool TryParseNodeState(string value, out Position position, out double amount)
    {
        position = default;
        amount = 0.0;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string[] parts = value.Split(',');
        if (parts.Length < 3 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        position = new Position(x, y);
        return true;
    }

    /// <summary>« x.xx,y.yy » → position.</summary>
    public static bool TryParsePosition(string value, out Position position)
    {
        position = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string[] parts = value.Split(',');
        if (parts.Length < 2 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
        {
            return false;
        }

        position = new Position(x, y);
        return true;
    }

    /// <summary>
    /// Contenu canonique d'une observation de gisement — mémoire, souvenir et
    /// <b>message</b> partagent ce format : <c>node|label|x.xx|y.yy|amount.0|conf</c>.
    /// Un format unique est ce qui permet à un message reçu de devenir une croyance
    /// de plein droit (D-13).
    /// </summary>
    public static bool TryParseNodeMemory(
        string content,
        out string label,
        out Position position,
        out double amount,
        out double confidence)
    {
        label = string.Empty;
        position = default;
        amount = 0.0;
        confidence = 0.0;
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        string[] parts = content.Split('|');
        if (parts.Length < 6 || !string.Equals(parts[0], "node", StringComparison.Ordinal))
        {
            return false;
        }

        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
            !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out amount) ||
            !double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out confidence))
        {
            return false;
        }

        label = parts[1];
        position = new Position(x, y);
        return true;
    }

    /// <summary>
    /// Convertit le contenu d'un <b>message compris</b> en fait observable :
    /// « entité en (x, y) » ou « gisement en (x, y), quantité a ». Renvoie faux
    /// pour un contenu non interprétable (le message reste un message, il ne
    /// produit pas de croyance).
    /// </summary>
    public static bool TryParseSharedObservation(string content, out Fact fact, out double payloadConfidence)
    {
        fact = default;
        payloadConfidence = 0.0;
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        if (TryParseNodeMemory(content, out string label, out Position nodePosition, out double amount, out double nodeConfidence))
        {
            fact = new Fact(
                $"node-{label}",
                NodeStatePredicate,
                string.Concat(
                    nodePosition.X.ToString("0.00", CultureInfo.InvariantCulture),
                    ",",
                    nodePosition.Y.ToString("0.00", CultureInfo.InvariantCulture),
                    ",",
                    amount.ToString("0.###", CultureInfo.InvariantCulture)));
            payloadConfidence = nodeConfidence;
            return true;
        }

        string[] parts = content.Split('|');
        if (parts.Length >= 5 &&
            string.Equals(parts[0], "entity", StringComparison.Ordinal) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
            double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double confidence))
        {
            fact = new Fact(
                $"entity-{parts[1]}",
                EntityPositionPredicate,
                string.Concat(
                    x.ToString("0.00", CultureInfo.InvariantCulture),
                    ",",
                    y.ToString("0.00", CultureInfo.InvariantCulture)));
            payloadConfidence = confidence;
            return true;
        }

        return false;
    }

    /// <summary>Type de gisement déduit du libellé canonique (« resource-water-2 » → Water).</summary>
    public static ResourceKind? KindFromLabel(string label)
    {
        const string prefix = "resource-";
        if (!label.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string rest = label[prefix.Length..];
        int dash = rest.LastIndexOf('-');
        string kindName = dash > 0 ? rest[..dash] : rest;
        foreach (ResourceKind kind in Enum.GetValues<ResourceKind>())
        {
            if (string.Equals(ResourceNodePlacement.KindName(kind), kindName, StringComparison.Ordinal))
            {
                return kind;
            }
        }

        return null;
    }

    private static ulong ParseEntityId(string subject)
    {
        const string prefix = "entity-";
        return subject.StartsWith(prefix, StringComparison.Ordinal) &&
               ulong.TryParse(subject[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong id)
            ? id
            : 0UL;
    }
}
