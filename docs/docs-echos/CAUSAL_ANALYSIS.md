# CAUSAL_ANALYSIS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `LOGGING_INSTRUMENTATION.md`, `../docs-syne/PERSISTENCE.md`, `adr/ADR-002-mode-calcul-causal.md`
**Source Monographie** : §4.5

---

## 1. Corrélation vs causalité

Une simple corrélation n'est pas suffisante pour démontrer une émergence. ECHOS doit permettre de reconstruire autant que possible les **chaînes causales** :

```text
Événement initial → Perceptions → Décisions → Actions → Conséquences → Événements secondaires
```

**Objectif** : répondre à une question telle que « Pourquoi ce groupe est-il apparu ? » en remontant aux interactions et contraintes qui ont précédé sa formation, plutôt qu'en constatant seulement qu'il existe.

## 2. Les outils causaux

| Outil | Description | Source |
| :-- | :-- | :-- |
| **Traces de décision** | Chaque `DecisionRecord` contient le contexte complet (besoins, croyances, scores d'utilité) | `decision_traces` (SQLite) |
| **Journal d'événements** | Chaque événement est horodaté et attribué | `events_log` (SQLite) |
| **Reconstruction par tick** | La boucle de simulation peut être rejouée pas-à-pas | `../docs-syne/PERSISTENCE.md` (reprise bit-à-bit) |

## 3. Les limites de l'analyse causale

- L'émergence d'un phénomène social implique généralement **de nombreuses causes concourantes**.
- Les **boucles de rétroaction** rendent l'attribution causale difficile : *qui a causé quoi, quand ?*
- **Biais de conception** : ECHOS observe un système que le concepteur a défini ; les catégories de mesure dépendent de ce que le développeur a choisi d'instrumenter.

## 4. Le mode de calcul causal (ADR-002 ECHOS)

Le calcul causal est une **reconstruction déterministe à partir des traces persistées** (pas de calcul en ligne sur le flux temps réel) :
- les traces (`decision_traces`) et événements (`events_log`) sont la source canonique ;
- la reconstruction s'appuie sur l'ID de chaîne (`Action ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception`) ;
- les résultats sont mis en cache et invalidés si un run est réanalysé après modification.

Cette décision est formalisée dans `adr/ADR-002-mode-calcul-causal.md` **[Accepted]**.

## 5. Reconstruction implémentée (ECHOS-061 → ECHOS-063, jalon ph6)

`echos/echos/analysis/causal.py` expose `build_chain(store, run_id, agent_id, tick=None, depth=7, max_depth=12)` :

- **Sources par couche** (hors ligne, ADR-002) :
  - `Action`/`Besoin`/`Mémoire` — ligne `decision_traces` du tick (action choisie, besoins, compteur mémoire) ;
  - `Intention` — événement `decision_made` correspondant (`value.intention`, repli action) ;
  - `Objectif`/`Croyance` — contexte `agents` du tick ≤ tick le plus récent (buts `goals[].kind`, sujets de croyances triés) ;
  - `Perception` — derniers `message_received` de l'entité ≤ tick (jusqu'à 3, tri (tick, id)).
- **Une couche = un nœud** : les multiples (croyances, perceptions) sont agrégés dans `detail` ; couche sans donnée → libellé `—` (chaîne « tronquée » signalée, ADR-002 — négatives).
- **Déterminisme** (ECHOS-041) : ordres stables (besoins par (valeur, clé), croyances/sujets triés, perceptions (tick, id)), aucun tirage, aucune écriture.
- **Boucles de rétroaction (ECHOS-062, §4.5.3)** : une action déjà choisie aux ticks précédents du run ⇒ `cycle=true` + récurrence (`cycles[].ticks`) ; duplicat intra-chaîne ⇒ arrêt au seuil du retour. La **profondeur d'affichage est bornée** (`depth ≤ max_depth = 12`, défaut 7) ; troncature signalée (`truncated`).
- **Accès** : `GET /api/runs/{run_id}/causal-chains/{agent_id}?tick=&depth=` (`API_REST.md` §3.8) ; `tick` optionnel (dernière décision de l'entité).
- **Cache (ECHOS-063)** : `CausalCache` LRU borné (256), invalidé sur `AnalyticsStore.ingest_version` — un re-run du même run force la re-analyse (résultat reproductible).

**Exemple de sortie** (extrait) :

```json
{"run_id": "run-7", "agent_id": "A", "tick": 3, "depth_requested": 7, "depth_served": 7,
 "chain": [
   {"layer": "Action", "tick": 3, "label": "SeekFood",
    "detail": {"utility": 0.75, "deliberated": true, "interrupted": false,
               "cause": "hunger=80,thirst=20,fatigue=5"}},
   {"layer": "Intention", "tick": 3, "label": "SeekFood", "detail": {}},
   {"layer": "Objectif", "tick": 3, "label": "SeekFood",
    "detail": {"kinds": ["SeekFood", "Eat"], "goals_count": 2}},
   {"layer": "Besoin", "tick": 3, "label": "hunger (80.0)",
    "detail": {"needs": {"hunger": 80.0, "thirst": 20.0, "fatigue": 5.0}}},
   {"layer": "Croyance", "tick": 3, "label": "2 croyance(s)",
    "detail": {"subjects": ["food-1", "water-2"], "beliefs_count": 2}},
   {"layer": "Mémoire", "tick": 3, "label": "4 souvenir(s)", "detail": {"memory_count": 4}},
   {"layer": "Perception", "tick": 3, "label": "Information", "detail": {"received": [...]}}],
 "cycle": true,
 "cycles": [{"layer": "Action", "label": "SeekFood", "ticks": [1, 2]}],
 "truncated": false}
```

---

## Points restés ouverts dans ce document
- Aucun — les limites et la méthode sont établies. La profondeur maximale de chaîne causale affichable sera calibrée à l'implémentation (outil de reconstruction).