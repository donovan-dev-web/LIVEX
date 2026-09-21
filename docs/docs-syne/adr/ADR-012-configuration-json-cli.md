# ADR-012 : Configuration moteur (JSON Annexe H) — chargement, priorité, flags CLI

**Composant** : SYNE
**Statut** : Accepted
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : ADR-001 (séparation), ADR-006 (PRNG reproductible), `CONFIGURATION.md`
**Source Monographie** : Annexe H (configuration et paramètres), §3.6.2 (seed)

---

## Contexte

La configuration est un **contrat reproductible** : le même `config.json` + même
seed + même version moteur doit produire la même trajectoire (CONFIGURATION.md §1).
Le socle U0 a besoin d'une implémentation unique du chargement (schéma Annexe H)
et des overrides de ligne de commande, avant l'arrivée en SYNE-2 de la boucle de
simulation qui consommera ces options.

Trois exigences en tension :
1. **Priorité Annexe H §5** : défauts intégrés → `--config` (surcouche) → flags CLI (overrides).
2. **Fichier partiel** : un `config.json` ne doit surcharger que les sections/propriétés
   qu'il contient (les autres conservent les défauts).
3. **Déterminisme** : `random.seed` résolu doit piloter le PRNG xoshiro256\*\* (ADR-006),
   jamais `System.Random`.

## Décision

- **Schéma unique** : `SimulationOptions` (POCO mutable, noms camelCase identiques à
  l'Annexe H), désérialisé via `System.Text.Json` (aucune dépendance externe).
- **Fusion au niveau document JSON** : la surcouche `--config` est fusionnée dans les
  défauts par parcours récursif (`objects` récursifs, `scalars`/`arrays` écrasés).
  Les sous-sections partielles (ex. `resources.water` seul) préservent les frères.
- **Flags CLI** : `--seed <uint64>`, `--max-ticks <int>`, `--world-size <w> <h>`,
  `--config <path>`, `--headless` ; analysés par `CliOptions.Parse` (barre inconnue →
  erreur explicite, code de sortie 2). Aucun parser externe.
- **Validation à l'import** : plages Annexe H §6 (dimensions > 0, `maxTicks > 0`,
  traits dans [0, 2], moteur `"xoshiro256**"` exclusif). Toute erreur stoppe avec
  un message explicite, code de sortie 2.
- **Seed résolu** : les defauts/fichier/flags convergent vers `options.Random.Seed`
  (défaut 12 345) ; la graine alimente `Xoshiro256StarStar.Create(seed)` (ADR-006).
  En mode non-`--headless`, la CLI affiche une sonde de déterminisme (3 tirages hex).

## Conséquences

### Positives
- Reproductibilité bit-à-bit vérifiable en interne (vecteurs épinglés) et à la CLI.
- Aucune dépendance NuGet ajoutée ; le chargement est testable en isolation.
- Le consommateur SYNE-2 (boucle, monde, entités) peut s'appuyer sur des garanties de plage déjà validées.

### Négatives
- Les clés de configuration des premiers étages d'usage (`ticksPerSecond`,
  `autoSaveEveryNTicks`, ressources…) ne sont pas encore **consommées** par la
  simulation (SYNE-2+ / jalons ultérieurs) — elles sont portées par le contrat mais inertes.
- Le schéma « complet » de l'Annexe H reste un sous-ensemble de ce que la
  monographie décrit (ex. clés par espèce §4) — ajout à venir, rétro-compatible
  grâce à la fusion niveau document.

### Risques
- Dérive du schéma implémenté vs Annexe H documentaire → atténué par un test
  pinning des défauts (`Defaults_RespectAnnexeH`).

## Alternatives considérées

- **Surcouche par section C#** (copie propriété à propriété) : abandonné — verbeux,
  et oublie les valeurs par défaut des sous-propriétés d'une section partielle
  (bug constaté : `resources.food.degradationTick` perdu) ; la fusion JSON-élément
  corrige le cas au niveau du document.
- **`System.CommandLine`** : refus — dépendance NuGet non nécessaire pour 5 flags,
  impératif « zéro dépendance externe » du socle.
- **`appsettings.json` + binding .NET** : refus — format et sémantique propres à
  .NET, hors norme Annexe H (JSON camelCase explicite).

## Validation / rejet

- Publier les tests `ConfigLoaderTests`, `CliArgsTests`, `SimulationOptionsValidatorTests`
  (CI `syne-dotnet`).
- Commande de référence : `dotnet run --project Simulation.Console -- --config x.json --seed 1`
  → affiche la sonde commençant par `b3f2af6d0fc710c5` (graine 1).
- Condition de réouverture : toute évolution de l'Annexe H doit mettre à jour le
  test des défauts **et** cette ADR (changement de schéma = nouvelle ADR si impact contrat).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 septembre 2026 | Création | ADR pilote du socle U0 (ISSUE SYNE-005) |