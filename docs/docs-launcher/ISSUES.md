# ISSUES.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `ROADMAP.md`, `TESTING.md`, `adr/`
**Source Monographie** : —

---

## 1. Objectif

Ce document consolide les **points restés ouverts** dans la documentation du
Launcher. Chaque point est une question à trancher, avec son origine, son impact et
l'échéance à laquelle il doit l'être.

Un point qui n'apparaît nulle part ailleurs, et qui n'est pas ici, est un point
perdu. La section « Points restés ouverts » de chaque document alimente ce fichier.

## 2. Points ouverts

| # | Question | Origine | Impact | Échéance |
| :-- | :-- | :-- | :-- | :-- |
| O-01 | Politique de redémarrage automatique des composants | `OBSERVABILITY.md` §8 | Haute | G3 |
| O-02 | Procédure d'acceptation d'un composant | `TESTING.md` §9, `INTEGRATION_CONTRACT.md` | Haute | G2 |
| O-03 | Granularité du paquet : un paquet par campagne, ou un par run | `PACKAGE_FORMAT.md` §11 | Haute | G1 |
| O-04 | Chiffrement et signature des paquets | `PACKAGE_FORMAT.md` §11 | Moyenne | Hors V0.1 |
| O-05 | Plafond d'hébergement d'un artefact et dépôt externe | `PACKAGE_FORMAT.md` §9 | Haute | G1 |
| O-06 | Politique face à une dérive de schéma | `ARCHITECTURE.md` §9, `PACKAGE_FORMAT.md` §7 | Haute | G1 |
| O-07 | Quelle part du pilotage d'un run appartient à ECHOS et non au Launcher | `VISION.md` §9, `ADR-003` | Haute | G5 |
| O-08 | Relais WebSocket par la Gateway au débit réel de SYNE | `NETWORK.md` §4.3 | Haute | Hors V0.1 |
| O-09 | Reprise partielle d'un run par point de contrôle | `EXPERIMENTS.md` §13 | Moyenne | G3 |
| O-10 | Liste close des capacités déclarables | `COMPONENTS.md` §12 | Haute | G2 |
| O-11 | Critères de détection et seuils de ressources | `TESTING.md` §11, `PACKAGING.md` §12 | Moyenne | G6 |
| O-12 | Correspondance entre version de format et version de Launcher | `PACKAGE_FORMAT.md` §7 | Moyenne | G1 |
| O-13 | Stratégie d'estimation de durée d'une campagne | `EXPERIMENTS.md` §13 | Basse | G3 |
| O-14 | Plateforme de référence pour les cibles de performance | `TESTING.md` §11 | Haute | G2 |
| O-15 | Niveau d'accessibilité de référence | `TESTING.md` §13 | Moyenne | G4 |
| O-16 | Langue de distribution du paquet Linux | `PACKAGING.md` §12 | Moyenne | G6 |
| O-17 | Emplacement des données utilisateur sous Windows | `PACKAGING.md` §12 | Moyenne | G6 |
| O-18 | Interface de référence en cas d'adoption multiple | `COMPONENTS.md` §12 | Basse | G4 |
| O-19 | Normalisation du vocabulaire des causes | `OBSERVABILITY.md` §9 | Moyenne | G4 |
| O-20 | Réutilisation d'artefacts entre campagnes | `EXPERIMENTS.md` §13 | Moyenne | G3 |
| O-21 | Outil de génération de cas pour les tests de propriétés | `TESTING.md` §13 | Basse | G1 |
| O-22 | Position du Launcher dans la feuille de route racine | `ROADMAP.md` §2 | Basse | G0 |
| O-23 | Granularité du jalon d'interface G4 | `ROADMAP.md` §2 | Basse | G0 |
| O-24 | Stratégies de graines au-delà de `derived` | `EXPERIMENTS.md` §13 | Basse | G3 |
| O-25 | Support IPv6 et double pile | `NETWORK.md` §6.2, §12 | Basse | Hors V0.1 |
| O-26 | Réouverture éventuelle de macOS | `ARCHITECTURE.md` §9 | Basse | Hors V0.1 |
| O-27 | Conservation du type de composant utilitaire | `COMPONENTS.md` §12 | Basse | G0 |
| O-35 | Mécanisme de l'espace d'adressage interne LIVEX : plage de ports réservée ou interface loopback dédiée (spike Windows/Linux) | `NETWORK.md` §12 | **Haute** | G0 |
| O-36 | Bornes exactes de l'espace interne et migration des ports par défaut (5180/5181/5000) | `NETWORK.md` §12, §6.2 | Haute | G1 |
| O-37 | Mécanisme de mesure des flux par composant (compteurs socket, `/proc`, eBPF léger) | `NETWORK.md` §12, `OBSERVABILITY.md` §5 | Moyenne | G1 |
| O-28 | Comportement à l'installation de deux composants identiques | `COMPONENTS.md` §12 | Moyenne | G2 |
| O-29 | **SYNE sait-il tourner seul et se terminer ?** `--seed`, `--ticks`, `--export-dir`, sortie automatique | `INTEGRATION_CONTRACT.md` §3.3 | **Bloquante** | Porte P2 |
| O-30 | **ECHOS est-il pilotable sans son interface ?** `AnalyzeRun`, `AnalyzeExperiment`, `GenerateReport` | `INTEGRATION_CONTRACT.md` §10 | **Bloquante** | Porte P3 |
| O-31 | Décision n°28 sur le protocole des messages `snapshot` et `event` | `NETWORK.md` §2.5 | Haute | Porte P1 |
| O-32 | ECHOS fonctionne-t-il sous Linux, exécutable et interface | `PACKAGING.md` | **Bloquante** | Porte P4 |
| O-33 | Le déterminisme tient-il entre deux runs identiques, et entre plateformes | `INTEGRATION_CONTRACT.md` §12 | Haute | Porte P5 |
| O-34 | Arrêt propre d'un processus sans console sous Windows : HTTP ou signal | `INTEGRATION_CONTRACT.md` §5.1 | Haute | G2 |

