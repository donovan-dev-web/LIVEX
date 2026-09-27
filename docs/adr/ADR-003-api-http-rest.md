# ADR-003 : API HTTP REST légère

**Composant** : LIVEX (transverse — contrôle de SYNE)
**Statut** : [Accepted — décision de transport ; routes historiques remplacées par le contrat courant]
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.4 (ADR-003), §5.4.2

---

## Contexte

Le moteur doit être contrôlable par des outils externes (scripts, interface ECHOS, debugger). Le contrôle doit être simple et rapide à implémenter.

## Décision

Exposer une **API HTTP légère** sur le port **5181** par défaut, bind local par défaut. La liste ci-dessous retranscrit la proposition d'origine de la Monographie ; elle n'est pas la liste des routes courantes :

| Méthode | Endpoint | Rôle |
| :-- | :-- | :-- |
| POST | `/start` | Démarrer une simulation |
| POST | `/stop` | Arrêter proprement |
| POST | `/tick` | Exécuter un tick |
| GET | `/status` | État courant (tick, nombre d'entités, état) |
| POST | `/save` | Forcer la sauvegarde |
| POST | `/load` | Restaurer une sauvegarde |
| POST | `/reset` | Réinitialiser à un état initial |

## Conséquences

> **Contrat courant :** les routes supportées et le cycle de contrôle sont
> `prepare`, `ready`, `start`, `pause`, `resume`, `stop`, `reset` et `status`.
> Les chemins, payloads, erreurs et règles d'initialisation sont maintenus dans
> [`docs/docs-syne/API_CONTRACTS.md`](../docs-syne/API_CONTRACTS.md) et
> [`COMMUNICATION.md`](../../COMMUNICATION.md).

### Positives
- Contrôle simple et faiblement couplé, consommable par tout client HTTP.
- JSON comme format (simple, inspectable).

### Négatives
- Pas de streaming (un contrôle à la fois).

### Risques
- Endpoints de contrôle à protéger de tout accès distant (binding local uniquement).

## Alternatives considérées

- **IPC natif / gRPC** : surpuissant pour ce besoin et lié au runtime → écarté.
- **WebSocket bidirectionnel** : réservé aux flux temps réel (ADR-004), pas au contrôle ponctuel.

## Validation / rejet

- La liste des endpoints est alignée sur la Monographie F.4 ; elle pourra être étendue en V0.1 sans rupture (évolution additive).
- Réouverture si un besoin de streaming de contrôle apparaît.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |