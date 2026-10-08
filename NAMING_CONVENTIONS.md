# NAMING_CONVENTIONS.md — Convention de nommage

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `CONTRIBUTING.md`, `.editorconfig`, `CI_CD.md`, `GITFLOW.md`, `GLOSSARY.md`

---

## 1. Principe

Deux règles universelles, valables pour **tous** les langages du projet :

| Élément | Langue | Raison |
| :-- | :-- | :-- |
| **Identifiants** — noms de fichiers, dossiers, classes, fonctions, variables, constantes, branches, tags, clés de configuration | **Anglais** | Standard international de l'industrie ; le code est lu et outillé (linters, IDE, search) au-delà du français. |
| **Commentaires et documentation** — commentaires dans le code, docstrings, XML doc, JSDoc, docs Markdown, messages de commit, messages d'erreur utilisateur | **Français** | Compréhension par l'équipe et traçabilité métier du projet LIVEX. |

Conséquences pratiques :

- Un identifiant en français (`calculerDistance`) est **refusé en revue** ; un commentaire en anglais (`// compute distance`) aussi.
- Les messages d'erreur destinés à l'utilisateur et les libellés UI restent en français (sauf chaînes exposées par un **contrat de transport**, voir `COMMUNICATION.md`).
- Les mots-clés du langage, les API des bibliothèques standard et les noms techniques imposés par un framework (`onStart`, `IDisposable`, `UPROPERTY`) ne sont pas traduisibles : on les tels quels.

## 2. Règles transverses

Références : `.editorconfig` (fichier racine) et `CI_CD.md` §3.

- Encodage **UTF-8 sans BOM**, fins de ligne **LF**, une ligne vide en fin de fichier.
- Pas d'espace en fin de ligne, pas de tabulation d'alignement (`align trailing`).
- Charset et encodage : les accents sont autorisés partout (fichiers et commentaires).

| Langage | Indentation | Longueur de ligne max |
| :-- | :-- | :-- |
| C# | 4 espaces | 120 (analyseurs .NET) |
| Python | 4 espaces | 100 (`black` / `.editorconfig`) |
| JavaScript | 2 espaces | 100 |
| C++ (Unreal) | tabulation | 120 (style Epic) |
| Markdown / JSON / YAML | 2 espaces | 100 (prose : retour à la ligne autour de 80) |

## 3. Règles de nommage communes

1. **Anglais**, présent, impératif pour les fonctions (`startWorld`, pas `worldStarted` ni `demarrerMonde`).
2. **Pas d'abréviation inventée** : `config` et `params` passent, `cfg` et `prm` non. Les abréviations du domaine sont autorisées une fois définies dans `GLOSSARY.md` : `SYNE`, `ECHOS`, `PRISM`, `LDK`, `BDI`, `PRNG`, `PID`, `TTL`, `ID`.
3. **Noms explicites plutôt que courts** : `regenerationRatePerTick` plutôt que `rrpt`.
4. **Booléens** : nommés pour se lire en `if` — `isReady`, `hasFailed`, `canPause`, `shouldRestart` (+ préfixe `b` en C++, voir §7).
5. **Unités dans le nom quand elles ne sont pas dans le type** : `timeoutMs`, `energyCost`, `distanceInMeters`, `tickCount`. Le vocabulaire temporel suit `GLOSSARY.md` : `tick`, `runId`, `sessionId`.
6. **Nombres en clair** : `MAX_RETRY_COUNT`, jamais `MAX3` ; les codes magiques n'existent pas, tous deviennent des constantes nommées.
7. **Apostrophe et espaces interdits** dans les noms ; séparateurs selon le langage (§4 à §7).
8. Les identifiants qui traversent un **contrat** (`runId`, `ticksPerSecond`, `seed`, …) respectent exactement la casse définie dans `docs/docs-*/API_CONTRACTS.md` / `TRANSPORT_API.md` : on ne renomme pas un champ de contrat pour se conformer à un style local.

