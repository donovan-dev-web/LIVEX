# Stratégie de test de l'installateur LIVEX

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : [`SPECIFICATION.md`](SPECIFICATION.md), [`ARCHITECTURE.md`](ARCHITECTURE.md)

---

## 1. Principe

Un installateur casse une machine quand il échoue mal. La stratégie de test ne
cherche donc pas seulement à prouver que l'installation nominale fonctionne,
mais à prouver qu'**aucun échec ne détruit ni ne corrompt les données de
l'utilisateur**, et qu'aucun succès n'est annoncé à tort.

Trois règles :

1. **Le plan est testé avant l'écriture.** La majorité des cas se vérifient sur
   un plan calculé, sans toucher au disque.
2. **Chaque test de bout en bout tourne dans un bac à sable.** Répertoire
   temporaire, magasin d'enregistrement en mémoire ou isolé, aucun accès au
   profil réel de l'agent.
3. **Une exigence sans test est un défaut de recette** (`INS-N-08`). La
   traçabilité est tenue dans la section 8.

Conventions de vérification du dépôt appliquées :

- capture explicite du code de sortie (`set -o pipefail`, `${PIPESTATUS[0]}`) ;
- vocabulaire **[R+V]** (réalisé et exécuté), **[R]**, **[P]**, **[A]** ;
- aucune ré-épinglage silencieux d'une référence dorée.

## 2. Niveaux de test

| Niveau | Projet | Portée | Temps cible |
| :-- | :-- | :-- | :-- |
| **Unitaire** | `Setup.Tests.Unit` | `Setup.Core`, sans disque réel ni interface | < 30 s |
| **Intégration** | `Setup.Tests.Integration` | fichiers réels dans un répertoire temporaire, magasins simulés | < 3 min |
| **Bout en bout** | `Setup.Tests.EndToEnd` | binaire publié, installation silencieuse, `--check`, désinstallation | < 10 min |
| **Machine propre** | manuel / VM | installation graphique réelle, vérification visuelle et système | par release |

## 3. Environnements

| Environnement | Usage | Contraintes |
| :-- | :-- | :-- |
| `windows-latest` (CI) | unitaire, intégration, bout en bout | pas d'élévation ; nettoyage `HKCU` |
| `ubuntu-latest` (CI) | unitaire, intégration, bout en bout | `--unattended`, pas d'affichage |
| Windows 11 x64 (VM propre) | recette de release | sans SDK .NET, sans Python |
| Ubuntu 24.04 LTS (VM propre) | recette de release | sans Python, `libicu` présent |
| Debian 12 (VM) | vérification de la borne basse | `libicu` présent |
| Ubuntu minimal sans `libicu` | test négatif de préflight | échec attendu, message vérifié |
| Serveur HTTP local (fixture) | téléchargement, reprise, erreurs | aucune requête vers GitHub en test |

Les versions minimales supportées sont figées par cette matrice, pas par une
phrase de documentation.

## 4. Doubles et bacs à sable

| Dépendance | Double | Raison |
| :-- | :-- | :-- |
| Enregistrement Windows | `InMemoryInstallationStore` | éviter d'écrire `HKCU` en test unitaire |
| Enregistrement Linux | `InMemoryInstallationStore` | déterminisme |
| Processus en cours | `FakeProcessProbe` | simuler un fichier verrouillé |
| Système de fichiers | répertoire temporaire réel | les sémantiques de renommage/verrou importent |
| Charge utile | `payload-fixture/` versionnée | petites tailles, empreintes stables |
| Téléchargement | `FakeDownloader` + serveur HTTP local | reprise, coupure, 404, empreinte fausse, sans GitHub |
| Horloge | `IClock` | journaux et horodatages déterministes |
| Élévation | jamais simulée | les tests échouent si une opération l'exige |

Le `payload-fixture` contient trois composants factices (un obligatoire, un
facultatif, un « indisponible ») et un manifeste signé avec une clé de test
dont la clé privée est **versionnée dans le dépôt de test uniquement**, jamais
utilisée pour une release.

## 5. Cas unitaires

