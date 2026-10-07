# LIVEX Launcher

Orchestrateur de la pile LIVEX : il pilote SYNE (moteur), ECHOS (analyse) et — le jour
de sa livraison — PRISM (immersion), depuis une interface unique et un format de données
unique, le paquet `.livexp`.

La spécification complète vit dans [`../docs/docs-launcher/`](../docs/docs-launcher/README.md) :
vision, architecture, modèle de composants, contrat d'intégration, format de paquet,
campagnes, observabilité, interface, empaquetage, tests, feuille de route et six ADR.
La liste ordonnée et vérifiable des travaux restant pour finaliser V1 est dans
[`ROADMAP-V1.md`](ROADMAP-V1.md).
Le contrat versionné du manifeste et du cycle de vie est dans
[`contracts/INTEGRATION-CONTRACT-v1.md`](contracts/INTEGRATION-CONTRACT-v1.md),
avec le [`schéma JSON v1`](contracts/component-manifest-v1.schema.json).

## État de la réalisation (cible V1)

| Jalon | Contenu | État |
| :-- | :-- | :-- |
| **G1** | Format de paquet `.livexp` : ZIP64 vivant puis scellé, ordre d'écriture normatif, déterminisme octet pour octet, contre-mesures de sécurité | Livré, 10 propriétés testées |
| **G2** | Orchestration sans interface : machine à états, registre, profils, sondes de santé câblées (Démarrage → Prêt, perte de contact), gestion de processus, détection par manifeste | Livré |
| **G3** | Campagnes : planification séquentielle, graines dérivées, politiques d'échec, reprise exacte, annulation, scellement | Implémenté et testé contre les stubs ; le défaut SYNE réel `reference` est aligné, mais les campagnes et la collecte restent à accepter contre une installation publiée |
| **G4** | Interface d'orchestration : quatre modes, neuf écrans, cycle de vie, monitoring, lecteur Markdown et CLI `--check` | Parcours principaux, consultation/export des logs de run et gestion des installations présents ; variantes UI/headless et validation complète des états visuels restent à faire |
| **G5** | Session complète : run réel, analyse headless ECHOS, rapport archivé et relu, reprise sans rejouer les runs terminés | Non franchi : SYNE batch et les opérations d'analyse ECHOS attendues ne sont pas disponibles |
| **G6** | Livraison : diagnostic, installation propre et mise à jour | `--check` existe ; installateurs Launcher Windows/Linux et validation sur machines propres restent à faire |
| **G7** | Déverrouillage Immersion selon manifeste et acceptation PRISM | Verrouillé : aucun manifeste PRISM Launcher ni parcours de rendu accepté |
| — | Portes externes P1 – P5 (protocole, batch SYNE, ECHOS pilotable, Linux, déterminisme) | Hors du Launcher (`ROADMAP.md`, `ISSUES.md`) |

Les modes `Console`, `Standard`, `Développement` et `Personnaliser` sont
sélectionnables dans l'application. Configuration permet d'ajouter des
installations et de choisir l'installation active ; la sélection explicite
des variantes UI/headless selon les capacités des manifestes reste à faire.
Les campagnes ont été validées contre les stubs, pas contre SYNE réel et les
opérations d'analyse ECHOS réelles. L'état observé et la séquence de travail
sont détaillés dans [`ROADMAP-V1.md`](ROADMAP-V1.md) et la
[`matrice des capacités`](V1-CAPABILITY-MATRIX.md).

## Arborescence

```text
launcher/
├── Launcher.Protocol/          Types de contrat versionnés — aucune dépendance
├── Launcher.Domain/            Orchestration, campagnes, santé, profils (référence Protocol seul)
├── Launcher.Infrastructure/    Processus, sondes, détection, journaux, espace de travail
├── Launcher.Application/       Cas d'usage : campagnes, exécution, analyse (ports sortants)
├── Launcher.Package/           Format .livexp : écriture en incrément, scellement, lecture
├── Launcher.Presentation/      Vues et vues modèles Avalonia (MVVM)
├── Launcher.App/               Point d'entrée, composition, CLI, façade d'orchestration
├── Stub.Syne/                  Moteur simulé, conforme au contrat, pannes injectables
├── Stub.Echos/                 Analyste simulé, endpoints d'analyse §10.1, déterministe
├── Stub.Prism/                 PRISM simulé (§13) : processus vivant + client WebSocket factice
├── Launcher.Tests.Unit/        Domaine, paquet, campagnes — sans aucun composant démarré
├── Launcher.Tests.Integration/ Processus réels contre les stubs
└── Launcher.Tests.EndToEnd/    Campagne complète : détection → run → scellement → relecture
```

## Ressources graphiques de l'interface

Les dimensions ci-dessous sont les dimensions de livraison attendues en pixels. Les actifs
listés sont présents dans le projet et les références utilisées par la vue sont intégrées
au build.

