namespace Launcher.Domain.Model;

/// <summary>
/// Résolution des points d'accès (NETWORK.md §4.1). Le Launcher ne connaît pas une adresse,
/// il connaît une résolution : c'est ce qui garde la Gateway ajoutable sans refonte.
/// </summary>
public interface IEndpointResolver
{
    /// <summary>Résout l'adresse d'un point d'accès pour une instance.</summary>
    ResolvedEndpoint Resolve(string componentId, string instanceId, string endpointKind, int? declaredPort);
}

/// <summary>
/// Allocation des adresses dans la plage interne LIVEX (NETWORK.md §6.2, §12).
/// - Priorité au manifeste : un port déclaré libre n'est jamais réattribué ailleurs (TESTING.md §6).
/// - Repli sur la plage interne : un port déclaré déjà pris par une autre instance de la
///   session n'est pas une erreur, c'est le multi-instance attendu (§6.2, §9.3).
/// - Pré-vol : chaque candidat est testé avant attribution ; un occupant est signalé.
/// - Aucune instance ne choisit seule une adresse.
/// </summary>
public sealed class PortAllocator : IEndpointResolver
{
    private readonly object _gate = new();
    private readonly HashSet<int> _reserved = new();
    private readonly Func<int, bool> _isFree;
    private readonly int _rangeFrom;
    private readonly int _rangeTo;

    /// <summary>Crée un allocateur. « isFree » teste la disponibilité réelle d'un port (pré-vol).</summary>
    public PortAllocator(int rangeFrom, int rangeTo, Func<int, bool> isFree)
    {
        if (rangeTo < rangeFrom)
        {
            throw new ArgumentException("plage de ports vide");
        }

        _rangeFrom = rangeFrom;
        _rangeTo = rangeTo;
        _isFree = isFree;
    }

    /// <summary>Résout un port : port déclaré s'il est libre, sinon premier libre de la plage interne.</summary>
    public ResolvedEndpoint Resolve(string componentId, string instanceId, string endpointKind, int? declaredPort)
    {
        lock (_gate)
        {
            if (declaredPort is { } declared)
            {
                // Résolution idempotente : la même instance et le même point conservent
                // l'adresse déjà attribuée, sans nouveau pré-vol — l'instance la détient.
                if (_reservationsByKind.TryGetValue((instanceId, endpointKind), out var own) && own == declared)
                {
                    return new ResolvedEndpoint(endpointKind, $"http://127.0.0.1:{declared}/", declared);
                }

                // Un port déclaré déjà détenu par une autre instance de la session est le
                // fonctionnement normal du multi-instance (NETWORK.md §6.2, §9.3) : c'est
                // précisément ce que l'espace d'adressage interne existe pour absorber, et
                // non une erreur de configuration. Seul un occupant étranger — absent des
                // réservations — reste un refus : l'opérateur doit l'identifier (§6.2).
                if (!_reserved.Contains(declared))
                {
                    if (!_isFree(declared))
                    {
                        throw new PortUnavailableException(declared, "port déclaré occupé (pré-vol) — processus occupant à identifier");
                    }

                    _reserved.Add(declared);
                    _reservationsByKind[(instanceId, endpointKind)] = declared;
                    return new ResolvedEndpoint(endpointKind, $"http://127.0.0.1:{declared}/", declared);
                }
            }

            for (var port = _rangeFrom; port <= _rangeTo; port++)
            {
                if (_reserved.Contains(port) || !_isFree(port))
                {
                    continue;
                }

                _reserved.Add(port);
                _reservationsByKind[(instanceId, endpointKind)] = port;
                return new ResolvedEndpoint(endpointKind, $"http://127.0.0.1:{port}/", port);
            }

            throw new PortUnavailableException(null, $"plage interne {_rangeFrom}–{_rangeTo} épuisée");
        }
    }

    /// <summary>Libère les ports d'une instance arrêtée.</summary>
    public void Release(string instanceId)
    {
        lock (_gate)
        {
            foreach (var key in _reservationsByKind.Keys.Where(k => k.InstanceId == instanceId).ToList())
            {
                _reserved.Remove(_reservationsByKind[key]);
                _reservationsByKind.Remove(key);
            }
        }
    }

    private readonly Dictionary<(string InstanceId, string Kind), int> _reservationsByKind = new();
}

/// <summary>Échec du pré-vol d'adresse : erreur de configuration explicite (NETWORK.md §6.2).</summary>
public sealed class PortUnavailableException : Exception
{
    public PortUnavailableException(int? port, string reason)
        : base(port is { } p ? $"port {p} indisponible : {reason}" : reason)
    {
        Port = port;
    }

    /// <summary>Port fautif, si connu.</summary>
    public int? Port { get; }
}
