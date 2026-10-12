# Launcher IV. Le paquet, le flux de données et le réseau

Une campagne laisse des traces : un paquet, des fichiers, des flux entre composants. Ce chapitre décrit les trois ensemble, parce qu'ils forment une seule question : **qu'est-ce qui prouve qu'une campagne a eu lieu, et dans quelles conditions ?**

**Pourquoi ce chapitre importe pour la question directrice.** La question directrice exige des comportements **reproductibles**. Reproduire ne consiste pas seulement à relancer le moteur avec la même graine : il faut aussi pouvoir prouver, des mois plus tard, quels paramètres ont été employés, quelles versions de composants tournaient, et quels fichiers de résultats en découlent. Le paquet et son flux sont donc la **mémoire longue** du projet : sans eux, chaque observation serait une anecdote.

## 1. Le paquet `.livexp`

Le paquet est une archive ZIP portant l'extension `.livexp`. Le choix du conteneur est un choix d'**implémentation** : le format est la structure logique, et un lecteur doit pouvoir ouvrir un paquet sans dépendre de ZIP.

| Caractéristique | Choix | Raison |
| :-- | :-- | :-- |
| Conteneur | ZIP | lecture partielle sans extraction, ajout possible |
| Variante | ZIP64 nécessaire | une campagne dépasse couramment 4 Gio |
| Compression | `deflate` par défaut, `store` au choix | éviter de recompresser ce qui l'est déjà |
| Chiffrement / signature | aucun en V0.1 | questions distinctes, à trancher |
| Encodage | UTF-8, fins de ligne LF | portabilité |

La structure est stable et lisible :

```text
campagne.livexp
├── manifest.json          identite, etat, schema, inventaire
├── experiment.json        definition : profil, graines, config resolue
├── journal.ndjson         journal d'execution, ajout seul
├── provenance.json        qui, quand, quelles versions, quelle plateforme
├── README.md              note de lecture, generee a la creation
└── runs/
    ├── index.json         index des runs et de leur etat
    └── RUN-0001/
        ├── run.json               graine, versions, derivation, empreinte
        ├── config.resolved.json   configuration effective complete
        ├── data/                  artefacts de donnees du run
        ├── logs/                  journaux correles au run
        └── integrity.json         tailles et empreintes SHA-256
```

Le fichier `config.resolved.json` mérite une attention particulière : il porte la configuration **effective**, c'est-à-dire celle qui a réellement été appliquée après fusion des valeurs par défaut, du fichier et des options de ligne de commande. Une campagne relue plus tard ne doit pas avoir à deviner ce qu'était un paramètre : la réponse est dans le paquet.

## 2. Le manifeste, ou l'état déclaré

Le manifeste est la seule entrée qui atteste de l'état du paquet. Il est **écrit en dernier** : son absence à jour signifie que le paquet est *récupérable*, non qu'il est valide. Cette convention est un choix de robustesse : un paquet partiellement écrit ne peut pas se faire passer pour complet.

Le manifeste porte l'identité (identifiant trié par date, nature du paquet), l'état, les dates de création et de scellement, le générateur, la campagne, les compteurs de runs (total, terminés, échoués), les **versions des composants engagés**, et une empreinte d'intégrité.

| État | Signification |
| :-- | :-- |
| `live` | campagne en cours, écriture attendue |
| `sealed` | campagne terminée, paquet immuable |
| `recoverable` | écriture interrompue, reprise possible, contenu incomplet |

Le scellement est le geste le plus important du cycle de vie. Tant que le paquet est vivant, il est modifiable : un run peut être repris, le manifeste réécrit. Une fois scellé, il est **immuable**. C'est ce qui distingue un résultat d'un travail en cours, et c'est ce qui rend une campagne citable.

## 3. Le flux de données : un seul écrivain

L'arborescence est **l'unique** hiérarchie de référence. Deux principes la gouvernent.

D'abord, la définition d'une campagne est autosuffisante. Le fichier de campagne décrit le profil d'exécution, la configuration, le nombre de runs, l'horizon en ticks, la stratégie de dérivation des graines et la politique en cas d'échec. Le fichier d'un run rappelle sa graine, les versions concernées, sa dérivation et son empreinte, afin qu'un run isolé reste reproductible sans la campagne entière.

Ensuite, **un seul écrivain par fichier**. Le Launcher orchestre, mais il ne produit aucun résultat : il demande, attend, collecte et archive. L'analyse appartient à l'observatoire. Cette répartition est la traduction, au niveau des fichiers, de la décision d'architecture déjà rencontrée : celui qui calcule n'affiche pas, et celui qui orchestre ne calcule pas.

**Les graines sont dérivées, jamais tirées.** Trois stratégies existent : une dérivation arithmétique à partir d'une graine de base, une liste explicite pour rejouer un jeu connu, et une dérivation par hachage pour disperser largement. Une campagne sans stratégie déclarée est refusée à la création. C'est une règle sévère, et elle a une raison simple : une graine tirée au hasard à l'exécution rendrait la campagne irreproductible, donc inutile comme preuve.

## 4. Le réseau : contrôle, données, restitution

Le réseau du projet distingue deux **plans**, et cette distinction est ce qui empêche le Launcher de devenir un proxy de données.

| Canal | Direction | Contenu | Volume | Passe par le Launcher |
| :-- | :-- | :-- | :-- | :-- |
| 1, contrôle | Launcher et composants | démarrage, arrêt, suspension, santé, information, métriques, registre | faible, sur commande | **oui**, c'est son rôle |
| 2, données | moteur et observatoire, moteur et rendu | monde initialisé, instantanés par tick, événements typés | **très élevé, continu** | **non, jamais** |
| 3, restitution | observatoire vers Launcher | analyse de run, analyse de campagne, rapport | moyen, en fin de run | **oui**, en bordure, pour archivage |

Il y a donc **trois canaux, pas deux**. Les confondre conduit à faire passer des millions de messages par un composant dont le rôle est d'orchestrer, un goulot d'étranglement volontaire, et un point de panne unique placé sur le chemin critique de la science.

**La topologie réalisée en V0.1 est unique** : une machine, une session interactive, aucun routage. Le Launcher démarre, arrête et sonde des instances du moteur ; les flux de données vont directement du moteur vers l'observatoire. Deux propriétés doivent être lisibles dans le schéma de cette topologie : il peut y avoir **plusieurs instances** du moteur pour un seul Launcher, et **le Launcher n'est pas sur le chemin des flux de données**. Chaque instance possède son propre bloc d'adresses interne, ce qui rend chaque flux attribuable à une paire concrète. C'est ce service que rend la segmentation d'adresses : savoir de quel monde vient quel flux, sans ambiguïté.

Les formes de sortie plus ambitieuses (agent de nœud, passerelle) sont documentées mais **non planifiées** : le livre les mentionne comme direction, pas comme état.

## 5. Ce que ce chapitre apporte à la question directrice

Ce chapitre a décrit la mémoire longue du projet. Le paquet conserve la définition d'une campagne, la configuration effectivement appliquée, les versions engagées et les empreintes d'intégrité ; le manifeste déclare l'état et rend le scellement explicite ; le flux de données sépare ce qui doit être archivé de ce qui doit circuler ; le réseau garantit que l'orchestration ne s'interpose pas entre le monde et son observateur.

C'est ce dispositif qui permet de dire d'un résultat, non pas seulement « cela a produit une structure intéressante », mais « cela l'a produite avec cette graine, cette version, cette configuration, et le paquet est scellé ». La reproductibilité cesse d'être une intention : elle devient un objet.

**Ce que ce chapitre ne permet pas de conclure.**

- Le format `.livexp` est spécifié ; l'ouverture d'un paquet par un outil tiers n'est pas garantie par un test d'interopérabilité.
- Le scellement protège l'immuabilité dans le flux prévu ; il n'est ni signé ni chiffré, donc il n'établit pas l'**authenticité** d'un paquet.
- Le scellement manuel introduit un geste humain : un paquet non scellé peut rester indéfiniment dans un état intermédiaire.
- Les canaux de contrôle et de restitution dépendent d'un contrat de santé par composant, dont le cas du rendu reste à confirmer.
- Les topologies multi-machines, l'agent de nœud et la passerelle sont des directions documentées, **non implémentées** : le livre ne les présente pas comme disponibles.