| Nom | Chemin | Extension | Taille attendue | Description et emplacement |
| :-- | :-- | :-- | :-- | :-- |
| Panorama de l'accueil | `Launcher.App/Assets/hero-panorama.png` | PNG | 670 × 204 px | Illustration panoramique à droite du bandeau d'accueil, sous le titre LIVEX. Extrait de la maquette de référence. |
| Maquette source | `../docs/docs-launcher/maquettes/Interface futuriste du launcher LIVEX.png` | PNG | 1536 × 1024 px | Référence visuelle complète pour la composition, les espacements, les couleurs et les commandes intégrées. |
| Logo LIVEX | `Launcher.App/Assets/livex-mark.svg` | SVG | viewBox 1080 × 1080; rendu 50 × 50 px et 34 × 34 px | Symbole de marque dans l'en-tête et en bas de la barre latérale. |
| Emblèmes de composants | `Launcher.App/Assets/syne-mark.svg`, `echos-mark.svg`, `prism-mark.svg` | SVG | viewBox 1080 × 1080; rendu 48–66 px | Marques des composants dans leurs cartes et dans la colonne d'état système. |
| Icônes navigation | `Launcher.App/Assets/icons/icons-home.svg`, `icons-experience.svg`, `icons-campagne.svg`, `icons-analyse.svg`, `icons-report.svg`, `icons-setting.svg`, `icons-log.svg` | SVG | 24 × 24 px | Icônes des sept entrées de la maquette historique ; Monitoring et Documentation n'ont pas encore d'icônes dédiées. |
| Icônes ressources et actions | `Launcher.App/Assets/icons/icons-ram.svg`, `icons-cpu.svg`, `icons-storage.svg`, `icons-play.svg`, `icons-stop.svg` | SVG | 24 × 24 px (ressources), viewBox 60 × 60 (actions) | Jauges RAM/CPU/stockage et icônes d'action de démarrage et d'arrêt des composants. |
| Icône pause (réserve) | `Launcher.App/Assets/icons/icons-pause.svg` | SVG | viewBox 60 × 60 | Présente dans les ressources; aucun bouton pause n'existe encore dans le cycle de vie exposé par l'interface, donc elle n'est pas référencée par la vue. |
| Fonds des cartes composants | `Launcher.App/Assets/component-syne.png`, `component-echos.png`, `component-prism.png` | PNG | 292 × 272 px chacun | Visuels des cartes COMPOSANTS; cadrage `UniformToFill`, superposé derrière leurs textes. |
| Décor de la barre latérale | `Launcher.App/Assets/sidebar-landscape.png` | PNG | 222 × 430 px | Paysage sombre dans le panneau de navigation, sous le bloc de marque. |

Les SVG sont rendus par le paquet Avalonia.Svg. Tous les actifs sont inclus comme
`AvaloniaResource`; la capture complète reste la source de mesure et n'est pas utilisée
comme une image aplatie de l'interface.

Les frontières d'assembly sont vérifiées par un test d'analyse statique
(`Launcher.Tests.Unit/StaticAnalysis/AssemblyBoundaryTests.cs`) : toute dépendance interdite
fait échouer la construction, conformément à `ADR-001`.

## Build et tests

```bash
cd launcher
dotnet build Launcher.sln          # avertissements = erreurs
dotnet test Launcher.Tests.Unit --nologo
dotnet test Launcher.Tests.Integration --nologo
dotnet test Launcher.Tests.EndToEnd --nologo
```

SDK requis : .NET 10 (épinglé par `global.json`, comme pour SYNE).

## Lancement

> `livex-launcher` n'est **pas installé sur le PATH** tant que le jalon G6
> (installation, `PACKAGING.md`) n'est pas réalisé : c'est le nom du fichier exécutable
> produit par la build, dans le répertoire de sortie. Partir de `launcher/` pour toutes
> les commandes ci-dessous.

```bash
cd launcher

dotnet run --project Launcher.App                    # fenêtre native (1536 × 1024, redimensionnable)
dotnet run --project Launcher.App -- --check         # vérification de l'environnement, sans interface
dotnet run --project Launcher.App -- --package EXP.livexp   # ouvrir un paquet existant

# Ou directement le binaire construit :
./Launcher.App/bin/Debug/net10.0/livex-launcher --check
./Launcher.App/bin/Debug/net10.0/livex-launcher               # fenêtre native
```

Un premier lancement sans composant installé ouvre quand même l'interface : la pile
affiche chaque composant attendu à l'état **Absent** avec l'emplacement de recherche
(`USER_INTERFACE.md` §7). `--check` le dit aussi :

```console
[ÉCHEC] composants : aucun composant détecté — emplacements attendus : components/, LIVEX_HOME
[ÉCHEC] moteur : moteur absent : SYNE non détecté
```

La commande exécute les huit vérifications de `PACKAGING.md` §6 (exécution, droits
d'écriture, espace disque, version du format, composants, moteur, ports, navigateur en
information non bloquante) et se termine par le code 2 dès qu'une vérification bloque.

