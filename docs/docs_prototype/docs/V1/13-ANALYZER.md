# Analyzer (Phase 5)

Service **C#/.NET** de la mono-repo, chargé de quantifier l'émergence produite par
le Simulation Core. Il ne contient aucune logique de simulation : il **consomme**
le flux de transport et **expose** des résultats.

## 1. Position dans l'architecture

```text
Simulation Core  --(WebSocket: snapshots + events)-->  Analyzer (.NET)
                                                        |
                                                        | REST API
                                                        v
                                                     Web UI (Phase 6)
```

Le Simulation Core ne sait pas que l'Analyzer existe (principe §7 de
`docs/V1/04-ARCHITECTURE.md`). Communication via le contrat `Transport` déjà défini
(`WorldSnapshot`, `ExternalEvent`, cf. `docs/V1/09-EVENTS-API.md`).

## 2. Responsabilités

- S'abonner au WebSocket de simulation (client `.NET` réutilisant les DTO de
  `simulation-core/Simulation.Core/Transport`).
- Calculer les métriques définies dans `docs/V1/11-EMERGENCE-ANALYSIS.md` §2 :
  - **Population** : totale, morts, durée de vie.
  - **Ressources** : consommation, disponibilité, concentration, épuisement.
  - **Spatial** : densité, distance moyenne, concentration autour des ressources, déplacements.
  - **Comportement** : fréquence/durée des actions, interruptions, changements de décision.
- Mesures d'émergence (§3 de la doc `11`) : persistance dans le temps,
  reproductibilité (via seed/config), réseaux sociaux (graphe des liens/confiance),
  clustering spatial, entropie / diversité des comportements.
- Exposer une **API REST** (rapports, séries temporelles, comparaison de runs).
- Stocker / exporter les résultats d'expérience : `Seed`, `Configuration`,
  `Population`, `Durée`, `Version du moteur`, `Résultats`.

## 3. Contraintes

- Mono-repo 100 % .NET : **pas de Python** par défaut (cohérence de toolchain).
- Aucune dépendance graphique ; utilisable en mode service/console (sans interface).
- Réutilise les DTO de transport tels quels (pas de duplication de contrat).
- Déterminisme : les comparaisons de runs doivent être reproductibles (seed + config).

## 4. API REST (implémentée en Phase 5)

Service `analyzer/Analyzer.Service` (ASP.NET minimal API, Kestrel) :

- `GET /health` — sonde de vie.
- `GET /api/runs` — liste des runs enregistrés (infos).
- `GET /api/runs/{id}` — métriques complètes du run (série temporelle + synthèse).
- `GET /api/runs/{id}/export` — exporte le JSON des métriques dans `analyzer/data/runs/{id}.json`.
- `GET /api/compare?a=<idA>&b=<idB>` — comparaison de deux runs (distances normalisées + `reproducible`).

Lancement :

```bash
dotnet run -c Release --project analyzer/Analyzer.Service -- --sim=ws://127.0.0.1:5180/ --rest=http://localhost:5000 --run-id=<id> --seed=<seed>
dotnet run -c Release --project analyzer/Analyzer.Service -- --selftest
```

Le client WebSocket (`SimClient`) se reconnecte automatiquement tant que le
serveur de simulation n'est pas disponible.

## 5. Tests

Projet `analyzer/Analyzer.Tests` (xUnit, net10.0), exécutable via
`dotnet test analyzer/Analyzer.slnx`. Couverture (18 tests) :

- **`MetricsTests`** : stats d'un `TickSample` (moyennes, comptes d'actions),
  entropie de comportement (nulle si action uniforme, `log2(n)` si actions
  distinctes), distance à la ressource la plus proche (valeur connue), réseau de
  co-localisation (clusters + degré moyen), `Compute` (référence) == `Aggregate`
  (incrémental, validé tick à tick), `Downsample`, `Compare` (reproductible /
  non reproductible), `ResourceDepletion`.
- **`RunStoreTests`** : agrégation incrémentale (`AddSnapshot` → `Series`),
  décès (`AddEvent` AgentDied), sous-échantillonnage (`sampleEvery`), cache des
  métriques invalidé sur nouveau tick, `Export` (fichier JSON), `Compare` de deux
  runs identiques → reproductible.

Lancement :

```bash
dotnet test analyzer/Analyzer.slnx -c Release
```

Test d'intégration (manuel) : connexion à un serveur WebSocket de simulation de
test (`wstest`) et vérification que les métriques calculées sont cohérentes.