## 3. Points bloquants sur des composants externes

Cinq points ne dépendent pas du Launcher. Ils ne peuvent pas être tranchés par la
documentation, seulement par une vérification sur le composant réel. Tant qu'ils
subsistent, le Launcher se limite au mode Contrôle et à la supervision.

| # | Question | Ce qui est bloqué |
| :-- | :-- | :-- |
| **O-29** | SYNE en mode batch | Toute campagne automatique multi-run |
| **O-30** | ECHOS pilotable sans interface | Tout rapport, donc le jalon G5 |
| **O-31** | Protocole `snapshot` et `event` | L'adaptateur de protocole, donc l'immersion |
| **O-32** | ECHOS sous Linux | Le paquet Linux, donc le jalon G6 |
| **O-33** | Déterminisme du moteur | Toute campagne à valeur scientifique |

Leurs contreparties sont les portes **P1 à P5** de `ROADMAP.md`, et les spikes
**S1 à S8**.

## 4. Détail de quelques points ouverts

### 4.1 O-01 — Politique de redémarrage

**Question.** Le Launcher redémarre-t-il un composant défaillant, et selon quelles
conditions ?

**Enjeu.** Le redémarrage automatique masque les pannes récurrentes, ce qui rend un
diagnostic plus difficile, et interrompt un run en cours, ce qui perd du temps de
calcul. L'absence de redémarrage laisse l'utilisateur seul face à un incident
mineur.

**Données nécessaires.** La fréquence observée des défaillances transitoires, et le
coût d'un run interrompu.

**Position actuelle.** Le Launcher ne redémarre rien. C'est la règle la plus
restrictive, retenue faute de décision.

### 4.2 O-02 — Procédure d'acceptation d'un composant

**Question.** Qui exécute les tests d'acceptation, à quelle fréquence, et où le
résultat est-il consigné ?

**Enjeu.** `INTEGRATION_CONTRACT.md` énonce des exigences, et `TESTING.md` §9 des
tests. Sans procédure, un composant peut être considéré conforme sans que l'exigence
ait jamais été vérifiée.

**Position actuelle.** Les tests existent dans la spécification, la procédure
n'existe pas.

### 4.3 O-03 — Granularité du paquet

**Question.** Un paquet `.livexp` porte-t-il une campagne, ou un run ?

**Enjeu.** Un paquet par campagne est simple et cohérent avec le scellement en fin de
campagne. Un paquet par run est plus partageable et plus facile à manipuler. Une
très grande campagne produirait un paquet que personne ne peut s'envoyer.

**Piste.** Le paquet par campagne, avec un format d'index qui référencerait des
paquets de run dans une archive de campagne. Cela ajoute un second format, ce qui
contredit `ADR-004`.

**Position actuelle.** Un paquet par campagne, par défaut.

### 4.4 O-06 — Dérive de schéma

**Question.** Un paquet écrit par un Launcher plus récent est-il refusé, lu en mode
dégradé, ou ouvert en lecture seule ?

**Enjeu.** Le refus protège contre une lecture fausse, mais bloque l'utilisateur qui
n'a pas encore mis à jour. La lecture dégradée évite le blocage, mais risque
d'afficher une campagne incomplète sans le dire.

**Position actuelle.** Refus explicite, avec la version requise et la version lue.

### 4.5 O-07 — Frontière d'orchestration Launcher / ECHOS

**Question.** Quelles décisions d'orchestration appartiennent au Launcher, et
lesquelles restent à ECHOS ?

**Enjeu.** ECHOS possède déjà des capacités d'orchestration de ses analyses. Si les
deux décident de planifier une analyse, l'utilisateur verra deux propriétaires
pour la même action.

**Dépendance.** `ADR-003` tranche la propriété de l'analyse, mais pas celle de son
orchestration. C'est une distinction fine, et elle doit être explicite avant G0
d'être clos.

### 4.6 O-14 — Plateforme de référence

**Question.** Sur quelle machine les cibles de performance de `TESTING.md` §11 sont-elles
mesurables ?

**Enjeu.** Sans machine de référence, « < 2 s » et « < 100 ms » ne sont pas
arbitrables, donc la porte G6 ne peut pas être franchie objectivement.

**Impact.** Bloque G6, et fragilise G2 et G3, dont les tests de charge en dépendent.

## 5. Points par échéance de jalon

| Jalon | Points à trancher |
| :-- | :-- |
| **G0** | O-07, O-22, O-23, O-27, O-35 |
| **G1** | O-03, O-05, O-06, O-12, O-21, O-36, O-37 |
| **G2** | O-02, O-10, O-14, O-28, O-34 |
| **G3** | O-01, O-09, O-13, O-20, O-24 |
| **G4** | O-15, O-18, O-19 |
| **G5** | — |
| **G6** | O-11, O-16, O-17 |
| **Hors V0.1** | O-04, O-08, O-25, O-26 |
| **Portes P1–P5** | O-29, O-30, O-31, O-32, O-33 |

## 6. Points en attente d'affectation

Tous les points ouverts ont désormais une échéance, renseignée en §5. Cette
section ne contient donc plus de point en attente ; elle est conservée pour
recueillir les points futurs avant qu'ils ne reçoivent une échéance.

| # | Question |
| :-- | :-- |
| — | Aucun point en attente à ce jour. |

## 7. Conventions de suivi

| Champ | Règle |
| :-- | :-- |
| **Numéro** | `O-nn`, stable, jamais réattribué |
| **Origine** | Le document et la section qui ont posé la question |
| **Échéance** | Le jalon avant lequel la réponse est nécessaire |
| **Statut** | Ouvert, en cours de décision, tranché |
| **Résolution** | Renvoyée dans le document d'origine, avec la décision et son ADR si elle est structurante |

Un point est **tranché** lorsqu'il disparaît de la section « Points restés ouverts »
du document d'origine. Il reste alors dans le présent tableau avec son statut, pour
la mémoire de la décision.

## 8. Références

- `ROADMAP.md` — jalons et conditions de franchissement
- `TESTING.md` — vérifications et propriétés
- `adr/` — décisions déjà tranchées
- Sections « Points restés ouverts » de tous les documents du composant

---

## Points restés ouverts dans ce document

- **Gouvernance.** La numérotation `O-nn` et les échéances de jalon suivent la
  convention de `../docs-syne/ISSUES.md`, mais le Launcher n'a pas de carte Kanban
  propre. Il faut décider si les points ouverts sont suivis par ce tableau ou par le
  Kanban du projet.
- **Priorisation.** Aucune colonne de priorité n'est renseignée. Les points O-05,
  O-14 et O-07 sont cités comme bloquants dans le présent document, sans que cette
  priorisation soit formelle.
- **Volume.** Vingt-huit points ouverts pour un composant non implémenté est un
  nombre attendu pour une phase de conception, mais il faut fixer un seuil au-delà
  duquel la phase de conception est jugée insuffisamment aboutie.

