# Annexe D. Schémas d'architecture

Cette annexe rassemble les schémas structurants du projet sous forme de diagrammes Mermaid, reproductibles dans le dépôt. Chaque schéma porte une légende qui indique ce qu'il montre et, surtout, ce qu'il ne montre pas.

## D.1 Vue d'ensemble de la pile

```mermaid
flowchart TB
    UR[Utilisateur] --> LN[Launcher]
    LN --> SY[SYNE moteur]
    LN --> EC[ECHOS analyse]
    LN --> PR[PRISM Unreal]
    SY -->|instantanés, événements| EC
    SY -->|transport| PR
    PR -->|contrôle| SY
    LN -->|paquet .livexp| ST[Workspace]
    EC --> ST
```

Lecture : le Launcher orchestre, le moteur décide, ECHOS mesure, PRISM montre. Aucun composant ne contourne le Launcher pour écrire dans le workspace. Le schéma ne montre pas les composants internes, décrits dans les sections suivantes.

## D.2 Architecture en couches du moteur

```mermaid
flowchart TB
    subgraph C0[C0 Perspectives]
        P0[Question de recherche]
    end
    subgraph C2[C2 Architecturale]
        L1[Système]
        L2[Persistance]
        L3[Communication]
        L4[Collectifs]
        L5[Télémétrie]
    end
    subgraph C1[C1 Système]
        S1[Noyau de simulation]
        S2[Modèle d'agent]
        S3[Système cognitif]
    end
    P0 --> S1
    S1 --> S2
    S2 --> S3
    S3 --> L3
    S3 --> L4
    S1 --> L1
    S1 --> L2
    S1 --> L5
```

Lecture : les couches techniques reposent sur des responsabilités séparées. Aucune couche n'a de vue du collectif. Le schéma n'indique pas les flux de données précis, décrits en D.3.

## D.3 Boucle de simulation

```mermaid
flowchart LR
    A[Horloge logique] --> B[Perception]
    B --> C[Croyances]
    C --> D[Mémoire]
    D --> E[Objectifs]
    E --> F[Délibération]
    F --> G[Action]
    G --> H[Mise à jour du monde]
    H --> A
```

Lecture : la boucle est locale à chaque entité, à chaque tick. La perception est partielle et faillible. Le schéma ne montre pas les systèmes collectifs, qui se forment par agrégation de ces boucles.

## D.4 Boucle d'une entité

```mermaid
flowchart TB
    BES[Besoins] --> URG[Urgence]
    CRO[Croyances] --> EV[Évaluation]
    OBJ[Objectifs] --> DEL[Délibération par utilité]
    PER[Personnalité] --> DEL
    URG --> DEL
    EV --> DEL
    DEL --> ACT[Action]
    ACT --> MDP[Mise à jour du monde]
    MDP --> BES
    MDP --> CRO
```

Lecture : c'est le cycle de l'agent, de la tension interne à l'action. Une seule action est exécutée par tick. Le schéma n'inclut pas la décroissance de mémoire, qui agit sur les croyances et les souvenirs.

## D.5 Publication par PRISM

```mermaid
sequenceDiagram
    participant M as SYNE
    participant P as PRISM-LDK
    participant U as Vue Unreal
    M->>P: complet à la connexion et toutes les 60 s
    loop toutes les 100 ms
        M->>P: delta depuis le dernier complet
    end
    loop chaque tick
        M->>P: liste d'événements
    end
    loop toutes les 3 s
        M->>P: santé
    end
    P->>M: contrôle, pause, reprise, avance d'un tick
    P->>U: mise à jour du monde
```

Lecture : l'instantané est la source de vérité, l'événement un signal complémentaire. Le contrôle est le seul flux montant. Le schéma ne montre pas la compression, interdites de détailler hors annexe technique.

## D.6 Cycle de vie d'un paquet `.livexp`

```mermaid
stateDiagram-v2
    [*] --> Vivant: création de la campagne
    Vivant --> Vivant: runs et mise à jour
    Vivant --> Scellé: scellement déterministe
    Scellé --> [*]: archivage
```

Lecture : un paquet est vivant pendant la campagne, scellé après. Le scellement recalcule un hachage et ne doit plus être modifié. Le schéma ne montre pas la structure interne, décrite dans la documentation du Launcher.

## D.7 Dérivation des graines

```mermaid
flowchart LR
    BS[Graine de base 1000] --> D1[Graine 1 = 1000]
    BS --> D2[Graine 2 = 1001]
    BS --> D3[Graine 3 = 1002]
    BS --> DN[Graine n = 1000 plus n moins 1]
```

Lecture : les graines sont prévisibles et dérivables, jamais tirées à l'exécution. Le schéma ne montre pas les stratégies explicite et hachée, décrites dans la documentation du Launcher.

## D.8 Contexte du système

```mermaid
flowchart TB
    subgraph P["Portée du système"]
        L[Loi locale et aveugle]
        F[Conditions de flux]
        B[Contraintes de besoins]
    end
    subgraph E["Contexte du monde"]
        R[Environnement]
        N[Réseau social]
        T[Temps]
    end
    E --> S[Système multi-agents]
    S --> O["Observables (trajectoires et métriques)"]
```

Lecture : le système transforme des conditions contextuelles en observables, sous des lois locales. Le schéma ne montre pas la frontière interprétative, qui relève de l'analyse causale.

## D.9 Frontière causale

```mermaid
flowchart LR
    C[Conditions internes] --> M[Mécanisme local]
    M --> O[Observables mesurés]
    F[Conditions de flux] --> M
    O -.->|borne haute| EX[Interprétation externe]
    EX -.->|borne haute| I[Interprétation normative]
```

Lecture : le livre sépare franchement le mécanisme mesuré (traits pleins) de l'interprétation (pointillés). La borne haute est une limite explicite, pas une conclusion du moteur. Le schéma ne fournit aucune preuve des interprétations externes.

## D.10 Reproductibilité

```mermaid
flowchart TB
    G[Graine] --> N[nombres pseudo-aléatoires]
    C[Configuration] --> N
    V[Version moteur] --> N
    I[État initial] --> N
    N --> T[Trajectoire]
    T --> H1[Empreinte de contenu]
    T --> H2[Empreinte de cohérence]
    H1 --> R[Comparaison]
    H2 --> R
    R --> D[Même graine, même forme trace]
```

Lecture : quatre éléments définissent un run et deux empreintes le vérifient. Le schéma n'explique pas la tolérance temporelle, détaillée dans la partie sur la reproductibilité.
