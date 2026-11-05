# Annexe C. Architecture détaillée

Cette annexe rassemble des détails d'architecture trop fins pour le corps du livre. Les sections C.1 à C.4 et leurs valeurs du profil SYNE `prism` décrivent la photographie V1 du 12 octobre 2026. La section C.5 distingue l'architecture PRISM V2 connue au 4 novembre. Le profil SYNE `prism` ne constitue pas la carte de PRISM V2.

## C.1 Sous-systèmes du moteur SYNE

Le moteur est organisé en sept sous-systèmes, chacun avec un rôle unique. Aucun ne prend de décision collective.

| Sous-système | Rôle |
| :-- | :-- |
| **World** | Espace 2D (dimensions configurables), ressources, obstacles |
| **Agents** | Cycle de vie des entités (naissance, vie, dissolution) |
| **Cognition** | BDI : boucle cognitive de chaque entité |
| **Actions** | Répartition des actions possibles (déclaratives) |
| **Interaction** | Interactions inter-entités (social, combat, commerce) |
| **Spatial** | Grille spatiale uniforme (perception et communication quasi linéaires) |
| **Nav** | Cheminement 2D autour des obstacles |

Une couche applicative indépendante porte la configuration (chargement JSON, validation, flags de ligne de commande), la persistance, l'observabilité et le serveur de contrôle. Elle entoure les sous-systèmes sans décider à leur place.

## C.2 Ordonnancement, niveaux de détail et fréquences

Le moteur exécute les sous-systèmes à des fréquences différentes, selon leur coût et leur besoin, réparties en quatre classes : élevée (mouvement, collisions, contraintes immédiates), moyenne (besoins, perception locale, environnement), adaptative (délibération, planification) et faible (analyse, agrégations, statistiques).

Le **niveau de détail décisionnel** espace la délibération des entités lointaines sans la supprimer : fréquence de décision `1 / 2^LOD`, avec la zone 0 (jusqu'à 100 unités) délibérant à chaque tick, la zone 1 (100 à 200) tous les deux ticks et la zone 2 (200 à 400) tous les quatre ticks.

Le budget de référence, pour mille entités et un tick de cent millisecondes, se répartit ainsi : perception 20 ms, mémoire et croyances 15 ms, besoins et objectifs 10 ms, utilité 20 ms, communication 15 ms, actions et mouvement 15 ms, événements 5 ms. Ces chiffres sont des repères d'architecture, non des garanties figées.

## C.3 Contrat de composant

Un composant livrable expose un manifeste `component.json` qui décrit son comportement et ses contraintes.

| Champ | Rôle |
| :-- | :-- |
| `schema` | version du schéma de manifeste |
| `id` | identifiant stable |
| `name` | nom lisible du composant |
| `type` | nature du composant (simulation, analyse, émulation) |
| `version` | version livrée |
| `protocolVersion` | version du protocole d'intégration |
| `runtime` | dépendance d'exécution (par exemple `dotnet`) |
| `executable` | binaire par système d'exploitation (`linux`, `windows`) |
| `workingDirectory` | répertoire de travail du processus |
| `endpoints` | routes et pipes exposés (`transport`, `port`, `enabled`, `launchArgument`) |
| `capabilities` | capacités déclarées (`headless`, `control`, `observability`, `batch`) |
| `health` | sonde de santé (`probe`, `path`, `intervalMs`) |
| `timeouts` | délais de démarrage et d'arrêt (`startupMs`, `shutdownMs`) |

Le Launcher détecte les composants par ce manifeste ; l'Installateur les dépose sans connaître leur logique interne.

## C.4 Configuration du monde de rendu

| Paramètre | Valeur |
| :-- | :-- |
| Taille du monde | 2 240 sur 2 240 unités |
| Taille de cellule | 32 unités (70 sur 70 cellules) |
| Cadence cible | 6 ticks par seconde |
| Secondes simulées par tick | 5 |
| Mètres par unité | 1,0 |
| Réserves (nourriture, eau) | 20 000, régénération 20 et 10 par tick |
| Communication | relais désactivé, coûts énergétiques nuls, un envoi et une réception par tick |
| Coût de déplacement | 0,03 d'énergie par tick de déplacement |
| Repos | 1,5 d'énergie et 2 de fatigue par tick |

Les valeurs sont celles des profils de configuration versionnés (`reference.json` et `prism.json`). Le monde de référence (500 sur 500 unités, cellule de 10) est celui des campagnes calibrées ; le monde du profil PRISM (2 240 sur 2 240, cellule de 32) est celui de la représentation.

> Ces dimensions sont celles du monde historique SYNE configuré par le profil
> `prism`. Le monde PRISM V2 est généré séparément, à partir de son pack, sur
> une grille de colonnes 2,5D.

## C.5 PRISM V2 au 4 novembre 2026

PRISM V2 est une solution .NET composée de `Prism.Contracts`, `Prism.Core`,
`Prism.Content`, `Prism.Host`, `Prism.Bots` et `Prism.Viewer`. Le host porte le
monde physique, les commandes, le tick et la persistance. La géométrie actuelle
est une grille bornée de colonnes 2,5D ; le viewer est un client facultatif.

Le contrat `1.0.0` relie les commandes SYNE aux observations et événements
PRISM. Au 4 novembre, l'adaptateur SYNE et la boucle couplée avaient été
exercés sur 1 000 ticks. Restent le run R2 de 5 000 ticks, les répétitions
bit-à-bit, la reprise commune, l'ingestion de production ECHOS, le viewer live
et les validations de plateforme.

---

Cette annexe est le point d'entrée vers les fiches techniques de chaque composant : `docs/syne/`, `docs/echos/`, `docs/prism/`, `docs/launcher/` et `docs/installer/`.
