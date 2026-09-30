# INTEGRATION_CONTRACT.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `COMPONENTS.md`, `ARCHITECTURE.md`, `OBSERVABILITY.md`, `NETWORK.md`
**Source Monographie** : —

---

## 1. Objet

Ce document définit ce que **chaque composant** — SYNE, ECHOS, PRISM, et les
composants réseau — doit exposer pour que le Launcher puisse l'orchestrer.

Il est volontairement **minimal**, et indépendant du protocole de données, afin que
le Launcher ne dépende pas d'une décision encore ouverte. Tout ce qui touche au
format des messages `snapshot` et `event` n'est **pas** traité ici.

Règle d'or : un composant respectant ce contrat **fonctionne sans Launcher**. Le
contrat ne crée aucune dépendance.

Ce document a deux lectures :

- les **exigences normatives**, en « doit », qui engagent les composants ;
- le **contrat opératoire**, qui donne la forme concrète — champs, endpoints, codes
  de sortie — pour que le Launcher puisse être écrit et testé sans attendre.

## 2. Manifeste de composant

Chaque composant installe un fichier `component.json` à sa racine.

```json
{
  "schema": 1,
  "id": "syne",
  "name": "SYNE",
  "version": "0.1.0",
  "protocolVersion": 1,
  "type": "service",
  "executable": { "windows": "SYNE.exe", "linux": "syne" },
  "capabilities": ["headless", "seed", "tickLimit", "export", "pause"],
  "requires": [],
  "endpoints": { "control": true, "ws": true, "metrics": true }
}
```

| Exigence | Précision |
| :-- | :-- |
| **Fournir un manifeste** | Fichier à la racine de l'installation. |
| **Versionner le manifeste** | Le champ `schema` est obligatoire et croît uniquement par ajout de champs optionnels. |
| **Être lisible sans lancer** | Le Launcher détecte et vérifie la compatibilité **sans démarrer** le composant. |
| **Déclarer ses capacités** | `capabilities` permet d'activer ou de désactiver une fonction du Launcher selon ce que la version installée sait faire. |
| **Ne pas dépendre de l'environnement** | Aucun chemin absolu, aucune variable d'environnement non déclarée. |

## 3. Ligne de commande

### 3.1 Arguments communs

| Argument | Rôle |
| :-- | :-- |
| `--instance-id <id>` | Identifiant d'instance, utilisé par le registre, les journaux et les métriques |
| `--port <n>` / `--control-port <n>` / `--ws-port <n>` | Ports alloués par le Launcher |
| `--bind <addr>` | Adresse d'écoute, `127.0.0.1` par défaut |
| `--work-dir <path>` | Répertoire de travail et de sortie |
| `--log-dir <path>` | Répertoire des journaux |
| `--config <path>` | Fichier de configuration résolu |
| `--correlation-id <id>` | Identifiant de corrélation de l'opération |
| `--headless` | Pas d'interface, quand cela s'applique |
| `--version` / `--info` | Affiche version et manifeste, puis quitte avec le code 0 |

### 3.2 Variables d'environnement

| Variable | Rôle |
| :-- | :-- |
| `LIVEX_SESSION_TOKEN` | Jeton de session exigé pour les commandes de contrôle. **Jamais** passé en argument, qui serait visible dans la liste des processus. |
| `LIVEX_INSTALL_ROOT` | Racine d'installation |

### 3.3 Arguments propres à SYNE

Indispensables pour l'exécution autonome, donc pour les campagnes.

| Argument | Rôle |
| :-- | :-- |
| `--simulation <id>` | Simulation à charger |
| `--seed <n>` | Seed du run |
| `--ticks <n>` | Limite de ticks, avec sortie automatique à la fin |
| `--export-dir <path>` | Répertoire de destination des résultats |
| `--autostart` | Démarre sans attendre une commande, mode batch |

> **[À CONFIRMER]** : SYNE sait-il déjà tourner sans interaction et se terminer seul à
> la fin des ticks ? Sinon c'est un prérequis bloquant pour toute campagne
> multi-run. C'est la porte **G2** de `ROADMAP.md`.

## 4. Codes de sortie

Le Launcher distingue une fin normale d'un crash. Sans cette distinction, une
campagne ne peut pas appliquer sa politique d'échec.

| Code | Signification | Traitement côté Launcher |
| :-- | :-- | :-- |
| 0 | Fin normale : run terminé, ou arrêt demandé proprement | `Completed` |
| 1 | Erreur non classée | `UnknownError` |
| 2 | Configuration invalide | `ConfigurationError` |
| 3 | Port indisponible, échec d'écoute | `ConfigurationError` |
| 4 | Ressource manquante : simulation, fichier | `InstallationError` |
| 5 | Incompatibilité de version ou de protocole | `CommunicationError` |
| 6 | Erreur de simulation ou d'analyse en cours d'exécution | `SimulationError` / `AnalysisError` |
| 7 | Espace disque insuffisant ou écriture impossible | `FileSystemError` |
| 130 / 143 | Interruption par signal | Annulation |
| autre | Crash | `ProcessError` |

## 5. Arrêt propre et confinement des processus

### 5.1 Séquence d'arrêt imposée

1. **Demande d'arrêt** : `POST /control/shutdown` avec jeton, ou signal. Sous Linux
   le signal est `SIGTERM`. Sous Windows, la commande HTTP est la voie de référence,
   les signaux console étant peu fiables pour un processus lancé sans console.
   **[À vérifier par spike.]**
2. Le composant **termine l'état courant**, ferme ses WebSocket, **vide** ses
   exports et ses journaux, et écrit un marqueur `shutdown.ok`.
3. **Délai de grâce** configurable, par exemple `gracefulTimeoutSeconds`.
4. Au-delà : arrêt forcé par le gestionnaire de processus, et de tout l'arbre de
   processus. L'état est `Failed` ou `Cancelled` selon le contexte.

### 5.2 Aucun processus orphelin

| Exigence | Précision |
| :-- | :-- |
| **Confinement** | Chaque composant est placé dans un **Job Object** sous Windows, avec terminaison à la fermeture, ou dans un **groupe de processus** sous Linux. |
| **Survivance impossible** | Si le Launcher s'interrompt brutalement, aucun composant ne survit. |
| **Autonomie** | Un composant lancé sans Launcher ignore simplement ce mécanisme. |

### 5.3 Instance unique et pré-vol

- Un seul Launcher peut opérer une installation à la fois ; le second doit le dire
  explicitement au lieu d'échouer en silence.
- Avant tout démarrage, le Launcher **teste la disponibilité des ports** et échoue
  avec une erreur nommant le processus occupant s'il est identifiable.

## 6. Endpoints standard

Sur le port de contrôle du composant, avec écoute locale par défaut et jeton pour
les écritures.

| Endpoint | Méthode | Auth | Rôle |
| :-- | :-- | :-- | :-- |
| `/health/live` | GET | non, en local | Vivant |
| `/health/ready` | GET | non, en local | Prêt |
| `/health/details` | GET | non, en local | Détail des vérifications |
| `/info` | GET | non, en local | Manifeste et état courant |
| `/metrics` | GET | non, en local | Métriques, format Prometheus texte et/ou JSON |
| `/control/shutdown` | POST | **jeton** | Arrêt propre |
| `/control/config` | GET et PUT | **jeton** | Lecture et modification de configuration, si supporté |

SYNE conserve en plus ses commandes de simulation — `start`, `pause`, `resume`,
`reset`, seed, ticks — et son WebSocket `snapshot`/`event`, tels que définis par le
protocole.

## 7. Définition de « prêt »

| Composant | `ready` signifie |
| :-- | :-- |
| SYNE | API de contrôle en écoute **et** simulation chargée ou chargeable |
| ECHOS | API d'analyse en écoute ; la connexion à SYNE est un état de lien, pas une condition de `ready` |
| PRISM | Processus vivant et, si son moteur l'expose, scène chargée **[À CONFIRMER]** |
| Gateway *(forme de sortie, non planifiée — hors V0.1)* | Routes chargées et registre accessible |

## 8. Exigences communes

| Exigence | Précision |
| :-- | :-- |
| **Accepter un démarrage explicite** | Le composant démarre sur commande, sans rester en attente d'une interaction. |
| **Rendre son état interrogeable** | Un état de santé, une cause, et une date de dernière observation. |
| **Signaler sa disponibilité** | Une sonde de santé dont le résultat est un booléen et une cause. |
| **Retourner un résultat explicite** | Toute commande porte un résultat, un code et un message. |
| **Ne jamais rester muet** | Une commande sans réponse dans le délai produit un incident côté Launcher. |
| **Rester idempotent** | Répéter une commande d'arrêt ou de suspension ne corrompt pas l'état. |
| **Émettre des événements datés** | Chaque événement porte un horodatage et un type stable. |
| **Rester en lecture seule pour l'observabilité** | La supervision ne modifie jamais l'état du composant. |
| **Supporter l'absence de données** | Un silence est un état valide, pas une panne. |
| **Écrire uniquement dans le dossier fourni** | Un run n'écrit que dans son propre dossier ; aucun fichier partagé entre runs. |
| **Fonctionner sans Launcher** | Le contrat ne crée aucune dépendance. |

## 9. Exigences propres à SYNE

| Exigence | Précision |
| :-- | :-- |
| **Produire les données du run** | Écriture dans le dossier de run fourni, formats à confirmer. |
| **Honorer la seed** | La seed reçue détermine le résultat de façon reproductible. |
| **Respecter la limite de ticks** | Sortie automatique à la fin de la limite. |
| **Déclarer son coût** | Durée de tick, cadence, nombre d'agents, volume d'instantanés. |

## 10. Exigences propres à ECHOS

| Exigence | Précision |
| :-- | :-- |
| **Analyser un run** | `AnalyzeRun(runPath)` produit l'analyse individuelle. |
| **Analyser une expérience** | `AnalyzeExperiment(experimentPath)` produit l'analyse agrégée. |
| **Générer un rapport** | `GenerateReport(experimentPath)` écrit le rapport d'émergence. |
| **Être pilotable sans interface** | Ces opérations doivent être invocables sans passer par l'interface web. |
| **Définir les métriques scientifiques** | Le Launcher ne définit aucune métrique ; il transporte et organise. |
| **Exposer son interface à titre de télémétrie** | L'interface web reste accessible pour l'observation en direct, sans être un prérequis. |

## 11. Exigences propres à PRISM

| Exigence | Précision |
| :-- | :-- |
| **Consommer le flux de simulation** | Sans modifier la simulation. |
| **Déclarer sa cadence de rendu** | Images par seconde et temps par image. |
| **Rester découplé** | Son indisponibilité n'interrompt pas la simulation. |

## 12. Reproductibilité — engagements des composants

1. À seed, configuration, version et plateforme identiques, SYNE produit des
   résultats **identiques**. C'est le niveau *exact*.
2. Entre plateformes ou processeurs différents, l'équivalence est **statistique**,
   sauf preuve du contraire. En .NET, les calculs flottants peuvent différer entre
   plateformes.
3. Aucune source d'aléa implicite — horloge, ordre d'itération, threads — ne doit
   influencer la simulation. **[À auditer.]**
4. L'empreinte de résultat est calculée sur les données exportées, et stable d'un
   run à l'autre dans les conditions du point 1.

## 13. Composants factices pour développer avant les versions réelles

Le Launcher ne peut être développé, testé ni packagé sans savoir comment lancer un
composant, savoir quand il est prêt, l'arrêter, savoir où il écrit et comment il
signale une erreur. Les stubs rendent cette question et permettent de développer
le Launcher avant que SYNE, ECHOS et PRISM n'atteignent leur version cible.

| Stub | Comportement |
| :-- | :-- |
| `Stub.Syne` | Écoute les ports, expose `/health` et `/metrics`, émet des instantanés factices, avance les ticks à cadence réglable, écrit un dossier de run, respecte `--seed` et `--ticks`, quitte avec les codes du §4 |
| `Stub.Echos` | API d'analyse factice, produit un résultat déterministe à partir d'un run, génère un rapport |
| `Stub.Prism` | Processus vivant et client WebSocket factice |

