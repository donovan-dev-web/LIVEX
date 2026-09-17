# GLOSSARY.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe E (glossaire), Partie 6 (concepts détaillés)

---

> Glossaire central de LIVEX. Les termes spécifiques par composant sont détaillés dans les glossaires de chaque module (référencés ci-dessous).

## A

### Action
Unité de comportement exécutable d'une entité (se déplacer, manger, boire, se reposer, explorer, socialiser, communiquer, commercer, combattre, ramasser...). La sélection de l'action est le fruit de la délibération par **utilité**. (Monographie §3.14)

### ADR
Architecture Decision Record — document structuré qui consigne une décision d'architecture (contexte, décision, conséquences, alternatives). Voir `docs/adr/0000-template.md`.

### Agent / Entité
L'entité est la notion centrale de LIVEX : un être simulé autonome doté d'un état, de besoins, d'une perception, d'une mémoire, de croyances, de capacités, d'un système de décision et d'actions. Le moteur fournit les structures, jamais les conduites. (Monographie §3.7)

## B

### Besoin
Tension comportementale (faim, soif, fatigue, sécurité, social, curiosité) exprimée sur une échelle 0-100 (ou 0-1). Un besoin élevé alimente la génération d'objectifs et la fonction d'utilité — il ne déclenche pas de comportement direct. (Monographie §3.12)

### BDI (Belief-Desire-Intention)
Architecture cognitive : Croyances (ce que l'entité tient pour vrai), Désirs/objectifs (ce qu'elle cherche), Intentions (ce qu'elle s'engage à faire). (Monographie §3.8)

### Boucle de simulation
Séquence d'opérations causales exécutée à chaque tick. Cible V0.1 : 15 étapes (Percevoir → … → Nouvelles perceptions). (Monographie §3.3.4)

## C

### Croyance (Belief)
Représentation structurée d'un fait avec une **confiance** (0-1) et une source (perception, mémoire, communication, inférence). Une croyance est « vraie » pour l'entité si confiance ≥ 0.5. (Monographie §3.11)

## D

### Déterminisme bit-à-bit
Propriété selon laquelle deux exécutions identiques (même seed, même config, même version moteur, même état initial) produisent exactement la même trajectoire. (Monographie §2.3.4, §3.6.3)

### DecisionRecord
Trace complète d'une décision : besoins, croyances considérées, scores d'utilité, action choisie. Base de l'observabilité causale. (Monographie §3.14.12)

## E

### ECHOS
Emergent Complex Hierarchical Observation System — module d'observation, d'analyse et de pilotage. (Monographie §2.3.2)

### Emergence
Apparition de structures collectives (groupes, spécialisations, conflits, marchés) non programmées : elles résultent d'interactions locales sous contraintes. (Monographie §1.1, Partie 6)

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

### PRISM
Perceptual Rendering & Interactive Simulation Module — couche de représentation 3D et d'interaction, jamais source de vérité. (Monographie §2.3.3)

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

## T

### Tick
Unité de temps simulé : 1 tick = 1 minute simulée par défaut (défaut 10 ticks/seconde réelle → 1 s = 10 min simulées). (Monographie §3.6.1)

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