# ADR — index transverse LIVEX

**Composant** : LIVEX (général)
**Statut** : index à jour
**Dernière mise à jour** : 12 octobre 2026
**Dépend de** : `../NAMING_CONVENTIONS.md`, `0000-template.md`

Ce dossier regroupe les ADR **transverses**, qui engagent plusieurs composants
(transport, socle de cognition, boucles de second ordre). Les ADR propres à un
composant restent dans `docs/<composant>/adr/`.

**Filtre V0.1** (plan de refonte de la documentation) : seuls les ADR
`Accepted` décrivant un choix **réellement implémenté et toujours en vigueur**
sont conservés. Les ADR reportés ou sans implémentation ne sont pas recopiés ici
(Ils restent dans l'historique Git).

## ADR transverses

| ADR | Sujet | Statut |
| :-- | :-- | :-- |
| `ADR-003-api-http-rest.md` | API HTTP REST légère (contrôle SYNE :5181) | [Accepted] |
| `ADR-004-websocket-temps-reel.md` | WebSocket temps réel (données SYNE :5180) | [Accepted] |
| `ADR-005-primitives-actions.md` | Primitives atomiques d'action (implémenté, engineVersion 0.14.0) | [Accepted] |
| `ADR-006-systeme-inventaire.md` | Inventaire (capacité poids/slots, ressources stockables, implémenté) | [Accepted] |
| `ADR-007-means-end-reasoning.md` | Plans alternatifs par objectif | [Accepted] |
| `ADR-008-politique-reconsideration.md` | Politique de reconsidération (contrôle de saillance) | [Accepted] |
| `ADR-009-engagements-communicationnels.md` | Engagements communicationnels (promesses) | [Accepted] |

Les ADR-010 (intentions partagées) et ADR-011 (institutionnalisation) étaient
classés `Reporté V2` sans implémentation : retranchés du dossier courant le
12 octobre 2026 (historique Git).

## Modèle

Le modèle d'ADR est [`0000-template.md`](0000-template.md), identique à celui des
autres composants. Le numéro `0000` est réservé au gabarit.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 11 octobre 2026 | Création de l'index ; numérotation des ADR restés sans numéro (`ADR-XXX`, `ADR-<sujet>`) | Mise en conformité avec `../NAMING_CONVENTIONS.md` §4 |
| 12 octobre 2026 | Retrait des ADR-010 et ADR-011 (`Reporté V2`, aucune implémentation) | Filtre V0.1 : seuls conservés les ADR Accepted + implémentés + en vigueur |
