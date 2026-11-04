# ADR — PRISM

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 2 novembre 2026

---

## 1. Index

| ADR | Titre | Statut | Remplace / complète |
| :-- | :-- | :-- | :-- |
| ADR-002 | Unreal Engine et plugin PRISM-LDK pour PRISM | [Superseded] par ADR-004/005 (historique V1, à archiver dans `legacy-unreal/`) | — |
| ADR-003 | Échelle et cadence SYNE ↔ Unreal (k = 100, TPS 6) | [Superseded] par ADR-004 : plus d'échelle moteur | — |
| [ADR-004](ADR-004-refonte-prism-monde-voxel-autoritaire.md) | Refonte de PRISM : monde voxel autoritaire, SYNE allégé | [Proposed] | ADR-002, ADR-003 |
| [ADR-005](ADR-005-stack-dotnet-silknet-headless.md) | Stack .NET, rendu Silk.NET OpenGL, noyau headless | [Proposed] | ADR-002 |
| [ADR-006](ADR-006-modele-voxel-borne-chunks-tags.md) | Modèle voxel borné, chunks, bloc 3 octets, tags | [Proposed] | — |
| [ADR-007](ADR-007-determinisme-entier-flux-hasard.md) | Déterminisme : arithmétique entière, hasard à compteur | [Proposed] | — |
| [ADR-008](ADR-008-contrat-syne-prism-macro-actions.md) | Contrat SYNE↔PRISM : quatre familles, macro-actions, transport (réponse à la décision n°28) | [Proposed] | — |
| [ADR-009](ADR-009-planification-dans-syne-savoir-des-recettes.md) | Planification dans SYNE (GOAP/HTN) et savoir des recettes | [Proposed] | — |
| [ADR-010](ADR-010-eau-statique-gabarits-conservation.md) | Eau statique, gabarits, conservation de la matière | [Proposed] | — |
| [ADR-011](ADR-011-modele-25d.md) | Géométrie 2.5D par colonnes et navigation de surface | [Accepted] | ADR-006 (géométrie) |
| [ADR-012](ADR-012-transport.md) | Transport de référence HTTP et WebSocket JSON | [Accepted] | ADR-008 (liaison de référence) |

Les décisions ADR-011 et ADR-012 ont été acceptées le 2 novembre 2026 par décision du propriétaire du projet ; leurs preuves techniques restent vérifiées aux portes indiquées. Les ADR antérieurs suivent leur jalon de validation. Gabarit : [`0000-template.md`](0000-template.md).

## 2. Règle

Une décision chiffrée dans la documentation PRISM est une **hypothèse de départ à calibrer**, sauf si un ADR `[Accepted]` la fige. Les valeurs de [`../CONTENT_SPEC.md`](../CONTENT_SPEC.md) §7 en sont l'inventaire.

## 3. Registre des questions ouvertes

Chaque question porte une **recommandation** et la **porte** (jalon) à laquelle elle doit être tranchée.

| ID | Question | Recommandation | Porte |
| :-- | :-- | :-- | :-- |
| Q-01 | Transport SYNE↔PRISM (décision n°28) | **Tranché** : contrôle HTTP JSON, ticks et lecture WebSocket JSON, appels directs `IWorldHost` pour les lots ; codec binaire uniquement si B4/B5 prouve que JSON dépasse le budget (ADR-012) | Validé par décision utilisateur le 2 novembre 2026 ; mesuré en G1/G3 |
| Q-02 | Modèle de propriété et d'accès | Liste d'identifiants autorisés par structure/conteneur, opaque pour PRISM ; droits `take`, `put`, `use` distincts (`SYSTEMS_SPEC.md` §5) | G4 |
| Q-03 | Écoulement de l'eau | Reporté ; source statique en V2 (ADR-010) | G7 |
| Q-04 | Durée du jour et de la saison en ticks | `dayLengthTicks = 480`, 4 jours par saison (hypothèse) ; fixés par calibrage | G7 |
| Q-05 | Intervention humaine dans le monde (« monde modifiable ») | Interventions = commandes journalisées, rejouables ; hors résultats scientifiques par défaut | G8 |
| Q-06 | Naissance, mort, objets des morts | `Incarner`/`Retirer` ; l'inventaire d'un agent retiré tombe au sol (conservation) | G3 |
| Q-07 | Perception souterraine (« rayons X ») | Seuls les blocs exposés sont perceptibles ; résumés de chunk limités aux blocs exposés | G3 |
| Q-08 | Collision entre agents | Pas de collision entre agents (cohérent avec SYNE ADR-010) ; collision avec les blocs uniquement | G3 |
| Q-09 | Croissance des cultures : mises à jour programmées ou aléatoires | Programmées avec gigue pour les cultures ; aléatoires pour les phénomènes diffus (ADR-007) | G2 |
| Q-10 | Contenu des livres et recettes | Variante riche : les recettes peuvent voyager dans des livres SYNE (livres existants `Book`) | G7 |
| Q-11 | Version de SYNE après extraction du monde | SYNE `0.18.0`, contrat d'observabilité `0.6.0` (rupture en `0.x` → incrément de `x`) | G5 |
| Q-12 | Sort de PRISM-LDK / projet Unreal | Archivé et maintenu en `1.x` correctif jusqu'à la release `2.0.0`, puis retiré du build | G8 |
