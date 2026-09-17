# ADR-003 : API HTTP REST légère

**Composant** : LIVEX (transverse — contrôle de SYNE)
**Statut** : [Accepted]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Annexe F.4 (ADR-003), §5.4.2

---

## Contexte

Le moteur doit être contrôlable par des outils externes (scripts, interface ECHOS, debugger). Le contrôle doit être simple et rapide à implémenter.

## Décision

Exposer une **API HTTP REST légère** sur le port **5181**, bound local (`127.0.0.1:5181`), avec les endpoints suivants :

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