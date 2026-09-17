# ADR-002 : Mode de calcul causal (hors ligne sur traces persistées)

**Composant** : ECHOS
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `ADR-001-stack-applicative.md`
**Source Monographie** : §4.5 (analyse causale)

---

## Contexte

L'analyse causale doit répondre à « Pourquoi ce groupe est-il apparu ? » en reconstruisant les chaînes `Événement initial → Perceptions → Décisions → Actions → Conséquences`. Le calcul sur flux temps réel serait coûteux et non reprocessable.

## Décision

Le calcul causal est une **reconstruction déterministe hors ligne** à partir des traces persistées :
- **Sources** : `decision_traces` (contexte BDI complet) et `events_log` (SQLite).
- **Chaînage** : remontée le long de `Action ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception`.
- **Cache** : les résultats sont mis en cache et invalidés si un run est réanalysé après modification.
- Le flux temps réel n'alimente que les métriques en ligne (pas les analyses causales profondes).

## Conséquences

### Positives
- Reprocessable et déterministe (réanalyse d'un run = mêmes résultats).
- Indépendant de la charge temps réel.
- Réutilise directement les traces déjà persistées par SYNE.

### Négatives
- Délai entre fin du run et analyse complète.
- Dépend de la complétude des traces (si des traces manquent, la chaîne est tronquée — signalée à l'utilisateur).

### Risques
- Boucles de rétroaction = chaînes se recoupant → limiter la profondeur d'affichage et marquer les cycles (cf. §4.5.3).

## Validation / rejet

- Réouverture si un besoin d'analyse causale en ligne (pilote temps réel) apparaît.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |