# Contrat d’intégration LIVEX — version 1

Ce document fixe le contrat versionné que le Launcher accepte pour un service
de composant. Le schéma machine-readable du manifeste est
[`component-manifest-v1.schema.json`](component-manifest-v1.schema.json).
Des exemples partageables sont fournis dans [`examples/`](examples/), et
[`validate_manifests.py`](validate_manifests.py) valide le schéma, ces exemples
et tous les manifestes `component.json` suivis ou non suivis (non ignorés) du
workspace. La CI exécute ce contrôle lorsqu'un composant ou le Launcher change,
avec la dépendance épinglée dans
[`requirements-validation.txt`](requirements-validation.txt).
La spécification cible plus détaillée reste dans
[`../../docs/docs-launcher/INTEGRATION_CONTRACT.md`](../../docs/docs-launcher/INTEGRATION_CONTRACT.md).

> **État au 3 octobre 2026 :** la version de schéma 1 est implémentée par le
> Launcher. SYNE réel, ECHOS headless et `syne-mock` ont maintenant chacun un
> manifeste Linux. Le batch `reference` de SYNE et les routes d'analyse ECHOS
> existent. Le Launcher prépare l'agrégat ECHOS en produisant un
> `experiment.json`, mais l'ingestion SYNE→ECHOS et l'acceptation réelle des
> parcours complets ne sont pas terminées. Ce document formalise le contrat attendu ;
> il ne déclare pas ces intégrations bout en bout conformes. Les écarts sont
> suivis dans
> [`../V1-CAPABILITY-MATRIX.md`](../V1-CAPABILITY-MATRIX.md).

## 1. Versionnement

- `schema` est la version du document `component.json`. La version 1 est
  décrite par le JSON Schema lié ci-dessus.
- Un lecteur v1 accepte les champs ajoutés facultativement seulement si son
  parseur les tolère ; il refuse `schema` supérieur à 1 et tout champ v1
  obligatoire absent/invalide.
- Un changement incompatible de structure ou de sens d’un champ nécessite une
  nouvelle valeur entière de `schema` et une nouvelle version de schéma.
- `protocolVersion` est le numéro entier du protocole annoncé par
  l’implémentation dans `/info`. Il ne remplace ni `schema`, ni la version du
  flux scientifique (actuellement versionnée séparément dans les contrats SYNE).
- Les capacités sont des identifiants stables, sensibles à la casse. Une
  capacité ne doit être déclarée que si l’installation la fournit réellement.
- `headless` déclare la possibilité d'exécuter l'installation sans interface ;
  `ui` déclare une interface graphique lançable. Si les deux sont déclarées
  sur la même installation, l'absence de `--headless` demande le mode UI et sa
  présence demande le mode headless. Plusieurs exécutables pour ces variantes
  ne sont pas décrits par le schéma v1 : ils doivent être distribués comme des
  installations distinctes jusqu'à une évolution versionnée du manifeste.

## 2. Résolution et lancement

1. Le Launcher lit le manifeste sans exécuter le composant, vérifie `schema`,
   les champs requis et l’exécutable de la plateforme courante.
2. Les chemins `executable` et `workingDirectory` sont relatifs à la racine
   d’installation. Ils ne peuvent pas remonter au-dessus de cette racine.
3. Le Launcher alloue les ports requis avant le démarrage. Un port occupé est
   une erreur explicite ; il n’est ni réattribué silencieusement ni partagé.
4. Le processus est démarré dans son répertoire de travail assigné. Les
   arguments/variables sensibles ne doivent pas contenir le jeton de session.
5. Le Launcher attend la sonde déclarée dans `health.path`. Un service est
   prêt seulement après une réponse HTTP réussie à cette sonde ; un processus
   vivant sans readiness n’est pas utilisable.
6. Le Launcher transmet le port de contrôle sous `--control-port`. Pour tout
   autre endpoint qui déclare `launchArgument`, il transmet le port résolu à
   cet argument. `launchArgument` est un seul token CLI, pas un fragment de
   commande.
7. `--headless` ne s’applique que si le lancement demandé est headless et que
   cette capacité est déclarée. Le Launcher ne doit pas l’ajouter
   inconditionnellement à un composant qui ne prend pas en charge ce mode.

## 3. Arguments communs v1

Les options marquées « service supervisé » sont celles qu’un processus de
service lancé par le Launcher doit accepter. Le support des variantes UI
distinctes est décrit par les capacités du manifeste ; la sélection de
variantes reste à compléter dans l’interface.

| Argument | Sens v1 | Règle |
| :-- | :-- | :-- |
| `--instance-id <id>` | Identifie cette instance pendant sa durée de vie | Obligatoire au lancement supervisé |
| `--control-port <port>` | Port HTTP de contrôle/santé | Obligatoire si le manifeste déclare le contrôle HTTP |
| `--work-dir <path>` | Espace de travail de cette instance | Obligatoire ; le composant n’écrit pas en dehors |
| `--log-dir <path>` | Destination des journaux du composant | Obligatoire ; stdout/stderr du processus sont également capturés par le Launcher |
| `--correlation-id <id>` | Relie les opérations, traces et journaux | Obligatoire ; aussi transmis par `LIVEX_CORRELATION_ID` |
| `--headless` | Demande un démarrage sans interface utilisateur | Présent seulement pour une variante headless déclarée |
| `--version` / `--info` | Affiche l’identité/version puis quitte | Mode diagnostic, ne démarre pas le service |

Un argument inconnu est refusé avec un diagnostic et un code non nul ; il ne
doit pas être ignoré. Les options suivantes ne sont pas des options communes à
tous les services :

