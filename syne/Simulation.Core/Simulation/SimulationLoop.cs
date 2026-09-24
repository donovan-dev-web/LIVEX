using System.Diagnostics;
using Simulation.Core.Performance;
using Simulation.Core.Prng;

namespace Simulation.Core.Loop;

/// <summary>
/// Boucle de simulation (SYNE-002, SIMULATION_LOOP.md §1).
/// <list type="bullet">
/// <item>1 tick = 1 minute simulée (défaut) — <see cref="SimulationTime"/>.</item>
/// <item>Respect de <c>maxTicks</c> : la boucle s'arrête après le tick n° <c>maxTicks</c>.</item>
/// <item>L'état du PRNG avance d'un tirage par tick (flux ancré au tick index).</item>
/// <item>Depuis U1 : pipeline cognitif BDI (perception, mémoire, croyances, besoins,
///  objectifs, utilité, intention, action) exécuté à chaque tick (SYNE-010).</item>
/// </list>
/// </summary>
public sealed class SimulationLoop
{
    private readonly Simulation.Core.Cognition.CognitionPipeline _cognition;
    private readonly TickBudgetCollector? _budget;
    private readonly int _autoSaveEveryNTicks;
    private readonly Action<SimulationLoop>? _autosaveHandler;
    private readonly Simulation.Core.Configuration.SimulationOptions _options;
    private readonly List<Simulation.Core.Configuration.SeasonChange> _seasonChanges = new();
    private readonly List<World.TerritoryMembershipChange> _territoryChanges = new();
    private readonly List<Simulation.Core.World.BookChange> _bookChanges = new();
    private System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<ulong>> _territoryMembership =
        new(System.StringComparer.Ordinal);
    private Xoshiro256StarStar _rng;

    public SimulationLoop(World.World world, Xoshiro256StarStar initialRng)
        : this(world, initialRng, new Simulation.Core.Configuration.SimulationOptions())
    {
    }

    public SimulationLoop(World.World world, Xoshiro256StarStar initialRng, Simulation.Core.Configuration.SimulationOptions options)
        : this(world, initialRng, options, null)
    {
    }

