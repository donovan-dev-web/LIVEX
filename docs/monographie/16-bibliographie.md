# Bibliographie et lectures : ce que ce livre doit à d'autres

Aucune référence ne vaut validation externe des résultats de LIVEX. Les sources citées nomment des lignées de pensée, non des preuves importées. Elles servent à situer le projet, à nommer ses dettes et à reconnaître ce qu'il refuse.

Il faut lire cette bibliographie comme une carte d'influences, non comme un appareil de citations savantes. Chaque entrée correspond à une dette explicite du projet : une idée reprise, une méthode adaptée, ou une position refusée. Le lecteur qui souhaite situer LIVEX dans son paysage intellectuel trouvera ici les repères ; celui qui cherche des preuves externes ne les trouvera pas, car le livre ne prétend pas en importer.

## 1. Artificial Life

- Langton, C. (1987) : l'Artificial Life comme horizon, la formule « la vie telle qu'elle pourrait être ».
- Conway, J. (1970) : le *Game of Life*, les automates cellulaires comme preuve qu'un petit jeu de règles produit des dynamiques riches.
- Dorigo, M. (1992) : les fourmis artificielles, l'optimisation par colonies.
- Sims, K. (1994) : les créatures virtuelles, la morphologie émergente.

**Dette du projet.** L'audace de ne pas programmer le résultat, et l'idée qu'un environnement riche est un préalable à l'inattendu.

## 2. Systèmes complexes

- Holland, J. (1998) : *Emergence: From Chaos to Order*, l'émergence et l'adaptation.
- Gell-Mann, M. : complexité et auto-organisation.
- Santa Fe Institute : les fondements de la science des systèmes complexes.

**Dette du projet.** La notion de rétroaction, et l'exigence de mesurer sans perturber un système dont les résultats modifient leurs propres conditions.

## 3. Simulation multi-agents

- Epstein, J. et Axtell, R. (1996) : *Sugarscape*, migration, commerce et conflit à partir de règles simples et de ressources limitées.

**Dette du projet.** La preuve miniature qu'un environnement contraint suffit à faire naître du social, complétée par une cognition faillible.

## 4. Intelligence collective

- Bonabeau, E., Dorigo, M. et Theraulaz, G. (1999) : *Swarm Intelligence*, la robustesse sans chef.

**Dette du projet.** L'idée d'une robustesse sans autorité, déplacée du suivi de voisins vers la décision individuelle.

## 5. Architectures cognitives

- Rao, A. et Georgeff, M. (1995) : l'architecture BDI (croyances, désirs, intentions).

**Dette du projet.** Le cadre organisationnel du raisonnement, opérationnalisé par une utilité transparente.

## 6. Agents génératifs

- Park, J. S. et al. (2023) : *Generative Agents*, la richesse sociale avec modèles de langage, et ses limites (coût, non-reproductibilité, intraçabilité, cohérence fragile) qui justifient le refus du modèle opaque au cœur du monde.

**Dette du projet.** Une leçon en creux : ce qu'il faut refuser pour garder la traçabilité et la reproductibilité.

## 7. Standards et instruments

- Keep a Changelog et SemVer : versionner sans mentir, format et compatibilité.
- xoshiro256** (Blackman et Vigna) : générateur pseudo-aléatoire rapide et sérialisable ; splitmix64 pour l'initialisation.
- FIPS 180-4 : le hachage SHA-256, utilisé pour les empreintes et la reproductibilité.
- RFC 4180 : le format CSV, utilisé pour les exports.

**Dette du projet.** Des conventions techniques adoptées pour garantir la reproductibilité et l'interopérabilité, jamais comme arguments d'autorité.

## 8. Stacks et outils

Ces technologies sont des instruments, jamais des arguments d'autorité.

| Composant | Technologies |
| :-- | :-- |
| SYNE | .NET (C#), xUnit |
| ECHOS | Python, FastAPI, NumPy, Pandas, SciPy, NetworkX, SQLite, Parquet (PyArrow), pytest |
| PRISM | Unreal Engine, plugin C++ |
| Launcher | .NET, Avalonia |
| syne-mock | Node.js |
| Installateur | spécification seule, technologies à trancher |

## 9. Lectures complémentaires

- La **snapshot historique** de conception (document racine de la monographie d'origine) : conception initiale, moteurs graphiques encore ouverts, non prescriptive.
- La **documentation par composant** du dépôt : le détail opératoire (installation, configuration, contrats, procédures).
- Les **scripts de campagnes** rejouables : ils permettent de refaire les enquêtes décrites au chapitre 12.
- Les **décisions d'architecture** conservées : elles consignent les choix réellement implémentés et toujours en vigueur.

## 10. Avertissement

Les références ci-dessus servent à nommer des influences. Elles n'établissent pas que les phénomènes observés dans LIVEX aient un statut scientifique comparable à ceux des travaux cités. Toute correspondance entre les concepts du projet (besoins, croyances, groupe) et les disciplines des sciences humaines reste approximative et doit être maniée avec prudence.

Ce livre est le récit ; ces sources sont l'atelier.
