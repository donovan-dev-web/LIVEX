// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Le monde : plan 2D continu non-toroidal, grille spatiale de perception, index des entités.

namespace Simulation.Core.World;

/// <summary>Sens d'une modification d'environnement (SYNE-071).</summary>
public enum EnvironmentChangeKind
{
    Added,
    Removed,
}

/// <summary>
/// Modification d'environnement tracée (SYNE-071) : pose ou retrait d'une
/// construction (= obstacle statique). Consommée par l'émetteur d'observabilité
/// (événements <c>world.construction_placed</c> / <c>world.construction_removed</c>).
/// </summary>
public sealed record EnvironmentChange(EnvironmentChangeKind Kind, Obstacle Obstacle);

/// <summary>
/// Le monde : plan 2D continu non-toroidal, grille spatiale de perception,
/// index des entités. Positions dans [0, Width] x [0, Height].
/// </summary>
public sealed class World
{
    private readonly List<Simulation.Core.Entities.Entity> _entities = [];
    private readonly List<Obstacle> _obstacles = [];
    private readonly List<Territory> _territories = [];
    private readonly List<Book> _books = [];
    private readonly List<ResourceNode> _resourceNodes = [];
    private readonly List<EnvironmentChange> _environmentChanges = [];
    private ulong _obstacleRevision;

    public World(WorldSize size)
        : this(size, size.Width / 10.0)
    {
    }

    public World(WorldSize size, double spatialCellSize)
    {
        Size = size;
        Grid = new SpatialGrid(size, spatialCellSize);
    }

    public WorldSize Size { get; }

    public SpatialGrid Grid { get; }

    public IReadOnlyList<Simulation.Core.Entities.Entity> Entities => _entities;

    public IReadOnlyList<Obstacle> Obstacles => _obstacles;

    /// <summary>
    /// Zones de territoire (SYNE-073) : disques « point de survie »
    /// posés à l'init (config <c>world.territories.zones[]</c>), ordre de pose
    /// stable. La présence d'une entité dans une zone la délimite comme
    /// territoire effectif (appartenance suivie par la boucle, 0 PRNG).
    /// </summary>
    public IReadOnlyList<Territory> Territories => _territories;

    /// <summary>
    /// Gisements localisés de ressources (Phase 0 du réalignement) : sources
    /// spatiales perceptibles et consommables, placées à l'init par la règle
    /// partagée <see cref="ResourceNodePlacement"/> (mêmes emplacements que
    /// <c>WorldDescription.resources[]</c>, publiés à PRISM). Ordre de pose stable
    /// (type puis indice) — déterminisme d'itération.
    /// </summary>
    public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;

    /// <summary>
    /// Livres du monde (SYNE-121) : matérialisations de
    /// connaissance à l'instant T, écrites par des entités (coût en énergie payé
    /// à l'écriture, consultées par des lecteurs distincts). Actifs seulement si
    /// <c>world.books.enabled</c> (API de la boucle <c>WriteBook</c>/<c>ReadBook</c>).
    /// </summary>
    public IReadOnlyList<Book> Books => _books;

    /// <summary>
    /// Révision des obstacles — incrémentée à chaque ajout/retrait (y compris le
    /// layout d'init et la restauration). Pilote la re-rasterisation du cheminement
    /// (SYNE-071) : quand elle change, <c>AStarPathfinder.Refresh</c> re-calcule la
    /// grille bloquée et purge le cache de chemins.
    /// </summary>
    public ulong ObstacleRevision => _obstacleRevision;

    /// <summary>
    /// Modifications d'environnement tracées depuis la dernière collecte
    /// (pose/retrait de construction, SYNE-071). Drainée par l'émetteur
    /// d'observabilité via <see cref="ClearEnvironmentChanges"/>.
    /// </summary>
    public IReadOnlyList<EnvironmentChange> LastEnvironmentChanges => _environmentChanges;

    public void AddEntity(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Position != Position.Clamp(entity.Position, Size))
        {
            throw new ArgumentOutOfRangeException(nameof(entity), "La position de l'entité sort du monde (non-toroidal).");
        }