    public SimulationLoop(
        World.World world,
        Xoshiro256StarStar initialRng,
        Simulation.Core.Configuration.SimulationOptions options,
        TickBudgetCollector? budget)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        World = world;
        _rng = initialRng;
        _budget = budget;
        _options = options;
        Resources = new World.ResourceStocks(options.Resources);
        _cognition = new Simulation.Core.Cognition.CognitionPipeline(world, options, Resources, budget);
        _autoSaveEveryNTicks = options.Simulation.AutoSaveEveryNTicks;
        _autosaveHandler = null;
    }

    /// <summary>
    /// Boucle avec autosave branche : <paramref name="autosaveHandler"/> est appelé
    /// après chaque <c>n</c>-ième tick (PERSISTENCE.md §5). Le handler ne doit
    /// consommer aucun tirage du PRNG — le déterminisme bit-à-bit reste inchangé.
    /// </summary>
    public SimulationLoop(
        World.World world,
        Xoshiro256StarStar initialRng,
        Simulation.Core.Configuration.SimulationOptions options,
        TickBudgetCollector? budget,
        int autoSaveEveryNTicks,
        Action<SimulationLoop>? autosaveHandler)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        World = world;
        _rng = initialRng;
        _budget = budget;
        _options = options;
        Resources = new World.ResourceStocks(options.Resources);
        _cognition = new Simulation.Core.Cognition.CognitionPipeline(world, options, Resources, budget);
        _autoSaveEveryNTicks = Math.Max(1, autoSaveEveryNTicks);
        _autosaveHandler = autosaveHandler;
    }

    public World.World World { get; }

    /// <summary>Réserves globales de ressources (SYNE-042) : consommées par Eat/Drink, exposées dans le snapshot.</summary>
    public World.ResourceStocks Resources { get; internal set; }

    public ulong CurrentTick { get; private set; }

    public Xoshiro256StarStar Rng => _rng;

    public Simulation.Core.Cognition.CognitionPipeline Cognition => _cognition;

    /// <summary>
    /// Collecteur de budgets de tick (SYNE-090) si la boucle en a été munie
    /// (sinon <c>null</c> — aucun coût sur le chemin nominal).
    /// </summary>
    public TickBudgetCollector? Budgets => _budget;

    /// <summary>
    /// Changements de saison survenus au tick courant (SYNE-072) : vidés
    /// (<see cref="ClearSeasonChanges"/>) par la couche d'observabilité après
    /// réémission — même protocole que les modifications d'environnement.
    /// </summary>
    public IReadOnlyList<Configuration.SeasonChange> LastSeasonChanges => _seasonChanges;

    /// <summary>Vide la file des changements de saison (drain d'observabilité).</summary>
    public void ClearSeasonChanges() => _seasonChanges.Clear();

    /// <summary>Saison courante du monde au tick courant (fonction pure du tick, 0 PRNG).</summary>
    public World.Season CurrentSeason => _options.World.Seasons.At(CurrentTick);

    /// <summary>
    /// Cycle de saisons activé (<c>world.seasons.enabled</c>) — modifieurs de
    /// régénération et événement <c>world.season_changed</c> actifs.
    /// </summary>
    public bool SeasonsEnabled => _options.World.Seasons.Enabled;

    /// <summary>
    /// Suivi du territoire activé (<c>world.territories.enabled</c>, SYNE-073) —
    /// appartenance des entités aux zones, événements <c>world.territory_membership_changed</c>
    /// et champ snapshot <c>territories[]</c> actifs.
    /// </summary>
    public bool TerritoriesEnabled => _options.World.Territories.Enabled;

    /// <summary>
    /// Livres activés (SYNE-121, décisions n°18/19, Monographie §3.18) —
    /// <c>world.books.enabled</c> : écriture payée en énergie par l'auteur
    /// (coût <c>world.books.writeCostEnergy</c>), lecture à bénéfice posé en
    /// principe (n°19, <c>world.books.read_benefit</c>). Désactivés par défaut
    /// ⇒ trajectoire du scénario de référence inchangée, checksums dorés
    /// ré-épinglés inchangés (pin contractuel).
    /// </summary>
    public bool BooksEnabled => _options.World.Books.Enabled;

    /// <summary>
    /// Changements d'appartenance aux zones de territoire survenus depuis la
    /// dernière collecte (SYNE-073) : vidés (<see cref="ClearTerritoryChanges"/>)
    /// par la couche d'observabilité après réémission — même protocole que les
    /// modifications d'environnement et les changements de saison.
    /// </summary>
    public IReadOnlyList<World.TerritoryMembershipChange> LastTerritoryChanges => _territoryChanges;

    /// <summary>Vide la file des changements d'appartenance (drain d'observabilité).</summary>
    public void ClearTerritoryChanges() => _territoryChanges.Clear();

    /// <summary>
    /// Mutations de livres survenues depuis la dernière collecte (SYNE-121) :
    /// vidées (<see cref="ClearBookChanges"/>) par la couche d'observabilité
    /// après réémission — même protocole que les changements de saison, de
    /// territoire et de constructions (drain).
    /// </summary>
    public IReadOnlyList<Simulation.Core.World.BookChange> LastBookChanges => _bookChanges;

    /// <summary>Vide la file des mutations de livres (drain d'observabilité).</summary>
    public void ClearBookChanges() => _bookChanges.Clear();

    /// <summary>
    /// Identifiants des entités présentes dans la zone <paramref name="zoneId"/>
    /// au tick courant — ordre croissant (déterministe), initialement vide.
    /// </summary>
    public IReadOnlyList<ulong> MembersOfTerritory(string zoneId)
    {
        if (_territoryMembership.TryGetValue(zoneId, out System.Collections.Generic.HashSet<ulong>? members))
        {
            return members.OrderBy(member => member).ToList();
        }

        return [];
    }

    /// <summary>
    /// Écrit un livre (SYNE-121, décision n°18, Monographie §3.18) : l'auteur
    /// (cognition requise) paie le **coût d'écriture posé en principe** —
    /// énergie <c>world.books.writeCostEnergy</c> **débitée** de ses besoins
    /// corporels (<see cref="Simulation.Core.Cognition.MindState.Needs"/> ;
    /// durée de rédaction et pénalité non modélisées en V0.1) ; le livre est poinçonné
    /// (<c>writtenTick</c> = tick courant) et ajouté au monde. Mutation
    /// **tracée** dans <see cref="LastBookChanges"/>. API de la boucle —
    /// **0 tirage PRNG** (déterministe, DETERMINISM.md §3).
    /// </summary>
    public void WriteBook(Simulation.Core.World.Book book)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!BooksEnabled)
        {
            throw new InvalidOperationException("Les livres sont désactivés (world.books.enabled = false).");
        }

        if (!Cognition.HasMind(book.AuthorId))
        {
            throw new InvalidOperationException($"L'auteur {book.AuthorId} n'a pas de cognition.");
        }

        if (World.Books.Any(existing => existing.Id == book.Id))
        {
            throw new ArgumentException($"Un livre portant l'identifiant « {book.Id} » existe déjà.", nameof(book));
        }

        double cost = _options.World.Books.WriteCostEnergy;
        book.WrittenTick = CurrentTick;
        World.AddBook(book);
        Cognition.MindOf(book.AuthorId).Needs.ExertEnergy(cost);
        _bookChanges.Add(new Simulation.Core.World.BookChange(
            Simulation.Core.World.BookChangeKind.Written, book, -cost, null));
    }

    /// <summary>
    /// Lit un livre (SYNE-121, décision n°19, Monographie §3.18) : le lecteur
    /// (cognition requise) tire le **bénéfice posé en principe**
    /// (<c>world.books.read_benefit</c>, chiffrage savoir/confiance reporté au
    /// moteur de mémoire) et la lecture est **tracée** (lecteurs distincts,
    /// ordre de première consultation) dans <see cref="LastBookChanges"/>.
    /// API de la boucle — **0 tirage PRNG** (déterministe, DETERMINISM.md §3).
    /// </summary>
    public void ReadBook(Simulation.Core.World.Book book, ulong readerId)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!BooksEnabled)
        {
            throw new InvalidOperationException("Les livres sont désactivés (world.books.enabled = false).");
        }

        if (!Cognition.HasMind(readerId))
        {
            throw new InvalidOperationException($"Le lecteur {readerId} n'a pas de cognition.");
        }

        if (!World.Books.Any(existing => ReferenceEquals(existing, book)))
        {
            throw new ArgumentException("Le livre ne fait pas partie de ce monde.", nameof(book));
        }

        double benefit = _options.World.Books.ReadBenefit;
        book.MarkReadBy(readerId);
        _bookChanges.Add(new Simulation.Core.World.BookChange(
            Simulation.Core.World.BookChangeKind.Read, book, benefit, readerId));
    }

    /// <summary>
    /// Avance d'un tick (1 minute simulée, SIMULATION_LOOP.md §1) puis exécute le
    /// pipeline cognitif BDI (U1, SYNE-010) dans l'ordre causal strict. Le PRNG
    /// n'avance que d'un tirage par tick (contrat DETERMINISM.md §3).
    /// </summary>
    public void AdvanceOneTick()
    {
        CurrentTick += 1;
        _rng = _rng.NextUInt64(out _);
        if (_budget is null)
        {
            _cognition.Step(CurrentTick);
            Resources.ApplyLifecycle(CurrentTick, _options.Resources, SeasonFactorsForTick());
        }
        else
        {
            long start = Stopwatch.GetTimestamp();
            _cognition.Step(CurrentTick);
            using (TickPhaseScope resourcesScope = _budget.Begin(TickPhase.EventsGroupsPopulation))
            {
                Resources.ApplyLifecycle(CurrentTick, _options.Resources, SeasonFactorsForTick());
            }

            double elapsedMs = (Stopwatch.GetTimestamp() - start) * (1000.0 / Stopwatch.Frequency);
            _budget.RecordPipelineTick(elapsedMs);
        }

        if (_autosaveHandler is not null && CurrentTick % (ulong)_autoSaveEveryNTicks == 0)
        {
            _autosaveHandler(this);
        }

        TrackTerritoryMembership();
    }

    /// <summary>
    /// Suivi de l'appartenance aux zones de territoire au tick courant (SYNE-073,
    /// décision n°21) : la présence d'une entité dans le disque la classe membre du
    /// territoire effectif. Recalculée à chaque tick (les positions sont finales
    /// après les boucles entités) — **0 tirage PRNG** ; chaque différence avec le
    /// tick précédent est **tracée** (<see cref="LastTerritoryChanges"/>) dans un
    /// ordre déterministe : par zone (ordre de pose), par entité (id croissant),
    /// sorties avant entrées. Cycle inactif : aucun tracé, appartenance nulle.
    /// </summary>
    private void TrackTerritoryMembership()
    {
        Simulation.Core.Configuration.TerritorySettings territories = _options.World.Territories;
        if (!territories.Enabled)
        {
            _territoryMembership.Clear();
            return;
        }

        var current = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<ulong>>(System.StringComparer.Ordinal);
        foreach (World.Territory zone in World.Territories)
        {
            var members = new System.Collections.Generic.HashSet<ulong>();
            foreach (Simulation.Core.Entities.Entity entity in World.Entities)
            {
                if (zone.Contains(entity.Position))
                {
                    members.Add(entity.Id.Value);
                }
            }

            current[zone.Id] = members;
        }

        foreach (World.Territory zone in World.Territories)
        {
            System.Collections.Generic.HashSet<ulong> previous =
                _territoryMembership.TryGetValue(zone.Id, out System.Collections.Generic.HashSet<ulong>? prev) ? prev : [];
            System.Collections.Generic.HashSet<ulong> now = current[zone.Id];

            foreach (ulong left in previous.Where(member => !now.Contains(member)).OrderBy(member => member))
            {
                _territoryChanges.Add(new Simulation.Core.World.TerritoryMembershipChange(
                    Simulation.Core.World.TerritoryMembershipChangeKind.Left, zone, left));
            }

            foreach (ulong entered in now.Where(member => !previous.Contains(member)).OrderBy(member => member))
            {
                _territoryChanges.Add(new Simulation.Core.World.TerritoryMembershipChange(
                    Simulation.Core.World.TerritoryMembershipChangeKind.Entered, zone, entered));
            }
        }

        _territoryMembership = current;
    }

    /// <summary>
    /// Facteurs de régénération saisonniers du tick courant (SYNE-072) + trace du
    /// changement de saison quand le cycle est actif. <c>null</c> sans cycle actif
    /// (régénération nominale ×1) ; pendant un changement, un <see cref="Configuration.SeasonChange"/>
    /// est ajouté à <see cref="LastSeasonChanges"/>. 0 tirage PRNG (DETERMINISM.md §3).
    /// </summary>
    private Simulation.Core.Configuration.SeasonFactors? SeasonFactorsForTick()
    {
        Simulation.Core.Configuration.SeasonSettings seasons = _options.World.Seasons;
        if (!seasons.Enabled)
        {
            return null;
        }

        World.Season current = seasons.At(CurrentTick);
        World.Season previous = seasons.At(CurrentTick - 1);
        if (current != previous)
        {
            _seasonChanges.Add(new Simulation.Core.Configuration.SeasonChange(previous, current));
        }

        return seasons.Factors(CurrentTick);
    }

    /// <summary>Exécute la boucle jusqu'au tick n° <paramref name="maxTicks"/> inclus.</summary>
    public void Run(int maxTicks)
    {
        if (maxTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTicks), "maxTicks doit être &gt; 0.");
        }

        while (CurrentTick < (ulong)maxTicks)
        {
            AdvanceOneTick();
        }
    }

    /// <summary>
    /// Restauration bit-à-bit (SYNE-111, PERSISTENCE.md §4) : pointe la boucle sur
    /// le tick <paramref name="tick"/> et remplace l'état du PRNG par les 4 × 64 bits
    /// sauvegardés. Aucun tirage supplémentaire — la suite est identique à une
    /// exécution ininterrompue.
    /// </summary>
    internal void RestoreState(ulong tick, Xoshiro256StarStar rng)
    {
        CurrentTick = tick;
        _rng = rng;
    }
}

/// <summary>Correspondance tick ↔ temps simulé (1 tick = 1 minute, SIMULATION_LOOP.md §1).</summary>
public static class SimulationTime
{
    public static readonly int TicksPerSimulationMinute = 1;

    public static long ToSimulatedMinutes(ulong tick) => (long)tick * TicksPerSimulationMinute;

    /// <summary>Format horloge simulée « T+h:mm » depuis le tick 0.</summary>
    public static string FormatClock(ulong tick)
    {
        long minutes = ToSimulatedMinutes(tick);
        long hours = minutes / 60;
        int minutesOfHour = (int)(minutes % 60);
        return $"T+{hours}:{minutesOfHour:D2}";
    }
}