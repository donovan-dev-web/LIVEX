namespace Simulation.Core.World;

/// <summary>Nature d'une mutation de livre (SYNE-121, API_CONTRACTS.md §2.2).</summary>
public enum BookChangeKind
{
    Written,
    Read,
}

/// <summary>
/// Mutation de livre tracée (SYNE-121, décisions n°18/19, Monographie §3.18) :
/// un livre a été **écrit** (par un auteur qui en paie le coût) ou **lu** (par
/// un lecteur qui en tire le bénéfice posé en principe). Consommée par
/// l'émetteur d'observabilité — même protocole que les changements de saison,
/// de territoire et de constructions (drain).
/// </summary>
public sealed record BookChange(BookChangeKind Kind, Book Book, double Value, ulong? ReaderId);

/// <summary>
/// Livre (SYNE-121, décisions n°18/19, Monographie §3.18, DATA_DATA.md §3.11) :
/// une **connaissance matérialisée à l'instant T** — {id, auteur, titre,
/// contenu} figés à l'écriture, <c>writtenTick</c> poinçonné par la boucle
/// (instant T exact). V0.1 : **stockage observationnel** — l'écriture coûte de
/// l'énergie à l'auteur (payée, décision n°18 : coût = temps + énergie +
/// pénalité, structure figée), la lecture produit un **bénéfice posé en
/// principe** (décision n°19 : cognition, confiance, savoir — le chiffrage
/// dépend du moteur de mémoire, reporté) et trace les lecteurs distincts.
/// Déterministe : écritures/lectures = API de la boucle (0 tirage PRNG),
/// ordre de trace des lecteurs = ordre de première consultation.
/// </summary>
public sealed class Book
{
    private readonly List<ulong> _readers = [];

    public Book(string id, ulong authorId, string title, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Id = id;
        AuthorId = authorId;
        Title = title;
        Content = content;
    }

    public string Id { get; }

    /// <summary>Identifiant de l'entité qui a écrit (a payé le coût, décision n°18).</summary>
    public ulong AuthorId { get; }

    public string Title { get; }

    /// <summary>Contenu (connaissance matérialisée à l'instant T, consultation V0.1).</summary>
    public string Content { get; }

    /// <summary>Instant T (tick) d'écriture — poinçonné par la boucle à <c>WriteBook</c>.</summary>
    public ulong WrittenTick { get; internal set; }

    /// <summary>Lecteurs distincts, dans l'ordre de première consultation (V0.1, déterministe).</summary>
    public IReadOnlyList<ulong> Readers => _readers;

    /// <summary>Nombre de lecteurs distincts.</summary>
    public int ReadCount => _readers.Count;

    internal void RestoreReaders(IEnumerable<ulong> readers)
    {
        _readers.Clear();
        _readers.AddRange(readers.Distinct());
    }

    internal void MarkReadBy(ulong readerId)
    {
        if (!_readers.Contains(readerId))
        {
            _readers.Add(readerId);
        }
    }

    public override string ToString() => $"{Id} « {Title} » (par {AuthorId}) @{WrittenTick}";
}
