# Composants LIVEX et ce que l'installateur en fait

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : [`SPECIFICATION.md`](SPECIFICATION.md),
[ADR-006](adr/ADR-006-perimetre-composants-v02.md),
[ADR-008](adr/ADR-008-acquisition-modules-facultatifs.md)

---

## 1. Objet

Ce document répond à une question précise : **pour chaque composant du dépôt,
quelle est sa nature, quel artefact le rend utilisable, d'où vient cet artefact,
et que doit en faire l'installateur ?**

Il distingue trois choses que l'on confond facilement :

- les **composants obligatoires**, embarqués et non décochables : Launcher,
  SYNE, ECHOS, avec leurs éléments de support ;
- **PRISM**, **composant de base coché par défaut et désactivable**, embarqué et installé sans réseau ;
- les **modules facultatifs**, décochés par défaut et acquis depuis la release :
  PRISM-LDK, documentation, SYNE-Mock ;
- les **artefacts de développement** qui restent dans le dépôt et ne sont pas
  installés.

## 2. Vue d'ensemble

| Composant | Rôle dans LIVEX | Nature | Autorité |
| :-- | :-- | :-- | :-- |
| **Launcher** | point d'entrée, orchestration, observation locale, archivage | application .NET | orchestre, ne simule pas |
| **SYNE** | moteur de simulation, état du monde, déterminisme | service .NET | autorité unique de l'état simulé |
| **ECHOS** | ingestion, analyse, métriques d'émergence, API d'observation | service Python | observe, ne décide pas |
| **PRISM** | représentation 3D et interaction | application **packagée** Windows/Linux | reflète l'état, ne décide pas |
| **PRISM-LDK** | plugin d'intégration pour un projet Unreal | archive ZIP de plugin | adaptateur, pas un moteur |
| **SYNE-Mock** | émulation du contrat SYNE pour le développement | service Node | outil, pas un moteur |
| **Contrats** | schémas et protocoles entre composants | documents + schéma JSON | référence normative |
| **Ressources** | icônes, licences, mentions | fichiers | — |

Règle produit : **SYNE est la seule autorité**. ECHOS et PRISM consomment ses
contrats ; le Launcher les orchestre. L'installateur ne change jamais cette
hiérarchie : il installe des artefacts et vérifie leur présence.

## 3. Fiches composants

### 3.1 Launcher

| Élément | Valeur vérifiée |
| :-- | :-- |
| **Rôle** | fenêtre native, orchestration des composants, consoles de logs, analyse, paquets `.livexp` |
| **Langage / runtime** | C# / .NET 10 (`net10.0`), Avalonia 11.3.22 |
| **Artefact** | `livex-launcher[.exe]` + bibliothèques managées |
| **Contrat** | détecte `component.json` dans `components/` à côté de son exécutable |
| **Ports** | aucun port propre ; il réserve ceux des composants |
| **Données** | `LIVEX_DATA` ou `~/.livex-data` (`workspace/`, `packages/`, `archives/`, `sessions/`, `cache/`, `echos/`) |
| **CLI** | `--check`, `--package <fichier>` |
| **Installation** | **à la racine du répertoire logiciel** ; sinon `BaseDirectory/components` ne résout plus |
| **Provenance** | socle, charge utile de la distribution |
| **Rôle de l'installateur** | copier la publication self-contained, créer les raccourcis, ne jamais réécrire son code |

Le Launcher n'installe pas ses composants : il les détecte et signale ceux qui
manquent. L'installateur ne doit donc pas lui déléguer l'installation.

### 3.2 SYNE

| Élément | Valeur vérifiée |
| :-- | :-- |
| **Rôle** | moteur de simulation déterministe, mode headless, contrôle HTTP, flux WebSocket |
| **Langage / runtime** | C# / .NET 10 |
| **Artefact** | `Simulation.Console` (nom publié `Simulation.Console[.exe]`) |
| **Manifeste** | `syne/component.json`, `schema: 1`, version `0.17.0`, `runtime: dotnet` |
| **Endpoints** | contrôle HTTP `5181`, flux WebSocket `5180` (valeurs par défaut du manifeste) |
| **Santé** | `GET /health/ready`, intervalle 500 ms |
| **Délais** | démarrage 30 s, arrêt 15 s |
| **Installation** | `components/syne/`, manifeste copié tel quel |
| **Provenance** | socle |
| **Rôle de l'installateur** | publier self-contained, copier, vérifier la présence du binaire et du manifeste |

### 3.3 ECHOS

| Élément | Valeur vérifiée |
| :-- | :-- |
| **Rôle** | API d'observation headless : ingestion, analyse, rapports, métriques |
| **Langage / runtime** | Python ≥ 3.11 (livré en 3.12), FastAPI, uvicorn, pydantic, httpx, websockets, pyarrow |
| **Artefact** | `echos-launcher` (Linux) / `echos-launcher.cmd` (Windows) |
| **Manifeste** | `echos/component.json`, `schema: 1`, version `0.1.0`, `runtime: script` |
| **Endpoints** | contrôle HTTP `5000` |
| **Santé** | `GET /health/ready`, intervalle 1000 ms |
| **Délais** | démarrage 30 s, arrêt 5 s |
| **Données** | base d'analyse `<LIVEX_DATA>/echos/analytics.sqlite` (hors work-dir) |
| **Installation** | `components/echos/` + `runtime/` CPython + `site-packages/` |
| **Provenance** | socle |
| **Rôle de l'installateur** | embarquer le runtime et les dépendances, ne jamais dépendre du Python système |

### 3.4 Contrats

| Élément | Valeur vérifiée |
| :-- | :-- |
| **Rôle** | schéma de manifeste de composant, documents d'intégration, validation CI |
| **Artefact** | `launcher/contracts/component-manifest-v1.schema.json`, `INTEGRATION-CONTRACT-v1.md`, exemples |
| **Runtime** | aucun |
| **Installation** | `contracts/` |
| **Provenance** | socle |
| **Rôle de l'installateur** | copier, ne pas modifier, ne pas confondre avec son propre manifeste de distribution |

### 3.5 PRISM — application packagée

| Élément | Valeur |
| :-- | :-- |
| **Rôle** | application de rendu et d'interaction : elle reflète l'état de SYNE et présente les résultats |
| **Nature livrée** | **application packagée par plateforme** (`win-x64`, `linux-x64`), construite depuis le projet PRISM |
| **Runtime requis sur le poste** | **aucun** : la build packagée est autonome ; Unreal Engine n'est pas installé par l'installateur et n'est pas nécessaire à l'utilisateur |
| **Manifeste de composant** | **aucun** : PRISM n'expose pas de service de contrôle, donc le contrat `component-manifest-v1` ne s'applique pas |
| **Détection par le Launcher** | non — un répertoire sans `component.json` est ignoré |
| **Installation** | `components/prism/` |
| **Intégration** | raccourci de menu propre à PRISM (application destinée à l'utilisateur) |
| **Statut** | **composant de base** : proposé **coché par défaut**, désactivable ; sa désactivation est avertie |
| **Provenance** | charge utile de la distribution (`acquisition.kind = "payload"`), aucun téléchargement |
| **Rôle de l'installateur** | extraire depuis la charge utile vers `components/prism/`, créer le raccourci ; ne jamais modifier la build |

PRISM n'est donc **pas** décrit ici comme un projet à compiler par
l'utilisateur, ni comme un composant du Launcher. La chaîne de production de la
build packagée relève de PRISM ; l'installateur ne connaît que l'archive publiée.
Un utilisateur qui le décoche perd le rendu et l'interaction 3D : le message de
perte fonctionnelle est obligatoire (base §3.3).

### 3.6 PRISM-LDK — plugin Unreal en archive

| Élément | Valeur |
| :-- | :-- |
| **Rôle** | plugin `PrismLdk` : types, transport, événements Blueprint pour un projet Unreal |
| **Nature livrée** | archive **ZIP** du plugin, publiée telle quelle |
| **Runtime requis** | Unreal Engine, dans le **projet hôte de l'utilisateur** — jamais installé ni modifié par l'installateur |
| **Manifeste de composant** | aucun ; ce n'est pas un service |
| **Installation** | `plugins/prism-ldk/PrismLdk-<version>.zip`, archive intacte |
| **Provenance** | module facultatif : actif `prism-ldk-<version>.zip` de la release |
| **Rôle de l'installateur** | télécharger, vérifier, déposer l'archive ; expliquer où la copier ; **ne pas l'extraire** dans LIVEX |

Deux règles :

- cocher **PRISM-LDK** n'active pas PRISM, et réciproquement ;
- l'archive déposée doit être **bit à bit** celle de la release, pour que
  l'empreinte de l'inventaire et la copie dans un projet Unreal restent
  vérifiables.

### 3.7 Documentation hors ligne

| Élément | Valeur |
| :-- | :-- |
| **Rôle** | documentation consultable sans réseau, rendue par le lecteur Markdown du Launcher |
| **Nature livrée** | archive ZIP de documents Markdown |
| **Installation** | `docs/` |
| **Provenance** | module facultatif : actif `livex-docs-<version>.zip` |
| **Rôle de l'installateur** | télécharger (ou embarquer en profil `full`), vérifier, extraire ; ne pas dupliquer les textes dans ses ressources |

**Prérequis côté Launcher :** la publication du Launcher embarque aujourd'hui
`docs/**/*.md` (`Launcher.App.csproj`) ; pour que la documentation soit réellement
facultative, cette copie doit être retirée de la charge utile du socle et le
Launcher doit fonctionner sans `docs/`. C'est une évolution du Launcher, suivie
par une issue dédiée.

### 3.8 SYNE-Mock

| Élément | Valeur |
| :-- | :-- |
| **Rôle** | émulation du contrat SYNE pour le développement client |
| **Langage / runtime** | Node.js 20+ (`src/cli.js`) ; l'archive publiée **embarque son runtime** |
| **Manifeste** | `syne-mock/component.json`, version `0.1.0`, `runtime: script` |
| **Endpoints** | contrôle HTTP `5181`, WebSocket `5180` — **conflit avec SYNE** |
| **Installation** | `components/syne-mock/` |
| **Provenance** | module facultatif : actif `syne-mock-<version>.zip` |
| **Rôle de l'installateur** | télécharger, vérifier, extraire ; ne jamais lancer le mock ni l'opposer à SYNE |

Une archive SYNE-Mock sans runtime Node ne doit pas être publiée : le module
serait inutilisable sur une machine sans Node. L'installateur ne propose que ce
qui est réellement exécutable.

## 4. Matrice d'installabilité

| Composant | Nature | Provenance | Runtime autonome | Installé par défaut | Motif |
| :-- | :-- | :-- | :-- | :-- | :-- |
| Launcher | application .NET | socle | oui (self-contained) | **oui, verrouillé** | point d'entrée |
| SYNE | service .NET | socle | oui (self-contained) | **oui, verrouillé** | moteur |
| ECHOS | service Python | socle | oui (CPython embarqué) | **oui, verrouillé** | analyse |
| Contrats | documents | socle | sans objet | **oui, support non décochable** | référence |
| Ressources | fichiers | socle | sans objet | **oui, support non décochable** | icônes, licences |
| PRISM | application packagée | **socle (payload)** | oui | **non, composant de base coché par défaut** | rendu et interaction |
| PRISM-LDK | archive de plugin | actif de release | non (projet Unreal hôte) | **non, facultatif** | intégration Unreal |
| Documentation | archive Markdown | actif de release | sans objet | **non, facultatif** | confort hors ligne |
| SYNE-Mock | service Node | actif de release | oui (Node dans l'archive) | **non, facultatif** | développement |

Un module dont l'actif manque pour la plateforme n'apparaît pas comme une case :
il est déclaré **indisponible**, avec sa raison (ADR-006 §5). PRISM, embarqué,
est toujours disponible avec la distribution ; seule sa sélection est facultative.

## 5. Ports et dépendances

| Composant | Port par défaut | Dépend de | Remarque |
| :-- | :-- | :-- | :-- |
| SYNE | 5181 (HTTP), 5180 (WS) | — | valeurs du manifeste, pas une garantie de disponibilité |
| ECHOS | 5000 (HTTP) | SYNE (flux) | l'installateur ne réserve aucun port |
| SYNE-Mock | 5181/5180 | — | exclusif avec SYNE réel ; l'installateur ne le démarre pas |
| PRISM | aucun port exposé | SYNE (contrat) | application ; connexion sortante vers SYNE |
| PRISM-LDK | aucun port propre | Unreal Engine (projet hôte) | l'installateur ne le charge pas |

L'installateur **ne réserve pas** les ports et ne modifie pas les manifestes :
c'est le Launcher qui résout les conflits au démarrage (`--check` les signale).

## 6. Conséquences pour l'installateur

1. **Deux profils** : `core` (socle **et PRISM** ; modules facultatifs à la
   demande) et `full` (socle, PRISM et les trois modules embarqués). Le socle et
   PRISM ne dépendent jamais du réseau.
2. **Placement** : le Launcher reste à la racine ; les composants dans
   `components/<id>/` — SYNE, ECHOS, PRISM (base) et SYNE-Mock (module) ; le LDK
   en archive dans `plugins/prism-ldk/`. Toute autre arborescence casse la
   détection ou la copie du plugin.
3. **Pas d'orchestration** : l'installateur ne démarre pas les composants, ne
   réserve pas les ports, ne réécrit pas les manifestes et n'extrait pas le LDK
   dans un projet Unreal.
4. **Licences** : GPL-3.0-or-later pour LIVEX ; CPython, wheels, .NET, Node et
   les bibliothèques tierces embarquées dans la build PRISM ont leurs propres
   licences et doivent figurer dans `THIRD-PARTY-NOTICES.md`. La build PRISM est
   distribuée comme **produit packagé** ; les conditions de son moteur de rendu
   sont vérifiées séparément (issue PRISM).
5. **Mise à jour** : les composants ont des cycles indépendants, mais
   l'installateur publie un ensemble cohérent ; une version de composant n'entre
   dans une distribution qu'après validation de la pile.
6. **Disponibilité** : un module facultatif n'est proposé que si son actif est
   publié pour la plateforme, avec sa taille et son empreinte ; PRISM, embarqué,
   est toujours disponible avec la distribution.

## 7. Références

- `launcher/contracts/component-manifest-v1.schema.json`
- `launcher/Launcher.Infrastructure/ManifestDetector.cs`
- `launcher/Launcher.Infrastructure/WorkspaceLayout.cs`
- `launcher/Launcher.App/Launcher.App.csproj` (copie actuelle de `docs/`)
- `syne/component.json`, `echos/component.json`, `syne-mock/component.json`
- `echos/pyproject.toml`, `syne-mock/package.json`
- `docs/prism/README.md`, `docs/prism/ARCHITECTURE.md`
- `docs/launcher/PACKAGING.md`

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Analyse des composants pour le périmètre de l'installateur |
| 21 octobre 2026 | Révision | PRISM packagé, LDK en ZIP, SYNE-Mock et documentation en archives facultatives ; provenance et runtime corrigés |
