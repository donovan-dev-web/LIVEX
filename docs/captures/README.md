# Captures et animations de présentation

**Composant** : LIVEX (général)
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 octobre 2026

Ce dossier regroupe les captures d'écran de présentation du projet LIVEX, utilisées dans le `README.md` racine et réutilisables dans la documentation. Le site vitrine ne sert pas ce dossier tel quel : il rend ses propres visuels optimisés (`site/src/assets/captures/`).

## Conventions

- Format image : PNG pour les captures statiques, SVG pour les schémas.
- Largeur recommandée : 1280 px pour les captures pleine largeur.
- Nommer les fichiers en `kebab-case`, avec un préfixe de composant (`launcher-`, `echos-`, `prism-`, `syne-`).
- Ne pas stocker ici de données de run, de journaux ni d'artefacts de build.

## Captures statiques

| Fichier | Sujet |
|:--|:--|
| `launcher-home.png` | Accueil du Launcher LIVEX |
| `launcher-experiences.png` | Vue des expériences dans le Launcher |
| `launcher-prism.png` | Vue PRISM dans le Launcher |
| `echos-relations.png` | Vue des relations observées dans ECHOS |
| `echos-agents.png` | Vue des agents observés dans ECHOS |
| `livex-banner.svg` | Bannière du projet |

## Animations

Le dépôt n'embarque aucune animation GIF : les placeholders provisoires qui
y figuraient ont été supprimés. Si une animation est un jour nécessaire, elle
rejoint ce dossier — au format WebP ou MP4 de préférence, pour rester légère
sur le web.
