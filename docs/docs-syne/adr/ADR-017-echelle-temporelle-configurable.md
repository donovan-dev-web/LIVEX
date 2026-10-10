# ADR-017 : Échelle temporelle configurable

**Composant** : SYNE
**Statut** : [Acceptée]
**Dernière mise à jour** : 10 octobre 2026
**Dépend de** : ADR-005 (temporalité), ADR-016 (calibration B1), ADR-003 PRISM (échelle/cadence)
**Source** : spec « Profil gameplay PRISM — temps, échelle, vitesse » §2-§4

---

## Contexte

`1 tick = 1 minute simulée` était une **constante statique**
(`SimulationTime.TicksPerSimulationMinute = 1`). Besoins, saisons et
décroissances sont exprimés **par tick** : le TPS (`simulation.ticksPerSecond`)
ne peut pas ralentir le jeu suffisamment (à TPS = 1, une journée reste à
24 minutes réelles), et à 6 TPS le profil PRISM ferait tourner l'horloge à
×360 — faim en ~17 s réelles. Il faut un **second levier : la durée simulée
d'un tick**.

## Décision

**Nouvelle clé `simulation.simulatedSecondsPerTick`** (entier, défaut **60**,
plage `[1, 3600]`) : 1 tick vaut `simulatedSecondsPerTick` secondes simulées.
Au défaut, 1 tick = 1 minute simulée — **identique à ADR-005, neutre par
défaut, checksums dorés inchangés**.

- **Ratio** : `R = ticksPerSecond × simulatedSecondsPerTick` (1 s réelle =
  R s simulées). Profil `prism` : 6 TPS × 5 s = **R = 30**.
- **Horloge instanciée** : `SimulationClock` (Configuration/SimulationClock.cs)
  remplace la constante statique, exposée par `SimulationLoop.Clock` ;
  `ToSimulatedSeconds(tick) = tick × simulatedSecondsPerTick` (long 64 bits),
  `ToSimulatedMinutes` = division entière (plancher — contrat ECHOS INTEGER).
- **Règle de conversion en trois classes** (spécification §3, inventaire
  `TICK_QUANTITIES.md`) :
  - **A — processus continus du temps simulé** : × `dt` (= `simulatedSecondsPerTick / 60`),
    décroissances multiplicatives `facteur ^ dt`, périodes `période / dt`.
    Les valeurs de config gardent leur sens « par minute simulée ». Appliqué
    une fois au démarrage, de façon déterministe, par
    `Configuration/TemporalScale.cs` (besoins, énergie de mouvement/repos,
    régénérations/dégradation, mémoire, croyances, confiance, saisons).
  - **B — cadences mécaniques** : inchangées, en ticks (délibération,
    perception, groupes, communication, sauvegarde, A*, portées).
  - **C — effets par action** : inchangés (`energyRecovery` Eat/Drink, bonus
    de confiance, seuils).
  - Le **pas de déplacement n'est pas multiplié par `dt`** : la vitesse
    apparente reste `speed × k × ticksPerSecond` (parité joueur UE, ADR-003
    PRISM). Décision D3 : sans `moveEnergyCost × dt`, un cycle de besoin
    paierait 12× plus d'énergie et l'ADR-016 perdrait sa stabilité.
- **Clé informative `world.metersPerUnit`** (double > 0, défaut 1,0) : jamais
  utilisée dans un calcul de moteur, transmise aux clients (PRISM : k = 100 uu
  / unité, 1 unité = 1 m).
- **Profil `prism`** : `SimulationProfiles.Prism()` +
  `configs/simulation/prism.json` (profil **complet**) — monde 2 240 × 2 240,
  cellule 32, 6 TPS, 5 s/tick.
- **Contrats additifs (MINOR, VERSIONING.md §3)** : contrat d'observabilité
  0.3.0 → **0.4.0** ; description `world_initialized` 1.0 → **1.1**
  (`simulatedSecondsPerTick`, `metersPerUnit`) ; snapshot +
  `simulatedTimeSeconds` (minutes entières conservées) ;
  `/api/control/status` + `simulatedSecondsPerTick`.

## Conséquences

### Positives
- Un facteur 12 sur tout ce qui est « vie » (besoins, saisons) **sans toucher
  à la vitesse visuelle** : faim à 3 min 20 au lieu de 17 s à R = 30.
- Un seul levier (`simulatedSecondsPerTick`) pour régler le tempo après
  playtest (12 / 30 / 60 / 120) — ni TPS, ni `k`, ni vitesses.
- Neutre par défaut : `dt == 1` court-circuite chaque mise à l'échelle
  (retour de l'instance d'entrée) — trajectoire bit-à-bit préservée.

### Négatives
- Deux grandeurs temporelles à distinguer partout (cadence réelle vs durée
  simulée du tick) — le ratio R doit être affiché par les UIs (ADR-005).
- Les constantes codées en dur exprimées en ticks (`ActiveGoals` 100,
  cooldown aide 20) restent en classe B — voir TICK_QUANTITIES.md §3.

### Risques
- Double-scaling si une même instance de config alimentait deux runs :
  neutralisé — `TemporalScale` ne mute jamais le profil d'origine (copie de
  premier niveau portée par la boucle/pipeline).
- La restauration bit-à-bit doit reconstruire les esprits avec les réglages
  effectifs (`CognitionPipeline.EffectiveOptions`) — couvert par les tests.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 10 octobre 2026 | Création | Spec profil gameplay PRISM §2-§4 ; inventaire A/B/C `TICK_QUANTITIES.md` |
