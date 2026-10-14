---
title: "SYNE : un moteur décisionnel déterministe pour l'émergence"
description: "SYNE est le cœur de LIVEX : boucle à ticks, agents autonomes, formule d'utilité, persistance SQLite, serveur de contrôle HTTP et déterminisme strict."
date: 2026-10-04
tags: ["syne", "moteur", "agents", "determinisme", "persistance"]
draft: false
---

**SYNE** (*Systems & Emergent Network Engine*) est le moteur de simulation multi-agents de LIVEX. Il détient l'**unique vérité** de l'état du monde et exécute la logique décisionnelle indépendamment du rendu.

## Principes directeurs

- **Déterminisme** : à même graine et configuration cohérente, l'exécution produit un déroulement reproductible.
- **Autonomie locale** : les agents prennent leurs décisions à partir de leur perception partielle, mémoire, croyances, besoins et capacités d'action.
- **Émergence traçable** : les structures collectives ne sont pas programmées ; elles résultent d'interactions locales dont on peut retracer les causes.
- **Non-intrusif pour l'observation** : SYNE diffuse son état sans attendre de retour modifiant ses décisions.
- **Reprise exacte** : persistance bit-à-bit permettant de restaurer l'état à un tick donné.

## Architecture conceptuelle

SYNE opère en **temps discret** (ticks). À chaque tick, la boucle applique un ordre causal strict :

1. **Perception** : chaque agent perçoit son environnement partiel (portée, visibilité, mémoire).
2. **Évaluation** : pondération des besoins, fatigue, énergie, traits/tempéraments, croyances et contexte social via une **formule d'utilité**.
3. **Sélection d'action** : choix de l'action la plus apte compte tenu des contraintes locales.
4. **Application** : exécution des effets (déplacement, interaction, consommation, communication...).
5. **Mises à jour** : besoins, énergie, fatigue, mémoire, croyances, réserves, événements.
6. **Fin de tick** : diffusion snapshot + événements vers les consommateurs.

Cet ordre est invariant. Aucun tirage ne dépend de l'horloge système pour trancher une décision.

## Agents autonomes

Un agent SYNE est caractérisé par :

- **Besoins** (faim, soif, sécurité, social...) avec seuils et dérives.
- **Énergie & fatigue** : coûts/recouvrements liés aux actions, repos.
- **Mémoire & croyances** : traces d'interactions passées, mises à jour graduelles.
- **Perception partielle** : champ de vision borné, information incomplète.
- **Traits/tempéraments** : biais stables influençant l'évaluation utilitaire.
- **Capacités d'action** : ensemble borné d'actions possibles selon contexte.
- **Communication locale** : échanges bornés (envois/réceptions par tick) pour éviter les effets globaux non maîtrisés.

L'objectif est de favoriser des compromis locaux plausibles plutôt que des optimisations globales omniscientes.

## Monde, ressources et environnement

Le monde est une grille à **cellules discrètes**. Il gère :

- **Ressources** (nourriture, eau, minéraux...) avec régénération/dégradation périodique selon configuration.
- **Obstacles statiques** : constructions/obstacles tracés, avec révision du monde et rafraîchissement du cache A* quand l'environnement change.
- **Espace et voisinage** : rayon d'interaction borné, perception locale.
- **Échelle temporelle configurable** : `simulatedSecondsPerTick` (défaut 60, 1 tick = 1 minute simulée). `metersPerUnit` est informatif. L'horloge `SimulationClock` est instanciée au démarrage ; les conversions s'appliquent une fois. Les effets par action restent inchangés.

## Persistance, contrôle et observabilité

- **Persistance SQLite** : schéma versionné (user_version), 11 tables typiques, transactions atomiques, **autosave** périodique, **rotation** bornée des backups, reprise exacte au dernier `tick_states`.
- **Serveur de contrôle HTTP** (`--serve`, port 5181) : `POST /api/control/start|pause|resume|stop|reset`, `GET /api/control/status`. `reset` accepte une surcouche `config` (même logique que `start/prepare`), permettant d'enchaîner des runs cohérents.
- **Flux d'observabilité** : WebSocket diffusant **snapshot puis événements** par tick. Le snapshot inclut `simulatedTimeSeconds`, `seed`, `runId`. Ordre strict garanti.
- **`run_id` canonique** : `run-<seed>-<12hex>` — traçable entre SYNE, ECHOS et artefacts.
- **Détermination par contrats** : `API_CONTRACTS.md`, `TICK_QUANTITIES.md` (inventaire classes A/B/C des quantités par tick), `OBSERVABILITY.md`.

## Profil « reference » et profil « prism »

- **`reference`** : profil calibré pour stabilité 2500 ticks (valeurs B1 explicitées dans `SimulationProfiles.Reference()` et `configs/simulation/reference.json`). Objectif : 0 extinction, |pente| < seuil, population ≈ initiale sur 6 runs (3 seeds × 2 tailles).
- **`prism`** : profil orienté rendu perceptif (monde plus grand/cellules adaptées, `simulatedSecondsPerTick=5`, `metersPerUnit=1`) — strictement additif, sans altérer l'ordre causal.

## Preuves de déterminisme

SYNE maintient des **oracles dorés** (checksums épinglés). Tout changement volontaire de trajectoire justifie un bump `engineVersion` (MINOR). Les campagnes multi-seeds, les tests de reprise bit-à-bit, les golden tests et les parcours bout-en-bout contre ECHOS réel valident continûment ces garanties.

## Position dans l'écosystème

SYNE est **l'autorité**. ECHOS observe sans influer. PRISM visualise sans décider. Cette séparation est ce qui permet à LIVEX de demander : « Qu'émerge-t-il de ces règles seules ? » — et d'y répondre par un **rejeu reproductible**, pas par une interprétation.