### Faire détecter un composant (essai avec le stub)

La détection lit les manifestes `component.json` (COMPONENTS.md §2) dans, par priorité :
`components/` à côté de l'exécutable, `$LIVEX_HOME`, `~/.livex/components/`, puis les
répertoires listés dans `~/.livex/components.registry` (un par ligne). Pour un essai
immédiat avec le moteur simulé :

```bash
mkdir -p ~/.livex/components/syne
cp Stub.Syne/bin/Debug/net10.0/Stub.Syne* ~/.livex/components/syne/
cat > ~/.livex/components/syne/component.json <<'EOF'
{
  "schema": 1,
  "id": "syne",
  "name": "SYNE",
  "type": "engine",
  "version": "0.1.0-stub",
  "executable": { "path": "Stub.Syne" },
  "capabilities": ["headless", "seed", "tickLimit", "export", "pause"],
  "endpoints": { "control": { "transport": "http" } },
  "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
  "timeouts": { "startupMs": 30000, "shutdownMs": 15000 },
  "contributesTo": ["analyse", "immersion"]
}
EOF

./Launcher.App/bin/Debug/net10.0/livex-launcher --check
```

Le diagnostic passe alors à `[OK  ] composants : 1 composant(s) détecté(s), 1 valide(s)` et les cartes de l'interface
permettent de **démarrer/arrêter** réellement le composant (cycle de vie, `INTEGRATION_CONTRACT.md`
§5 : jeton par variable d'environnement, arrêt gracieux `/control/shutdown` puis forcé).
Sous Windows, ajoutez `"windows": "Stub.Syne.exe"` dans l'objet `executable` du manifeste.

## Les stubs

`INTEGRATION_CONTRACT.md` §13 impose des composants simulés pour développer et tester le
Launcher avant les composants réels. `Stub.Syne` expose `/health/live|ready|details`,
`/info`, `/metrics` et `POST /control/shutdown` (jeton exigé), avance ses ticks à cadence
réglable, écrit uniquement dans le dossier fourni (`--work-dir`), respecte `--seed` et
`--ticks`, et sort seul à l'horizon avec les codes de sortie du contrat §4.

Pannes injectables : `--crash-at-tick n`, `--freeze-at-tick n`, `--slow-factor x`,
`--exit-code n`, `--refuse-shutdown`.

`Stub.Echos` répond aux demandes d'analyse du §10.1 (`POST /analysis/run`,
`/analysis/experiment`, `/analysis/report`) avec un contenu déterministe dérivé du dossier
D'entrée, et consigne chaque demande dans `analysis/requests.log` de son dossier de
travail — c'est la preuve, en E2E, qu'une campagne se déroule sans ouvrir l'interface web.
`Stub.Prism` (§13) est un PRISM simulé : endpoints standard §6, `args.txt` écrit au
démarrage (vérifie que le Launcher ne passe d'arguments SYNE qu'au moteur) et client
WebSocket factice activé par `--ws-url`. Les trois stubs sortent en code 3 si leur port
est occupé (§4).

## Données utilisateur

La racine des données est `~/.livex-data` par défaut, déplaçable par la variable
d'environnement `LIVEX_DATA`. Paquets vivants dans `packages/`, journaux de session
dans `sessions/` (rotation quotidienne). L'installation reste remplaçable sans perte
de données (`PACKAGING.md` §2.2).

## Limites connues de la réalisation

- La sidebar compte neuf écrans : sept de la maquette historique, plus Monitoring
  et Documentation. Ces deux derniers n'ont pas encore de spécification pixel.
- Le journal de session peut être ouvert dans son dossier et exporté en NDJSON.
  Les journaux stdout/stderr par run restent consultables dans les paquets, sans
  vue centralisée dans le Launcher.
- Le lecteur Markdown du rapport d'émergence est intégré à Analyse et Rapports
  (`Markdown.Avalonia`). La navigation par section et la provenance par section
  ne sont pas encore ajoutées (`USER_INTERFACE.md` §3.1).
- Le mode batch du SYNE réel (`--seed`, `--ticks`, `--export-dir`) reste la porte **P2**,
  et le pilotage d'analyse d'ECHOS (`AnalyzeRun`, `AnalyzeExperiment`, `GenerateReport`)
  la porte **P3** : les campagnes et les rapports sont validés contre les stubs tant
  qu'elles ne sont pas confirmées.
- Compatibilité avec les composants réels codés (`syne/`, `echos/`, `syne-mock/`) auditée
  le 2 octobre 2026 : aucun ne livre encore de `component.json`, SYNE rejette les arguments
  communs du §3.1, SYNE/ECHOS n'exposent pas les endpoints standard du §6. Écarts suivis
  en `../docs/docs-launcher/ISSUES.md` (O-38 à O-41) — leur levée appartient aux composants.
