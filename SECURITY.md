# SECURITY.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `COMMUNICATION.md`

---

## 1. Périmètre

LIVEX est un **projet de recherche en simulation multi-agents**. Les services et le mock sont destinés au développement local ; PRISM est le projet Unreal final qui intègre le plugin PRISM-LDK (`PrismLdk`). Ne présumez pas que les protections locales de SYNE ou du mock sécurisent automatiquement une distribution du projet PRISM.

## 2. Bindings réseau (V0.1)

| Service | Port | Binding |
| :-- | :-- | :-- |
| WebSocket SYNE (données) | 5180 | local uniquement (`127.0.0.1`) |
| HTTP SYNE (contrôle) | 5181 | local uniquement |
| HTTP ECHOS (API REST) | 5000 | local uniquement |
| syne-mock HTTP control | 5181 par défaut | localhost par défaut ; config JSON |
| syne-mock WebSocket | 5180 par défaut | localhost par défaut ; config JSON |

**Règle** : aucun binding réseau ouvert sur l'extérieur sans décision explicite. Les serveurs de développement, dont le mock, ne constituent pas des services sécurisés pour exposition publique. En cas d'ouverture, ajouter authentification, validation des entrées et durcissement avant publication.

## 3. Signalement de vulnérabilités

Aucune faille connue en V0.1 (serveurs locaux). Toute vulnérabilité découverte doit être signalée **avant** discussion publique :

1. Ouvrir une issue GitHub **privée** (ou contacter le mainteneur) — ne pas exposer la faille en issue publique.
2. Décrire : version, composant, port concerné, reproduction minimale, impact.
3. Accusé de réception sous 5 jours ouvrés ; correctif selon la gravité (hotfix si critique, cf. `GITFLOW.md`).

## 4. Secrets

- **Aucun secret** (clés, tokens) ne doit être versionné dans le dépôt.
- Configuration sensible via variables d'environnement locales uniquement (ex. `.env.*` hors git).
- Les fichiers de log et de persistance (SQLite, Parquet, JSON) ne doivent pas contenir d'informations personnelles.

## 5. Dépendances

- Mises à jour régulières des dépendances (.NET, npm, pip et Unreal Engine/plugins) via les PR de dépendances et le pipeline CI.
- À la merci d'une CVE sur un paquet : escalader en issue de sécurité prioritaire (label `priority:P0`, voir `docs/governance/ISSUES.md`).

## 6. Responsable

Mainteneur du dépôt (développeur solo pour l'instant). Une fiche de sécurité consolidée sera ajoutée si le projet est publié publiquement.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | — |