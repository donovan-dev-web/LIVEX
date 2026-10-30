# Commandes de l'installateur LIVEX

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT] — reflète l'implémentation de `installer/`, vérifiée sur exécution
**Dernière mise à jour** : 29 octobre 2026 (alignement V1)
**Dépend de** : [`ARCHITECTURE.md`](ARCHITECTURE.md) §18, [`SPECIFICATION.md`](SPECIFICATION.md) `INS-F-90` à `99`

---

## 1. Objet

Ce document est la référence des commandes de `livex-setup` : chaque option
listée ici a été exécutée sur un binaire construit depuis ce dépôt. Une option
qui ne figure pas dans ce document n'existe pas : elle est refusée avec le code
`1` et le message `[Error] Option inconnue : …`. C'est le cas de `--log`, retiré
parce qu'il n'était pas appliqué : il n'est plus reconnu du tout.

L'assistant graphique reste le mode par défaut. Les modes non interactifs
servent la vérification (CI, recette) et les opérations scriptées ; ils ne
demandent jamais de saisie et répondent toujours par un code de sortie.

## 2. Invocation

| Plateforme | Commande |
| :-- | :-- |
| Windows | `livex-setup.exe` |
| Linux | `./livex-setup` |

L'exécutable vit à la racine de la distribution, à côté de `distribution.json`
et du répertoire `payload/`. S'il est lancé sans distribution valide à côté de
lui, l'assistant bascule en monde fictif (voir §6) — et l'indique à l'écran.

## 3. Référence des options

| Option | Effet | Interface | Code de sortie |
| :-- | :-- | :-- | :-- |
| *(aucune)* | assistant guidé complet | oui | `0` |
| `--check` | contrôles préalables, sortie textuelle | non | `0`, `2` |
| `--unattended --config <fichier>` | installation sans interaction | non | `0`, `2`, `3`, `5`, `6` |
| `--uninstall` | désinstallation de l'installation détectée | non | `0`, `2`, `3`, `4` |
| `--repair` | vérification et restauration depuis la distribution d'origine | non | `0`, `2`, `3`, `6` |
| `--version` | version de l'installateur et de la distribution | non | `0` |
| `--help` (`-h`, `/?`) | aide, options et codes de sortie | non | `0` |
| `--demo` | monde fictif : parcours complet, aucune écriture | oui | `0` |
| `--demo-scenario <nom>` | scénario du monde fictif, implique `--demo` | oui | `0`, `1` |
| `--maintenance` | assistant ouvert sur l'écran de maintenance | oui | `0` |
| `--lang <fr\|en>` | langue de l'assistant (défaut : `fr`) | oui | `0` |
| `--offline-assets <dir>` | actifs facultatifs lus depuis un répertoire local, sans réseau | oui / non | `0`, `3`, `6` |
| `--no-network` | aucune requête réseau autorisée | oui / non | `0`, `6` |
| `--verbose` | trace de diagnostic sur la sortie d'erreur (ou `LIVEX_SETUP_VERBOSE=1`) | oui / non | `0` |
| `--test` | alias historique de `--demo` | oui | `0` |
| `--scope a\|b\|c` | portée de désinstallation, avec `--uninstall` | non | voir §5 |
| `--purge-research` | supprime aussi les résultats de recherche (portée C) | non | voir §5 |
| `--confirm-workspace` | confirme la suppression de l'espace de travail (portée C) | non | voir §5 |

Une option inconnue, un nom de scénario non reconnu (`--demo-scenario bogus`) ou
une portée de désinstallation hors `a|b|c` (`--scope d`) échouent avec le code
`1`, sans ouvrir l'assistant. Les valeurs de scénario et de portée sont
insensibles à la casse.

## 4. Assistant guidé (défaut)

```bash
livex-setup            # Windows : livex-setup.exe
```

Huit étapes : accueil, langue, licence, composants, chemins, récapitulatif,
progression, résultat. Les branches maintenance et désinstallation partent de
l'accueil lorsqu'une installation est détectée.

`--maintenance` ouvre directement la branche maintenance (mise à jour,
réparation, gestion des composants, changement d'espace de travail).

## 5. Modes non interactifs

### 5.1 `--check`

Préflight seul : licence, plateforme, politique de chemins, chevauchements,
manifeste, dépendances entre composants, charge utile. Aucune écriture, aucune
interface, sortie textuelle stable.

```bash
livex-setup --check
```

```text
[Pass] license
[Pass] platform: win-x64
[Pass] path-policy
[Pass] workspace
[Pass] overlap
[Pass] writable
[Pass] process
[Pass] prerequisites
[Pass] manifest: 9 composants
[Pass] requirements
[Pass] payload: 42 fichiers
PREFLIGHT_OK
```

En monde fictif (`--check --demo`), `writable`, `process` et `prerequisites`
sont `[Simulated]` et `payload` affiche `[Info] Aucune charge utile (simulation)` :
aucun contrôle réel n'a été effectué.

| Situation | Sortie |
| :-- | :-- |
| distribution conforme | `PREFLIGHT_OK`, code `0` |
| distribution dégradée (charge utile manquante, chemin non inscriptible…) | `[Fail] …` puis `PREFLIGHT_FAIL`, code `2` |

Sur une distribution réelle, l'absence de `payload/` est un échec : `--check` ne
se contente pas de suspendre les écritures, il diagnostique. Avec `--demo`, les
contrôles qui dépendent de la machine sont marqués `[Simulated]` et la sortie se
termine par `PREFLIGHT_OK`.

### 5.2 `--unattended --config <fichier>`

Installation complète sans interaction, avec les réponses d'un fichier JSON
(§7). Aucune valeur n'est devinée : un champ obligatoire absent, un chemin
relatif, une licence non acceptée ou un composant inconnu font échouer la
commande avec le code `2` avant toute écriture.

```bash
livex-setup --unattended --config answers.json
```

Une installation déjà présente dans la même version sort en `5` ; un actif
facultatif demandé mais indisponible (réseau, 404, empreinte, `--no-network`)
sort en `6`.

### 5.3 `--uninstall`

Sans portée explicite, la portée `a` (logiciel et intégrations) s'applique.
La portée `c` supprime aussi l'espace de travail : elle exige
`--confirm-workspace`, sinon l'opération est annulée avec le code `2`.

La désinstallation ne demande pas `distribution.json` : elle s'appuie sur
l'enregistrement et l'inventaire de l'installation détectée. Une installation
inconnue de l'installateur ne peut donc pas être désinstallée par cette voie,
même avec une distribution complète à côté de l'exécutable.

```bash
livex-setup --uninstall                       # portée A
livex-setup --uninstall --scope b             # logiciel + configuration
livex-setup --uninstall --scope c --confirm-workspace
livex-setup --uninstall --scope c --confirm-workspace --purge-research
```

| Situation | Code |
| :-- | :-- |
| désinstallation complète | `0` |
| aucune installation détectée | `2` |
| portée inconnue (`--scope d`) | `1` |
| portée C sans `--confirm-workspace` | `2` |
| échec en cours d'opération | `3` |
| des éléments résiduels subsistent (fichier verrouillé, droit refusé) | `4` |

Les résidus sont listés sur la sortie standard : le code `4` signale une
désinstallation partielle, jamais un succès.

### 5.4 `--repair`

Vérifie l'installation détectée et restaure les fichiers manquants depuis la
distribution d'origine. Exige `distribution.json` à côté de l'exécutable.

```bash
livex-setup --repair
```

| Situation | Code |
| :-- | :-- |
| réparation appliquée (ou rien à restaurer) | `0` |
| aucune installation détectée, ou distribution d'origine absente | `2` |
| échec d'écriture | `3` |
| actif facultatif indisponible | `6` |

### 5.5 `--version` et `--help`

```bash
livex-setup --version
```

```text
livex-setup 0.1.0
distribution: LIVEX 0.2.0 (profil core, canal stable)
```

En monde fictif, la seconde ligne est suffixée ` — monde fictif`. `--help`
affiche les options et la table des codes de sortie.

> **Versions** : les valeurs ci-dessus sont mesurées avant le gel de la V1. Le
> passage à `livex-setup 1.0.0` et `distribution 1.0.0` (plan V1, décision D1)
> appartient à la phase de gel : il n'est pas appliqué à ce stade.

## 6. Monde fictif (vérification sans installation)

Le monde fictif déroule le parcours réel sur des données inventées : aucune
écriture disque, aucun téléchargement, aucun raccourci, aucune clé de registre,
aucun fichier de journal.

```bash
livex-setup --demo                            # installation nominale
livex-setup --demo --demo-scenario=existing   # accueil maintenance
livex-setup --demo --demo-scenario=prism      # PRISM indisponible
livex-setup --demo --demo-scenario=failure    # échec en cours d'écriture
livex-setup --check --demo                    # diagnostic textuel simulé
```

| Nom de scénario | Alias acceptés | Contenu |
| :-- | :-- | :-- |
| `fresh` (défaut) | `neuve`, `nouvelle` | machine vierge, installation nominale |
| `existing` | `existante`, `maintenance` | installation `0.1.0` présente, distribution `0.2.0-demo` |
| `prism` | `prism-absent`, `sans-prism` | PRISM absent du build, avertissement attendu |
| `failure` | `echec`, `échec` | échec injecté aux deux tiers des écritures |

Le monde fictif est refusé là où il produirait un succès trompeur, mais pour
deux raisons différentes :

- `--unattended` et `--repair` sortent en `2` faute de **distribution réelle**
  (le manifeste et la charge utile manquent : rien à installer, rien à restaurer) ;
- `--uninstall` sort en `2` faute d'**installation réelle détectée** — il
  n'a pas besoin de `distribution.json`, une désinstallation ne restaure rien :
  elle s'appuie sur l'enregistrement et l'inventaire de l'installation présente.

Seul `--check --demo` reste autorisé, et il annonce explicitement qu'aucun
contrôle réel n'a été effectué.

## 7. Fichier de réponses de `--unattended`

Objet JSON, commentaires et virgules finales tolérés, champs inconnus ignorés.

| Champ | Type | Obligatoire | Règle |
| :-- | :-- | :-- | :-- |
| `installDir` | chaîne | oui | chemin **absolu** du répertoire logiciel |
| `workspaceDir` | chaîne | oui | chemin **absolu** de l'espace de travail |
| `acceptLicense` | booléen | oui | doit valoir `true` ; personne n'accepte la licence à la place de l'utilisateur |
| `components` | tableau | non | identifiants du manifeste ; les composants obligatoires sont ajoutés d'office, un identifiant inconnu est refusé |
| `dataRoot` | chaîne | non | racine de données ; par défaut `LIVEX_DATA` ou `~/.livex-data` |
| `desktopShortcut` | booléen | non | raccourci sur le bureau (défaut `false`) |
| `launchAfterInstall` | booléen | non | lance le Launcher à la fin (défaut `false`) |
| `offlineAssetsDir` | chaîne | non | actifs facultatifs lus depuis ce répertoire |

Exemple complet :

```json
{
  "installDir": "C:/Users/alice/AppData/Local/Programs/LIVEX",
  "workspaceDir": "D:/LIVEX/workspace",
  "dataRoot": "D:/LIVEX/data",
  "components": ["launcher", "syne", "echos", "contracts", "resources", "prism"],
  "acceptLicense": true,
  "desktopShortcut": true,
  "launchAfterInstall": false
}
```

Sous Linux, les mêmes champs avec des chemins POSIX
(`/home/alice/.local/opt/livex`, `/home/alice/LIVEX/workspace`).

## 8. Codes de sortie

| Code | Sens | Modes concernés |
| :-- | :-- | :-- |
| `0` | succès | tous |
| `1` | erreur d'usage : option inconnue, scénario inconnu | tous |
| `2` | préflight en échec, opération annulée, rien à désinstaller ou à réparer, fichier de réponses invalide, monde fictif en mode non interactif | `--check`, `--unattended`, `--uninstall`, `--repair` |
| `3` | échec pendant l'opération | `--unattended`, `--uninstall`, `--repair` |
| `4` | désinstallation partielle, résidus signalés | `--uninstall` |
| `5` | installation déjà présente dans la même version | `--unattended` |
| `6` | actif facultatif indisponible (réseau, 404, empreinte, `--no-network`) | `--unattended`, `--repair` |

Un mode non interactif n'attend jamais de saisie : une décision impossible
devient un message explicite et un code, jamais une attente.

## 9. Journal

Le journal est écrit dans la racine de données, un fichier par opération :

| Plateforme | Emplacement |
| :-- | :-- |
| Windows | `%LIVEX_DATA%` ou `%USERPROFILE%\.livex-data`, sous `logs\installer-<AAAAMMJJ-HHMMSS>.log` |
| Linux | `$LIVEX_DATA` ou `~/.livex-data`, sous `logs/installer-<AAAAMMJJ-HHMMSS>.log` |

Une ligne par événement, en UTF-8 : horodatage ISO 8601, niveau, étape,
opération, résultat, code et chemin éventuels. Le chemin exact du journal est
affiché à la fin de l'opération (`Journal : …`) et sur l'écran de résultat.

Il n'existe pas d'option pour choisir le fichier de journal : le chemin est
déterminé par la racine de données (`LIVEX_DATA`). Le drapeau `--log` a été
retiré — de l'aide et du parseur — parce qu'il n'était pas appliqué : il est
désormais refusé comme option inconnue (`1`). Il fera l'objet d'un ajout
explicite s'il devient nécessaire.

## 10. Scénarios d'usage

Vérifier une distribution avant publication :

```bash
livex-setup --check                      # doit afficher PREFLIGHT_OK, code 0
```

Installer sur une machine de recette :

```bash
livex-setup --unattended --config recette.json --no-network
```

Installer hors ligne avec les actifs locaux :

```bash
livex-setup --unattended --config recette.json --offline-assets ./actifs --no-network
```

Vérifier toute l'interface sans rien installer :

```bash
livex-setup --demo
livex-setup --demo --demo-scenario=existing
livex-setup --demo --demo-scenario=failure
```

Désinstaller en conservant les résultats de recherche :

```bash
livex-setup --uninstall --scope b
```

Réparer une installation :

```bash
livex-setup --repair
```

## 11. État d'implémentation des options (au 29 octobre 2026)

Deux options sont **reconnues par le parseur mais sans effet** aujourd'hui :

| Option | État | Conséquence |
| :-- | :-- | :-- |
| `--offline-assets <dir>` | acceptée, transportée jusqu'à `InstallRequest`, jamais lue par le plan ni l'exécution | un module coché est téléchargé, pas lu depuis le répertoire local |
| `--no-network` | idem | aucune requête n'est bloquée par cette option |

Le cache `cache/downloads` et la reprise `Range` décrits dans `ARCHITECTURE.md`
§7.1 ne sont pas branchés non plus : `InstallerEngine` télécharge directement
vers la destination finale (dette `INS-L-12`, tâche T5 du plan d'alignement).
Tant que ce câblage n'est pas fait, une installation `core` sans module
facultatif n'émet aucune requête, mais un module coché dépend du réseau.

## 12. Ce que les commandes ne font pas

- aucune installation machine, aucun service, aucune élévation de privilèges ;
- aucune mise à jour automatique, aucune vérification de version en arrière-plan ;
- aucune requête réseau pour le socle ni pour PRISM, même sans `--no-network` ;
- aucun module facultatif installé dans un projet Unreal : le LDK est déposé en
  archive, le projet hôte n'est pas modifié ;
- aucune association de fichiers autre que `.livexp` ([ADR-009](adr/ADR-009-association-fichiers-livexp.md)) ;
- aucune suppression d'un paquet `.livexp` ou d'une archive hors portée C
  explicitement confirmée ;
- aucune installation sur macOS ni sur une architecture non x64.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 29 octobre 2026 | Création | Les commandes existent et sont vérifiées sur exécution ; `--log`, non appliqué, est retiré (refusé comme option inconnue) et signalé §9 |
| 29 octobre 2026 | §3 validation stricte, §5.3 portée inconnue, §6 distinction distribution / installation réelle | Revue croisée : `--scope` et `--log` durcis ; `--uninstall` ne dépend pas de `distribution.json` |
| 29 octobre 2026 | §5.1 sortie réelle, §11 état d'implémentation, §12 renuméroté | Check-up : `--offline-assets` et `--no-network` sans effet au 29 octobre 2026 |
| 29 octobre 2026 | §5.5 note de version | Plan V1 (D1) : le bump `1.0.0` relève de la phase de gel, pas de l'état actuel |
