# THIRD-PARTY-NOTICES.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 19 octobre 2026
**Dépend de** : `LICENSE`, `NOTICE`

---

Ce fichier recense les composants tiers utilisés par LIVEX et les fichiers
dont les droits ne relèvent pas du projet. Il ne remplace pas les textes de
licence des éditeurs, qui restent seuls opposables.

Le code propre à LIVEX couvert par la GPL est celui de **SYNE**, **ECHOS**,
du **Launcher**, de **syne-mock** et du **site vitrine** : il est distribué
sous GNU GPL v3.0 ou ultérieure (`GPL-3.0-or-later`, voir `LICENSE` et
`NOTICE`). Les dépendances listées ci-dessous sont compatibles avec la
GPLv3 : MIT et BSD-3-Clause sont compatibles ; Apache-2.0 est compatible avec
la GPLv3 dans le sens Apache-2.0 → GPLv3.

## 1. Fichiers et composants non couverts par la GPL du projet

### Projet Unreal PRISM et plugin PRISM-LDK

`prism/` est un projet Unreal Engine. Il n'est **pas** distribué sous la GPL
du projet : aucun fichier de `prism/` — code source du plugin PRISM-LDK,
assets, configurations, projet hôte — n'est couvert par
`GPL-3.0-or-later`. Son utilisation et sa distribution restent soumises à
l'Unreal Engine End User License Agreement d'Epic Games.

Les fichiers suivants, issus des modèles de projet Unreal, portent la mention
`Copyright Epic Games, Inc. All Rights Reserved.` et ne sont pas revendiqués
comme œuvre du projet :

- `prism/LDK/Source/LDK.Target.cs`
- `prism/LDK/Source/LDKEditor.Target.cs`
- `prism/LDK/Source/LDK/LDK.Build.cs`
- `prism/LDK/Source/LDK/LDK.cpp`
- `prism/LDK/Source/LDK/LDK.h`
- `prism/LDK/Plugins/PrismLdk/Source/PrismLdk/Public/PrismLdk.h`

Les autres fichiers de `prism/` sont des œuvres du projet, mais restent hors
du périmètre de la GPL et soumis aux conditions d'Epic Games applicables à
Unreal Engine. Ils ne portent volontairement pas d'en-tête SPDX
`GPL-3.0-or-later`.

### Polices auto-hébergées

| Police | Fichiers | Licence |
| :-- | :-- | :-- |
| Instrument Serif | `site/src/assets/fonts/instrument-serif-latin-400-normal.woff2`, `instrument-serif-latin-400-italic.woff2` | SIL Open Font License 1.1 |
| JetBrains Mono | `site/src/assets/fonts/jetbrains-mono-latin-400-normal.woff2` | SIL Open Font License 1.1 |

Le texte de la SIL Open Font License 1.1 est fourni dans
`site/src/assets/fonts/OFL.txt` et publié avec le site dans
`site/public/fonts/OFL.txt`.

## 2. Dépendances directes

Les versions indiquées sont celles déclarées par les manifestes du dépôt.
Les dépendances transitives sont décrites par les fichiers de verrouillage
(`package-lock.json`, résolution NuGet, `requirements.txt`) ; une distribution
binaire doit embarquer l'ensemble des notices correspondantes.

### SYNE (.NET)

| Paquet | Version | Licence |
| :-- | :-- | :-- |
| Microsoft.Data.Sqlite | 10.0.12 | MIT |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT |
| xunit | 2.9.2 | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 |
| coverlet.collector | 6.0.2 | MIT |

### Launcher (.NET / Avalonia)

| Paquet | Version | Licence |
| :-- | :-- | :-- |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Diagnostics, Avalonia.Headless, Avalonia.Headless.XUnit | 11.3.22 | MIT |
| Avalonia.Svg | 11.3.0 | MIT |
| Markdown.Avalonia | 11.0.3 | MIT |
| LiveChartsCore.SkiaSharpView.Avalonia | 2.0.5 | MIT |
| System.IO.Hashing | 9.0.0 | MIT |
| Tmds.DBus.Protocol | 0.95.1 | MIT |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| xunit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.0 | Apache-2.0 |
| coverlet.collector | 6.0.4 | MIT |

### ECHOS (Python)

| Paquet | Version | Licence |
| :-- | :-- | :-- |
| fastapi | >=0.115,<1.0 | MIT |
| uvicorn | >=0.30,<1.0 | BSD-3-Clause |
| pydantic | >=2.8,<3.0 | MIT |
| httpx | >=0.27,<1.0 | BSD-3-Clause |
| websockets | >=13,<16 | BSD-3-Clause |
| pyarrow | >=20,<26 | Apache-2.0 |
| pytest | >=8.3,<9.0 | MIT |
| pytest-cov | >=5.0,<6.0 | MIT |
| flake8 | >=7.1,<8.0 | MIT |

### syne-mock (Node.js)

| Paquet | Version | Licence |
| :-- | :-- | :-- |
| ws | ^8.18.0 | MIT |
| eslint | ^10.13.0 | MIT |
| prettier | ^3.9.10 | MIT |

### Site vitrine (Astro)

| Paquet | Version | Licence |
| :-- | :-- | :-- |
| astro | ^7.3.8 | MIT |
| @astrojs/rss | ^4.0.19 | MIT |
| @astrojs/sitemap | ^3.7.4 | MIT |
| @astrojs/check | ^0.9.10 | MIT |
| @astrojs/markdown-satteri | ^0.4.3 | MIT |
| pagefind | ^1.5.2 | MIT |
| typescript | ^6.0.3 | Apache-2.0 |

## 3. Contenus du dépôt

Sauf mention contraire, les textes, schémas et illustrations du site sont
placés sous CC BY 4.0 (voir la page Licence du site). Les marques et logos
LIVEX, SYNE, ECHOS et PRISM restent réservés à un usage descriptif.

Le logo GPLv3 (`docs/assets/gplv3-or-later.png`) provient de la Free Software
Foundation et est utilisé pour identifier la licence du projet.

## 4. Maintenance de ce fichier

Mettre à jour ce fichier à chaque ajout ou mise à jour significative d'une
dépendance directe, et avant toute distribution binaire (installeur, archive,
paquet NuGet/PyPI/npm). Les textes de licence des dépendances doivent alors
être joints à la distribution. Toute évolution du périmètre GPL — notamment
un changement de licence du projet Unreal PRISM — doit être répercutée ici,
dans `NOTICE` et dans les pages Licence du site.
