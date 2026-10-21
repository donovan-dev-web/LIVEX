# SYNTHESIS_SPECS.md — Synthèse de la spécification SYNE

**Composant** : SYNE 
**Statut** : [STABLE] 
**Dernière mise à jour** : 21 octobre 2026 

---

## 1. Aperçu général

SYNE est le moteur de simulation principal du projet LIVEX, responsable de la simulation du monde, des entités, des interactions BDI (Croyance-Désir-Intention) et de tous les systèmes physiques et cognitifs. Cette synthèse récapitule les décisions validées, les frontières connues et les points techniques restant à confirmer.

---

## 2. Décisions validées (Architecture Validée)

| Domaine | Décision | Référence |
| :-- | :-- | :-- |
| **Architecture globale** | Découpage en projets `.NET` (`Simulation.Core`, `Simulation.Console`) avec builds séparés. | ADR-001, SYNE-001 |
| **Boucle de simulation** | 1 tick = 1 minute simulée ; respect du `max-ticks` ; affichage boucle tête/queue. | ADR-002, SYNE-002 |
| **Structure du monde** | Monde continu 2D de 500×500 avec grille spatiale de perception configurable ; déterminisme des positions. | Décision n°2, SYNE-003 |
| **Contrats d'entité** | Entité typée avec traits hérités, validation des invariants (V0.1). | Décision n°4, SYNE-004 |
| **Conventions ADR** | Template ADR prêt, 1 ADR pilote fusionné. | SYNE-005, ADR-001/002 |
| **Configuration moteur** | `global.json` + paramètres moteur (tick, seed, monde) ; flags CLI (`--seed`, `--max-ticks`, `--world-size`) opérationnels. | SYNE-006, CONFIGURATION.md |

---

## 3. Frontières connues (Non implémenté / à confirmer)

| Référence | Point | Statut | Résultat attendu |
| :-- | :-- | :-- | :-- |
| T01 | Capacités réelles de Qt Installer Framework | Non applicable (projet .NET) | N/A — projet .NET, pas Qt |
| T02 | Installation sans privilèges administrateur | Non applicable | N/A — dépend du déploiement |
| T03 | Détection des installations existantes | Non applicable | N/A — dépend du déploiement |
| T04 | Persistance native du chemin de travail | En cours | Mécanisme de mémorisation à définir |
| T05 | Sémantique des options de désinstallation | Non applicable | N/A — dépend du déploiement |
| T06 | Inventaire des dépendances | En cours | Catalogage des dépendances réelles |
| T07 | Signature Windows | Non applicable | N/A — projet .NET interne |
| T08 | Vérification Linux | Non applicable | N/A — projet .NET interne |
| T09 | Nettoyage et retour arrière | En cours | Scénarios d'échec à définir |
| T10 | Licences tierces | En cours | Vérification des licences |
| T11 | Répertoire de travail déplacé | En cours | Parcours de sélection du nouvel emplacement |
| T12 | Langues | Non applicable | N/A — projet .NET interne |

---

## 4. Frontières de calibration (Valeurs configurables)

Les valeurs suivantes sont **configurables** et font l'objet de décisions de calibration V0.1 (7 décisions ouvertes devenues tranchées en principe, valeurs calibrées à l'implémentation) :

| Décision | Valeur | Statut |
| :-- | :-- | :-- |
| Décision n°4 | Unités de base | Configurable → tranchée |
| Décision n°5 | Seuils par défaut | Configurable → tranchée |
| Décision n°6 | Paramètres de mémoire | Configurable → tranchée |
| Décision n°7 | Unités de ressources | Configurable → tranchée |
| Décision n°8 | Unités de temps | Configurable → tranchée |
| Décision n°18 | Coûts d'actions | Configurable → tranchée |
| Décision n°19 | Paramètres de groupe | Configurable → tranchée |
| Décision n°20 | Mécanismes agents | Ouverte selon décision 20 |

Les références SYNE-120/121 sont livrées avec valeurs provisoires configurables et documentées dans ADR-014 — rien n'est figé par accident.

---

## 5. Points techniques restant à confirmer

| Référence | Domaine | Action requise |
| :-- | :-- | :-- |
| T04 | Persistance native du chemin de travail | Définir mécanisme de mémorisation (sans `livex.json`) |
| T06 | Inventaire des dépendances | Cataloguer les dépendances réelles, versions, conditions de redistribution |
| T11 | Répertoire de travail déplacé | Définir parcours permettant de sélectionner un nouvel emplacement |
| T10 | Licences tierces | Vérifier les licences et mentions de tous les éléments distribués |

---

## 6. Synthèse des ADR du composant

Les ADR spécifiques au composant SYNE (format ADRG-*) complètent les ADR généraux :

| ADR | Sujet | Statut |
| :-- | :-- | :-- |
| ADRG-* | Template et conventions ADR | Accepted |
| ADRG-001 | Versionnement de l'installateur | Accepted |
| ADRG-002 | Procédure de remplacement | Accepted |

---

## 7. Synthèse des jalons

| Jalon | ID | Titre | Priorité | Statut |
| :-- | --: | :-- | --: | :--: |
| ph0 | SYNE-001 | Découpage en projets .NET | P0 | LIVRÉ |
| ph0 | SYNE-002 | Boucle de simulation minimale (tick) | P0 | LIVRÉ |
| ph0 | SYNE-003 | Structure du monde 500×500 | P0 | LIVRÉ |
| ph0 | SYNE-004 | Contrats d'entité | P0 | LIVRÉ |
| ph0 | SYNE-005 | Conventions ADR du composant | P1 | LIVRÉ |
| ph0 | SYNE-006 | Configuration `global.json` | P1 | LIVRÉ |
| ... | ... | ... | ... | ... |
| v1 | SYNE-130 | Abandon de la morphologie (ADR-010) | P1 | LIVRÉ |
| v1 | SYNE-131 | Processus de calibration continue | P2 | LIVRÉ |

---

## 8. Conséquences

### Positives
- Architecture claire et modulaire avec séparation des projets .
- Documentation ADR complète et traçable.
- Jalons validés avec critères d'acceptation définis.

### Négatives
- Calibrage restant pour certaines valeurs décisionnelles.
- Points techniques à confirmer avant考虑 V1.

### Risques
- Perte de cohérence si les décisions de calibration ne sont pas mises à jour.
- Dépendance à la complétion des points T04, T06, T11 avant V1.

---

## 9. Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Synthèse de la spécification SYNE basée sur la base de travail fournie |