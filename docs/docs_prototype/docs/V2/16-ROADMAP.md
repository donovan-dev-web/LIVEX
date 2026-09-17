# Roadmap détaillée V2

## 0. Conception et architecture (Étape préalable)

### Objectif
Établir spécifications formelles, ADRs, et modèles conceptuels avant développement.

### Livrables

- ✅ **Document de vision** (01-VISION.md)
- ✅ **Modèle conceptuel** (02-CONCEPTUAL-MODEL.md)
- ✅ **Spécification fonctionnelle** (03-V2-SPECIFICATION.md)
- ✅ **Architecture modulaire** (04-ARCHITECTURE.md)
- ✅ **Spécification du protocole de communication** (07-COMMUNICATION-PROTOCOL.md)
- ✅ **Conception de la persistance** (08-PERSISTENCE.md)
- ✅ **Métriques de l'Analyzer** (09-ANALYZER-V2.md)
- Architecture Decision Records (ADRs)
  - ADR-001: SQLite vs JSON for persistence
  - ADR-002: BDI model vs Behavior Tree
  - ADR-003: Partial observability implications
  - ADR-004: Communication radius and costs
  - ADR-005: Trait-based personality vs config files
  - ADR-006: Spatial grid vs quadtree
  - ADR-007: Emergence metrics vs custom KPIs
  - ADR-008: Save/load determinism via PRNG state
  - ADR-009: Structured-event logging via SQLite

### Durée
**1 semaine** (documentation uniquement)

---

## 1. Architecture cognitive BDI et perception (2 semaines)

### Objectif
Construire les fondations : un agent capable de percevoir et de mémoriser.

### 1.1 Moteur de simulation

**Tâches** :

- [ ] Refactoriser la classe `Agent` pour une structure BDI
  - Ajouter les sous-systèmes `Perception`, `Memory`, `BeliefStore`, `Needs`, `Goals`
  - Sérialiser/désérialiser pour SQLite
- [ ] Implémenter `PerceptionSystem`
  - Rayon configurable
  - Interroger les entités proches (grille spatiale)
  - Distinguer les types d'entités (agent, ressource, obstacle)
  - Retourner la liste de `Observation`
- [ ] Implémenter `MemorySystem`
  - Stocker les observations avec horodatage
  - Appliquer une décroissance exponentielle
  - Limiter le stockage (max 1000 entrées/agent)
- [ ] Implémenter `BeliefStore`
  - Représenter les croyances (fait + confiance + source + expiration)
  - Règles de révision lors de nouvelles observations
  - Logique d'expiration
- [ ] Tests unitaires (PerceptionSystemTests, MemoryTests, BeliefStoreTests)
  - **Couverture cible : 80 %**
  - Cas limites : perception vide, décroissance de la mémoire à zéro, contradictions de croyances

**Effort estimé** : 1 semaine (3 devs)

### 1.2 Analyzer

- [ ] Analyser les perceptions depuis le transport (le nouveau format de snapshot inclut les croyances)
- [ ] Métriques de base : agents avec croyances, confiance moyenne des croyances
- [ ] Stocker les runs dans SQLite (via `RunStore`)

**Effort estimé** : 2 jours (1 dev)

### 1.3 Web UI

- [ ] Ajouter le composant `BeliefInspector`
- [ ] Afficher les croyances des agents (liste avec confiance, horodatage)
- [ ] Vue chronologique des changements de croyances

**Effort estimé** : 3 jours (1 dev)

### 1.4 Godot

- [ ] Étendre le HUD pour afficher les croyances en mode debug
- [ ] Pas de changement majeur au rendu

**Effort estimé** : 1 jour

### Critères de succès

- [ ] 50 agents exécutent 1000 ticks sans crash
- [ ] La perception détecte correctement les entités proches
- [ ] Les entrées de mémoire décroissent de manière prévisible
- [ ] Les croyances se mettent à jour avec les nouvelles observations
- [ ] Les tests passent, couverture ≥ 80 %

---

## 2. Mémoire, croyances et observations avancées (2 semaines)

### Objectif
Enrichir la mémoire pour le support de la divergence informationnelle.

### 2.1 Moteur de simulation

- [ ] Améliorer le calcul de décroissance de `MemorySystem`
  - Taux de décroissance configurables par type
  - Tester différents paramètres de décroissance
- [ ] Implémenter la logique `BeliefRevision`
  - Mettre à jour la confiance quand l'observation concorde
  - Réduire la confiance quand l'observation contredit
  - Gérer les observations ambiguës
- [ ] Supporter les catégories de mémoire (observations, événements, interactions)
- [ ] Implémenter l'« expiration » des croyances obsolètes
  - La confiance des croyances plafonne à une valeur inférieure après expiry_tick
- [ ] Tests pour la décroissance, la révision, l'expiration

**Effort estimé** : 1 semaine (2 devs)

### 2.2 Analyzer

- [ ] Calculer les métriques :
  - Désaccord de croyances (% d'agents en désaccord sur le même fait)
  - Variance de confiance au sein de la population
  - Dérive des croyances dans le temps (réalité du monde vs croyances des agents)
- [ ] Détecter les incohérences (l'agent croit X mais le monde a Y)

**Effort estimé** : 3 jours (1 dev)

### 2.3 Web UI

- [ ] Chronologie : historique des croyances (graphique tick vs confiance)
- [ ] Vue de comparaison : « L'agent croit... » vs « La vérité du monde... »
- [ ] Bascule debug pour afficher toutes les croyances

**Effort estimé** : 3 jours (1 dev)

### 2.4 Godot

- [ ] Améliorations mineures du HUD

**Effort estimé** : 1 jour

### Critères

- [ ] 50 agents avec 2000 ticks montrent des croyances divergentes
- [ ] Les croyances décroissent correctement (courbe de confiance)
- [ ] L'Analyzer rapporte la divergence de croyances entre agents
- [ ] La Web UI affiche correctement la chronologie des croyances

---

## 3. Système de décision et utilité avancée (2 semaines)

### Objectif
Implémenter une délibération BDI complète avec une fonction d'utilité multidimensionnelle.

### 3.1 Moteur

- [ ] Implémenter `NeedsSystem`
  - Hunger, Thirst, Fatigue, SafetyNeed, SocialNeed, CuriosityDrive
  - Mettre à jour à chaque tick selon l'état de l'agent
  - Tester les calculs
- [ ] Implémenter `GoalSystem`
  - Générer des objectifs candidats à partir des besoins
  - Filtrer par faisabilité (l'agent peut-il y parvenir ?)
  - Assigner des priorités
- [ ] Implémenter `UtilityEvaluator`
  - Formule de scoring multi-critères
  - Considérer : satisfaction des besoins, coût, risque, confiance, traits
  - Retourner une liste d'actions scorées
- [ ] Implémenter `DecisionSystem`
  - Orchestrer : perception → belief → needs → goals → utility → intention
  - Sélectionner l'action à utilité la plus élevée
  - Générer `DecisionRecord` (trace)
  - Logique d'interruption (un nouvel objectif interrompt l'action en cours)
- [ ] Tests pour le scoring d'utilité, la génération d'objectifs, la sélection de décision

**Effort estimé** : 2 semaines (3 devs)

### 3.2 Analyzer

- [ ] Suivre les décisions (objectifs vs scores vs action choisie)
- [ ] Calculer les métriques : diversité des décisions, stabilité des objectifs

**Effort estimé** : 2 jours

### 3.3 Web UI

- [ ] Panneaux :
  - Besoins actuels (graphique en barres)
  - Objectifs actifs (liste + priorité)
  - Actions candidates avec scores
  - Intention sélectionnée
- [ ] Heatmap : traits × besoins

**Effort estimé** : 1 semaine (1 dev)

### 3.4 Godot

- [ ] Afficher l'intention dans le HUD

**Effort estimé** : 1 jour

### Critères

- [ ] Des agents différents avec des traits différents prennent des décisions différentes
- [ ] Les facteurs du scoring d'utilité sont observables (sortie debug)
- [ ] Les objectifs sont générés depuis les besoins
- [ ] Les décisions sont traçables (pourquoi l'agent a-t-il choisi cette action ?)

---

## 4. Actions, intentions et exécution (2 semaines)

### Objectif
Implémenter des actions modulaires guidées par BDI.

### 4.1 Moteur

- [ ] Refactoriser les actions (hériter de la base `Action`)
  - `MoveTo`, `Eat`, `Drink`, `Rest`, `Explore`, `Gather`, `Observe`, `Wait`, `Think`
  - Ajouter de nouvelles actions : `CommunicateTo`, `Trade`, etc.
  - Chaque action : `CanExecute()`, `Execute()`, `Update()`, `Complete()`
- [ ] Implémenter `ActionSystem`
  - Démarrer l'action (définir l'état sur Exécution)
  - Mettre à jour la progression à chaque tick
  - Terminer/Échouer/Annuler
  - Générer des événements
- [ ] Implémenter la logique d'interruption
  - Un nouvel objectif urgent annule l'action en cours
  - L'action devient invalide (la cible disparaît)
  - Ressource épuisée
- [ ] Actions multi-tick (ex. : MoveTo prend plusieurs ticks)
- [ ] Tests pour l'exécution des actions, l'interruption, les flux multi-tick

**Effort estimé** : 2 semaines (3 devs)

### 4.2 Analyzer

- [ ] Journaliser les événements d'action (démarrée, terminée, annulée, échouée)
- [ ] Métriques : taux de succès par type d'action, durée moyenne, fréquence d'interruption

**Effort estimé** : 2 jours

### 4.3 Web UI

- [ ] Chronologie des actions (historique des actions + résultats)
- [ ] Barre de progression pour l'action en cours

**Effort estimé** : 3 jours

### 4.4 Godot

- [ ] Coloration basée sur l'action (synchronisée avec l'action en cours)

**Effort estimé** : 2 jours

### Critères

- [ ] MoveTo : l'agent se déplace vers la cible sur plusieurs ticks
- [ ] Eat : la faim diminue, la ressource diminue
- [ ] Les actions peuvent être interrompues
- [ ] Les actions échouées ne corrompent pas l'état

---

## 5. Communication et protocole (2 semaines)

### Objectif
Protocole de communication structurée avec portée locale et coûts.

### 5.1 Moteur

- [ ] Implémenter la classe `Message` (expéditeur, destinataire, type, payload, confiance, coûts)
- [ ] Implémenter `CommunicationSystem`
  - Envoyer un message (mise en file, coût en énergie)
  - Diffuser dans le rayon (requête spatiale)
  - Recevoir un message (ajouter à la file)
  - Traiter les messages entrants (mettre à jour les croyances)
- [ ] Types de messages : Information, Request, Response, Announcement, Warning, Trading, Acknowledgement
- [ ] Système de coût (énergie pour envoyer/recevoir)
- [ ] Dégradation des messages (la confiance diminue par hop/temps)
- [ ] Intégration de croyances basée sur la confiance (confiance de l'expéditeur × confiance du message)
- [ ] Tests : diffusion, réception, calcul des coûts, dégradation

**Effort estimé** : 2 semaines (3 devs)

### 5.2 Analyzer

- [ ] Suivre les messages (expéditeur, destinataire, contenu, résultat)
- [ ] Calculer les métriques : volume de messages, vitesse de diffusion de l'information, précision des rumeurs
- [ ] Graphe social depuis les messages (qui parle à qui)

**Effort estimé** : 1 semaine (1 dev)

### 5.3 Web UI

- [ ] Journal de communication (messages reçus par l'agent)
- [ ] Graphe du réseau social (nœuds = agents, arêtes = communication)
- [ ] Inspection du contenu des messages

**Effort estimé** : 1 semaine (1 dev)

### 5.4 Godot

- [ ] Visualisation des messages (flèches entre agents lors d'une comm)

**Effort estimé** : 2 jours

### Critères

- [ ] Les messages n'atteignent que les agents dans le rayon
- [ ] La confiance se dégrade avec les hops
- [ ] Les agents mettent à jour leurs croyances depuis les messages
- [ ] La communication coûte de l'énergie
- [ ] La confiance affecte la mise à jour des croyances

---

## 6. Groupes et coalitions explicites (1.5 semaines)

### Objectif
Les agents peuvent former et rejoindre des groupes explicitement.

### 6.1 Moteur

- [ ] Implémenter la classe `Group` (id, leader, membres, objectif, affectations de rôles)
- [ ] Implémenter `GroupSystem`
  - Créer un groupe
  - Rejoindre/quitter un groupe
  - Inventaire de ressources partagées
  - Prise de décision collective (vote sur les actions)
  - Dissolution quand l'objectif est atteint ou s'il reste trop peu de membres
- [ ] Suivi `GroupMembership` (agent → groupe, rôle, joined_tick, left_tick)
- [ ] Tests : création de groupe, adhésion, départ, rôles des membres, dissolution

**Effort estimé** : 1.5 semaines (2 devs)

### 6.2 Analyzer

- [ ] Suivre les formations/dissolutions de groupes
- [ ] Métriques : taille moyenne des groupes, durée de vie, taux de succès, rotation des membres

**Effort estimé** : 3 jours

### 6.3 Web UI

- [ ] Explorateur de groupes (liste, membres, objectifs)
- [ ] Chronologie des groupes (formation → dissolution)

**Effort estimé** : 1 semaine (1 dev)

### 6.4 Godot

- [ ] Colorer les agents selon leur appartenance à un groupe

**Effort estimé** : 1 jour

### Critères

- [ ] Les agents peuvent créer un groupe
- [ ] D'autres agents peuvent rejoindre via communication
- [ ] Les groupes prennent des décisions collectives
- [ ] Les groupes se dissolvent une fois terminés

---

## 7. Dynamique systémique (ressources, production, rétroactions) (2 semaines)

### Objectif
Enrichir le monde avec des cycles de ressources et des événements.

### 7.1 Moteur

- [ ] Améliorer `ResourceSystem`
  - Production/régénération (taux configurable)
  - Dégradation si non utilisé (la quantité diminue au fil du temps)
  - Types de ressources (food, water, wood, mineral)
  - Suivre la consommation (qui, quand, combien)
- [ ] Implémenter `EnvironmentSystem`
  - Saisons (printemps, été, automne, hiver)
  - Cycles jour/nuit (affectent la perception, l'activité)
  - Événements (sécheresse, abondance, épidémie, tremblement de terre)
  - Appliquer les effets des événements aux agents/ressources
- [ ] Tests : production, dégradation, changements saisonniers, effets d'événements

**Effort estimé** : 2 semaines (3 devs)

### 7.2 Analyzer

- [ ] Suivre la consommation vs la régénération des ressources
- [ ] Détecter la durabilité (la ressource est-elle renouvelable ?)
- [ ] Analyse d'impact des événements

**Effort estimé** : 1 semaine

### 7.3 Web UI

- [ ] Graphiques de ressources : consommation vs régénération (séries temporelles)
- [ ] Heatmap de distribution des ressources
- [ ] Journal d'événements

**Effort estimé** : 1 semaine

### 7.4 Godot

- [ ] Visualiser les saisons (couleur/éclairage)
- [ ] Effets d'événements (retour visuel)

**Effort estimé** : 2 jours

### Critères

- [ ] Les ressources se régénèrent au taux configuré
- [ ] La dégradation fonctionne (les ressources inutilisées diminuent)
- [ ] Les saisons changent le comportement des agents
- [ ] Les événements modifient l'état du monde

---

## 8. Monde partiellement observable (intégration complète) (1 semaine)

### Objectif
L'observabilité partielle est entièrement intégrée dans tous les systèmes.

### 8.1 Moteur

- [ ] Valider que les agents n'accèdent pas globalement à World.AllAgents/AllResources
- [ ] Toutes les décisions utilisent uniquement agent.beliefs/perception
- [ ] Test : 1000 agents, décisions basées sur info partielle
- [ ] Benchmark : agents < 500 agents perf V1

**Effort estimé** : 1 semaine

### 8.2 Analyzer + others

- [ ] Supporter l'analyse d'observabilité partielle
- [ ] Métriques de divergence implémentées

**Effort estimé** : inclus dans les phases précédentes

### Critères

- [ ] L'agent ne peut pas « tricher » en accédant à la vérité du monde
- [ ] Les décisions sont démontrablement basées sur les croyances
- [ ] Des erreurs sont possibles (fausses croyances → mauvaises décisions)

---

## 9. Optimisation et scaling (50 → 500 → 1000 agents) (3 semaines)

### Objectif
Passer à l'échelle sans casser la physique du jeu.

### 9.1 Moteur

- [ ] Profilage (mesurer les goulots d'étranglement)
  - Perception (actuel : O(n) pour chaque agent)
  - Décision (évaluation d'utilité pour chaque objectif/action)
  - Communication (diffusion à tous les agents dans le rayon)
- [ ] Implémenter la grille spatiale (optimiser la perception en O(1))
- [ ] Implémenter le LOD (Level of Detail)
  - Agents éloignés : fréquence de décision réduite
  - Agents proches : fréquence complète
  - Config : LOD par zones de distance
- [ ] Traitement par lots pour la communication
- [ ] Benchmarks : 50 agents (référence), 500 agents (stress), 1000 agents (limite)
  - Cible : 20 ticks/sec maintenus
  - Mesurer : temps de perception, temps de décision, temps d'action, temps de comm
- [ ] Optimiser les chemins chauds (vecteurs, collections, allocations mémoire)

**Effort estimé** : 3 semaines (2–3 devs)

### 9.2 Analyzer

- [ ] Gérer les données de 1000 agents
- [ ] Échantillonnage intelligent (sous-échantillonnage des grands jeux de données)
- [ ] Métriques agrégées (ne pas stocker par agent si inutile)

**Effort estimé** : 1 semaine

### 9.3 Web UI

- [ ] Pagination des agents (afficher un sous-ensemble si 1000)
- [ ] Graphiques sous-échantillonnés

**Effort estimé** : 1 semaine

### 9.4 Godot

- [ ] Éliminer les agents distants (culling)
- [ ] Rendu LOD (moins de polygones au loin)

**Effort estimé** : 1 semaine

### Critères

- [ ] 50 agents : 30+ ticks/sec
- [ ] 500 agents : 20+ ticks/sec
- [ ] 1000 agents : 10+ ticks/sec
- [ ] Le rapport de profilage montre les gains d'optimisation

---

## 10. Tests et couverture ≥80% (2 semaines)

### Objectif
Assurance qualité complète, couverture 80%+.

### 10.1 Moteur

- [ ] Tests unitaires pour tous les systèmes (Perception, Memory, Beliefs, Needs, Goals, Decision, Actions, Communication, Groups, Resources, Environment)
  - Chaque système : 15–20 tests
  - Total : 150+ tests
  - Outil de couverture : `dotnet test --collect:"XPlat Code Coverage"`
- [ ] Tests d'intégration :
  - Cycle complet : perception → décision → action → conséquences
  - Scénarios multi-agents (coopération, conflit)
  - Impact de l'observabilité partielle
  - Propagation de la communication
- [ ] Tests de régression :
  - Save/load bit-parfait
  - 100 ticks reproductibles

**Effort estimé** : 2 semaines (3 devs)

### 10.2 Analyzer

- [ ] Tests de métriques : tests unitaires pour chaque calcul de métrique
  - 20+ tests
  - Couverture ≥ 80 %

**Effort estimé** : 1 semaine

### 10.3 Web UI

- [ ] Tests Vitest (composants React)
  - 20+ tests
  - Étendre les 11 tests de V1

**Effort estimé** : 1 semaine

### 10.4 Godot

- [ ] Validation de l'import du playtest
- [ ] Pas de changement de code, uniquement de la validation

**Effort estimé** : 2 jours

### Critères

- [ ] `Simulation.Core` ≥ 80 % de couverture de lignes
- [ ] `Analyzer.Core` ≥ 80 %
- [ ] Tous les chemins critiques testés
- [ ] La porte CI applique le seuil de 80 %

---

## 11. Analyzer repensé et métriques émergence V2 (2 semaines)

### Objectif
Refondre l'Analyzer pour mesurer l'émergence BDI avancée.

### 11.1 Moteur

- [ ] Exporter croyances, objectifs, relations dans WorldSnapshot
- [ ] Nouveaux types d'événements pour l'Analyzer (BeliefCreated, BeliefRevised, GoalGenerated, etc.)

**Effort estimé** : 1 semaine (1 dev)

### 11.2 Analyzer

- [ ] Implémenter toutes les métriques (Phase 3) :
  - CognitiveDiversityMetrics
  - InformationPropagationMetrics
  - SocialComplexityMetrics
  - GoalConvergenceMetrics
  - FeedbackLoopDetector
  - GroupDynamicsMetrics
  - EmergenceIndicators
- [ ] Endpoints REST pour toutes les métriques
- [ ] Comparaison de runs (reproductibilité)
- [ ] Tests pour les calculs de métriques

**Effort estimé** : 2 semaines (2 devs)

### 11.3 Web UI

- [ ] Rapports V2 :
  - Panneau de diversité cognitive
  - Graphique de propagation de l'information
  - Visualisation du réseau social
  - Graphique d'alignement des objectifs
  - Liste des phénomènes émergents
  - Outil de comparaison de runs (avancé)

**Effort estimé** : 2 semaines (2 devs)

### 11.4 Godot

- [ ] Heatmaps : distribution des croyances, clusters d'objectifs, réseau social

**Effort estimé** : 1 semaine

### Critères

- [ ] Toutes les métriques calculées correctement
- [ ] Les endpoints API retournent des données
- [ ] La comparaison de runs montre la reproductibilité

---

## 12. CI/CD, persistance BD et déploiement (1.5 semaines)

### Objectif
Finaliser l'infrastructure, la migration SQLite, le déploiement.

### 12.1 Moteur

- [ ] Persistance SQLite complète
  - Schéma v1 (toutes les tables de la Phase 8)
  - Implémentation de Save/Load
  - Validation du déterminisme (bit-parfait)
  - Gestionnaire de migration (versioning)
- [ ] Image Docker (Simulation.Console + SQLite)
- [ ] Docker compose (sim + analyzer + web-ui)

**Effort estimé** : 1 semaine

### 12.2 Analyzer

- [ ] Connexion SQLite (lire les métriques depuis la BD partagée)
- [ ] Image Docker (ASP.NET + Kestrel)

**Effort estimé** : 3 jours

### 12.3 Web UI

- [ ] Image Docker (nginx serve dist/)

**Effort estimé** : 2 jours

### 12.4 CI/CD

- [ ] Workflow GitHub Actions (`.github/workflows/ci.yml`)
  - Build .NET (Release)
  - Tests + couverture seuil 80 %
  - Lint (dotnet format)
  - Build Web UI (npm, lint, format:check, tests)
  - Docker build (les 3 services)
- [ ] Workflow de release
  - Tags SemVer (v2.0.0)
  - Push des images Docker vers GHCR
  - Créer une GitHub Release

**Effort estimé** : 1 semaine (1 dev)

### Critères

- [ ] Docker compose up fonctionne
- [ ] La CI passe avec 80 %+ de couverture
- [ ] Save/load se reproduit exactement
- [ ] Les tags de release déclenchent la CI/CD

---

## Récapitulatif du calendrier

| Phase | Durée | Équipe | Livrable clé |
|-------|-------|--------|--------------|
| 0 | 1 semaine | 1 | Docs, spécs |
| 1 | 2 semaines | 3 | BDI, perception |
| 2 | 2 semaines | 3 | Mémoire, croyances |
| 3 | 2 semaines | 3 | Décision, utilité |
| 4 | 2 semaines | 3 | Actions |
| 5 | 2 semaines | 3 | Communication |
| 6 | 1.5 semaines | 2 | Groupes |
| 7 | 2 semaines | 3 | Ressources, environnement |
| 8 | 1 semaine | 2 | Observabilité |
| 9 | 3 semaines | 3 | Montée en charge, optimisation |
| 10 | 2 semaines | 3 | Tests, couverture |
| 11 | 2 semaines | 2 | Métriques de l'Analyzer |
| 12 | 1.5 semaines | 2 | CI/CD, déploiement |
| **Total** | **~24 semaines** | **2–3 devs** | **V2.0 terminée** |

---

## Allocation des ressources

- **Lead architect** : superviser toutes les phases, maintenir la cohérence
- **Core dev 1** : Simulation Core (Phases 1–9, 12)
- **Core dev 2** : Simulation Core + Analyzer (Phases 1–11)
- **Front-end dev** : Web UI (Phases 1–11)
- **QA/DevOps** : Tests, CI/CD (Phases 10, 12)
- **Godot specialist** : Renderer (Phases 1–12, selon les besoins)

---

## Atténuation des risques

1. **Performance** : Benchmark tôt (Phase 9), itérer
2. **Complexité** : Commencer avec un BDI minimal, étendre itérativement
3. **Cohérence des données** : Valider save/load fréquemment (Phase 12)
4. **Délais CI/CD** : Mettre en place tôt (Phase 0), itérer
