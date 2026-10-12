# Chapitre 10. Intégration système

Les composants ont été décrits séparément. Ce chapitre les relie. Il raconte comment la pile fonctionne de bout en bout, quels contrats la tiennent, et comment un parcours complet a été vérifié contre les composants réels. C'est ici que la séparation des responsabilités cesse d'être un principe d'architecture pour devenir une pratique observée.

Il faut comprendre l'enjeu de ce chapitre. Décrire des composants isolés est relativement facile : chacun a ses contrats, ses tests, son rôle. La difficulté commence lorsqu'il faut les faire travailler ensemble. Deux composants qui fonctionnent parfaitement séparément peuvent échouer ensemble, parce qu'ils ne s'entendent pas sur un format, un ordre, une sémantique. L'intégration est donc le moment de vérité d'une architecture.

**Pourquoi ce chapitre importe pour la question directrice.** Quatre instruments justes pris séparément peuvent former un ensemble faux : c'est le moment où les pièces doivent s'emboîter qui révèle si l'architecture tient. Pour la question directrice, l'intégration est l'épreuve de vérité : elle vérifie que le moteur décide, que l'observatoire mesure sans toucher, que la scène montre sans décider et que l'orchestrateur archive sans relayer, **ensemble**, sur des composants réels. Si un seul interdit cédait, la réponse à la question serait entachée.

## 1. La pile en un regard

Quatre responsabilités, deux outils, un monde.

```mermaid
flowchart TB
    subgraph SYNE["SYNE : decider"]
        SRV["Flux 5180 et controle 5181"]
    end
    subgraph ECHOS["ECHOS : mesurer sans toucher"]
        API["API lecture seule 5000"]
    end
    subgraph PRISM["PRISM : montrer sans decider"]
        PLG["Plugin PRISM-LDK et scene Unreal"]
    end
    subgraph LAU["Launcher : archiver sans relayer"]
        ORCH["Orchestration, campagnes, paquet"]
    end
    SYNE -->|"snapshots, evenements"| ECHOS
    SYNE -->|"snapshots, evenements"| PRISM
    ECHOS -->|"controle"| SYNE
    PRISM -->|"controle"| SYNE
    ECHOS -->|"metriques, rapports"| LAU
    LAU -->|"controle et archivage"| SYNE
    LAU -->|"controle"| ECHOS
```

Le moteur produit la vérité du monde et la diffuse. L'observatoire la lit et la mesure. La scène la reflète. L'orchestrateur pilote les cycles de vie, laisse les données circuler directement et archive les résultats. Chaque composant protège les autres de leurs tentations.

## 2. Les contrats qui tiennent la pile

L'intégration repose sur trois contrats.

| Contrat | Transport | Port | Contenu |
| :-- | :-- | --: | :-- |
| Temps réel | WebSocket | 5180 | initialisation, instantané par tick, événements |
| Contrôle | HTTP | 5181 | préparer, acquitter, démarrer, pause, reprise, arrêt, réinitialisation, état |
| Lecture | REST | 5000 | runs, métriques, exports, comparaisons, inspections |

- Le **contrat temps réel** (WebSocket, port 5180 par défaut) : un message d'initialisation, puis un instantané global par tick et des événements typés. JSON textuel, camelCase.
- Le **contrat de contrôle** (HTTP, port 5181 par défaut) : préparer, accuser réception, démarrer, mettre en pause, reprendre, arrêter, réinitialiser, lire l'état.
- Le **contrat de lecture** (API REST d'ECHOS, port 5000 par défaut) : runs, métriques, exports, comparaisons, inspections, en lecture seule.

La règle de compatibilité est unique : un ajout est compatible, une suppression ou un changement de sens est incompatible. C'est cette règle qui a permis d'ajouter des fonctionnalités (saisons, territoires, livres, inventaire, engagements, temps simulé en secondes) sans rompre les consommateurs.

## 3. Le chemin de bout en bout

Un parcours complet relie cinq moments.

1. **Préparer.** L'orchestrateur demande au moteur de préparer un monde, avec une graine et une cadence. Le moteur construit ce monde et en diffuse la description.
2. **Acquitter.** La scène génère sa représentation et accuse réception. Après une préparation explicite, le démarrage exige cet accusé.
3. **Démarrer.** Le moteur lance la simulation. Les instantanés partent vers l'observatoire et la scène, directement, sans passer par l'orchestrateur.
4. **Mesurer et montrer.** L'observatoire agrège et expose ses métriques en lecture seule ; la scène met à jour sa représentation sans jamais décider.
5. **Archiver.** À la fin de chaque run, l'orchestrateur collecte les restitutions et scelle la campagne dans un paquet.

```mermaid
sequenceDiagram
    participant L as Launcher
    participant S as SYNE
    participant E as ECHOS
    participant P as PRISM
    L->>S: preparer (graine, cadence)
    S-->>P: description du monde
    P->>S: acquittement
    L->>S: demarrer
    S-->>E: instantanes (direct)
    S-->>P: instantanes (direct)
    E-->>L: metriques et rapports
    L->>S: arreter
```

Le point essentiel est que les données de télémétrie survivent à un arrêt de l'orchestrateur : elles vont directement du moteur aux consommateurs. L'orchestrateur est sur le chemin du contrôle et de l'archivage, jamais sur celui des données temps réel.

## 4. La preuve d'extrémité réelle

Le projet ne se contente pas d'un parcours théorique. Un parcours automatisé de bout en bout a été exécuté contre les composants réels (un moteur publié et l'analyse réelle). Il couvre : une campagne, une interruption, une reprise sans rejouer, un paquet récupérable, des analyses individuelles par l'API, un rapport archivé identique à sa régénération, et les frontières entre composants. Ce parcours a réussi en totalité (33 tests sur 33) le 12 octobre 2026 sur une machine Windows, et il est également déclaré et vert en intégration continue.

Ce type de preuve est important parce qu'il dépasse le test unitaire. Un test unitaire valide un comportement isolé ; un parcours de bout en bout valide que les contrats s'emboîtent réellement : que l'observatoire ingère ce que le moteur émet, que l'orchestrateur reprend là où il s'est arrêté, que le paquet scellé se rouvre et que le rapport régénéré est identique.

Il faut mesurer la force et la faiblesse de cette preuve. Sa force : elle porte sur les composants réels, enchaînés, sur une plateforme réelle, et couvre un scénario complet. Sa faiblesse : elle porte sur une seule plateforme (Windows), à une date donnée, et ne prouve pas que d'autres combinaisons fonctionneraient. Le projet le dit : « prouvé sur une machine Windows au 12 octobre 2026 » ne signifie pas « prouvé partout et toujours ».

## 5. Les frontières vérifiées

Chaque interdit de l'architecture est vérifié.

- **Le piloté égale l'interrompu.** Un run piloté par le contrôle produit exactement le même état qu'un run laissé libre : l'ordonnancement du contrôle ne consomme aucun tirage.
- **Le rapport égale sa régénération.** Un rapport archivé est identique au rapport régénéré à partir des mêmes données : la production est déterministe.
- **L'entrée n'est jamais rejouée.** Une reprise reprend après le dernier run terminé, sans le rejouer.
- **Le zéro lu est distingué du zéro aveugle.** Un chiffre non mesuré n'est jamais affiché comme un zéro observé.

| Interdit | Preuve exécutable |
| :-- | :-- |
| SYNE ne doit pas être mis en scène | piloté égal à l'interrompu |
| ECHOS ne doit pas altérer le monde | lecture seule, provenance des mesures |
| PRISM ne doit pas décider | la scène suit l'instantané |
| Le Launcher ne doit pas relayer | données directes moteur vers consommateurs |
| La reprise ne doit pas rejouer | entrée prise après le dernier run terminé |
| Un zéro ne doit pas mentir | provenance publiée point par point |

Ces quatre propriétés forment la version exécutable des quatre interdits : SYNE décide, ECHOS mesure sans toucher, PRISM montre sans décider, le Launcher archive sans relayer.

## 6. Les divergences assumées

L'intégration ne cache pas ses écarts. Plusieurs divergences entre la conception initiale et la réalisation sont consignées. L'observatoire n'utilise pas le cadre applicatif initialement prévu, mais un cadre plus léger. L'observatoire n'a plus d'interface propre : la présentation est portée par l'orchestrateur. L'échelle et la cadence de la représentation ont été formalisées par une décision dédiée. La communauté de détection utilise une méthode déterministe plutôt que l'algorithme initialement envisagé, incompatible avec la contrainte de déterminisme.

Une divergence assumée n'est pas un défaut dissimulé : c'est une décision documentée, avec sa justification.

La dernière divergence est particulièrement instructive. Un algorithme de détection de communautés avait été envisagé pour l'analyse ; il a été écarté au profit d'une méthode déterministe, parce qu'il introduisait de l'aléa et donc de la non-reproductibilité. C'est un exemple de contrainte forte (le déterminisme) qui l'emporte sur une préférence technique. Le projet choisit la reproductibilité, même au prix d'un algorithme moins raffiné.

## 7. Ce que ce chapitre apporte à la question directrice

Ce chapitre a montré que la pile fonctionne de bout en bout : préparation, acquittement, démarrage, mesure, archivage, reprise, scellement. Il a montré que les frontières entre composants sont vérifiées, et que les écarts de conception sont assumés.

Ce que ce chapitre ne permet pas encore de conclure : que la pile soit fiable dans le temps. Pour cela, il faut examiner un contrat encore plus exigeant que les autres, celui qui commande tout le reste : le déterminisme. C'est le sujet du chapitre suivant.
