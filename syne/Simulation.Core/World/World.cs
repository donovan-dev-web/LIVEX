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
/// index des entités. Positions dans [0, Width] x [0, Height] (DATA_MODEL.md §2).
/// </summary>
public sealed class World
{
    private readonly List<Simulation.Core.Entities.Entity> _entities = [];
    private readonly List<Obstacle> _obstacles = [];
    private readonly List<Territory> _territories = [];
    private readonly List<Book> _books = [];
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
    /// Zones de territoire (SYNE-073, décision n°21) : disques « point de survie »
    /// posés à l'init (config <c>world.territories.zones[]</c>), ordre de pose
    /// stable. La présence d'une entité dans une zone la délimite comme
    /// territoire effectif (appartenance suivie par la boucle, 0 PRNG).
    /// </summary>
    public IReadOnlyList<Territory> Territories => _territories;

    /// <summary>
    /// Livres du monde (SYNE-121, décisions n°18/19) : matérialisations de
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
    /// Ajoute un obstacle statique de base au monde (SYNE-011, DATA_MODEL.md §2) :
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
    /// Pose une construction (SYNE-071, décision n°20) : un obstacle statique de la
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
    /// Applique le layout d'obstacles configuré (SYNE-071, CONFIGURATION.md §6.8) :
    /// place les disques de <c>world.obstacleLayout</c> quand <c>world.obstacles</c>
    /// est vrai. Obstacles d'init non tracés. Pose aussi les zones de territoire
    /// (SYNE-073, CONFIGURATION.md §6.10) quand <c>world.territories</c> est actif.
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