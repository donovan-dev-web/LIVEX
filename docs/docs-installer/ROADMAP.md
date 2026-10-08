# ROADMAP.md — Installateur LIVEX

**Composant** : LIVEX (Installateur)
**Statut** : [DRAFT]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `README.md`, `TESTING.md`, `../docs-launcher/ROADMAP.md`, `../../launcher/ROADMAP-V1.md`
**Source Monographie** : —

---

## 1. Objet

Séquence de travail pour livrer l'installateur et franchir **G6** sur Linux puis
sur Windows. Chaque jalon a un critère vérifiable. Les prérequis externes à
l'installateur sont isolés en §3, car ils ne se règlent pas dans `installer/`.

## 2. Jalons

| Jalon | Contenu | Critère de sortie | Dépend de |
| :-- | :-- | :-- | :-- |
| **I0 — Décisions** | Validation des quatre ADR ; mise à jour de `PACKAGING.md`, `ISSUES.md`, `README.md`, `INSTALLATION.md`, `CI_CD.md` (`README.md` §7) | ADR [Accepted] ; points OI-01 à OI-08 reportés dans `ISSUES.md` | — |
| **I1 — Manifestes et artefacts de release** | Manifestes de release Linux et Windows de SYNE, ECHOS, mock ; lanceurs ECHOS et mock pour Windows ; publication self-contained du Launcher et de SYNE ; schéma du catalogue | Chaque manifeste valide au schéma ; un composant extrait dans un répertoire vierge est détecté par le Launcher (clôt O-41 sur Linux) | I0 ; P4 pour ECHOS |
| **I2 — Cœur de l'installateur** | `Setup.Core` : plan, journal, annulation, intégrité, catalogue ; mode `--unattended`, `--dry-run`, `--uninstall`, `--repair` ; plateformes Linux d'abord | Tests unitaires, propriétés et intégration verts ; TS-02, TS-06, TS-07, TS-08, TS-19 sur Ubuntu | I1 |
| **I3 — Assistant graphique** | `Setup.App` : les dix étapes, contrôle système, options avec tailles, récapitulatif, reprise après interruption ; format de livraison Linux | TS-01 et TS-03 à TS-14 sur Ubuntu machine propre ; décision OI-01 | I2 |
| **I4 — Mise à jour, signature, hors-ligne** | Catalogue publié, signature, remplacement atomique, retour arrière, mise à jour du Launcher, paquet hors-ligne | TS-09, TS-10, TS-15 à TS-18, TS-24 ; décisions OI-04, OI-06, OI-07, OI-08 | I3 |
| **I5 — Windows** | Plateforme Windows : variables, raccourcis, entrée de désinstallation ; acceptation SYNE et ECHOS sous Windows ; signature | Critères de l'acceptation d'un OS (`TESTING.md` §7) sur Windows 10 et 11 | I3, acceptation des composants sous Windows |

L'ordre privilégie **Linux d'abord** parce que SYNE et ECHOS ne sont acceptés que
sous Linux (`V1-CAPABILITY-MATRIX.md`). Le développement de la plateforme Windows
(`Setup.Platform.Windows`) peut commencer dès I2, mais l'installateur Windows ne
proposera les composants qu'à mesure que le catalogue les marque acceptés.

## 3. Prérequis externes à l'installateur

| # | Prérequis | Pourquoi | Où le suivre |
| :-- | :-- | :-- | :-- |
| D-01 | Manifeste de release SYNE avec exécutable publié | Rien à installer sinon | `ISSUES.md` O-41 |
| D-02 | Persistance du workspace choisi, indépendante de `LIVEX_DATA` (OI-05) | Éviter un workspace « perdu » selon le contexte de lancement | À créer côté Launcher |
| D-03 | Arrêt propre d'un composant sans console sous Windows | Sans lui, une désinstallation ou mise à jour peut laisser des orphelins | `ISSUES.md` O-34 |
| D-04 | Acceptation du cycle de vie de SYNE sous Windows | Condition pour `accepted: true` sur `win-x64` | `ISSUES.md` O-39 |
| D-05 | ECHOS validé et installable sous Windows | Idem | `ROADMAP.md` du Launcher, P4 |
| D-06 | Détection : fiabiliser le périmètre d'hermétisme | L'installateur écrit dans `~/.livex/` ; ce périmètre doit être défini | `ISSUES.md` O-45 |
| D-07 | Lanceur ECHOS et mock pour Windows | Voir I1 | `ARCHITECTURE.md` §6.3 |
| D-08 | Publication des tailles et des empreintes par la CI | Alimente options et catalogue | `RELEASE_AND_UPDATE.md` §5 |

## 4. Risques

| Risque | Probabilité | Impact | Atténuation |
| :-- | :-- | :-- | :-- |
| Acceptation Windows des composants tardive | Élevée | L'installateur Windows livre peu | Linux d'abord ; catalogue honnête ; installateur Windows livrable avec Launcher + mock en attendant |
| Dérive entre manifeste de développement et manifeste de release | Moyenne | Composants non détectés après installation | Validation de schéma en CI ; test de détection (I1) |
| Taille ou durée de téléchargement trop élevées | Moyenne | Abandon d'installation | Tailles mesurées par la CI ; options ; reprise ; hors-ligne |
| `pyarrow` ou autre roue native indisponible pour la version de Python choisie | Moyenne | ECHOS non installable | Version Python fixée avec la CI (OI-03) ; roues dans le paquet hors-ligne |
| Blocage SmartScreen ou antivirus sur l'installateur non signé | Élevée (Windows) | Défiance des testeurs | Signature (OI-04) ; instructions affichées |
| Mise à jour du Launcher sous Windows | Moyenne | Mise à jour impossible | Décision OI-06 avant I4 |
| Variables d'environnement non propagées | Moyenne | Workspace introuvable | Registre d'installations + D-02 |
| Bibliothèques système Linux variables selon les images Ubuntu | Moyenne | Interface qui ne démarre pas | Contrôle de l'étape 3 ; liste mesurée sur machine propre |

## 5. Décisions à prendre

| # | Décision | Jalon de décision |
| :-- | :-- | :-- |
| OI-01 | Format de livraison Linux | I3 |
| OI-02 | Emplacement par défaut du workspace | I1 |
| OI-03 | Version de Python embarquée | I1 |
| OI-04 | Signature du catalogue et de l'installateur | I4 |
| OI-05 | Persistance du workspace choisi | I2 |
| OI-06 | Mise à jour du Launcher | I4 |
| OI-07 | Hébergement du catalogue et des artefacts | I4 |
| OI-08 | Parcours hors-ligne | I4 |

## 6. Hors périmètre de la V0.1

- Mise à jour automatique silencieuse.
- Installation système (administrateur ou `root`).
- macOS et architectures arm64.
- Installation d'Unreal Engine ou du SDK .NET.
- Canal de développement du catalogue.
