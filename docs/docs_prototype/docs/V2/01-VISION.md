# Vision V2 — Simulation de jeu vidéo 3D persistant

## 1. Évolution de la V1 à la V2

### V1 : Simulation de recherche

- Agents avec besoins simples et Utility AI
- Monde entièrement observable (test d'émergence comportementale)
- Session de simulation courte (quelques milliers de ticks)
- Objectif : prouver la faisabilité des comportements émergents

### V2 : Jeu vidéo 3D persistant

- Agents avec **cognition BDI complète** (perceptions, mémoire, croyances, objectifs, intentions)
- Monde **partiellement observable** (chaque agent a sa propre représentation du monde)
- Simulation **longue durée** (potentiellement des centaines d'heures)
- Monde **persistant** : save/load complet, reprises exactes, world qui évolue constamment
- Objectif : créer une plateforme de **jeu vidéo 3D** où l'émergence est la mécanique centrale

---

## 2. Principes directeurs V2

### 2.1 Autonomie cognitive

Chaque agent doit être capable de :

- **Percevoir** son environnement (rayon limité, perception locale)
- **Mémoriser** ce qu'il a vu, appris, expérimenté
- **Croire** ce qu'il sait (avec confiance/certitude variable)
- **Délibérer** sur ses objectifs (quoi faire ?)
- **Décider** intentionnellement (comment le faire ?)
- **Agir** en conséquence
- **Apprendre** de ses expériences (V3, mais préparation dès V2)

**Rupture majeure avec V1** : l'agent n'a pas accès à la vérité complète du monde. Il prend des décisions basées sur ses **informations incomplètes et potentiellement fausses**.

### 2.2 Monde persistant

Le monde n'est **pas une suite de simulations indépendantes**. C'est un **seul et unique monde** qui :

- Existe en permanence (même sans client graphique)
- **Évolue continuellement** (agents agissent, ressources changent, événements se produisent)
- Peut être **sauvegardé** (état complet en BD)
- Peut être **restauré** (reprises exactes, déterministes)
- Peut fonctionner des **centaines d'heures** de jeu

### 2.3 Observabilité partielle = Émergence riche

Contrairement à V1 où tous les agents voyaient tout :

- Agent A peut connaître la position de la nourriture
- Agent B l'ignore complètement
- Agent C croit avoir vu de la nourriture ailleurs (croyance fausse)
- Ces divergences d'information **génèrent des dynamiques impossibles en V1**

Exemples d'émergence enrichie :

```
- Rumeurs (B apprend de A, mais l'info se déforme)
- Conflits informationnels (A et B croient posséder la même ressource)
- Commerce basé sur l'asymétrie d'information
- Formations de groupes par manque d'info complète
- Erreurs stratégiques (conviction fausse)
```

### 2.4 Système multi-agent vs Simulation émergente

**Important** : la V2 n'est pas juste un "système multi-agents amélioré".

V2 cherche à créer une **simulation émergente systémique** où :

```
Agents autonomes
+
Environnement persistant
+
Ressources limitées
+
Information partielle
+
Communication locale
+
Interactions répétées
         ↓
Phénomènes collectifs non programmés
         ↓
ÉMERGENCE
```

Chaque phénomène collectif (groupes, réseaux commerciaux, hiérarchies, migrations) **émerge** des mécanismes simples, sans être **scripté**.

### 2.5 Séparation cognitive et physique

La V2 maintient (comme V1) une séparation nette :

```
Simulation Core (cognition + logique)
         ↓
Transport API (contrat universel)
         ↓
Présentations (Godot 3D, Web UI, Analyzer)
```

Le moteur **ne sait rien** de Godot. Godot **ne décide rien** pour le moteur.

Cela rend possible :

- Lancer la simulation **headless** (0 graphiques, 1000 agents, vitesse accélérée)
- Étudier l'émergence **sans renderer** (logs, data analysis)
- Remplacer Godot par Unity ou autre sans toucher la simulation

---

## 3. Différences conceptuelles majeures V1 → V2

| Aspect | V1 | V2 |
|--------|----|----|
| **Observation** | Entièrement observable | Partiellement observable |
| **Mémoire** | Rudimentaire (liste d'objets) | Riche (observations datées, decay, confiance) |
| **Croyances** | N/A | Explicites (beliefs séparés de facts) |
| **Décision** | Utility AI simple | BDI délibératif (goals → intentions → actions) |
| **Communication** | N/A | Protocole structuré, portée locale |
| **Groupes** | N/A | Explicites (agents s'inscrivent) |
| **Durée simulation** | Courte (ticks comptés) | Longue (jeu vidéo, centaines d'heures) |
| **Persistance** | Save/load JSON | Save/load SQLite (reprise exacte) |
| **Ressources** | Statiques | Dynamiques (production, régénération, dégradation) |
| **Événements monde** | Aucun | Catastrophes, cycles saisonniers |
| **Scaling** | ~50 agents | 500–1000 agents (avec LOD) |

---

## 4. Architecture conceptuelle V2

```text
┌──────────────────────────────────────────────────────┐
│                  AGENT BDI V2                        │
├──────────────────────────────────────────────────────┤
│                                                      │
│  PERCEPTION                                          │
│      ↓ (ce que je perçois localement)               │
│  MEMORY                                              │
│      ↓ (ce dont je me souviens)                     │
│  BELIEFS                                             │
│      ↓ (ce que je crois savoir = facts + confiance) │
│  INTERNAL STATE                                      │
│      ↓ (santé, énergie, émotions)                   │
│  NEEDS                                               │
│      ↓ (faim, soif, sécurité, social)               │
│  GOALS                                               │
│      ↓ (objectifs générés à partir des besoins)     │
│  UTILITY EVALUATION                                  │
│      ↓ (évaluer actions candidates)                 │
│  DELIBERATION                                        │
│      ↓ (comparer utilités, choisir)                 │
│  INTENTION                                           │
│      ↓ (engagement envers une action)               │
│  ACTION SELECTION & EXECUTION                        │
│      ↓                                               │
│  WORLD INTERACTION                                   │
│                                                      │
└────────────┬─────────────────────────────────────────┘
             ↓
    ┌────────────────┐
    │   ENVIRONMENT  │
    │  (Persistent)  │
    └─────┬──────────┘
          ↓
   Other Agents / Resources / Events
          ↓
    New Perceptions
```

### 4.1 Composants clés

- **Perception** : détection locale (rayon configurable)
- **Mémoire** : historique observations, avec décroissance temporelle
- **Croyances** : représentation croyances (type fait, position, horodatage, confiance)
- **État interne** : physiologie (santé, énergie, traits)
- **Besoins** : pulsions (faim, soif, fatigue, sécurité, social, curiosité)
- **Objectifs** : objectifs à court/moyen terme générés
- **Utilité** : fonction multicritères (satisfaction besoin - coûts - risques)
- **Délibération** : sélection intention basée utilité
- **Intention** : engagement envers une action
- **Actions** : Move, Eat, Drink, Rest, Explore, Communicate, Trade, etc.
- **Communication** : protocole structuré, portée locale
- **Relations** : confiance, familiarité avec autres agents

---

## 5. Monde persistant V2

### 5.1 État complet du monde

Un monde V2 contient :

```
État Simulation
├── Temps (numéro tick, minutes/heures/jours simulés)
├── Agents (positions, états, croyances, objectifs, etc.)
├── Ressources (nourriture, eau, bois, minerais, etc.)
├── Obstacles (statiques)
├── Groupes (coalitions d'agents)
├── Environnement (saison, météo, événements)
├── Canal communication (file messages)
└── Historique événements (history)
```

### 5.2 Persistance en BD

Contrairement à V1 (JSON), V2 utilise **SQLite** :

**Avantages** :

- Requêtes rapides (SELECT agents NEAR position)
- Transactions ACID (consistency garantie)
- Évolutivité (1000 agents + historique)
- Logging structuré (analytics)
- Migration schéma facile (V2.0 → V2.1)

**Schéma V2.0** :

```sql
agents (id, name, x, y, health, energy, hunger, thirst, ...)
agent_beliefs (id, agentId, fact_type, position_x, position_y, 
               timestamp, confidence, expiry_tick)
agent_memories (id, agentId, observation_type, data, timestamp, decay_rate)
agent_relationships (id, agentId, targetAgentId, trustLevel, 
                     lastInteraction, relationshipType)
groups (id, name, leader_id, objective, created_tick)
group_memberships (id, groupId, agentId, role, joined_tick)
resources (id, type, x, y, quantity, capacity, production_rate)
obstacles (id, x, y, width, height, type)
events_log (id, tick, type, agentId, data)
```

### 5.3 Save/Load garantie exacte

Comme V1, une reprise doit être **bit-perfect** :

```
Tick 1000 : Save state → SQLite
            [100 ticks offline]
Tick 1000 : Load state
Tick 1001 : Execute
            → Résultats identiques à exécution sans sauvegarde
```

Cela valide le **déterminisme complet** (PRNG `xoshiro256**` seed stable).

---

## 6. Boucle décisionnelle V2

À chaque tick pour chaque agent :

```
1. Perceive()         — détecter entités rayon local
2. UpdateMemory()     — intégrer perceptions, appliquer décroissance
3. UpdateBeliefs()    — réviser croyances vs nouvelles observations
4. UpdateInternalState()  — mettre à jour santé/énergie/traits
5. UpdateNeeds()      — calculer niveaux besoins
6. GenerateGoals()    — créer objectifs pertinents
7. SelectAction()     — évaluer utilité candidats → choisir intention
8. ExecuteAction()    — appliquer action, produire effets
9. EmitEvents()       — générer événements pour monde + analyzer
10. CommunicationProcessing() — recevoir/traiter messages
```

Toutes les étapes **ne tournent pas à même fréquence** :
- Mouvement : haute fréquence
- Perception : moyenne fréquence
- Décision : adaptative (si contexte change)
- Analyse sociale : basse fréquence

---

## 7. Émergence en V2

### 7.1 Critères

Un phénomène est **émergent** si :

1. **Non scripté** : aucune règle globale ne l'impose
2. **Résulte d'interactions locales** : agents autonomes + communication + ressources
3. **Reproductible mais variable** : seed + config → même type phénomène, détails différents
4. **Mesurable** : Analyzer peut le quantifier
5. **Chaîne causale longue** : action A → modification monde → besoin B ↑ → décision C ↓ → ...

### 7.2 Exemples V2

**Migrations de population** :
```
- Ressource locale épuisée
- Agents cherchent alternatives (beliefs divergent)
- Groupe se forme (objectif : trouver nourriture)
- Migration vers nouvelle zone riche
- Établissement de nouveau centre économique
→ Tout cela sans règle "créer village"
```

**Hiérarchie sociale** :
```
- Agent A efficace à la chasse
- Autres agents l'observent
- B lui demande aide (communication)
- Relation confiance ↑
- Groupe coopération se forme
- A devient informellement "leader"
→ Tout cela sans rôle assigné
```

**Commerce** :
```
- Agent A a surplus alimentaire
- Agent B en manque
- Communication → offre
- Échange de ressources
- Relation récurrente
- Réseau commercial émerge
→ Tout cela sans économie programmée
```

---

## 8. Défis V2

### 8.1 Scaling

- V1 : ~50 agents, pas d'optimisation
- V2 : 500–1000 agents, **nécessite niveau de détail (LOD) + partitionnement spatial**

Stratégies :
- Grille spatiale pour perception O(1)
- LOD : agents loin = décisions moins fréquentes
- Traitement par lots par cellule

### 8.2 Observabilité et debug

Avec croyances + mémoire + communication, **tracer l'émergence est complexe** :

- Pourquoi agent A a choisi action X ?
  - Besoin dominant → Objectif → Utilité élevée → Intention → Action
  - Mais aussi : mémoire ancienne, croyance erronée, message reçu
- Analyzer doit exposer **chaîne causale complète**

### 8.3 Coordination sans scripting

Comment faire coopérer 1000 agents sans règles globales ?

- Communication + relations
- Besoins communs + groupes
- Ressources limitées = compétition naturelle
- **Pas d'"orchestrateur"** qui dirige

---

## 9. Étapes suivantes

1. **Phase 0** : Finaliser architecture, spécifications ADRs
2. **Phase 1** : Implémenter BDI + Perception (fondation)
3. **Phase 2–8** : Étendre progressivement (mémoire → communication → groupes → dynamique)
4. **Phase 9** : Scaling (LOD, spatial grid, benchmarks)
5. **Phase 10** : Tests + couverture 80%+
6. **Phase 11** : Analyzer repensé
7. **Phase 12** : CI/CD, persistance BD, déploiement

---

## 10. Synthèse

La **V2 transforme la simulation V1** en une **plateforme de jeu vidéo 3D persistant** où :

- Les agents sont **cognitifs** (BDI)
- Le monde est **persistant** (BD, save/load)
- L'information est **partielle** (chaque agent sa vision)
- L'**émergence est la mécanique** (groupes, commerce, hiérarchies naissent naturellement)
- Le système **scale** à 1000 agents
- Tout est **observable et analysable** (Analyzer repensé)

C'est un **saut conceptuel majeur** : de "simulation d'étude" à "jeu vidéo systémique vivant".

