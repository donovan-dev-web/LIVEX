# Annexe I. Paramètres et bases de données

Cette annexe rassemble deux matériaux que le récit n'interrompt pas : les **paramètres** qui règlent le monde, et les **schémas de bases de données** où le monde et son analyse sont conservés. Elle sert de référence rapide : un lecteur qui veut vérifier une valeur citée dans le corps du livre la trouvera ici, avec sa valeur par défaut.

**Avertissement de statut.** La quasi-totalité des valeurs ci-dessous sont des valeurs par défaut, et beaucoup sont marquées comme héritées et non calibrées. Elles décrivent le monde tel qu'il a été réglé, non un monde optimal. Toute modification d'un de ces paramètres change le comportement observé, ce qui est précisément la raison pour laquelle ils sont publiés.

## 1. Paramètres du monde et du temps

| Paramètre | Valeur par défaut | Rôle |
| :-- | --: | :-- |
| `simulation.ticksPerSecond` | 10 (défaut) ; 6 (profil PRISM) | Cadence du moteur |
| `simulation.simulatedSecondsPerTick` | 60 (défaut, soit un tick = une minute) ; 5 (profil PRISM) | Temps simulé par tick |
| `worldWidth` / `worldHeight` | 500 (défaut) ; 2 240 (profil PRISM) | Dimensions du monde |
| `worldCellSize` | 10 (défaut) ; 32 (profil PRISM) | Taille de cellule de la grille spatiale |
| `world.metersPerUnit` | 1,0 | Conversion des unités logiques |

Deux profils coexistent : le scénario de référence (cadence 10) et le profil destiné au rendu (cadence 6, monde de 2 240 sur 2 240, soit environ 5 km² après conversion). Le facteur 100 de conversion vers Unreal est fixé par l'ADR-003 de PRISM.

## 2. Paramètres des ressources

| Ressource | Quantité initiale | Régénération par tick |
| :-- | --: | --: |
| Nourriture | 20 000 | 20 |
| Eau | 20 000 | 10 |
| Bois | 50 | 0,1 |
| Minéraux | 0 | 0 |

Les quatre types sont des réserves globales du monde, régénérées en fin de tick. La nourriture et l'eau portent le métabolisme du scénario de référence ; le bois et les minéraux existent avec des valeurs initiales faibles ou nulles et ne sont pas encore consommés par la cognition. Du point de vue de l'analyse, une réserve est dite critique sous 20 % de sa capacité, et son retour au-dessus de 80 % borne un épisode de récupération.

## 3. Paramètres de l'agent

### 3.1 Traits

Les huit traits ont pour valeur par défaut 1,0 (le neutre). Leur plage est de 0 à 2. La clé `aggressiveness` de la configuration correspond au champ `aggression` du moteur.

### 3.2 Besoins

| Paramètre | Défaut | Effet |
| :-- | --: | :-- |
| `hungerRate` | 0,5 | Faim gagnée par tick |
| `thirstRate` | 0,7 | Soif gagnée par tick |
| `fatigueRate` | 0,3 | Fatigue gagnée par tick |
| `safetyDriftRate` | 0,001 | Insécurité gagnée par tick |
| `socialDriftRate` | 0,0002 | Besoin social gagné par tick |
| `curiosityDriftRate` | 0,0005 | Ennui gagné par tick |
| `hungerTriggerThreshold` | 50 | Seuil de déclenchement de la faim |
| `thirstTriggerThreshold` | 50 | Seuil de déclenchement de la soif |
| `fatigueTriggerThreshold` | 70 | Seuil de déclenchement du repos |
| Seuil de sécurité | 0,5 | Déclenche la fuite **sous** ce niveau |
| Seuil social | 0,7 | Déclenche la socialisation au-dessus |
| Seuil de curiosité | 0,3 | Déclenche l'exploration au-dessus |

Deux seuils critiques commandent l'interruption : faim supérieure à 85, énergie inférieure à 10.

### 3.3 Perception, mémoire, croyances, confiance

| Paramètre | Défaut |
| :-- | --: |
| Rayon de perception | 50 |
| Atténuation de confiance avec la distance | 0,3 |
| Confiance plancher | 0,7 |
| Rotation de perception étagée | 4 ticks |
| Capacité mémoire | 1 000 souvenirs |
| Seuil de rappel (oubli) | 0,01 |
| Décroissance mémoire : observation / événement / interaction | 0,01 / 0,005 / 0,002 |
| Force de révision des croyances | 0,3 |
| Plafond de variation par pas | 0,5 |
| Bonus d'alignement | 0,2 |
| Pénalité de conflit | 0,1 |
| Érosion des croyances par tick | 0,999 |
| Péremption d'une croyance | 100 ticks |
| Plafond après péremption | 0,4 |
| Confiance initiale | 0,5 |
| Décroissance de confiance par tick sans interaction | 0,9 |
| Bonus de vérité constatée | 0,05 |
| Pénalité de tromperie constatée | 0,2 |

### 3.4 Actions et délibération

| Paramètre | Défaut |
| :-- | --: |
| Coût énergétique d'un déplacement | 0,03 |
| Énergie regagnée par le repos | 1,5 |
| Fatigue récupérée par le repos | 2,0 |
| Intervalle de délibération | 10 ticks |
| Bonus d'alignement avec l'objectif courant | 1,2 |
| Bonus maximal d'alignement collectif | 1,2 |
| Marge anti-oscillation (hystérésis) | 0,05 |
| Marge de conflit de priorités | 0,5 |
| Marge d'excès d'utilité pour interruption | 10 |

### 3.5 Groupes, communication, héritage, vie

| Paramètre | Défaut |
| :-- | --: |
| Intervalle de révision des groupes | 10 ticks |
| Seuil de confiance d'un lien social | 0,3 |
| Taille minimale d'un groupe | 3 |
| Bonus d'affinité par croyance partagée | 0,10 |
| Bonus d'affinité par but partagé | 0,20 |
| Quorum pondéré d'une décision collective | 0,5 |
| Portée de pulsation | 55 unités |
| Décroissance de confiance par saut | 0,9 |
| Sauts maximaux | 2 |
| Taux d'incompréhension | 0,05 |
| Coût d'émission | 0 (neutre) ; forme `sendEnergyCost` + longueur × `sendEnergyPayloadFactor` |
| Coût de réception | 0 (neutre) ; forme `receiveEnergyCost` + longueur × `receiveEnergyPayloadFactor` |
| Dominance d'héritage | 0,0 |
| Taux de mutation | 0,0 |
| Amplitude de mutation | 0,1 |
| Seuil de saillance d'un souvenir transmis | 0,01 |

## 4. Base de données SYNE (SQLite, schéma relationnel v2)

Le moteur persiste son état de deux façons complémentaires : un **snapshot bit-à-bit** au format JSON (dont la colonne capitale contient l'état complet du monde et de la cognition, et l'état du générateur pseudo-aléatoire), et un **schéma relationnel** de onze tables destiné aux requêtes.

| Table | Contenu |
| :-- | :-- |
| `runs` | Identité du run : nom, graine, configuration, nombre de ticks |
| `tick_states` | État à un tick, dont l'état du PRNG (4 × 64 bits) |
| `agents` | Entités : espèce, naissance, mort, traits initiaux |
| `agent_snapshots` | État par tick : position, énergie, faim, soif, fatigue, action courante, croyances, objectifs, relations, mémoire |
| `resources` | Ressources : type, position, quantité, capacité |
| `resource_snapshots` | Quantité par tick |
| `groups` | Groupes : formation, dissolution, leader, cohésion moyenne |
| `group_memberships` | Appartenances : rôle, entrée, sortie |
| `events` | Événements : type, entité, cible, détails |
| `messages` | Communications : type, émetteur, récepteur, contenu, confiance, sauts |
| `metrics` | Agrégats par tick : population, moyennes, compteurs |

La colonne qui porte la garantie de reprise exacte est `tick_states.state` : elle contient le snapshot déterministe complet. Les états dérivés (grille spatiale, cache de cheminement, tampons du tick) ne sont pas sauvegardés : ils sont reconstruits de façon déterministe au chargement, ce qui évite de persister ce qui n'est qu'un intermédiaire de calcul.

## 5. Base de données ECHOS (SQLite, schéma v6)

L'observatoire stocke ses analyses dans huit tables.

| Table | Colonnes principales |
| :-- | :-- |
| `_meta` | Clé et valeur (dont la version du schéma) |
| `runs` | Identifiant du run, version, graine |
| `tick_summaries` | Tick, temps simulé, population vivante, moyennes d'énergie, faim, soif, fatigue, nombre de décisions, nourriture et eau moyennes |
| `events_log` | Tick, type, agent, cible, action, cause, valeur |
| `tick_metrics` | Moteur, métrique, valeur, et le drapeau **`measured`** |
| `tick_contexts` | Contexte détaillé par tick et par type (agents, groupes) |
| `decision_traces` | Trace de décision : action choisie, utilité, flags de délibération et d'interruption, compteurs, besoins |
| `calibration_reports` | Rapport de calibration au format JSON |

Deux détails de schéma méritent l'attention. La table `tick_metrics` porte une colonne **`measured`** : la distinction entre « zéro observé » et « non mesuré » est donc inscrite dans le stockage lui-même, et non ajoutée par convention d'affichage. La table `decision_traces` conserve l'utilité retenue, `held_utility`, `decision_state` et `decision_tick`, ce qui permet de distinguer absence et score reporté.

**Note de version du relevé courant (18 octobre 2026).** Le code écrit `SCHEMA_VERSION = "6"` dans `_meta`. La migration BL-09 reconstruit `decision_traces` pour rendre `utility` nullable et ses commentaires/tests la nomment v7, mais la constante n'a pas été incrémentée. La valeur documentée ici (v6) décrit donc le compteur réellement publié; la discordance de numérotation reste à harmoniser dans le code et les migrations. Voir le [statut LIVEX](../../governance/LIVEX-STATUS.md).

Cinq index couvrent les requêtes fréquentes : résumés par run, événements par run et tick, métriques par run et tick, contextes par run et tick, traces de décision par run et tick.

## 6. Ce que cette annexe ne donne pas

Cette annexe recense les valeurs et les schémas, mais elle ne dit pas **pourquoi** telle valeur a été retenue. Cette justification appartient aux chapitres techniques (SYNE VI pour la configuration, ECHOS VI pour les constantes analytiques) et aux décisions d'architecture (Annexe F). Elle ne dit pas non plus quelles valeurs sont **bonnes** : la plupart sont héritées, certaines ont été calibrées, et aucune n'a fait l'objet d'une analyse de sensibilité systématique. Un paramètre publié est un paramètre vérifiable et modifiable, pas un paramètre validé.
