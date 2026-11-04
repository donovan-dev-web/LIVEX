# GLOSSARY.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 3 novembre 2026
**Dépend de** : —
**Source Monographie** : Annexe E (glossaire), Partie 6 (concepts détaillés)

---

> Glossaire central de LIVEX. Les termes spécifiques par composant sont détaillés dans les glossaires de chaque module (référencés ci-dessous).

## A

### Action
Unité de comportement exécutable d'une entité (se déplacer, manger, boire, se reposer, explorer, socialiser, communiquer, commercer, combattre, ramasser...). La sélection de l'action est le fruit de la délibération par **utilité**. (Monographie §3.14)

### ADR
Architecture Decision Record — document structuré qui consigne une décision d'architecture (contexte, décision, conséquences, alternatives). Voir `../adr/0000-template.md`.

### Agent / Entité
L'entité est la notion centrale de LIVEX : un être simulé autonome doté d'un état, de besoins, d'une perception, d'une mémoire, de croyances, de capacités, d'un système de décision et d'actions. Le moteur fournit les structures, jamais les conduites. (Monographie §3.7)

## B

### Besoin
Tension comportementale (faim, soif, fatigue, sécurité, social, curiosité) exprimée sur une échelle 0-100 (ou 0-1). Un besoin élevé alimente la génération d'objectifs et la fonction d'utilité — il ne déclenche pas de comportement direct. (Monographie §3.12)

### BDI (Belief-Desire-Intention)
Architecture cognitive : Croyances (ce que l'entité tient pour vrai), Désirs/objectifs (ce qu'elle cherche), Intentions (ce qu'elle s'engage à faire). (Monographie §3.8)

### Bloc
Unité élémentaire du monde voxel de PRISM V2 : une cellule `(x, y, z)` porte un **matériau** (identifiant stable 16 bits), un **état** (0-255 : stade de croissance, variante) et éventuellement une surcharge clairsemée lorsqu'il diffère de la colonne uniforme de surface. Un bloc peut être solide, liquide ou non perceptible ; ses propriétés (dureté, palier d'outil, drops, tags) viennent du pack de contenu, jamais du code. (`../../docs/prism/ARCHITECTURE.md`, PRISM-023)

### Boucle de simulation
Séquence d'opérations causales exécutée à chaque tick. Cible V0.1 : 15 étapes (Percevoir → … → Nouvelles perceptions). (Monographie §3.3.4)

## C

### Chantier
Structure en cours de construction dans PRISM V2 : un **gabarit** (template) posé sur un site, avec un volume, un travail requis en ticks et des matériaux dus. Un chantier est réservé par son initiateur, progresse par contributions horodatées (plusieurs agents, sans message dirigé), publie son avancement par la **perception différentielle** et peut être abandonné. Ses droits (`build`, `use`) séparent propriétaire, agents et groupes. (PRISM-090/091/093)

### Chunk / Région
Subdivision horizontale du monde de PRISM V2 : `regionSize` blocs de côté (16 par défaut), indexée `regionId = regionX + regionCountX * regionY`. La région est l'unité de **dirty flag**, de maillage incrémental (PRISM-110) et de mise à jour aléatoire parallèle (PRISM-042) ; « chunk » désigne la même subdivision dans la documentation de génération et de rendu. Le protocole de lecture parle de **delta régional**. (ADR-011, PRISM-023)

### Croyance (Belief)
Représentation structurée d'un fait avec une **confiance** (0-1) et une source (perception, mémoire, communication, inférence). Une croyance est « vraie » pour l'entité si confiance ≥ 0.5. (Monographie §3.11)

## D

### Déterminisme bit-à-bit
Propriété selon laquelle deux exécutions identiques (même seed, même config, même version moteur, même état initial) produisent exactement la même trajectoire. (Monographie §2.3.4, §3.6.3)

### DecisionRecord
Trace complète d'une décision : besoins, croyances considérées, scores d'utilité, action choisie. Base de l'observabilité causale. (Monographie §3.14.12)

## E

### ECHOS
Emergent Cognition & Holistic Observation System — module d'observation, d'analyse et de pilotage. (Monographie §2.3.2)

### Emergence
Dans le cadrage LIVEX, apparition de motifs collectifs (groupes, échanges, spécialisations, conflits) qui ne sont pas prescrits par un scénario et résultent d'interactions locales sous contraintes. Le projet parle d'émergence collective « faible » : les causes restent traçables aux règles explicites. L'apprentissage individuel n'est pas l'objet de recherche ; un changement d'état ou de maîtrise défini par une règle n'est pas, à lui seul, une émergence collective. (Monographie §1.1, Partie 6)

### Événement (event)
Fait observable et horodaté produit par la simulation (`spawn`, `mort`, `décision`, `message`...). (Monographie §3.15)

## F

### Fonction d'utilité
Formule centrale de décision : `utility = (benefit − cost − risk) × confidence × personality_modifier + urgency`. Elle rend chaque choix transparent et explicable. (Monographie §3.14.1)

## G

### Groupe
Regroupement d'entités formé par émergence, doté d'un leader, d'une cohésion et de rôles. (Monographie §3.17)

### Grille spatiale
Structure de données (cellules uniformes) accélérant les requêtes de perception et de communication de O(n²) à quasi-linéaire. (Monographie §3.9.6)

## M

### Macro-action
Action composée, pilotée par une **machine à états** côté PRISM V2 (`ObtenirObjet`, `FabriquerMacro`, `Cultiver`) : elle enchaîne des actions primitives (se déplacer, prendre, miner, planter) pour atteindre un but exprimé en objet, en recette ou en parcelle. Elle n'écrit jamais de décision : elle exécute une intention reçue, renvoie un échec motivé (`source_not_found`, `agent_busy`…) et son avancement survit au point de reprise. (PRISM-067)

### Mémoire
Enregistrement des expériences passées selon une décroissance exponentielle de la salience. Capacité bornée (jusqu'à 1000 entrées). À ne pas confondre avec les croyances. (Monographie §3.10)

### Message
Unité de communication inter-entités (pulsation lumineuse). 7 types : Information, Request, Response, Announcement, Warning, Trading, Acknowledgement. (Monographie §3.16)

## O

### Objectif (Goal)
État recherché généré à partir des besoins non satisfaits, doté d'une priorité. (Monographie §3.13)

### Obstacle
Élément statique qui bloque le mouvement (et en V2 la ligne de vue). Formes : rectangle, cercle. (Monographie §3.5.2)

### Observabilité partielle
Une entité ne connaît qu'une partie du monde (rayon de perception). Source de diversité, d'erreur et d'exploration. (Monographie §3.9)

## P

### Pathfinding
Sous-système de navigation de SYNE calculant des chemins 2D autour des obstacles, indépendant de tout moteur graphique. (Monographie §3.19)

### Perception
Première étape de la boucle BDI : transformation de l'état du monde en observations locales, potentiellement inexactes. (Monographie §3.9)

### Pack (pack de contenu)
Jeu de fichiers JSON versionné qui **définit** les règles de PRISM V2 : manifeste, matériaux, objets, recettes, gabarits, croissance, effets, tags et paramètres. Il est compilé et validé au démarrage (erreurs structurées, pack invalide = démarrage refusé) et résumé par un `contentHash` : deux runs ne sont comparables que si le hash est identique. Le pack minimal `prism/content/minimal` est le pack de référence. (PRISM-021/022)

### PRISM
Pour la V1 : *Perceptual Rendering & Interactive Simulation Module*, projet Unreal de représentation livré dans LIVEX 1.0.0. Pour la cible V2 : *Persistent Reality & Interactive Simulation Module*, autorité .NET headless du monde physique ; SYNE conserve la cognition et le viewer Silk.NET facultatif lit le flux publié.

### PRISM-LDK
Plugin Unreal historique (*LIVEX Development Kit*) de PRISM V1 ; module `PrismLdk`. La cible PRISM V2 abandonne cette architecture au profit d'un noyau .NET headless.

### PRNG (xoshiro256\*\*)
Générateur pseudo-aléatoire rapide et de haute qualité, utilisé pour tous les tirages — initialisation splitmix64(seed). `System.Random` est **interdit**. (Monographie §3.6.3)

## R

### Ressource
Élément spatial consommable (nourriture, eau, bois...) : fini ou régénérant, source de contraintes comportementales. (Monographie §3.18, §6.9)

### Run
Exécution complète et reproductible d'une expérience, définie par seed + config + version moteur + état initial. (Monographie §3.6.2)

## S

### Seed
Entier 64 bits initialisant le PRNG ; elle rend deux runs comparables et reproductibles. (Monographie §3.6.2)

### Scheduler
Composant qui exécute les sous-systèmes à des fréquences différentes (élevée, moyenne, adaptative, faible) selon leur coût et leur besoin. (Monographie §3.4)

### SYNE
Systems & Emergent Network Engine — moteur de simulation ; possède la vérité du monde. (Monographie §2.3.1)

### syne-mock
Serveur Node.js de développement qui émule une partie du protocole et du cycle de simulation de SYNE pour tester les clients, en particulier le plugin Unreal. Ses algorithmes sont simplifiés ; il n'est pas le moteur de simulation et ses résultats ne sont pas interchangeables avec ceux de SYNE.

### LIVEX Development Kit (LDK)
Nom complet du plugin PRISM-LDK de la V1. LDK ne désigne pas le projet complet : celui-ci est LIVEX. Le plugin et l'hôte Unreal sont conservés comme historique de la V1 ; la cible PRISM V2 est documentée dans `docs/prism/`.

## T

### Tag
Étiquette hiérarchique nommée d'un pack de contenu, rattachée à des objets, des matériaux ou des recettes. Les tags permettent d'écrire des règles génériques (« tout ce qui est `fuel` ») sans énumérer les identifiants, et de sélectionner des entrées de recette par tag. Un tag est résolu en **masque de bits** au chargement du pack pour que la perception et les filtres restent entiers et déterministes. (PRISM-021, PRISM-054)

### Tick
Unité de temps simulé : 1 tick = 1 minute simulée par défaut (défaut 10 ticks/seconde réelle → 1 s = 10 min simulées). (Monographie §3.6.1) En PRISM V2, le tick est l'unité de **pas de verrouillage** : l'hôte exécute exactement une fois chaque phase dans l'ordre fixe de `SIMULATION_LOOP.md`, refuse un tick non consécutif (`unexpected_tick`) et rejoue une commande déjà appliquée à l'identique sans la réexécuter (fenêtre d'idempotence). (PRISM-071/072)

### Trait
Paramètre de personnalité (bravery, curiosity, sociability, greed, pessimism, aggression, strength, speed), plage 0-2, neutre 1.0, modulant la fonction d'utilité. (Monographie §3.7.4)

## U

### Utilité
Voir **Fonction d'utilité**.

## V

### Version moteur
Identifiant qui garantit des règles identiques entre runs (navbar inclus dans la définition d'une expérience reproductible). (Monographie §3.6.2)

---

## Points restés ouverts dans ce document
- Le glossaire de la Monographie (Annexe E) peut contenir d'autres entrées à reprendre si le besoin surgit ; ce document restera la référence centrale et sera enrichi sans rupture.
