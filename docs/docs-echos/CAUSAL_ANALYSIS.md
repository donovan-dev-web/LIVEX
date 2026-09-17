# CAUSAL_ANALYSIS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `LOGGING_INSTRUMENTATION.md`, `../docs-syne/PERSISTENCE.md`
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

## 4. Le mode de calcul causal (ADR ECHOS-001)

Le calcul causal est une **reconstruction déterministe à partir des traces persistées** (pas de calcul en ligne sur le flux temps réel) :
- les traces (`decision_traces`) et événements (`events_log`) sont la source canonique ;
- la reconstruction s'appuie sur l'ID de chaîne (`Action ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception`) ;
- les résultats sont mis en cache et invalidés si un run est réanalysé après modification.

Cette décision est formalisée dans `adr/ADR-001-analyse-causale.md`.

---

## Points restés ouverts dans ce document
- Aucun — les limites et la méthode sont établies. La profondeur maximale de chaîne causale affichable sera calibrée à l'implémentation (outil de reconstruction).