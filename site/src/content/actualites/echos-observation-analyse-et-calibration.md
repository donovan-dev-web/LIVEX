---
title: "ECHOS : observation, analyse et calibration rigoureuses"
description: "ECHOS consomme le flux SYNE sans intrusion, détecte les trous de ticks, calcule des métriques traçables avec provenance, et produit des rapports de calibration versionnés."
date: 2026-10-05
tags: ["echos", "analyse", "metriques", "calibration", "observabilite"]
draft: false
---

**ECHOS** (*Emergent Cognition & Holistic Observation System*) est l'observateur et l'analyste de LIVEX. Il transforme l'observabilité brute de SYNE en **faits traçables, comparables et reproductibles**.

## Rôle et principes

- **Observation non-intrusive** : ECHOS ne modifie jamais l'état décisionnel de SYNE. Il consomme passivement snapshots et événements.
- **Autorité séparée** : SYNE détient la vérité du monde ; ECHOS détient la vérité des **observations et analyses**.
- **Traçabilité** : toute métrique est soit **mesurée**, soit accompagnée d'un repli neutre justifié (`provenance.measured`).
- **Honnêteté sur les limites** : discontinuités (trous de ticks), indisponibilités, valeurs manquantes sont **consignées**, jamais masquées.
- **Comparaisons rigoureuses** : distinction claire entre résumé allégé et comparaison fondée sur l'empreinte bit-à-bit.

## Ingestion et alignement

ECHOS ingère le flux WebSocket de SYNE en respectant strictement l'ordre :

1. **Snapshot** (début de tick) — état complet à ce tick.
2. **Événements** du tick — conséquences appliquées.
3. **tick_summary** — agrégats consolidés.

Cet alignement est vérifié. À tout changement de `run_id`, ECHOS enregistre le nouveau run **avant** son premier tick, réinitialise ses fenêtres glissantes, flush sa série Parquet et prépare un rapport propre. Il **détecte les trous de ticks** (écart entre dernier tick vu +1 et tick reçu), les journalise structurellement et incrémente `gaps_detected`. Le rattrapage SYNE est signalé comme hors périmètre V0.1, jamais approximé.

## Persistance et schéma

ECHOS persiste dans un **analytics DB SQLite** versionné :

- **tick_summaries** : agrégats globaux par tick (population, moyennes, ressources...).
- **tick_metrics** : métriques normalisées avec colonnes `measured` (provenance) et valeurs bornées.
- **contexts** : contexte `agents`/`groups`/`phenomena`/`profiling` avec cadence contrôlée (`context_every`, défaut 20) — plus le **dernier tick** systématiquement conservé.
- **events** : journal des événements observés (déterministe dans sa forme).
- **runs** : métadonnées (`run_id`, `seed`, `engineVersion`, `outcome`, `extinctionTick`, `viability`, ...).

Migration additive (schéma v6) pour préserver la compatibilité ascendante.

## Métriques traçables et provenance

L'ajout du champ `measured` (booléen) par couple (moteur, métrique) distingue :

- **`measured = true`** : valeur réellement calculée depuis le flux pour cette fenêtre.
- **`measured = false`** : repli neutre appliqué faute de fenêtre exploitable (jamais présenté comme mesuré).

Cette distinction est exposée via `/api/runs/{id}/metrics` et `/api/runs/{id}`. L'objectif : **aucune métrique à zéro systématique** ne peut passer inaperçue.

## Rapports de calibration versionnés

À la fin d'un run (ou sur demande), ECHOS produit un rapport de calibration `schemaVersion 2` structuré :

- **En-tête** : `run_id`, `seed`, `engineVersion`, horodatage, provenance.
- **Résultat populationnel** : `outcome` (`extinct` | `surviving`), `extinctionTick` (premier tick où `alive_count == 0`).
- **Bloc `viability`** : `energySlopePerTick` sur les 200 derniers ticks ; `actionSharesWhenHungry` quand `mean_hunger > 70` ; `resourceRegime` (food/water) sur fenêtre pertinente.
- **Dérivés** : `population_outcome` calculé aussi en SQL pour cohérence entre API et persistance.

Ce rapport est **observe-only** : il décrit, il n'altère pas SYNE.

## API REST (points clés)

L'API ECHOS est contractuelle (`API_REST.md`) :

- `GET /api/runs` — liste avec `outcome`, `extinction_tick`, métadonnées.
- `GET /api/runs/{id}` — détail complet + provenance.
- `GET /api/runs/{id}/metrics` — métriques avec `measured`.
- `POST /api/compare` — comparaison entre deux runs. Par défaut résumé ; `light=1` renvoie uniquement résumé (sans séries/empreinte). La comparaison **complète** seule peut statuer sur « bit-identique » / « reproductible ».
- `POST /analysis/run|experiment|report` — orchestration analyse/rapport (utilisée par Launcher).
- `GET /health` — santé du service.

## Interface `echos-ui`

L'interface web consomme cette API de manière rigoureuse :

- **Sélection explicite** du `run_id` (plus de « run le plus récent » implicite pour les vues contextuelles).
- **Export CSV propre** (données tabulaires, pas d'enveloppe JSON illisible).
- **Graphiques cohérents** avec cadence respectée (clé de cache incluant cadence).
- **Vues ciblées** : beliefs/relationships/phenomena requièrent explicitement `run_id`.

## Intégration avec Launcher

Le Launcher délègue l'analyse à ECHOS via HTTP avec **délais adaptés** : appels standards courts, `ingest/run`, `analysis/run`, `analysis/experiment`, `analysis/report` jusqu'à **30 minutes** (compte tenu d'ingestions lourdes). La version d'ECHOS ayant produit le rapport est consignée dans le manifeste du paquet `.livexp` (`AnalysisComponentVersion`), garantissant traçabilité.

## Position

ECHOS est **l'observateur honnête**. Il ne cherche pas à lisser les discontinuités ni à combler les trous. Il les signale. Il ne prétend pas mesurer ce qu'il ne voit pas : il l'indique explicitement via `measured`. Cette discipline est ce qui permet à LIVEX d'affirmer des phénomènes émergents sur des **bases factuelles et traçables**, plutôt que sur des impressions.
