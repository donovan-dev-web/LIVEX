# TESTING.md — Installateur LIVEX

**Composant** : LIVEX (Installateur)
**Statut** : [DRAFT]
**Dernière mise à jour** : 8 octobre 2026
**Dépend de** : `SPECIFICATION.md`, `ARCHITECTURE.md`, `RELEASE_AND_UPDATE.md`, `../docs-launcher/TESTING.md`
**Source Monographie** : —

---

## 1. Objet

Ce document définit comment l'installateur est validé et ce qui permet de déclarer
le jalon **G6** franchi pour un OS. Un OS n'est annoncé que s'il a passé
l'acceptation de la section 7 sur une machine propre (décision de cadrage n° 8,
`V1-CAPABILITY-MATRIX.md`).

## 2. Niveaux de test

| Niveau | Projet | Environnement | Contenu |
| :-- | :-- | :-- | :-- |
| Unitaire | `Setup.Tests.Unit` | Aucun accès disque ni réseau réel | Calcul du plan, ordre et dépendances, journal, annulation, règles de répertoires, analyse du catalogue, codes de sortie |
| Intégration | `Setup.Tests.Integration` | Répertoire temporaire, serveur de catalogue local | Installation, mise à jour, réparation, désinstallation réelles dans un préfixe de test ; interruption simulée |
| Propriétés | `Setup.Tests.Unit` | — | Annuler après la *n*-ième écriture ramène à l'état initial, pour tout *n* ; rejouer un plan est idempotent |
| Machine propre | `Setup.Tests.Clean` | Machines virtuelles ou conteneurs vierges | Parcours complet par installateur publié (§5) |
| Interface | `Setup.Tests.Unit` + captures | Rendu hors-écran | États de chaque écran de l'assistant ; navigation clavier |

L'architecture en projets (`ARCHITECTURE.md` §2) permet que les trois premiers
niveaux ne démarrent **aucun composant LIVEX** et n'ouvrent aucune fenêtre.

## 3. Doubles de test

| Double | Rôle |
| :-- | :-- |
| Système de fichiers simulé | Injecte pannes (disque plein, accès refusé, écriture partielle) |
| Serveur de catalogue local | Sert un catalogue et des artefacts de test, avec empreintes valides ou corrompues |
| Composants factices | Reprennent `Stub.Syne`, `Stub.Echos`, `Stub.Prism` du Launcher, packagés comme composants installables |
| Plateforme simulée | Variables, raccourcis et entrée de désinstallation enregistrés en mémoire |

Les tests unitaires et d'intégration n'utilisent **jamais** les artefacts réels :
ils vérifient l'installateur, pas la qualité des composants.

## 4. Machines propres

Une machine propre est une image qui n'a **jamais** eu LIVEX, .NET SDK, Python
utilisateur, Node ni Unreal, et dont l'utilisateur de test n'a pas de droits
d'administrateur.

| Cible | Images de départ proposées | Remarque |
| :-- | :-- | :-- |
| Windows | Windows 10 22H2 x64 et Windows 11 x64, instantané vierge | Sans le SDK .NET ; avec et sans accès réseau |
| Ubuntu | Dernières LTS, session graphique et image minimale sans interface | Vérifie l'absence des bibliothèques et le mode non interactif |

Les versions exactes sont fixées à la première campagne et consignées ici
(point ouvert de `SPECIFICATION.md`). Un instantané est restauré avant **chaque**
scénario.

## 5. Scénarios

| ID | Scénario | Résultat attendu |
| :-- | :-- | :-- |
| TS-01 | Installation complète par l'assistant, composants par défaut | Les dix étapes réussissent ; `--check` sans échec bloquant |
| TS-02 | Installation non interactive, mêmes options | Résultat identique à TS-01 (arborescence, reçu, manifestes) |
| TS-03 | Répertoires personnalisés (programme et workspace) | `LIVEX_DATA` posée ; le Launcher utilise le workspace choisi |
| TS-04 | Chemin avec espaces et caractères accentués | Installation et lancement corrects |
| TS-05 | Espace disque insuffisant (volume contraint) | Blocage à l'étape 5 ou annulation propre, code 4 |
| TS-06 | Interruption brute pendant l'étape 7 (processus tué) | Au relancement, restauration ou reprise ; aucune installation à moitié |
| TS-07 | Annulation depuis l'assistant à chaque étape d'installation | État initial restauré (comparaison d'arborescence et de variables) |
| TS-08 | Artefact corrompu | Rejet, code 5, rien d'installé |
| TS-09 | Catalogue de `schema` inconnu | Refus avec versions requise et lue |
| TS-10 | Réseau absent | Avertissement ; parcours hors-ligne fonctionnel |
| TS-11 | Port 5000, 5180 ou 5181 occupé | Avertissement ; installation possible ; `--check` reporte le conflit |
| TS-12 | SYNE et mock sélectionnés ensemble | Avertissement, issue proposée, choix consigné au reçu |
| TS-13 | Bibliothèque Linux manquante | Blocage du Launcher avec la commande à exécuter ; rien n'est installé en élévation |
| TS-14 | Composant non accepté sur l'OS | Option grisée avec cause ; non installable |
| TS-15 | Mise à jour d'un composant | Remplacement atomique ; `--check` ; `.prev` conservé |
| TS-16 | Mise à jour avec échec de validation | Ancienne version intacte et utilisable |
| TS-17 | Mise à jour refusée pendant un run | Code 9 ; aucune modification |
| TS-18 | Réparation après suppression d'un fichier installé | Fichier restauré, empreintes conformes |
| TS-19 | Désinstallation | Programme, raccourcis, variables et ligne du registre retirés ; workspace intact |
| TS-20 | Désinstallation avec `--purge-workspace` | Confirmation du chemin exigée ; workspace supprimé seulement après confirmation |
| TS-21 | Réinstallation par-dessus un workspace existant | Workspace conservé, paquets `.livexp` relisibles |
| TS-22 | Lancement par raccourci sans terminal | Le Launcher détecte les composants et le workspace (OI-05) |
| TS-23 | Lancement en administrateur ou `root` | Avertissement ou refus explicite |
| TS-24 | Rétrogradation | Refusée sans option explicite |
| TS-25 | Installation de PRISM-LDK sans Unreal | Option masquée ou verrouillée, cause affichée |

## 6. Traçabilité

| Exigence | Scénarios |
| :-- | :-- |
| INS-F01, F13 | TS-01, TS-02 |
| INS-F02 | TS-01 (acceptation requise : test d'interface) |
| INS-F03 | TS-05, TS-11, TS-13 |
| INS-F04 | TS-03, TS-04 |
| INS-F05, F06 | TS-12, TS-14 |
| INS-F07 | TS-01, TS-13 |
| INS-F08 | TS-03, TS-22 |
| INS-F09 | TS-01, TS-02 |
| INS-F10, N06 | TS-06, TS-07 |
| INS-F11 | TS-15, TS-18, TS-19 |
| INS-F12 | TS-19, TS-20, TS-21 |
| INS-F14 | TS-15, TS-16, TS-17 |
| INS-N01 | TS-13, TS-23 |
| INS-N02 | TS-08 |
| INS-N03 | TS-01 (machine sans runtime) |
| INS-N04 | TS-19 (comparaison avant/après) |
| INS-N08 | TS-10 |

Toute exigence sans scénario est un défaut de la présente spécification.

## 7. Critères d'acceptation d'un OS (G6)

Un OS est déclaré supporté lorsque, sur machine propre :

1. TS-01 à TS-08, TS-15 à TS-22 passent ;
2. `livex-launcher --check` ne signale aucun échec bloquant après installation ;
3. une campagne réelle courte exécutée depuis une installation produite par
   l'installateur archive, analyse et scelle un paquet (critère de G5 réutilisé) ;
4. l'installateur est exécuté en `--unattended` dans la CI (`test-installer-clean`)
   à chaque tag de release ;
5. la désinstallation laisse le système dans l'état initial, hors workspace.

## 8. Intégration continue

| Déclencheur | Tests |
| :-- | :-- |
| Chaque PR touchant `installer/` | Unitaires, propriétés, intégration, analyse statique des frontières d'assembly |
| Tag de release | Scénarios TS-01, TS-02, TS-19 sur l'artefact publié, matrice Windows et Linux |
| Campagne manuelle avant G6 | Tous les scénarios sur machines propres, avec et sans réseau |

Les tests de machine propre complets ne sont pas exigés pour la fusion d'une PR ;
ils le sont pour une release.

---

## Points restés ouverts dans ce document

- **Images de machines propres** : choix et automatisation (instantanés, conteneurs,
  machines virtuelles) selon le matériel disponible.
- **Test de l'interface graphique sous Linux sans affichage** : stratégie de rendu
  hors-écran à confirmer avec Avalonia.
- **Campagne de stabilité de 72 heures** (G6) : dépend du franchissement de G5 pour
  disposer d'un composant réel exécutable (`ROADMAP.md` du Launcher §6.7).