        Grid.Add(entity);
        _entities.Add(entity);
    }

    /// <summary>
    /// Ajoute un obstacle statique de base au monde (SYNE-011) :
    /// configuration initiale ou restauration bit-à-bit. Non tracé (les obstacles
    /// d'init sont l'environnement, pas des modifications d'environnement).
    /// </summary>
    public void AddObstacle(Obstacle obstacle)
    {
        ArgumentNullException.ThrowIfNull(obstacle);
        EnsureCanAdd(obstacle);
        _obstacles.Add(obstacle);
        _obstacleRevision++;
    }

    /// <summary>
    /// Pose une construction (SYNE-071) : un obstacle statique de la
    /// grille, tracé comme modification d'environnement (événement
    /// <c>world.construction_placed</c>). Bloque le mouvement et la ligne de vue.
    /// </summary>
    public void PlaceConstruction(Obstacle obstacle)
    {
        ArgumentNullException.ThrowIfNull(obstacle);
        EnsureCanAdd(obstacle);
        _obstacles.Add(obstacle);
        _obstacleRevision++;
        _environmentChanges.Add(new EnvironmentChange(EnvironmentChangeKind.Added, obstacle));
    }

    /// <summary>
    /// Retire une construction (SYNE-071) : tracé comme modification d'environnement
    /// (événement <c>world.construction_removed</c>). Renvoie <c>false</c> si aucun
    /// obstacle ne porte cet identifiant.
    /// </summary>
    public bool RemoveConstruction(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        int index = _obstacles.FindIndex(obstacle => obstacle.Id == id);
        if (index < 0)
        {
            return false;
        }

        Obstacle removed = _obstacles[index];
        _obstacles.RemoveAt(index);
        _obstacleRevision++;
        _environmentChanges.Add(new EnvironmentChange(EnvironmentChangeKind.Removed, removed));
        return true;
    }

    /// <summary>
    /// Applique le layout d'obstacles configuré (SYNE-071) :
    /// place les disques de <c>world.obstacleLayout</c> quand <c>world.obstacles</c>
    /// est vrai. Obstacles d'init non tracés. Pose aussi les zones de territoire
    /// (SYNE-073) quand <c>world.territories</c> est actif.
    /// </summary>
    public void ApplyConfiguredLayout(Configuration.WorldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Obstacles)
        {
            foreach (Configuration.StaticObstacleSettings spec in settings.ObstacleLayout)
            {
                AddObstacle(new Obstacle(spec.Id, new Position(spec.X, spec.Y), spec.Radius));
            }
        }

        if (settings.Territories.Enabled)
        {
            foreach (Configuration.TerritoryZoneDefinition spec in settings.Territories.Zones)
            {
                AddTerritory(new Territory(spec.Id, new Position(spec.CenterX, spec.CenterY), spec.Radius));
            }
        }
    }

    /// <summary>
    /// Remplace l'ensemble des gisements (restauration bit-à-bit) : la disposition
    /// d'init est écartée au profit de l'état sauvegardé (quantités comprises).
    /// </summary>
    internal void ReplaceResourceNodes(IEnumerable<ResourceNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _resourceNodes.Clear();
        foreach (ResourceNode node in nodes)
        {
            _resourceNodes.Add(node);
        }
    }

    /// <summary>
    /// Ajoute (ou remplace) le gisement portant l'identifiant donné et renvoie le
    /// gisement résolu — restauration d'état et compatibilité de contrat.
    /// </summary>
    internal ResourceNode UpsertResourceNode(string id, ResourceKind kind, Position position, double amount, double capacity, double regenerationRate)
    {
        int index = _resourceNodes.FindIndex(node => node.Id == id);
        if (index >= 0)
        {
            _resourceNodes[index].RestoreState(amount);
            return _resourceNodes[index];
        }

        var created = new ResourceNode(id, kind, position, amount, capacity, regenerationRate);
        _resourceNodes.Add(created);
        return created;
    }

    /// <summary>
    /// Ajoute une zone de territoire au monde (SYNE-073) : configuration initiale
    /// (layout <c>world.territories.zones[]</c>) — appartenance non tracée (aucune
    /// mutation d'environnement ; la zone est statique, la présence des entités
    /// est suivie par la boucle).
    /// </summary>
    public void AddTerritory(Territory territory)
    {
        ArgumentNullException.ThrowIfNull(territory);
        EnsureCanAddTerritory(territory);
        _territories.Add(territory);
    }

    /// <summary>Consomme les modifications d'environnement tracées (drain par-tick de l'émetteur).</summary>
    public void ClearEnvironmentChanges() => _environmentChanges.Clear();

    /// <summary>
    /// Ajoute un gisement localisé (init par <see cref="ApplyResourceLayout"/> ou
    /// restauration bit-à-bit). Identifiant unique exigé.
    /// </summary>
    public void AddResourceNode(ResourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Position != Position.Clamp(node.Position, Size))
        {
            throw new ArgumentOutOfRangeException(nameof(node), "La position du gisement sort du monde (non-toroidal).");
        }

        if (_resourceNodes.Any(existing => existing.Id == node.Id))
        {
            throw new ArgumentException($"Un gisement portant l'identifiant « {node.Id} » existe déjà.", nameof(node));
        }

        _resourceNodes.Add(node);
    }

    /// <summary>
    /// Applique la disposition des gisements configurée : place les
    /// <c>resources.nodesPerType</c> gisements par type aux emplacements de la
    /// description du monde. <b>0 tirage PRNG</b> (LCG local dérivé de la graine).
    /// </summary>
    public void ApplyResourceLayout(Configuration.ResourceSettings settings, ulong seed, double cellSize)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Idempotent : la disposition courante est écartée avant repose. Une même
        // monde peut servir à construire plusieurs boucles (tests, reprise) et un
        // second passage ne doit pas lever « identifiant déjà présent ».
        _resourceNodes.Clear();
        foreach (ResourceNode node in ResourceNodePlacement.CreateNodes(
            Size,
            cellSize,
            _obstacles,
            seed,
            settings.NodesPerType,
            settings.NodeInitialAmount,
            settings.NodeCapacity,
            settings.NodeRegenerationRate))
        {
            AddResourceNode(node);
        }
    }

    /// <summary>
    /// Gisement disponible le plus proche d'une position, pour un type donné :
    /// distance croissante puis identifiant (ordre déterministe). Utilisé par
    /// l'<b>effet</b> d'une action terminale (consommer la source où l'on se
    /// tient), jamais pour choisir une cible — la cible d'une décision vient des
    /// croyances de l'agent.
    /// </summary>
    public ResourceNode? NearestResourceNode(Position position, ResourceKind kind, double maxDistance)
    {
        ResourceNode? nearest = null;
        double bestDistance = double.MaxValue;
        foreach (ResourceNode node in _resourceNodes)
        {
            if (node.Kind != kind)
            {
                continue;
            }

            double distance = node.Position.DistanceTo(position);
            if (distance > maxDistance)
            {
                continue;
            }

            if (distance < bestDistance ||
                (distance == bestDistance && nearest is not null && string.CompareOrdinal(node.Id, nearest.Id) < 0))
            {
                nearest = node;
                bestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Ajoute un livre au monde (SYNE-121) : stocké à l'écriture
    /// (<c>SimulationLoop.WriteBook</c>, tracé) ou pour la restauration d'état.
    /// Identifiant unique exigé (jamais de mutation silencieuse du référentiel).
    /// </summary>
    public void AddBook(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (_books.Any(existing => existing.Id == book.Id))
        {
            throw new ArgumentException($"Un livre portant l'identifiant « {book.Id} » existe déjà.", nameof(book));
        }

        _books.Add(book);
    }

    private void EnsureCanAdd(Obstacle obstacle)
    {
        if (obstacle.Position != Position.Clamp(obstacle.Position, Size))
        {
            throw new ArgumentOutOfRangeException(nameof(obstacle), "La position de l'obstacle sort du monde (non-toroidal).");
        }

        if (_obstacles.Any(existing => existing.Id == obstacle.Id))
        {
            throw new ArgumentException($"Un obstacle portant l'identifiant « {obstacle.Id} » existe déjà.", nameof(obstacle));
        }
    }

    private void EnsureCanAddTerritory(Territory territory)
    {
        if (territory.Center != Position.Clamp(territory.Center, Size))
        {
            throw new ArgumentOutOfRangeException(nameof(territory), "Le centre de la zone de territoire sort du monde (non-toroidal).");
        }

        if (_territories.Any(existing => existing.Id == territory.Id))
        {
            throw new ArgumentException($"Une zone de territoire portant l'identifiant « {territory.Id} » existe déjà.", nameof(territory));
        }
    }

    /// <summary>
    /// Retire une entité du monde (SYNE-074, mortalité) : grille spatiale + index.
    /// Lève si l'entité n'était pas présente.
    /// </summary>
    public void RemoveEntity(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Grid.Remove(entity);
        bool removed = _entities.Remove(entity);
        if (!removed)
        {
            throw new InvalidOperationException($"L'entité {entity.Id.Value} n'est pas indexée dans le monde.");
        }
    }

    /// <summary>
    /// Position uniforme dans le monde (déterministe en fonction de la graine du PRNG).
    /// </summary>
    public (Position Position, Simulation.Core.Prng.Xoshiro256StarStar Next) SamplePosition(Simulation.Core.Prng.Xoshiro256StarStar rng)
    {
        var next = rng.NextDouble(out double u);
        next = next.NextDouble(out double v);
        return (new Position(u * Size.Width, v * Size.Height), next);
    }
}