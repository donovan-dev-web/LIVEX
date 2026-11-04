# ADR-012 : Transport de référence HTTP et WebSocket JSON

**Composant** : PRISM
**Statut** : [Accepted]
**Dernière mise à jour** : 2 novembre 2026
**Dépend de** : ADR-008
**Source Monographie** : §2.4

---

## Contexte

PRISM doit exposer un plan de contrôle au Launcher, échanger les commandes et
résultats de ticks avec SYNE, et diffuser un flux de lecture à ECHOS et au
visualiseur. Les campagnes locales ne doivent pas payer le coût réseau.

## Décision

Le propriétaire du projet délègue le choix du transport ; la liaison de
référence est :

| Liaison | Transport et format | Usage |
| :-- | :-- | :-- |
| Plan de contrôle | HTTP local, JSON | Santé, prepare/start/pause/resume/stop/status |
| Plan de données SYNE↔PRISM | WebSocket, JSON UTF-8 | Requête tick lockstep et réponse observations/événements |
| Flux de lecture | WebSocket, JSON UTF-8 | Snapshot, deltas, entités, métriques, empreintes |
| Campagnes embarquées | Appel direct de `IWorldHost`, mêmes DTO | Bots, tests et benchmarks |

Le contrat et ses types restent indépendants des transports. Les champs sont
camelCase ; les messages portent la version du protocole et le tick. Les
commandes restent idempotentes. Les ports de départ sont 5182 (contrôle),
5183 (données) et 5184 (lecture), configurables pour éviter les conflits.

Aucun codec binaire n'est ajouté avant qu'une mesure représentative prouve que
la sérialisation JSON dépasse le budget. Les flux JSON peuvent ensuite être
compressés au niveau transport sans modifier la sémantique du contrat.

## Conséquences

### Positives
- Utilise les primitives .NET et facilite le diagnostic avec les outils
  existants.
- La même sémantique de tick est testable en réseau ou par appel direct.
- Évite une dépendance RPC/codegen et la duplication précoce des codecs.

### Négatives
- JSON est plus volumineux qu'un codec binaire pour les snapshots de chunks.
- Le lockstep réseau est soumis à la latence et à la sérialisation.

### Risques
- Le débit à 1 000 agents peut dépasser le budget. B4 et B5 mesurent les
  charges réelles avant le gel G3 ; une optimisation reste possible derrière
  le contrat stable.

## Alternatives considérées

- **gRPC** : écarté, car il ajoute une dépendance et une chaîne de génération
  sans avantage démontré pour l'interface lockstep.
- **Codec binaire propriétaire dès le départ** : écarté avant mesure, car il
  augmente le coût de débogage et de compatibilité.
- **Réseau pour les campagnes locales** : écarté, car l'appel embarqué évite
  une surcharge sans sacrifier la couverture des tests réseau.

## Validation / rejet

Décision de transport acceptée le 2 novembre 2026. B4, B5 et les tests de
contrat réseau/embarqué valident le budget et la cohérence avant G3. Un format
binaire ne sera envisagé que sur preuve de dépassement mesuré.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 2 novembre 2026 | Transport de référence choisi | Choix délégué pour avancer l'implémentation |