| Argument | Propriétaire |
| :-- | :-- |
| `--simulation <id>` | SYNE batch |
| `--seed <n>` | SYNE batch |
| `--ticks <n>` | SYNE batch ; horizon positif et fin automatique |
| `--export-dir <path>` | SYNE batch ; sous-répertoire d’artefacts du run |
| `--autostart` | SYNE batch ; commence le run après préparation |
| `--config <path>` | Composant qui déclare et documente l’option |

Ces arguments batch sont la cible de l’acceptation SYNE, pas une déclaration de
compatibilité de SYNE à la date de référence. En particulier,
`ProcessRunExecutor` doit les construire depuis le contrat validé et ne pas
traiter `--max-ticks` comme un alias implicitement garanti.

## 4. Variables d’environnement et sécurité

| Variable | Règle |
| :-- | :-- |
| `LIVEX_SESSION_TOKEN` | Jeton secret pour les commandes de contrôle ; variable d’environnement uniquement, jamais ligne de commande ni journal |
| `LIVEX_INSTALL_ROOT` | Racine de l’installation détectée |
| `LIVEX_CORRELATION_ID` | Identifiant de corrélation, identique à `--correlation-id` |

Le contrôle HTTP est lié à `127.0.0.1` par défaut. Les requêtes mutantes
(`/control/*`, dont shutdown) exigent le jeton de session ; les sondes de santé
et les lectures d’observabilité sont en lecture seule. Une requête invalide,
non autorisée ou non supportée retourne un statut HTTP d’erreur, ne modifie pas
l’état et n’est pas transformée en succès.

## 5. Cycle de vie et endpoints

| Endpoint | Méthode | Authentification | Contrat |
| :-- | :-- | :-- | :-- |
| Sonde déclarée par `health.path` | GET | Aucune, boucle locale | 2xx signifie « prêt » ; autre réponse signifie « pas prêt » |
| `/health/live` | GET | Aucune, boucle locale | Optionnel en v1 ; vivacité seulement, ne remplace pas readiness |
| `/health/details` | GET | Aucune, boucle locale | Optionnel ; détail lisible des checks |
| `/info` | GET | Aucune, boucle locale | JSON avec `id`, `version` et `protocolVersion` |
| `/metrics` | GET | Aucune, boucle locale | Optionnel ; métriques du composant avec unités documentées |
| `/control/shutdown` | POST | `LIVEX_SESSION_TOKEN` | Arrêt propre, fermeture des sockets et flush des sorties avant terminaison |

Séquence côté Launcher :

```text
détecté → processus démarré → readiness réussie → en service
   → arrêt authentifié → sorties flushées → processus terminé
```

Le Launcher applique un délai de démarrage et un délai de grâce déclarés dans
`timeouts`. Si l’arrêt propre dépasse le délai, il termine l’arbre de processus,
consigne la cause et marque l’opération comme forcée/échouée selon son contexte.
Il ne redémarre pas automatiquement le composant.

La readiness n’a pas de réponse JSON commune obligatoire en version 1 : le
statut HTTP est l’autorité. Le composant doit conserver dans ses journaux ou
dans l’endpoint de détail une cause utile à l’opérateur.

## 6. Sorties et codes de terminaison

- Une exécution batch SYNE réussie termine avec le code 0 après écriture et
  fermeture de tous les artefacts déclarés.
- Une annulation demandée est distinguable d’une erreur ; une erreur de
  configuration, de port, de ressource, de simulation ou d’écriture doit
  produire un code non nul et une cause conservée dans les journaux.
- Une campagne n’est pas scellée tant que tous les producteurs de fichiers ne
  sont pas terminés.
- Un run batch SYNE exécuté en mode supervisé déclare son identité et son flux
  d’observabilité (`--run-id`, `--export-stream`) : ces artefacts sont la matière
  première de l’analyse, et le paquet archivé doit suffire à la rejouer.
- ECHOS renvoie des références/bytes d’artefacts et un rapport Markdown selon
  l’opération d’analyse versionnée ; le Launcher archive les contenus sans en
  recalculer ni en reformuler les résultats.

La correspondance complète des erreurs et des codes est maintenue dans
[`../../docs/docs-launcher/INTEGRATION_CONTRACT.md`](../../docs/docs-launcher/INTEGRATION_CONTRACT.md).
Les opérations d'analyse ECHOS attendues, leur corps et leurs réponses sont
décrits au §10.1 et §10.2 de ce document. L'API ECHOS réelle expose les quatre
chemins, l'ingestion d'un run archivé étant **préalable** à son analyse.

## 7. Limites de la version 1

- Le schéma v1 ne déclare pas de capacité de campagne implicite.
- Les plateformes non indiquées dans `executable` ne sont pas supportées.
- `headless` et `ui` ne sont pas équivalents : l’absence de `headless` ne prouve
  pas l’existence d’une interface graphique lançable par le Launcher ; il faut
  aussi déclarer `ui`.
- Un composant qui n’a pas de manifeste valide n’est pas lançable par
  l’orchestration générique.
- Le protocole de flux `snapshot`/`event` a sa version séparée et doit être
  accepté indépendamment du protocole de contrôle.
- Le mock SYNE valide l’intégration de transport et ne valide ni le résultat
  scientifique, ni la reproductibilité du moteur réel.

Les manifestes concrets v1 présents dans le dépôt sont
[`syne/component.json`](../../syne/component.json),
[`echos/component.json`](../../echos/component.json) et
[`syne-mock/component.json`](../../syne-mock/component.json). Le manifeste SYNE
ne déclare que Linux et un batch `reference`; le test d’acceptation Launcher
valide une campagne depuis une copie isolée du publish Linux et son archivage
dans `.livexp`, sans prouver la compatibilité d’autres scénarios ni OS.
Les manifestes invalides sont construits par
les tests
[`ManifestContractTests`](../Launcher.Tests.EndToEnd/ManifestContractTests.cs).