## 4. Fichiers et dossiers

| Domaine | Convention | Exemple |
| :-- | :-- | :-- |
| Document racine | MAJUSCULES-AVEC-TIRETS `.md` | `NAMING_CONVENTIONS.md`, `ARCHITECTURE.md` |
| Document de composant | MAJUSCULES-AVEC-TIRETS `.md` | `docs/docs-syne/DETERMINISM.md` |
| ADR | `ADR-NNN-sujet-en-anglais-ou-francais.md` | `docs/docs-syne/adr/ADR-006-prng-reproductible.md` |
| Dossier de code | tel que défini par l'écosystème (§5–§7) | `Simulation.Core/`, `agent-decision-service.js` |
| Branche / commit | voir `GITFLOW.md` §3 et §7 | `feature/syne-bdi-10-etapes`, `fix(echos): …` |

- Un fichier source = **un type public principal** (C#, C++) ou **un module** (Python, JS) ; le nom du fichier est identique à celui du type/module.
- Pas de majuscules accidentelles (`MyFile.cs` vs `myfile.cs`) : la casse est significative pour Git sous Linux.
- Les artefacts générés (`__pycache__/`, `bin/`, `obj/`, `node_modules/`, `TestResults/`) ne sont jamais nommés à la main ni commités (`.gitignore`).

## 5. C# — `launcher/` et `syne/`

Standard de référence : **guidelines .NET** (pascalCase public, analyseurs du SDK). Vérification : `dotnet format` en CI (`CI_CD.md` §3).

| Élément | Convention | Exemple |
| :-- | :-- | :-- |
| Namespace | Anglais, PascalCase, sans préfixe société, espace de noms `file-scoped` | `namespace Simulation.Core.Persistence;` |
| Projet / assembly | Anglais, PascalCase, préfixe rôle pour les sous-projets | `Launcher.Domain`, `Simulation.Core.Tests` |
| Classe, record, struct, interface | PascalCase ; interface préfixée `I` | `SimulationSnapshotCodec`, `IPackageService` |
| Méthode, propriété, événement | PascalCase | `StartAsync`, `RunState` |
| Méthode asynchrone | suffixe obligatoire `Async` | `PrepareWorldAsync()` |
| Paramètre | camelCase | `PrepareWorld(TickCount tickCount)` |
| Variable locale | camelCase | `var maxRetries = 3;` |
| Champ privé | `_camelCase` | `private readonly ServiceRegistry _registry;` |
| Constante / `static readonly` | PascalCase | `MaxSessionSeconds` |
| Enum + membres | PascalCase des deux côtés (pas d'énumération de membres en majuscules) | `RunState.Ready` |
| Générique | PascalCase à une lettre ou mot complet | `T`, `TResult` |
| Test (xUnit) | `MéthodeÉtendue_CasAttendu_Résultat`, classe `SujetTests` | `StartAsync_ComposantAbsent_Echec()` |

Autres règles :

- Pas de préfixe de type dans les noms (`clsAgent`, `m_count` interdits), sauf `I` d'interface.
- Pas de sémantique cachée derrière un nom générique : le nom porte le sens (`HandleStop`, pas `DoStuff`).
- Les membres `public` sont ceux voulus en API ; le reste est `private`/`internal` (analyseurs d'API publique).
- Documentation XML obligatoire sur les types et méthodes `public` : `/// <summary>` en français (voir §9).

## 6. Python — `echos/` et `scripts/`

Standard de référence : **PEP 8** + `black` (ligne 100) + `flake8` (`echos/pyproject.toml`).

| Élément | Convention | Exemple |
| :-- | :-- | :-- |
| Module / fichier | `snake_case.py` | `resource_sustainability.py` |
| Package | `snake_case`, un `__init__.py` par package | `echos/analysis/` |
| Fonction / méthode | `snake_case`, verbe à l'infinitif | `build_chain()`, `load_snapshot()` |
| Classe | PascalCase | `CausalCache` |
| Variable / paramètre | `snake_case` | `tick_count`, `run_id` |
| Constante module | `SCREAMING_SNAKE_CASE` | `DEFAULT_TICKS_PER_SECOND` |
| Variable privée | `_snake_case` (jamais `__name` hors classe) | `_store` |
| Test | `test_<cas>_<condition>` dans `test_*.py` | `test_ingest_version_invalides_cache()` |

Autres règles :

- **Annotations de type** obligatoires sur les signatures publiques (`from __future__ import annotations` en tête de fichier).
- Docstring **française** sur tout module, fonction, méthode et classe publiques (voir §9).
- `import` en anglais, groupés : stdlib, tiers, local ; tri alphabétique (`isort` implicite via `dotnet`/flake8).
- Pas d'import circulaire, pas d'`import *`.
- Les scripts exécutables commencent par `#!/usr/bin/env python3`.

## 7. JavaScript — `syne-mock/`

Standard de référence : **style Airbnb/Standard épuré**, formaté manuellement cohérent (pas de linter configuré aujourd'hui — la cohérence du code existant fait foi).

| Élément | Convention | Exemple |
| :-- | :-- | :-- |
| Fichier / dossier | `kebab-case.js` | `agent-decision-service.js` |
| Fonction | `camelCase`, verbe à l'infinitif | `createSeededRandom()` |
| Variable locale / paramètre | `camelCase` | `numericSeed` |
| Constante module | `SCREAMING_SNAKE_CASE` | `MASK_64` |
| Constructeur / classe | `PascalCase` | `WorldGenerator` |
| Export | nommé, `camelCase` | `module.exports = { createSeededRandom };` |
| Test | `test/*.test.js`, cas descriptifs | `test/stream-order.test.js` |

Autres règles :

- Chaînes avec **apostrophes simples** (`'texte'`), sauf échappement (`'n\'est pas'`).
- Modules **CommonJS** (`require` / `module.exports`) — pas de conversion `import/export` de vive force.
- Pas de `var` ; `const` par défaut, `let` seulement si réaffectation.
- `undefined` ne se teste jamais avec `==` ; `null`/`undefined` se distinguent par `===`.
- JSDoc obligatoire sur toute fonction exportée (voir §9).

## 8. C++ / Unreal — `prism/`

Standard de référence : **Unreal Engine Coding Standard** (Epic). L'indentation est à la **tabulation**, style de la version d'UE utilisée.

| Élément | Convention | Exemple |
| :-- | :-- | :-- |
| Fichier | identique à la classe principale | `PrismLdkSubsystem.h` |
| Classe exposée | préfixe **U** (`UObject`), **A** (`AActor`), **S** (Slate) | `UPrismLdkSubsystem` |
| Struct | préfixe **F** | `FPrismWorldDescription` |
| Enum | préfixe **E** + membres PascalCase | `EPrismSyneRunState::Running` |
| Interface | préfixe **I** | `IPrismLdkTransport` |
| Template | préfixe **T** | `TArray<FAgentState>` |
| Booléen (membre, local, paramètre) | préfixe **b** | `bIsRunning`, `bAutoConnect` |
| Attribut / variable membre | PascalCase | `TicksPerSecond` |
| Méthode / fonction | PascalCase, verbe | `StartRun()` |
| Paramètre / variable locale | camelCase | `const float deltaSeconds` |
| Macro | `SCREAMING_SNAKE_CASE` | `PRISM_LOG_CATEGORY` |
| UPROPERTY / UFUNCTION | requis dès que l'élément est visible Blueprint ; `BlueprintReadOnly/Callable` au cas par cas | `UPROPERTY(BlueprintReadOnly)` |

Autres règles :

- `#pragma once` en tête de fichier, puis `.generated.h` en dernier `#include` des headers UHT.
- `const`, références `const&`, `TArray`/`TSharedPtr` plutôt que pointeurs nus non possédants.
- Pas de `using namespace` dans les headers.
- N'exposer au Blueprint que le nécessaire ; le reste reste C++ privé.
- Les commentaires de code suivent §9 (français, `//`).

## 9. Commentaires dans le code

### 9.1 Utilité

Le commentaire répond au **pourquoi**, jamais au **quoi** (le code dit quoi). Il est obligatoire dans les cas suivants :

1. **Contrainte non locale** : déterminisme, ordre de tirages PRNG, format canonique d'un identifiant — ex. `// Format canonique run-<seed>-<12hex> (parité SYNE, API_CONTRACTS.md §2/§3)`.
2. **Décision technique** : renvoi à une ADR ou une issue — ex. `# Critères d'acceptation (ADR-015 + cible 2500 ticks).`
3. **Optimisation / subtilité** : pourquoi une inclusion est ouverte ici, pourquoi un `round` précède un test.
4. **Périmètre dangereux** : persistance bit-à-bit, réseau, confinement de processus.
5. **TODO / FIXME** : uniquement accompagnés d'une référence (`TODO(SYNE-204) : …`), sinon le commentaire pourrit sans trace.

Il est **inutile** de commenter : le code auto-explicatif, les noms déjà explicites, les déclarations évidentes (« incrémente le compteur »), le code commenté abandonné (on le supprime, Git garde l'historique).

### 9.2 Structure

| Niveau | Portée | Format |
| :-- | :-- | :-- |
| Fichier / module | 1 bloc en tête de fichier, après la licence éventuelle | Docstring (Python), JSDoc (JS), `///` (C#), `//` (C++) |
| Type | classe, struct, enum, interface publique(s) | bloc de doc immédiatement au-dessus de la déclaration |
| Fonction / méthode | signature publique ou logique non triviale | bloc de doc au-dessus de la déclaration |
| Bloc | un raisonnement local | `//` au-dessus du bloc, jamais au milieu d'une expression |
| Ligne | clarification ponctuelle | `//` en fin de ligne, une seule, si le code tient sur une ligne |

Composition d'un bloc de doc :

1. **une phrase d'objet** (français, sans point final si c'est un fragment de titre en C#/JS, avec point en docstring Python) ;
2. **comportement et contraintes** (le « pourquoi », les cas limites) ;
3. **références** : issue (`SYNE-111`), ADR (`ADR-006`), document + section (`PERSISTENCE.md §3`), monographie (`Monographie §3.10.4`) — voir §9.4.

### 9.3 Formats par langage

| Langage | Commentaire ligne | Bloc de doc | Exemple |
| :-- | :-- | :-- | :-- |
| C# | `//` | XML doc `///` sur type/méthode publics | `/// <summary>Serveur de santé du composant (COMPONENTS.md §2).</summary>` |
| Python | `#` | docstring `"""…"""` (module, classe, fonction publique) | `"""Reconstruction de chaînes causales (ECHOS-060 → ECHOS-063)."""` |
| JavaScript | `//` | JSDoc `/** … */` sur toute fonction exportée | `/** Position atteignable en un pas, déjà arrondie. */` |
| C++ (Unreal) | `//` | `//` multi-lignes (pas de `/* */` dans le style UE) | `// État de connexion à SYNE après reconnexion automatique.` |
| Markdown | — | en-tête métadonnées + sections numérotées | `**Dépend de** : \`ARCHITECTURE.md\`` |

Règles de forme communes :

- **Français avec accents**, ponctuation complète, phrases courtes.
- Pas de `/* code */` commenté ni de directive `#if 0` : on supprime, Git garde.
- Un commentaire en fin de ligne reste court (≤ 100 caractères) ; au-delà, bloc au-dessus.
- Les commentaires de TODO sont rares, tracés, et retirés avec l'issue qu'ils référencent.

### 9.4 Références canoniques

Le format d'une référence est fixé par son origine :

| Origine | Format | Exemple |
| :-- | :-- | :-- |
| Issue du composant | `SYNE-nnn`, `ECHOS-nnn`, `PRISM-nnn`, `LAUNCHER-nnn` | `# ECHOS-013 : série Parquet par agent` |
| Décision d'architecture | `ADR-NNN` (+ statut si pertinent) | `(ADR-002 [Accepted])` |
| Document LIVEX | `CHEMIN.md §section` | `API_CONTRACTS.md §2.1`, `GLOSSARY.md §3` |
| Monographie | `Monographie §x.y.z` | `(Monographie §3.10.4)` |

Une référence dans le code pointe toujours vers un document **réel et à jour** : vérifier son existence avant de l'écrire (un lien mort en commentaire est pire que pas de commentaire).

## 10. Documentation Markdown et configuration

- **Titre** : `# NOMFICHIER.md — sous-titre en français`.
- **En-tête métadonnées** obligatoire pour un document racine ou de composant :
  `**Composant**`, `**Statut**`, `**Dernière mise à jour**`, `**Dépend de**`.
- **Sections numérotées** (`## 1. …`), tables au format `| :-- |`, séparateur `---` entre l'en-tête et le corps.
- Liens **relatifs** uniquement (`../ARCHITECTURE.md`), jamais d'URL absolue locale.
- Prose en français, noms techniques et titres de sections de code en anglais quand ils citent du code.
- Mise à jour de la table « Mises à jour » en pied de document à chaque modification.

Configuration :

| Fichier | Convention | Exemple |
| :-- | :-- | :-- |
| JSON de configuration | clés `camelCase` (contrat existant) | `ticksPerSecond`, `regenerationRate` |
| Fichier de config exemple | `snake_case` ou `kebab-case` selon l'écosystème hérité | `config_example.json`, `requirements-dev.txt` |
| Workflow CI / YAML | noms de jobs et d'étapes en anglais `kebab-case` | `build-syne`, `lint-echos` |
| `component.json` | schéma existant, clés en anglais | ne jamais renommer une clé sans ADR + `VERSIONING.md` |

## 11. À faire / à éviter

| À faire | À éviter |
| :-- | :-- |
| `startWorld()` (anglais, impératif) | `demarrerMonde()`, `worldStarted()` |
| `isReady` / `bIsReady` (booléen lisible) | `ready`, `flag`, `bool1` |
| `timeoutMs` (unité dans le nom) | `timeout` (ms ou s ?) |
| `// Format canonique (API_CONTRACTS.md §2)` | `// ici on formate l'id` |
| `/// <summary>Store SQLite (ADR-010).</summary>` | commentaire anglais auto-traduit |
| `agent-decision-service.js` (kebab) | `AgentDecisionService.js` en JS |
| `_registry` (champ privé C#) | `registry` en champ privé, `m_registry` |
| Référence `SYNE-111` tracée | `TODO: à nettoyer` sans trace |

## 12. Vérification

Les conventions ci-dessus sont **opposables en revue** (`CONTRIBUTING.md` §1) et contrôlées automatiquement :

- C# : `dotnet format` + analyseurs SDK (`CI_CD.md` §3).
- Python : `black` + `flake8` (`echos/pyproject.toml`).
- JavaScript : `npm test --prefix syne-mock` ; la cohérence de style est vérifiée en revue.
- C++/Unreal : compilation dans l'hôte `prism/LDK` + style Epic en revue.
- Markdown : liens relatifs valides, en-tête métadonnées présent, table de mise à jour à jour.

---

## Points restés ouverts dans ce document

- [ ] Activer un linter JS (ESLint/Prettier) sur `syne-mock/` pour automatiser la partie §7 (`CI_CD.md`).
- [ ] Confirmer la longueur de ligne C# (120) dans `.editorconfig` pour refléter la règle choisie.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 8 octobre 2026 | Création | Aucun document ne couvrait le nommage (constat : `CONTRIBUTING.md` renvoie aux linters sans énoncer les règles). |
