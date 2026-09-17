# ADR-001 : SQLite vs JSON pour la persistance

## Statut
✅ **Accepté**

## Contexte

V2 doit supporter une simulation persistante (save/load) pour un monde pouvant durer 100+ heures de jeu, potentiellement avec 1000 agents.

**Options considérées** :

1. **JSON** (approche V1)
   - Simple, lisible par l'humain
   - Sérialisation standard (.NET JsonSerializer)
   - Problème : lent pour les grands jeux de données (analyse de tout le fichier, requêtes O(n))

2. **SQLite**
   - Transactions ACID (durabilité)
   - Indexation (requêtes O(log n))
   - Évolutif (optimisé pour les grands jeux de données)
   - Versioning de schéma intégré

3. **MongoDB**
   - Orienté document (schéma flexible)
   - Bon pour les hiérarchies complexes
   - Nécessite un service externe (complexité de déploiement)

## Décision

**SQLite** (avec repli sur JSON pour les petites sauvegardes)

**Justification** :

- **Performance** : 1000 agents + relations → 10K+ lignes. La vitesse de requête SQLite est essentielle.
- **Intégrité** : transactions ACID → save/load bit-parfait, pas de corruption
- **Portabilité** : fichier unique (.db), pas de service externe
- **Évolution du schéma** : suivi de version intégré
- **Indexation** : requêtes de relations rapides

## Compromis

- **Complexité** : +conception de schéma, gestion des migrations
- **Débogage** : moins lisible que JSON (mais des outils sont disponibles)
- **Démarrages à froid** : l'initialisation de la BD est légèrement plus lente que l'analyse JSON

## Implémentation

- SQLite 3.x via System.Data.SQLite
- Schéma v1.0 (11 tables principales)
- Déterminisme bit-parfait via la persistance de l'état PRNG
- Gestionnaire de migration pour les futurs changements de schéma

---

# ADR-002 : BDI Model vs Behavior Trees

## Statut
✅ **Accepté**

## Contexte

La prise de décision des agents a besoin d'un formalisme supportant :
- **Observabilité partielle** (les agents peuvent avoir de fausses croyances)
- **Raisonnement multi-objectifs** (hiérarchie des besoins)
- **Modélisation sociale** (confiance, communication)
- **Comportement émergent** (règles codées en dur minimales)

**Options** :

1. **BDI (Belief-Desire-Intention)**
   - Les agents ont des croyances (modèle du monde)
   - Des Désirs (objectifs issus des besoins)
   - Des Intentions (engagées dans une action)
   - Bon pour : la délibération, les scénarios sociaux
   - Mauvais pour : le contrôle strict, les systèmes à contrainte de temps

2. **Behavior Trees**
   - Décomposition hiérarchique des tâches
   - Nœuds de séquence/sélection explicites
   - Bon pour : les jeux, l'animation
   - Mauvais pour : le raisonnement complexe, l'interaction sociale

3. **Utility-based AI**
   - Les agents scorifient les actions par utilité
   - Simple, transparent
   - Bon pour : la coordination multi-agents
   - Mauvais pour : la planification complexe à long terme

## Décision

**BDI** avec **sélection d'action basée sur l'utilité**

**Justification** :

- BDI fournit un **formalisme** pour l'observabilité partielle (croyances ≠ réalité)
- Le scoring d'utilité fournit de la **transparence** (pourquoi l'agent a-t-il choisi cette action ?)
- Séparation des préoccupations : Perception→Memory→Beliefs→Goals→Utility→Action
- Chaque couche est testable et remplaçable indépendamment
- S'aligne sur les normes académiques de la modélisation à base d'agents

## Compromis

- **Complexité** : pipeline d'agent complexe
- **Débogage** : processus de décision en plusieurs étapes à tracer
- **Performance** : plus de sous-systèmes à optimiser (mais dans des limites acceptables)

## Implémentation

- Pipeline BDI sous forme de machine à états séquentielle
- Chaque couche (Perception, Memory, Beliefs, etc.) est un module indépendant
- La fonction d'utilité combine plusieurs facteurs (bénéfice, coût, risque, personnalité)
- Les décisions capturent la trace complète pour le débogage

---

# ADR-003 : Partial Observability as First-Class Feature

## Statut
✅ **Accepté**

## Contexte

**V1** : la simulation était omnisciente (les agents avaient accès à World.AllAgents, World.AllResources)

**V2** : on souhaite des comportements émergents issus de l'asymétrie d'information (désinformation, découverte, coopération-pour-partage)

**Options** :

1. **Agents omniscients** (conserver V1)
   - Simple, prévisible
   - Pas de dynamique d'information émergente
   - Irréaliste

2. **Observabilité partielle**
   - Les agents ne voient que ce que les capteurs détectent
   - Maintiennent leur propre modèle de croyances
   - Peuvent avoir de fausses croyances
   - Complexe mais comportement émergent riche

## Décision

**Observabilité partielle comme mécanique centrale**

**Règles d'application** :