**Pannes injectables** — `--crash-at-tick`, `--freeze-at-tick`, `--slow-factor`,
`--exit-code`, `--refuse-shutdown` — pour tester le crash, le blocage, l'arrêt
forcé, la reprise et les alertes, sans avoir à provoquer une panne réelle.

En mode Développement, le Launcher peut substituer `Stub.Syne` au moteur réel. Voir
la matrice des modes de `COMPONENTS.md`.

## 14. Checklist de conformité par composant

- [ ] `component.json` valide et à jour
- [ ] `--version` et `--info` fonctionnent sans démarrer le service
- [ ] Arguments communs pris en charge
- [ ] Écoute sur `127.0.0.1` par défaut
- [ ] `/health/live`, `/health/ready`, `/health/details`, `/info`, `/metrics` opérationnels
- [ ] Jeton exigé sur les commandes de contrôle
- [ ] `POST /control/shutdown` : arrêt propre, exports vidés
- [ ] Codes de sortie conformes au §4
- [ ] Journaux JSON Lines conformes au schéma de `OBSERVABILITY.md` §7
- [ ] Écriture uniquement dans le dossier fourni
- [ ] Aucun processus orphelin après arrêt brutal du Launcher
- [ ] Fonctionne **sans** Launcher
- [ ] SYNE : mode batch `--seed`, `--ticks`, `--export-dir`, fin automatique
- [ ] SYNE : test de déterminisme passé

## 15. Matrice d'exigences

> Les colonnes **Gateway** et **Launcher** sont tenues à titre de forme de sortie :
> la Gateway n'est **pas planifiée** en V0.1 et n'a pas d'usage tant que LIVEX tient
> sur une machine (`NETWORK.md` §4.3). Les ✓ de sa colonne décrivent ce qu'elle
> devrait satisfaire **si** elle était construite, pas des exigences courantes.
> Le Launcher n'est pas un composant piloté : il est « responsable » des règles
> qu'il applique aux autres.

| Exigence | SYNE | ECHOS | PRISM | Gateway* | Launcher |
| :-- | :--: | :--: | :--: | :--: | :--: |
| Manifeste `component.json` | ✓ | ✓ | ✓ | ✓ | ✓ |
| Démarrage explicite | ✓ | ✓ | ✓ | ✓ | — |
| Arrêt propre | ✓ | ✓ | ✓ | ✓ | — |
| Aucun orphelin | ✓ | ✓ | ✓ | ✓ | responsable |
| `/health/live\|ready\|details` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `/info`, `/metrics` | ✓ | ✓ | ✓ | ✓ | ✓ |
| Jeton sur les commandes | ✓ | ✓ | ✓ | ✓ | — |
| Codes de sortie §4 | ✓ | ✓ | ✓ | ✓ | — |
| Journaux JSON Lines | ✓ | ✓ | ✓ | ✓ | ✓ |
| Écriture dans le dossier fourni | ✓ | ✓ | ✓ | — | ✓ |
| Fonctionne sans Launcher | ✓ | ✓ | ✓ | ✓ | — |

## 16. Références

- Manifeste et états : `COMPONENTS.md`
- Métriques, santé, journaux : `OBSERVABILITY.md`
- Ports, jeton, origines : `NETWORK.md`
- Flux de données et arborescence : `DATA_FLOW.md`
- Frontière calcul et présentation : `adr/ADR-003-analyse-propriete-de-echos.md`

---

## Points restés ouverts dans ce document

- Le mode batch de SYNE (`--seed`, `--ticks`, `--export-dir`) n'est pas confirmé
  comme existant. C'est une porte bloquante pour les campagnes, voir `ROADMAP.md`.
- Le mécanisme d'arrêt propre sous Windows sans console n'est pas validé.
- La capacité de PRISM à exposer des endpoints HTTP n'est pas confirmée. À défaut,
  un fichier de statut et un battement de cœur sur le WebSocket.
- Les formats d'export de SYNE ne sont pas figés.
