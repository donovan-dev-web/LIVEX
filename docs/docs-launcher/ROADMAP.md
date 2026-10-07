# ROADMAP.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `../../ROADMAP.md` (racine), `VISION.md`, `adr/`
**Source Monographie** : —

---

## 1. Principes

- Road map **en ordre, sans dates**, conformément à la méthode de la racine : les
  jalons s'enchaînent par dépendance technique, et le calendrier dépend des
  validations intermédiaires.
- **Une porte, pas une date.** Chaque jalon est franchi lorsqu'une condition est
  vérifiable, jamais parce qu'une date est atteinte.
- **Le format d'abord.** Le paquet `.livexp` est validé avant toute introduction de
  la concurrence, conformément à `adr/ADR-005-execution-sequentielle-mono-espace.md`.
- **Pas de jalon de déverrouillage de PRISM.** Le déverrouillage du mode Immersion
  n'est pas un jalon du Launcher : c'est une condition, évaluée à l'arrivée de
  PRISM. Voir `adr/ADR-006-prism-verrouille-en-attente.md`.

## 2. Position dans la feuille de route du projet

Le Launcher est un composant transversal. Il s'insère après la consolidation des
contrats de SYNE et d'ECHOS, dont il dépend pour piloter, et il accompagne PRISM.

| Phase racine | Rapport au Launcher |
| :-- | :-- |
| **0 — Socle & gouvernance** | Utilise les conventions de versionnage et de publication |
| **1 — Cadrage général** | Consomme `../../ARCHITECTURE.md` et `../../COMMUNICATION.md` de la racine |
| **2 — Moteur (SYNE)** | Dépend du contrat d'observabilité pour piloter le moteur |
| **3 — Observation (ECHOS)** | Dépend du contrat de restitution pour collecter les artefacts |
| **4 — Intégration Unreal (PRISM)** | Conditionne le déverrouillage du mode Immersion |
| **5 — Consolidation** | Le Launcher devient le point d'entrée unique de LIVEX |

## 3. Les jalons

> **État de réalisation observé :** les sections ci-dessous décrivent la cible
> et les portes de conception ; elles ne signifient pas que le jalon est livré.
> Au 2 octobre 2026, G1 et G2 sont présents dans le code, G3 est testé contre
> des composants simulés mais pas contre SYNE/ECHOS réels, G4 a ses parcours
> principaux (les variantes UI/headless et la validation complète restent
> ouvertes), G5 n'est pas franchi, G6 ne dispose pas encore des installateurs du
> Launcher et G7 reste verrouillé faute de PRISM conforme. La matrice factuelle
> et le plan d'exécution à jour sont dans
> [`../../launcher/V1-CAPABILITY-MATRIX.md`](../../launcher/V1-CAPABILITY-MATRIX.md)
> et [`../../launcher/ROADMAP-V1.md`](../../launcher/ROADMAP-V1.md).

| # | Jalon | Contenu | Livrables | Condition de franchissement |
| :-- | :-- | :-- | :-- | :-- |
| **G0** | Socle de conception | Vision, architecture, modèle de composants, contrat d'intégration, schémas JSON figés, ADR | `VISION.md`, `ARCHITECTURE.md`, `COMPONENTS.md`, `INTEGRATION_CONTRACT.md`, `adr/ADR-001` à `ADR-006` | Les six ADR sont écrites, cinq acceptées |
| **G1** | Format de paquet | Lecture, écriture, cycle de vie, sécurité, déterminisme | `Launcher.Package`, `PACKAGE_FORMAT.md` | Les dix propriétés de `TESTING.md` §5 sont vérifiées |
| **G2** | Orchestration sans interface | Machine à états, registre, résolution de profil, sondes | `Launcher.Domain`, `Launcher.Infrastructure` | Couverture du domaine ≥ 85 %, analyse statique verte |
| **G3** | Campagnes | Planification, dérivation des graines, politiques d'échec, reprise, scellement | `Launcher.Application`, `EXPERIMENTS.md` | Scénarios de bout en bout « campagne nominale » et « pause et reprise » verts |
| **G4** | Interface d'orchestration | Navigation par modes, vues, états vides, accessibilité | `Launcher.Presentation`, `USER_INTERFACE.md` | Tests d'interface de `TESTING.md` §10 verts, symétrie des modes vérifiée |
| **G5** | Session complète avec ECHOS | Détection, profil, demande d'analyse, **présentation du rapport d'émergence** | `PACKAGING.md`, session de bout en bout | Scénario « session complète » vert contre un ECHOS réel, rapport affiché conforme au fichier produit |
| **G6** | Livraison | Installation, premier démarrage, diagnostic, désinstallation | Installation Windows et Linux | `--check` correct sur les deux plateformes, cible de 72 h tenue |
| **G7** | Déverrouillage du mode Immersion | Intégration de PRISM selon ses exigences publiées | Mode Immersion sélectionnable | `INTEGRATION_CONTRACT.md` §11 entièrement satisfaite par PRISM |

## 4. Portes de dépendance externe

Les jalons **G0 à G7** sont internes au Launcher : ils décrivent son propre travail.
Les **portes P1 à P5** sont externes : elles dépendent de l'état de SYNE, d'ECHOS ou
de la plateforme, et le Launcher ne peut pas les franchir seul.

| # | Porte | Condition | Bloque |
| :-- | :-- | :-- | :-- |
| **P1** | Protocole tranché, ou contrat minimal jugé suffisant | Décision n°28 arbitrée | Intégration des flux de données, G5 |
| **P2** | SYNE en mode batch | `--seed`, `--ticks`, `--export-dir`, sortie automatique | Les campagnes réelles, G3 |
| **P3** | ECHOS pilotable sans interface graphique | `AnalyzeRun`, `AnalyzeExperiment`, `GenerateReport` | Les rapports, G5 |
| **P4** | ECHOS validé sous Linux | Spike de l'exécutable et de son interface | Le format de paquet Linux, G6 |
| **P5** | Test de déterminisme réussi | Deux exécutions identiques donnent la même empreinte | Toute campagne à valeur scientifique |

**P2 est la porte la plus structurante.** Tant que SYNE ne sait pas tourner seul et
se terminer, le Launcher ne peut piloter qu'une simulation à la fois avec une
présence humaine. Cette capacité est un prérequis à demander, pas une dépendance
logique.

## 5. Spikes

Chaque spike est borné, répond à une question précise, et doit être lancé **avant**
que l'architecture ne dépende de sa réponse.

| # | Question | Décision impactée |
| :-- | :-- | :-- |
| **S1** | Avalonia : rendu, packaging et fenêtre sous Linux, Wayland et X11 | Faisabilité de l'interface sous Linux |
| **S2** | Arrêt propre d'un processus sans console sous Windows : HTTP ou signaux | `INTEGRATION_CONTRACT.md` §5 |
| **S3** | Job Object sous Windows, groupe de processus sous Linux : aucun orphelin après arrêt brutal du Launcher | Robustesse du confinement |
| **S4** | Surcoût de l'instrumentation sur la boucle de tick | `OBSERVABILITY.md` §9 |
| **S5** | Déterminisme : deux runs identiques donnent-ils la même empreinte, et quel écart entre Windows et Linux | Niveau de reproductibilité, P5 |
| **S6** | ECHOS, exécutable et API, sous Linux (plus d'interface — ADR-007) | P4, format de paquet |
| **S7** | YARP relaie-t-il le flux d'instantanés au débit réel de SYNE | Gateway, hors V0.1 |
| **S8** | **Remplacé** : consoles de logs et fenêtre d'analyse natives, ouvertes depuis le Launcher | `USER_INTERFACE.md` §9, ADR-007 |

S1, S3 et S5 doivent être lancés en premier : ce sont celles qui peuvent remettre en
cause l'architecture.

## 6. Contenu par jalon

### 6.1 G0 — Socle de conception

- Finalité, principes, non-objectifs, égalité des modes.
- Frontière contrôle et données, découpage en assembly, modèle de concurrence.
- Modèle de composant, manifeste, machine à états, profils.
- Exigences d'intégration imposées à tout composant piloté.
- Six décisions d'architecture consignées.

**Livrable notable** : la documentation qui gouverne le Launcher, dont l'essentiel
n'est pas encore implémenté. G0 est un jalon documentaire, et il est le premier
parce que les décisions de conception conditionnent le format.

### 6.2 G1 — Format de paquet

- Écriture en incrément, index central, ordre d'écriture normatif.
- Manifeste comme point de commit, reprise par `runs/index.json`.
- Scellement, refus d'écriture, normalisation pour le déterminisme.
- Contre-mesures de sécurité : chemins, ratio, exécutables, symlinks, concurrence.

**Portée testable sans composant** : l'ensemble du jalon est vérifiable sans
démarrer SYNE, ECHOS ni PRISM.

### 6.3 G2 — Orchestration sans interface

- Machine à états des composants, registre, transitions normatives.
- Lecture de manifeste, détection, résolution de profil, validation de capacités.
- Sondes de santé, compte-rendus, agrégation.
- Résolution des ports, registre réseau, refus hors boucle locale.

### 6.4 G3 — Campagnes

- Définition de campagne, dérivation des graines, politiques d'échec.
- Exécution séquentielle, progression, estimation.
- Reprise après incident, annulation, scellement.
- Collecte des artefacts par production, périmètre d'écriture disjoint.

### 6.5 G4 — Interface d'orchestration

- Sélecteur des quatre modes de lancement : Console, Standard, Développement et Personnaliser.
- Navigation par neuf écrans : Accueil, Expériences, Campagnes, Analyse, Rapports, Configuration, Logs, Monitoring et Documentation.
- Cycle de vie des composants depuis les cartes et profils configurables.
- Rapport Markdown fidèle à ECHOS et documentation embarquée dans le même lecteur.
- Suivi d'état, causes, ressources locales et progression de campagne.

Le socle Avalonia et les parcours principaux sont implémentés. L'écran Logs
permet d'ouvrir un paquet, de consulter/exporter les journaux stdout/stderr de
ses runs ainsi que le journal de session. La configuration permet d'ajouter des
installations valides et de choisir l'installation/version active. G4 n'est
pas considéré terminé tant que les variantes UI/headless déclarées, les
parcours d'accessibilité, les états vides/erreur et le rendu visuel n'ont pas
été acceptés. Le mode Console est un profil de lancement, pas une preuve que les
composants réels fonctionnent sans interface.

### 6.6 G5 — Session complète

- Détection des composants réels, profil « Expérience », collecte des artefacts.
- Demande d'analyse à ECHOS, puis **affichage du rapport d'émergence** dans le
  Launcher, avec vérification de fidélité au fichier produit.
- Vérification que la campagne se déroule entièrement **sans** ouvrir l'interface
  web d'ECHOS.
- Vérification qu'un mode défaillant ne dégrade pas l'autre.

### 6.7 G6 — Livraison

- Arborescence d'installation, répertoire de données séparé.
- Parcours de premier démarrage, commande `--check`.
- Validation des cibles de performance, campagne de stabilité de 72 h.

### 6.8 G7 — Déverrouillage du mode Immersion

- Lecture du manifeste de PRISM, validation de ses exigences publiées.
- Cycle de vie de PRISM piloté comme tout autre composant.
- Retrait du verrou, la conception et l'interface étant déjà en place.

## 7. Définition de terminé pour V1

- [ ] Tous les écrans V1 et les quatre modes sont documentés et testés selon leur contrat.
- [x] Les journaux globaux, de session et de processus par run sont consultables et exportables depuis l'interface.
- [x] La configuration permet d'ajouter des installations valides et de choisir explicitement l'installation/version active d'un composant.
- [ ] La configuration permet de choisir les variantes UI/headless lorsque les composants les déclarent dans leurs manifestes.
- [ ] Le contrat d'intégration est respecté par les trois composants, checklist de `INTEGRATION_CONTRACT.md` §14.
- [ ] L'intégration continue est verte sous Windows et Linux.
- [ ] Aucun processus orphelin après arrêt brutal du Launcher.
- [ ] Une campagne interrompue reprend sans refaire les runs déjà terminés.
- [ ] L'état global est cohérent avec les règles d'agrégation, et sa cause est affichée.
- [ ] Les commandes de contrôle sont refusées sans jeton.
- [ ] Le test de déterminisme est réussi et les empreintes sont enregistrées.
- [ ] Monitoring activé et désactivé donnent des résultats identiques.
- [ ] Les schémas JSON sont validés au chargement, et un schéma inconnu est refusé.
- [ ] La visualisation du rapport d'émergence affiche le contenu produit par ECHOS.
- [ ] Les fenêtres d'observation (consoles, analyse) peuvent rester fermées pendant une campagne complète.

## 8. Hors V0.1

Ces éléments sont conçus et annoncés, et volontairement absents de la V0.1.

| Élément | Décision de référence | Condition de levée |
| :-- | :-- | :-- |
| **Exécution parallèle** | `ADR-005` | Besoin réel documenté |
| **Multi-espace** | `ADR-005` | Besoin réel documenté |
| **Accès à un poste distant** | `NETWORK.md` §3 (topologie T3) | Décision de sécurité |
| **Passerelle multi-composants** | `NETWORK.md` §4.3 | Décision de sécurité |
| **Chiffrement des paquets** | `PACKAGE_FORMAT.md` §11 | Besoin de confidentialité |
| **Signature des paquets** | `PACKAGE_FORMAT.md` §11 | Autorité de signature définie |
| **Installation par composant** | `PACKAGING.md` §12 | Modèle de sécurité |
| **macOS** | `ARCHITECTURE.md` §7 | Besoin fonctionnel |
| **Thème sombre** | `USER_INTERFACE.md` §11 | Décision d'interface |
| **Seconde langue** | `USER_INTERFACE.md` §10 | Besoin d'utilisateur |

## 9. Dépendances externes

Le Launcher dépend de contrats, pas de livraisons. Il avance dès lors que les
contrats sont figés.

| Dépendance | Contrat mobilisé | Effet sur le Launcher |
| :-- | :-- | :-- |
| SYNE | Cycle `Prepare` → `Ready` → `Start`, observabilité WebSocket, contrôle HTTP | Sans ce contrat, aucun profil n'est satisfiable |
| ECHOS | Restitution d'artefacts par run et par campagne | Sans ce contrat, aucune collecte d'analyse |
| PRISM | Consommation des contrats du moteur, état de session, commandes de vol | Conditionne G7 uniquement |
| Projet | `../../VERSIONING.md`, `../../COMMUNICATION.md` | Versionnage, transports, conventions |

Un changement de contrat côté composant est un **changement de version** du contrat,
et impose une adaptation du Launcher, non une adaptation rétroactive du composant.

## 10. Références

- `VISION.md` — finalité et non-objectifs
- `TESTING.md` — conditions de franchissement vérifiables
- `PACKAGE_FORMAT.md` — jalon G1
- `EXPERIMENTS.md` — jalon G3
- `adr/` — décisions de conception

---

## Points restés ouverts dans ce document

- **Position exacte dans la phase racine.** Le Launcher est transversal et n'a pas
  sa propre phase racine. Il faut décider s'il mérite une phase dédiée dans
  `../../ROADMAP.md` ou s'il reste rattaché à la consolidation.
- **Granularité de G4.** L'interface d'orchestration est un jalon unique. Il faut
  vérifier qu'il n'est pas trop large pour servir de porte de franchissement.
- **Critère de G6.** La campagne de stabilité de 72 heures suppose un poste
  instrumenté, dont la disponibilité n'est pas acquise. Voir `TESTING.md` §11.
- **Antériorité de G5.** G5 dépend d'un ECHOS réel. Si ECHOS n'est pas disponible,
  G5 ne peut pas être franchi, et G6 s'en trouve bloqué. Il faut décider si G6 doit
  pouvoir être franchi avec un banc de composants simulés.
- **Jalon de la politique de redémarrage.** La décision est en attente
  (`OBSERVABILITY.md` §8). Elle doit être prise avant G3, où la gestion des incidents
  devient visible.
