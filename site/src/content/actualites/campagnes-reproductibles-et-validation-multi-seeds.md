---
title: "Campagnes reproductibles et validation multi-seeds"
description: "Campagnes multi-seeds (50/100 agents × 3 graines) montrent stabilité 2500 ticks : 0 extinction, |pente| < seuil, population ≈ initiale."
date: 2026-10-08
tags: ["campagnes", "seeds", "calibration", "reference", "validation"]
draft: false
---

LIVEX privilégie **la preuve par campagne** plutôt que par exemple isolé. Des campagnes multi-graines, rejouables, valident la viabilité du profil de référence sur 2500 ticks avec des critères explicites.

## Objectif : transformer l'intuition en preuve

Pour affirmer qu'un profil est viable dans les conditions définies, il faut montrer :

- **Stabilité sur la durée** : survie à 2500 ticks, pas juste à 300.
- **Robustesse aux graines** : comportement cohérent sur plusieurs `seed` (12345, 424242, 999...).
- **Critères quantifiés** : seuils explicites, pas « ça tient ».
- **Traçabilité** : chaque run rejouable depuis graine + configuration.

## Profil « reference »

Le profil `reference` est calibré (valeurs **B1**) pour assurer une trajectoire stable :

- **Régénération ressources** : `food/water` initiales 20000, régénération 20/10, `degradationTick` = `null` (empêche l'annulation de la régénération par périodes).
- **Coûts énergétiques** : `moveEnergyCost` 0,03, `restEnergyGain` 1,5, `restFatigueRecovery` 2,0.
- **Bénéfices Eat/Drink** : rehaussés (`eat.energyRecovery=2.0`, `drink.energyRecovery=1.0`) avec plafonnement/déplafonnement cohérent via `UtilityEvaluator.BenefitOf`.
- **Dérives sociales/curiosité** : réduites (`socialDriftRate` 0,0002, `curiosityDriftRate` 0,0005).
- **Communication bornée** : `maxSendsPerTick`/`maxReceivesPerTick` = 1, `relayEnabled` false, coûts énergétiques messages à 0.
- **Un seul jeu de valeurs** : défini dans `SimulationProfiles.Reference()` et `configs/simulation/reference.json` (même source). Chemin `entries.*` strict pour éviter ignorées par JSON.

Changement volontaire de trajectoire → bump `engineVersion` MINOR (0.14.0 → 0.15.0 lors de la calibration B1).

## Campagne de validation

**Critères d'acceptation** (explicites) :

- **0 extinction** : `alive_count` > 0 à t=2500 pour tous les runs.
- **0 mort** : pas d'extinction observée.
- **Pente stable** : `|energySlopePerTick| < 0.005` sur fenêtre pertinente.
- **Population préservée** : population finale ≈ population initiale.

**Plan validé** : 6 runs = (50 agents + 100 agents) × 3 seeds (12345, 424242, 999).

**Résultat** : **6/6 validés**. Tous atteignent 2500 ticks sans extinction, population finale = initiale, pente ~0.0037/tick.

## Outils rejouables

Les campagnes sont **scriptables** :

- `scripts/calibration_campaign.py` — validation multi-seeds du profil reference.
- `scripts/reset_campaign.py` — enchaînement cohérent via `POST /api/control/reset` (avec surcouche `config`).
- `scripts/v3_campaign.py` — campagne V3 (cadence, backpressure, lag, reprise worker).

Ces scripts utilisent le **chemin HTTP réel** (`--serve`) avec flux WS segmenté par `runId`. Ils sont conçus pour être rejoués depuis un clone propre.

## Chemin reset vs prepare

Un point clé : `POST /api/control/reset` accepte désormais une surcouche `config` partielle (même logique que `start/prepare`). Cela permet d'**enchaîner** des runs de populations différentes sans relancer SYNE. La validation montre :

- Chemin `prepare` et chemin `reset` (avec même surcouche) produisent **des pentes identiques**.
- Application cohérente du profil de référence même après reset (plus de repli sur defaults non calibrés).

## Traçabilité ECHOS

ECHOS consigne `outcome` (`extinct|surviving`), `extinctionTick` et bloc `viability` (pente énergie sur 200 derniers ticks, `actionSharesWhenHungry`, `resourceRegime`). Ces signaux sont **observe-only** et exposés dans `/api/runs`. Le Launcher archive également `provenance.json` et `AnalysisComponentVersion` dans le paquet `.livexp`.

## Preuves concrètes

- **Oracles dorés** : checksums épinglés (perception `0xf4aaa2733e491935`, état complet `0x954b8e0e70c0af5f`) — revalidés après calibration.
- **Tests bout-en-bout** : parcours J3 réel (SYNE publié + ECHOS réel) vérifient reprise sans rejouer, intégrité octet-à-octet du paquet.
- **Détection honnête** : trous de ticks journalisés (`gaps_detected`), provenance `measured` explicite.
- **Campagne V3** : mesures de cadence/backpressure/lag sous contrainte (gel consommateur) — résultats publiés, jamais masqués.

## Philosophie

« Tenir 2500 ticks » n'est pas un artifice. C'est le fruit d'un **ajustement explicite** des défauts intégrés (B1), d'un **ordre causal strict**, d'une **communication bornée** et d'un **suivi utilitaire** cohérent. L'objectif n'est pas d'optimiser la survie à tout prix, mais d'obtenir un **comportement stable et traçable** dans un monde à règles explicites.

**Si ça tient sous 6 runs multi-seeds, on peut rejouer. Si on peut rejouer, on peut discuter honnêtement du phénomène observé.**