| Domaine | Cas | Exigence |
| :-- | :-- | :-- |
| Manifeste | JSON canonique stable, clés triées, UTF-8 sans BOM | INS-F-70 |
| Manifeste | fichier hors `files`, composant inconnu, `requires` invalide | INS-F-12 |
| Signature | signature valide, invalide, absente, clé inconnue | INS-F-70, 71 |
| Chemins | absolu, `..`, lecteur Windows, chemin vide, séparateur mixte | INS-N-03 |
| Chemins | répertoire système, racine de volume, répertoire personnel | INS-F-04 |
| Chemins | espace de travail = racine de données, ancêtre, chevauchement logiciel | INS-F-23 |
| Préférences | fusion avec clés inconnues, fichier absent, JSON invalide | INS-F-24, 27 |
| Préférences | écriture atomique : fichier temporaire supprimé après échec | INS-F-24 |
| Versions | SemVer, prérelease, downgrade refusé | INS-F-55 |
| Plan | installation neuve, mise à jour, réparation, retrait de composant | INS-F-50 à 58 |
| Plan | portée A, B, C, C + extension | INS-F-61 à 66 |
| Journal | format, rotation, champs interdits | INS-F-75, 76 |
| Ligne de commande | chaque option, combinaison invalide, codes de sortie | INS-F-90 à 99 |
| i18n | clé manquante → anglais + journal, aucune clé brute | INS-F-82 |
| Acquisition | URL non HTTPS, hôte étranger, actif sans empreinte, `unavailable` proposé | INS-F-12, 100 à 102 |
| Acquisition | reprise `Range`, serveur sans `Range`, coupure, annulation | INS-F-101, 104 |
| Extraction | entrée `..`, chemin absolu, lien symbolique, nombre d'entrées et taille dépassés | INS-F-103 |
| Cache | actif vérifié réutilisé, actif corrompu purgé, cache absent recréé | INS-F-101, 102 |
| Hors ligne | actif local accepté par empreinte, refusé sinon, `--no-network` sans requête | INS-F-105, 98, 99 |

## 6. Cas d'intégration

| Cas | Attendu | Exigence |
| :-- | :-- | :-- |
| Installation neuve dans un répertoire temporaire | fichiers, inventaire, enregistrement, préférences | INS-A-03 |
| Installation avec documentation facultative | `docs/` présent | INS-A-09 |
| Installation sans documentation | `docs/` absent, Launcher fonctionnel | INS-A-09 |
| Écriture refusée à mi-parcours | rollback, fichiers préexistants intacts | INS-F-73 |
| Disque plein simulé | échec avant écriture ou rollback complet | INS-F-05 |
| Fichier verrouillé par un faux processus | refus explicite, aucune modification | INS-F-57 |
| Fichier modifié après signature | échec avant écriture | INS-A-29 |
| Manifeste signé avec une autre clé | refus | INS-A-30 |
| `settings.json` invalide | archivage, écriture neuve, avertissement | INS-F-27 |
| Mise à jour | versions remplacées, données conservées | INS-A-19 |
| Réparation d'un fichier corrompu | fichier restauré depuis la distribution | INS-A-21 |
| Retrait d'un composant facultatif | fichiers du composant supprimés, les autres intacts | INS-F-14 |
| Modification de l'espace de travail | nouveau chemin écrit, ancien répertoire intact | INS-F-59 |
| Désinstallation A/B/C | portées respectées | INS-A-22 à 25 |
| Espace de travail déplacé | aucun fichier supprimé, message explicite | INS-A-26 |
| Fichier étranger dans le répertoire logiciel | conservé et signalé | INS-A-28 |
| Téléchargement d'un module depuis le serveur local | archive vérifiée, extraite, inventoriée | INS-A-41 |
| Module `unavailable` dans le manifeste | aucune case, socle installé normalement | INS-A-10, 19 |
| Coupure réseau à 50 % | reprise, aucune installation partielle | INS-A-46 |
| Empreinte modifiée | refus, cache purgé, socle intact | INS-A-45 |
| Profil `full` sans réseau | PRISM et les trois modules installés | INS-A-47 |

## 7. Recette de bout en bout

### 7.1 Automatisée (CI)

```text
1. construire et signer la distribution de test
2. livex-setup --check --log <tmp>/check.log          → code 0
3. livex-setup --unattended --config <tmp>/answers.json
4. vérifier l'arborescence, l'inventaire, l'enregistrement, settings.json
5. <install>/livex-launcher --check                  → code 0
6. livex-setup --unattended --config <tmp>/answers-update.json
7. vérifier la conservation des données et des préférences
8. livex-setup --uninstall --scope c --purge-research → code 0
9. vérifier l'absence de l'installation, la conservation des journaux
10. vérifier l'absence de résidus d'enregistrement
11. profil full : --unattended --no-network → code 0, PRISM et les trois modules présents
12. profil core : --unattended sans module facultatif → aucune connexion
    sortante observée, code 0
13. module facultatif : --offline-assets <dir> → code 0, empreinte vérifiée
```

Chaque étape capture son code de sortie ; un `0` obtenu à travers un pipeline
filtré n'est pas une preuve.

### 7.2 Manuelle (machine propre, par release)

| # | Scénario | Preuve attendue |
| :-- | :-- | :-- |
| M1 | Installation graphique Windows sans runtime .NET | capture des huit écrans, `--check` à 0 |
| M2 | Installation graphique Linux sans Python | idem |
| M3 | Refus d'un répertoire non inscriptible | capture du message |
| M4 | Refus d'un espace de travail chevauchant la racine de données | capture du message |
| M5 | Installation avec documentation hors ligne | `docs/` consultable |
| M6 | Désinstallation C avec espace de travail externe | chemin affiché, paquets conservés |
| M7 | Désinstallation C + extension | seconde confirmation, journaux conservés |
| M8 | Enregistrement supprimé puis relance | adoption proposée, installation retrouvée |
| M9 | Répertoire logiciel supprimé puis relance | enregistrement orphelin proposé au nettoyage |
| M10 | Mise à jour vers une version antérieure | refus explicite |
| M11 | Installation de PRISM packagé (socle, **sans réseau**) | charge utile vérifiée, `components/prism/` présent, raccourci fonctionnel, aucun Unreal Engine requis |
| M12 | Installation du profil `full` sans réseau | PRISM et les trois modules présents, aucune requête sortante |
| M13 | PRISM **décoché** puis poursuite | description des fonctionnalités 3D indisponibles affichée, installation terminée sans blocage, aucun fichier PRISM |
| M13 bis | Coupure réseau pendant le téléchargement d'un module facultatif (LDK) | reprise, socle intact, message |
| M14 | PRISM-LDK | archive identique à l'actif publié, mode d'emploi Unreal affiché |

Les captures sont jointes à la PR de release ; elles ne remplacent pas les
tests automatisés.

## 8. Matrice de traçabilité

| Exigences | Cas de test | Niveau |
| :-- | :-- | :-- |
| INS-F-01 à 09 | installation neuve, rollback, préflight | U + I + E2E |
| INS-F-10 à 19 | composants, disponibilité, retrait | U + I |
| INS-F-20 à 28 | chemins, préférences, arborescence | U + I |
| INS-F-30 à 37 | machine à états de l'assistant | U (ViewModel) + manuel |
| INS-F-40 à 46 | intégrations, nettoyage | I + manuel |
| INS-F-50 à 59 | détection, mise à jour, réparation, espace de travail | U + I + E2E |
| INS-F-60 à 69 | désinstallation A/B/C, résidus | U + I + E2E |
| INS-F-70 à 77 | signature, échecs, journal | U + I |
| INS-F-80 à 84 | langues, accessibilité | U + manuel |
| INS-F-90 à 99 | modes non interactifs, codes | U + E2E |
| INS-F-100 à 106 | acquisition, cache, extraction, hors ligne | U + I + E2E |
| INS-N-01 à 12 | sécurité, performance, portabilité, réseau | revue + mesures |

Les critères d'acceptation `INS-A-01` à `INS-A-49` de la spécification sont
marqués **[R+V]** uniquement lorsqu'un test exécuté est cité.

## 9. Mesures

À chaque release, mesurer et publier :

| Mesure | Méthode | Seuil |
| :-- | :-- | :-- |
| Taille de la distribution par plateforme | taille du dossier/archive | publiée, pas de seuil arbitraire |
| Taille installée | somme des fichiers installés | publiée |
| Durée d'installation | machine propre, `--unattended`, chronomètre | publiée |
| Pic mémoire de `livex-setup` | compteur du système | publié |
| Temps de `--check` | moyenne sur 5 exécutions | publié |

Aucune de ces valeurs n'est écrite dans la documentation utilisateur avant
d'avoir été mesurée sur la version concernée.

## 10. Tests de sécurité

| Test | Attendu |
| :-- | :-- |
| Entrée `../../Windows/System32/...` dans le manifeste | refus à la validation |
| Archive facultative contenant `../evil` ou un lien symbolique | refus avant extraction |
| URL d'actif en `http://` ou sur un hôte étranger | refus au build et à la lecture |
| Lien symbolique dans le répertoire cible | refus d'écriture à travers le lien |
| Distribution dont un fichier est remplacé après signature | refus avant écriture |
| Manifeste signé avec une clé non embarquée | refus |
| Chemin d'espace de travail pointant vers `/` ou `C:\` | refus |
| Processus LIVEX en cours | refus d'écraser, message |
| Journal après une erreur contenant un chemin utilisateur | chemin présent, aucun secret |

## 11. Ce qui n'est pas testé en V0.2

- installation machine et multi-utilisateur (hors périmètre) ;
- coupure de courant pendant une écriture (limite documentée) ;
- mise à jour différentielle (hors périmètre) ;
- installation du LDK **dans** un projet Unreal : l'installateur ne fait que
  déposer l'archive, dont l'identité avec l'actif publié est vérifiée ;
- indisponibilité réelle de GitHub (le test utilise un serveur local ; la panne
  d'un service externe n'est pas reproductible en CI) ;
- macOS et architectures non x64.

## 12. Rapport de vérification

Un rapport de release indique, pour chaque scénario : identifiant, résultat,
code de sortie, chemin du journal, capture éventuelle. Il distingue
explicitement :

- **[R+V]** : exécuté, résultat cité ;
- **[R]** : implémenté, non exécuté ;
- **[P]** : partiel ;
- **[A]** : non commencé.

Une phase de la roadmap ne peut pas être déclarée terminée avec des cases
`[R]` sur un critère de sortie.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Nouvelle base documentaire |