- Le code de l'agent ne **DOIT PAS** accéder à `World.AllAgents`, `World.AllResources`
- Toutes les requêtes d'information via **les croyances propres de l'agent**
- Le système de perception crée des observations (qui peuvent être inexactes)
- La communication est le seul moyen de partager l'information

**Avantages** :

- La désinformation se propage naturellement
- L'exploration est utile (découverte d'une ressource inconnue)
- La coopération est encouragée (partager l'information = survivre)
- Des conflits surgissent du désaccord

## Implémentation

- La grille spatiale remplace les requêtes globales
- Croyances stockées par agent (pas de modèle partagé)
- L'Analyzer calcule la « vérité terrain » vs la divergence des croyances des agents
- Les tests vérifient que les agents ne trichent pas en accédant directement à World

---

# ADR-004 : Communication Protocol — Local Radius vs Global

## Statut
✅ **Accepté**

## Contexte

V2 devrait faire coordonner les agents via **la communication**, et non des diffusions globales.

**Options** :

1. **Diffusion globale**
   - Tous les agents reçoivent tous les messages
   - Implémentation simple
   - Irréaliste (pas de localité)

2. **Rayon local uniquement**
   - Messages entendus uniquement dans le rayon (ex. : 50 unités)
   - Les agents doivent être proches pour communiquer
   - Supporte les hubs d'information (agents de confiance comme relais)
   - Plus réaliste

3. **Hybride**
   - Communication directe dans le rayon
   - Messages relayés au-delà du rayon (avec dégradation)

## Décision

**Rayon local avec relais optionnel**

**Détails** :

- Mécanisme principal : diffusion locale (l'expéditeur envoie, les proches reçoivent)
- Rayon : configurable (50 unités par défaut)
- Dégradation : la confiance du message diminue par hop (perte de 10 %/hop)
- Coût : énergie pour envoyer (5 points) + recevoir (2 points)

**Avantages** :

- La rareté de l'information crée des comportements émergents (pourquoi partager ?)
- Des réseaux d'information se forment (les hubs = agents précieux)
- Communication réaliste

## Implémentation

- `CommunicationSystem.Broadcast(message, radius)`
- Le message inclut `hops` (combien de fois relayé)
- Confidence = initial * 0.9^hops
- Les requêtes de grille spatiale trouvent les récepteurs proches

---

# ADR-005 : Trait-based Personality vs Config Files

## Statut
✅ **Accepté**

## Contexte

On souhaite que les agents aient des **comportements diversifiés** sans coder en dur les différences de comportement.

**Options** :

1. **Comportement codé en dur par agent**
   - Code différent pour chaque type d'agent
   - Inflexible

2. **Fichiers de config par agent**
   - YAML/JSON avec paramètres de comportement
   - Toujours complexe à gérer avec 1000 configs

3. **Système de traits (procédural)**
   - Les agents ont des traits (Bravery, Curiosity, etc.)
   - Les traits modifient les calculs d'utilité
   - Traits générés aléatoirement + stockés avec l'agent

## Décision

**Système de traits avec 8 traits fondamentaux**

**Traits** (plage 0-2, 1.0 = neutre) :

- Bravery (tolérance au risque)
- Curiosity (drive d'exploration)
- Sociability (préférence de groupe)
- Greed (focalisation sur les ressources)
- Pessimism (prudence)
- Aggression (volonté de combat)
- Strength (puissance de combat)
- Speed (vitesse de déplacement)

**Application** :

- Chaque trait modifie les scores d'utilité concernés
- `utility *= personality_modifier` basé sur trait × type d'action
- Stocké en SQLite par agent

## Implémentation

- Classe `Traits` avec champs publics
- `Agent.Traits` généré via `Traits.Randomize()`
- Sauvegardé/chargé dans la table `agents`
- Modifie les calculs d'utilité dans `UtilityEvaluator`

---

# ADR-006 : Spatial Grid vs Quadtree

## Statut
✅ **Accepté** (Phase 9)

## Contexte

Goulot d'étranglement de perception en O(n) : chaque agent scanne tous les autres agents. 500 agents = 250K comparaisons/tick.

**Options** :

1. **Brute force** (O(n²))
   - Simple
   - Inacceptable pour 500+ agents

2. **Grille spatiale**
   - Grille de cellules fixes
   - Cas moyen O(1) (ajuster la taille des cellules)
   - Implémentation simple

3. **Quadtree**
   - Subdivision adaptative
   - Cas pire O(log n)
   - Plus complexe

## Décision

**Grille spatiale avec reconstruction périodique**

**Détails** :

- Monde divisé en cellules (ex. : cellules de 50×50 unités)
- Agents hashés dans des cellules par position
- `QueryRadius()` ne vérifie que les cellules proches
- Fréquence de reconstruction : toutes les 10 ticks

**Réglages** :

- Cell size = sqrt(world_area / (agent_count / 7))
- 50 agents → 119 units/cell
- 500 agents → 37 units/cell
- 1000 agents → 26 units/cell

## Implémentation

- Classe générique `SpatialGrid<T>`
- `Insert(entity)`, `Update(entity)`, `QueryRadius(pos, radius)`
- Benchmark : comparer au O(n²) naïf pour vérifier l'accélération

---

# ADR-007 : Emergence Metrics vs Custom KPIs

## Statut
✅ **Accepté**

## Contexte

L'Analyzer doit **mesurer la complexité émergente** au-delà de simples statistiques.

**Options** :

1. **Score d'émergence unique**
   - Trop simplifié
   - Perte de nuance

2. **Métriques multidimensionnelles**
   - Diversité cognitive (entropie des croyances)
   - Complexité sociale (mesures de réseau)
   - Propagation de l'information (vitesse des rumeurs)
   - Boucles de rétroaction (cycles causaux)
   - Dynamique de groupe (formation/dissolution)

## Décision

**Cadre d'émergence à 7 métriques**

**Métriques** :

1. Diversité cognitive (entropie de Shannon des croyances)
2. Vitesse de propagation de l'information (ticks pour atteindre 80 %)
3. Complexité sociale (coefficient de clustering, communautés)
4. Convergence des objectifs (% d'agents sur le même objectif principal)
5. Force de la boucle de rétroaction (facteur d'amplification)
6. Dynamique de groupe (taux de formation, taux de succès, rotation)
7. Score d'émergence (composite 0-1)

**Justification** :

- Chaque métrique capture une facette différente de l'émergence
- Score composite pour les tendances de haut niveau
- Métriques détaillées pour une analyse approfondie
- Permet des études de reproductibilité

## Implémentation

- Classe `EmergenceIndicators` avec 7 métriques
- Calculé une fois toutes les 10 ticks
- Stocké dans la table SQLite `emergence_metrics`
- Visualisé dans la Web UI

---

# ADR-008 : Save/Load Determinism via PRNG State

## Statut
✅ **Accepté**

## Contexte

Le monde persistant doit être **reproductible bit-parfait** : charger depuis une sauvegarde, exécuter 100 ticks de plus, devrait donner des résultats identiques à une exécution continue.

**Options** :

1. **Resimuler depuis le tick 0**
   - Déterminisme garanti
   - Coût prohibitif (100+ heures)

2. **Sauvegarder l'état PRNG**
   - Persister l'état du générateur Random
   - Restaurer et continuer
   - Rapide, reproductible

3. **Régénération basée sur la seed**
   - Utiliser la seed pour régénérer tout l'état des entités
   - Complexe, sujet aux erreurs

## Décision

**Sauvegarder l'état PRNG (xorshift128+) en SQLite**

**Détails** :

- Tout l'aléatoire via une instance `Random` ensemencée unique
- Sauvegarder `.GetState()` (paire de seeds 64-bit) dans `simulation_state.rng_state`
- Charger `.SetState()` avant de reprendre le tick
- Test bit-parfait : comparer le monde sérialisé au tick T vs au tick T+100

## Implémentation

```csharp
public void SaveSimulation()
{
    using (var tx = conn.BeginTransaction())
    {
        // ... save agents, beliefs, etc. ...
        
        cmd.CommandText = "UPDATE simulation_state SET rng_state = @rng";
        cmd.Parameters.AddWithValue("@rng", world.RngState.Serialize());
        cmd.ExecuteNonQuery();
        
        tx.Commit();
    }
}

public void LoadSimulation()
{
    var rngState = GetRngStateFromDb();
    world.RngState = RngState.Deserialize(rngState);
    // Now call world.Tick() will produce identical results
}
```

## Cas de test

```csharp
[TestMethod]
public void BitPerfect_SaveLoad_Identical()
{
    var world1 = RunSimulation(ticks: 100, seed: 12345);
    SaveAndLoad(world1);
    var world2 = RunSimulation(ticks: 100, seed: 12345, startFrom: world1);
    
    Assert.AreEqual(Serialize(world1), Serialize(world2));  // Byte-for-byte
}
```

---

# ADR-009 : Logging via Structured Events + SQLite

## Statut
✅ **Accepté**

## Contexte

On souhaite **rejouer les décisions** pour le débogage et l'analyse sans rejouer la simulation complète.

**Options** :

1. **Journaux texte**
   - Simple
   - Non structurés, difficiles à interroger

2. **Journal d'événements JSON**
   - Structuré
   - Basé sur fichier (grand, lent à interroger)

3. **Table SQLite event_log**
   - Structuré + interrogeable
   - Stockage efficace
   - Intégré à la persistance

## Décision

**Tables SQLite `events_log` + `decision_traces`**

**Tables** :

- `events_log` : événements de Perception, Communication, Action, mise à jour de croyances
- `decision_traces` : état BDI complet à chaque point de décision
- Toutes deux indexées par (agent_id, tick) pour des requêtes rapides

**Avantages** :

- Requête : « Afficher toutes les décisions d'Alice entre le tick 100 et 200 »
- Rejeu : charger la trace de décision, inspecter la raison du choix
- Analyse : calculer les taux de succès par type d'action
- Débogage : trier par agent, rejouer le processus de réflexion

## Implémentation

- Modèle d'événement avec 8 types d'événements
- La classe `EventRecorder` écrit dans les tables
- Index sur (agent_id, tick, event_type)
- Outil d'export CSV pour l'analyse externe
