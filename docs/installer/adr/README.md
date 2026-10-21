# Décisions d'architecture — installateur LIVEX

**Composant** : LIVEX (installateur)
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 octobre 2026
**Dépend de** : [`../SPECIFICATION.md`](../SPECIFICATION.md)

---

## 1. Objet

Ce dossier consigne les décisions structurantes de l'installateur LIVEX :
contexte, décision, conséquences, alternatives écartées, conditions de
réouverture. Une décision acceptée n'est pas un détail d'implémentation : la
contredire demande un nouvel ADR, pas une retouche de phrase.

Format : [`../../../docs/adr/0000-template.md`](../../adr/0000-template.md).

## 2. Décisions

| Réf. | Décision | Statut | Portée |
| :-- | :-- | :-- | :-- |
| [ADR-001](ADR-001-technologie-installateur-dotnet.md) | Installateur .NET autonome, et non Qt Installer Framework | Accepté | technologie, découpage, modes |
| [ADR-002](ADR-002-installation-utilisateur-sans-elevation.md) | Installation par utilisateur, sans élévation | Accepté | privilèges, emplacements, refus |
| [ADR-003](ADR-003-dependances-embarquees.md) | Dépendances embarquées et auto-suffisance du socle hors ligne | Accepté | runtimes, prérequis Linux, taille |
| [ADR-004](ADR-004-racine-de-donnees-et-espace-de-travail.md) | Racine de données, espace de travail et portées de désinstallation | Accepté | modèle de données, `settings.json`, A/B/C |
| [ADR-005](ADR-005-enregistrement-et-cycle-de-vie.md) | Enregistrement, détection et cycle de vie | Accepté | registre, inventaire, mise à jour, réparation |
| [ADR-006](ADR-006-perimetre-composants-v02.md) | Périmètre, nature et provenance des composants | Accepté | PRISM packagé, LDK en ZIP, SYNE-Mock, docs, arborescence |
| [ADR-007](ADR-007-manifeste-de-distribution-et-confiance.md) | Manifeste de distribution, intégrité et chaîne de confiance | Accepté | `distribution.json`, Ed25519, Authenticode, actifs distants |
| [ADR-008](ADR-008-acquisition-modules-facultatifs.md) | Acquisition des modules facultatifs depuis les releases GitHub | Accepté | profils `core`/`full`, téléchargement, cache, rétention |

## 3. Dépendances entre décisions

```text
ADR-001 (technologie)
├── ADR-003 (runtimes du socle)
│   └── ADR-006 (composants et provenance)
│       └── ADR-008 (acquisition des modules facultatifs)
└── ADR-005 (enregistrement et cycle de vie)
    └── ADR-004 (données et désinstallation)
        └── ADR-002 (sans élévation)
ADR-007 (confiance) ── dépend de ADR-001, ADR-006 et ADR-008
```

## 4. Ce qui n'est pas décidé ici

Les points suivants restent ouverts et ne justifient pas encore un ADR :

- format de distribution Linux à long terme (`.deb`, Flatpak, AppImage) ;
- signature des paquets de données `.livexp` ;
- installation machine ou gérée par une organisation ;
- mise à jour automatique (hors périmètre V0.2) ;
- canaux de release (`stable`, `beta`) au-delà du champ de manifeste ;
- miroir des actifs de release hors de GitHub (INS-O-08) ;
- plafond et purge du cache de téléchargement (INS-O-09).

Ils sont suivis dans [`../ROADMAP.md`](../ROADMAP.md) et dans les issues
GitHub associées.

## 5. Révision

Un ADR est révisé lorsqu'une de ses hypothèses est invalidée par une mesure, un
changement de périmètre ou une décision produit. La révision crée une entrée
datée dans la section « Mises à jour » ; elle ne réécrit pas l'historique.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 21 octobre 2026 | Création | Nouvelle base documentaire de l'installateur |
