// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Gisement localisé de ressource : source spatiale perceptible, mémorisable, croyable et consommable —
//        décision de modèle du réalignement (Phase 0) : « sources localisées ».
//        Le placement est la règle UNIQUE partagée avec la description du monde (aucune divergence monde/description).

using Simulation.Core.Prng;

namespace Simulation.Core.World;

/// <summary>
/// Gisement localisé de ressource (Phase 0 du réalignement) : une position, un
/// type, une quantité courante bornée par une capacité et un taux de
/// régénération. C'est la source spatiale que l'agent doit <b>découvrir</b> puis
/// <b>rejoindre</b> — remplace l'ancienne quête implicite sur une réserve
/// globale (<c>SeekFood</c> sans cible).
///
/// Ce sont exactement les emplacements publiés par
/// <see cref="WorldDescriptionBuilder"/> à destination de PRISM
/// (<c>resources[]</c>) : le moteur ne publie plus une ressource qu'il ne
/// simule pas.
///
/// Déterminisme : aucune consommation du PRNG ; quantité, régénération et
/// consommation sont des opérations arithmétiques pures.
/// </summary>
public sealed class ResourceNode
{
    public ResourceNode(
        string id,
        ResourceKind kind,
        Position position,
        double amount,
        double capacity,
        double regenerationRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!double.IsFinite(capacity) || capacity < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "La capacité doit être un nombre fini >= 0.");
        }

        if (!double.IsFinite(regenerationRate) || regenerationRate < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(regenerationRate), "Le taux de régénération doit être un nombre fini >= 0.");
        }

        Id = id;
        Kind = kind;
        Position = position;
        Capacity = capacity;
        RegenerationRate = regenerationRate;
        Amount = double.IsFinite(amount) ? Math.Clamp(amount, 0.0, capacity) : 0.0;
        NumericId = Fnv1a64.HashUtf8(id);
    }

    /// <summary>Identifiant canonique publié (« resource-food-1 ») — porteur de la croyance <c>node-food-1</c>.</summary>
    public string Id { get; }

    /// <summary>Identifiant numérique stable (FNV-1a de <see cref="Id"/>) — identité d'observation.</summary>
    public ulong NumericId { get; }

    public ResourceKind Kind { get; }

    public Position Position { get; }

    /// <summary>Quantité courante (0 quand le gisement est épuisé).</summary>
    public double Amount { get; private set; }

    public double Capacity { get; }

    /// <summary>Régénération par tick (module saisonnier appliqué par la boucle).</summary>
    public double RegenerationRate { get; }

    public bool IsDepleted => !(double.IsFinite(Amount) && Amount > 0.0);

    /// <summary>Vrai si le gisement peut couvrir exactement <paramref name="quantity"/>.</summary>
    public bool HasAtLeast(double quantity) =>
        double.IsFinite(quantity) && quantity >= 0.0 && double.IsFinite(Amount) && Amount >= quantity;

    /// <summary>
    /// Prélèvement tout-ou-rien : renvoie <c>false</c> sans rien écrire si le
    /// gisement ne couvre pas la quantité (même sémantique atomique que
    /// <see cref="ResourceStocks.TryConsume"/> : jamais de bénéfice non payé).
    /// </summary>
    public bool TryConsume(double quantity)
    {
        if (!double.IsFinite(quantity))
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La consommation doit être un nombre fini.");
        }

        if (quantity < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "La consommation ne peut être négative.");
        }

        if (!HasAtLeast(quantity))
        {
            return false;
        }

        Amount -= quantity;
        return true;
    }

    /// <summary>
    /// Régénération d'un tick (bornée par la capacité). <paramref name="seasonFactor"/>
    /// module le taux (cycle de saisons, SYNE-072) ; 1 par défaut.
    /// </summary>
    public double Regenerate(double seasonFactor = 1.0)
    {
        double factor = double.IsFinite(seasonFactor) ? seasonFactor : 0.0;
        double before = Amount;
        double next = Amount + (RegenerationRate * factor);
        Amount = double.IsFinite(next) ? Math.Clamp(next, 0.0, Capacity) : 0.0;
        return Amount - before;
    }

    /// <summary>Restauration bit-à-bit de la quantité courante (persistance).</summary>
    internal void RestoreState(double amount)
    {
        Amount = double.IsFinite(amount) ? Math.Clamp(amount, 0.0, Capacity) : 0.0;
    }
}

/// <summary>Emplacement de gisement dans la grille de description (case, pas position monde).</summary>
public sealed record ResourceLocation(string Id, ResourceKind Kind, int CellX, int CellY);

/// <summary>
/// Règle <b>unique</b> de placement des gisements, partagée par la description du
/// monde (ce que lit le client) et par le monde simulé (ce que fait le moteur).
/// La duplication de cette règle est exactement ce qui avait laissé PRISM
/// afficher des ressources que SYNE ne simulait pas.
///
/// Générateur <b>local</b> (LCG) : la description du monde ne doit consommer
/// aucun tirage du PRNG de simulation (l'observabilité ne déplace pas la
/// trajectoire), et le monde ne doit pas non plus en consommer pour son
/// init — les emplacements sont donc dérivés de la graine par ce LCG, dont les
/// paramètres fixent la disposition annoncée au client.
/// </summary>
public static class ResourceNodePlacement
{
    /// <summary>Nombre d'emplacements par type (contrat de description : 3 par type).</summary>
    public const int LocationsPerType = 3;

    /// <summary>Quantité initiale publiée par la description pour un emplacement.</summary>
    public const double DefaultAmount = 100.0;

    /// <summary>Nom canonique du type dans la description (« food », « water », « wood », « mineral »).</summary>
    public static string KindName(ResourceKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>Nombre de cases de la grille de description (identique à la description du monde).</summary>
    public static (int CountX, int CountY) CellCounts(WorldSize size, double cellSize)
    {
        if (!double.IsFinite(cellSize) || cellSize <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "La taille de case doit être > 0.");
        }

        return (
            (int)Math.Ceiling(size.Width / cellSize),
            (int)Math.Ceiling(size.Height / cellSize));
    }

    /// <summary>
    /// Couverture des cases par les obstacles — même règle
    /// (<see cref="ObstacleGeometry.CoversCell"/>) que <c>cells[]</c> et que le
    /// placement des agents.
    /// </summary>
    public static bool[] BlockedCells(int countX, int countY, double cellSize, IReadOnlyList<Obstacle> obstacles)
    {
        ArgumentNullException.ThrowIfNull(obstacles);
        var blocked = new bool[countX * countY];
        for (int y = 0; y < countY; y++)
        {
            for (int x = 0; x < countX; x++)
            {
                double centerX = (x + 0.5) * cellSize;
                double centerY = (y + 0.5) * cellSize;
                foreach (Obstacle obstacle in obstacles)
                {
                    if (ObstacleGeometry.CoversCell(obstacle, centerX, centerY, cellSize))
                    {
                        blocked[(y * countX) + x] = true;
                        break;
                    }
                }
            }
        }

        return blocked;
    }

    /// <summary>
    /// Emplacements des gisements : tirages du LCG local sur les <b>seules cases
    /// marchables</b>, un tirage tombant dans un obstacle avançant jusqu'à la
    /// première case libre (jamais de gisement dans un rocher qui le masquerait).
    /// Le nombre d'emplacements par type est minoré par les cases marchables
    /// disponibles.
    /// </summary>
    public static IReadOnlyList<ResourceLocation> Locations(
        int countX,
        int countY,
        bool[] blocked,
        ulong seed,
        int maxPerType = LocationsPerType)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        int cellCount = countX * countY;
        if (cellCount <= 0 || blocked.Length != cellCount)
        {
            throw new ArgumentOutOfRangeException(nameof(blocked), "La couverture ne correspond pas à la grille de description.");
        }

        int walkableCount = 0;
        foreach (bool isBlocked in blocked)
        {
            if (!isBlocked)
            {
                walkableCount++;
            }
        }

        uint state = unchecked((uint)seed ^ (uint)(seed >> 32) ^ 0xA511E9B3u);
        int locationsPerKind = Math.Min(Math.Max(0, maxPerType), walkableCount);
        var locations = new List<ResourceLocation>(Enum.GetValues<ResourceKind>().Length * locationsPerKind);

        uint NextCell()
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            return state;
        }

        foreach (ResourceKind kind in Enum.GetValues<ResourceKind>())
        {
            var usedCells = new HashSet<int>();
            for (int locationIndex = 0; locationIndex < locationsPerKind; locationIndex++)
            {
                int cellIndex = (int)(NextCell() % (uint)cellCount);
                while (blocked[cellIndex] || usedCells.Contains(cellIndex))
                {
                    cellIndex = (cellIndex + 1) % cellCount;
                }

                usedCells.Add(cellIndex);

                locations.Add(new ResourceLocation(
                    $"resource-{KindName(kind)}-{locationIndex + 1}",
                    kind,
                    cellIndex % countX,
                    cellIndex / countX));
            }
        }

        return locations;
    }

    /// <summary>
    /// Construit les gisements du monde (positions = centre de case de la
    /// description).
    /// </summary>
    public static IReadOnlyList<ResourceNode> CreateNodes(
        WorldSize size,
        double cellSize,
        IReadOnlyList<Obstacle> obstacles,
        ulong seed,
        int maxPerType,
        double amount,
        double capacity,
        double regenerationRate)
    {
        ArgumentNullException.ThrowIfNull(obstacles);
        (int countX, int countY) = CellCounts(size, cellSize);
        bool[] blocked = BlockedCells(countX, countY, cellSize, obstacles);
        IReadOnlyList<ResourceLocation> locations = Locations(countX, countY, blocked, seed, maxPerType);

        var nodes = new List<ResourceNode>(locations.Count);
        foreach (ResourceLocation location in locations)
        {
            nodes.Add(new ResourceNode(
                location.Id,
                location.Kind,
                new Position(
                    (location.CellX + 0.5) * cellSize,
                    (location.CellY + 0.5) * cellSize),
                amount,
                capacity,
                regenerationRate));
        }

        return nodes;
    }
}
