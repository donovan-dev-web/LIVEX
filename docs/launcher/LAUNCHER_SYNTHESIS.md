# LAUNCHER_SYNTHESIS.md — Synthèse de la spécification Launcher

**Composant** : Launcher 
**Statut** : [STABLE] 
**Dernière mise à jour** : 21 octobre 2026 

---

## 1. Aperçu général

Le Launcher est le point d'entrée principal de LIVEX, responsable de l'installation, de la configuration, de la détection des installations existantes et du cycle de vie du logiciel. Cette synthèse récapitule les décisions validées, les frontières connues et les points techniques restant à confirmer.

---

## 2. Décisions validées (Architecture Validée)

| Domaine | Décision | Référence |
| :-- | :-- | :-- |
| **Technologie de l'installateur** | Qt Installer Framework + NSIS + configuration JSON. | ADR-001 technologie-installateur-dotnet, ADR-002 installation-utilisateur-sans-elevation |
| **Installation sans élévation** | Vérification fermeture LIVEX, refus si processus en cours, conservation `settings.json`, espace de travail et `.livexp`, réinstallation à partir reçu. | ADR-002 installation-utilisateur-sans-elevation |
| **Séparation logiciel/travail** | Répertoire logiciel et répertoire de travail distincts, mémorisation du chemin travail, gestion répertoire inaccessible. | ADR-004 racine-de-donnees-et-espace-de-travail |
| **Décomposition composants** | Composants obligatoires (Launcher, SYNE, ECHOS) et facultatifs (PRISM, PrismLDK, Documentation, SYNE-Mock). | ADR-006 périmetre-composants-v02 |
| **Pas de `livex.json`** | Suppression du fichier `livex.json` du périmètre fonctionnel. | ADR-005 enregistrement-et-cycle-de-vie, ADR-004 racine-de-donnees-et-espace-de-travail |
| **Détection installations existantes** | Détection sans `livex.json`, mécanisme d'enregistrement adapté OS, pas d'écrasement silencieux. | ADR-005 enregistrement-et-cycle-de-vie, ADR-006 périmetre-composants-v02 |
| **Désinstallation** | Trois options : logiciel uniquement, logiciel + configuration, suppression complète avec confirmation. | ADR-002 §10.3 options A/B/C |
| **Intégration système** | Raccourci menu applications (optionnel), pas de raccourci bureau par défaut, pas d'association fichiers ni modification variables d'environnement. | ADR-002 §8 intégration au système |
| **Signature Windows** | Les exécutables doivent être signés numériquement avant publication. | ADR-003 dépendances-embarquees §12.1 |
| **Licences** | Présentation licence pendant installation, mention dépendances tierces. | ADR-003 §13 |

---

## 3. Frontières connues (Non implémenté / à confirmer)

| Référence | Point | Statut | Résultat attendu |
| :-- | :-- | :-- | :-- |
| T01 | Technologie installateur | Validé | Qt IFW + NSIS + JSON |
| T02 | Installation sans élévation | Validé | Procédure coute (a)/(b)/(c)/(d) |
| T03 | Séparation logiciel/travail | Validé | Mécanismes définis |
| T04 | Décomposition composants | Validé | Obligatoires/facultatifs définis |
| T05 | Pas de `livex.json` | Validé | Supprimé du périmètre |
| T06 | Détection installations existantes | En cours | Mécanisme à affiner |
| T07 | Désinstallation options A/B/C | En cours | Interface utilisateur à finaliser |
| T08 | Intégration système | En cours | Raccourcis, associations à définir |
| T09 | Signature Windows | Planifié | Certificate evaluation |
| T10 | Licences | Planifié | Vérification mentions |

---

## 4. Frontières de calibration

Aucune calibration numérique n'est requise pour le Launcher à l'heure actuelle. Les décisions sont binaires (implémentées ou non) ou textuelles (messages utilisateur).

---

## 5. Points techniques restant à confirmer

| Référence | Domaine | Action requise |
| :-- | :-- | :-- |
| T06 | Détection installations existantes | Tester mécanismes sur Windows/Linux |
| T07 | Désinstallation options A/B/C | Finaliser interface utilisateur |
| T08 | Intégration système | Définir raccourcis, associations |
| T09 | Signature Windows | Évaluer certificat, coût, conditions |
| T10 | Licences | Vérifier mentions dépendances tierces |

---

## 6. Synthèse des ADR du composant

| ADR | Sujet | Statut |
| :-- | :-- | :-- |
| ADR-001 technologie-installateur-dotnet | Choix technologie | Accepted |
| ADR-002 installation-utilisateur-sans-elevation | Procédure remplacement | Accepted |
| ADR-003 dependances-embarquees | Dépendances embarquées | Accepted |
| ADR-004 racine-de-donnees-et-espace-de-travail | Séparation logiciel/travail | Accepted |
| ADR-005 enregistrement-et-cycle-de-vie | Détection et cycle de vie | Accepted |
| ADR-006 périmetre-composants-v02 | Composants obligatoires/facultatifs | Accepted |

---

## 7. Synthèse des jalons

| Jalon | ID | Titre | Priorité | Statut |
| :-- | --: | :-- | :--: | :--: |
| — | — | Installation hors ligne sous Windows | P0 | Validé |
| — | — | Installation hors ligne sous Linux | P0 | Validé |
| — | — | Sélection composants facultatifs | P0 | Validé |
| — | — | Interface utilisation sans élévation | P1 | Validé |
| — | — | Gestion chemins logiciel/travail | P1 | Validé |
| — | — | Désinstallation optionnelle | P1 | Validé |

---

## 8. Conséquences

### Positives
- Architecture claire et modulaire.
- Pas de dépendance à `livex.json`.
- Procédure d'installation et de désinstallation protégées.
- Intégration système minimale et configurable.

### Négatives
- Complexité dans la gestion des chemins et de la persistance.
- Interface utilisateur avec plusieurs étapes de confirmation.

### Risques
- Erreur dans la détection d'installation existante.
- Problème de persistance du chemin de travail.

---

## 9. Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Synthèse de la spécification Launcher basée sur la base de travail fournie |