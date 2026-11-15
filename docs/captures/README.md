# Captures et animations de présentation

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 15 novembre 2026

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
| `launcher-prism.png` | Vue PRISM dans le Launcher (V1, 27/09/2026) |
| `prism-viewer-embedded.png` | Viewer PRISM V2 en mode embarqué (monde 256×256, huit agents, 15/11/2026) |
| `echos-relations.png` | Vue des relations observées dans ECHOS |
| `echos-agents.png` | Vue des agents observés dans ECHOS |
| `Installer_home.png` | Écran d'accueil de l'assistant d'installation LIVEX 1.0.0 |
| `installer_ChoixComposant.png` | Écran de sélection des composants de l'assistant LIVEX 1.0.0 |
| `livex-banner.svg` | Bannière du projet |

Les deux fichiers de l'installateur conservent leurs noms historiques (`Installer_home.png`,
`installer_ChoixComposant.png`) : le README racine les référence tels quels.

`prism-viewer-embedded.png` est une capture réelle du viewer V2, produite le 15 novembre
2026 en mode embarqué avec le module `--screenshot` (monde 256×256, huit agents, HUD de
débogage). Elle documente l'interface livrée ; elle ne remplace ni le relevé B8 publiable,
ni la validation graphique Linux, qui restent à produire. Les autres captures du Launcher
et d'ECHOS décrivent la pile V1 ou des versions de travail.

## Captures de l'installateur

Les deux captures montrent des écrans différents du même assistant : l'accueil
et le choix des composants. Elles illustrent le parcours public dans le README
et la page de téléchargement ; ce ne sont pas des variantes Windows et Linux.
Les captures du Launcher et d'ECHOS décrivent la pile V1 et ne représentent pas
le viewer PRISM V2.

## Animations

Le dépôt n'embarque aucune animation GIF : les placeholders provisoires qui
y figuraient ont été supprimés. Si une animation est un jour nécessaire, elle
rejoint ce dossier — au format WebP ou MP4 de préférence, pour rester légère
sur le web.
