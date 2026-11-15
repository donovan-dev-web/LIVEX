# Captures — kit média du site

**Composant** : Site vitrine (LIVEX)
**Statut** : [DRAFT]
**Dernière mise à jour** : 15 novembre 2026

Ce dossier est le **kit média brut** du site vitrine : les captures d'écran
originales, en PNG, telles qu'elles peuvent être téléchargées et reprises par
un journaliste ou un partenaire (voir la page « Kit média »).

Ce que le site **affiche**, ce sont les versions optimisées de
`site/src/assets/captures/` (WebP, dimensions explicites, chargement
différé) : ce dossier n'est jamais affiché tel quel.

## Contenu

| Fichier | Sujet |
|:--|:--|
| `launcher-home.png` | Accueil du Launcher LIVEX |
| `launcher-experiences.png` | Vue des expériences dans le Launcher |
| `launcher-prism.png` | Vue PRISM dans le Launcher (V1, 27/09/2026) |
| `prism-viewer-embedded.png` | Viewer PRISM V2 en mode embarqué (monde 256×256, huit agents, 15/11/2026) |
| `echos-relations.png` | Vue des relations observées dans ECHOS |
| `echos-agents.png` | Vue des agents observés dans ECHOS |
| `livex-banner.svg` | Bannière du projet |

`prism-viewer-embedded.png` est une capture réelle du viewer V2, produite le 15 novembre
2026 en mode embarqué avec le module `--screenshot` (monde 256×256, huit agents). Elle
documente l'interface livrée ; elle ne remplace ni le relevé B8 ni la validation graphique
Linux (voir `docs/governance/LIVEX-CURRENT-STATUS.md`). Le visuel PRISM du Launcher
(`launcher-prism.png`) date de la V1.

## Animations

Aucune animation GIF n'est versionnée dans le site. Si le projet en a besoin un
jour, les animations restent dans le dépôt (`docs/captures/`) et sont servies
au site sous forme optimisée (WebP ou MP4) — jamais en GIF.
