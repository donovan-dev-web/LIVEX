# BENCHMARKS.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 25 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `../docs-syne/API_CONTRACTS.md`
**Source Monographie** : —

---

## 1. Le principe

Le script `scripts/benchmark.py` mesure le **coût réel de l'observabilité** : combien
coûte, en temps et en RAM, la pile d'analyse ECHOS branchée sur SYNE par rapport au
moteur seul.

Pour chaque cellule de la grille `(entités × ticks × seed)`, deux scénarios sont rejoués
sur le **même binaire Release** :

| Scénario | Chaîne | Sans observabilité |
| :-- | :-- | :-- |
| `syne` | `Simulation.Console --headless` | oui — pure CLI |
| `echos` | `Simulation.Console --serve` + API ECHOS (`:5000`) + ingestion WebSocket (`:5180`) pilotée via le contrôle (`:5181`) | non — ingestion complète |

Le scénario `echos` reproduit le chemin réel d'un batch : `POST /api/control/start`
→ drain jusqu'au dernier tick → `SIGINT` du serveur SYNE (fermeture propre du flux WS)
→ calibration `complete`. **Aucun rapport Markdown n'est généré** (seul le temps et la
RAM de la pipeline importent) ; la série agents Parquet est inactive par défaut.

## 2. Les métriques

| Métrique | Unité | Définition |
| :-- | :-- | :-- |
| `wall_pipeline_s` | s | Temps de la boucle de simulation seule (démarrage de la pile exclu) |
| `wall_total_s` | s | Temps mur cellule entier (démarrage API/SYNE/ingestion inclus) |
| `startup_s` | s | `wall_total_s − wall_pipeline_s` |
| `peak_rss_kb` | Ko | **SYNE seul** : pic du processus CLI. **ECHOS** : somme des pics API + SYNE + ingestion |
| `tps` | t/s | ticks ÷ temps pipeline |

La RAM est lue dans `ru_maxrss` via `os.wait4` au moment du reaping de chaque enfant
direct — **aucune dépendance externe** (`psutil` non requis). Les trois processus ECHOS
sont lancés en `start_new_session=True` (groupe de processus), ce qui permet un arrêt
déterministe (SIGTERM → SIGKILL) sans orphelins.

## 3. Exécution

```bash
echos/.venv/bin/python scripts/benchmark.py                 # grille complète
echos/.venv/bin/python scripts/benchmark.py --quick          # entités×ticks réduits, 1 seed
echos/.venv/bin/python scripts/benchmark.py --entities 20,100 --ticks 400,1000 --seeds 12345
```

Options utiles :

| Option | Défaut | Effet |
| :-- | :-- | :-- |
| `--ticks-per-second` | `50` | cadence SYNE du scénario `echos` |
| `--analysis-every` | `1` | planifie l'analyse ECHOS 1 tick sur N (filtre le coût RAM/CPU) |
| `--parquet` / `--no-parquet` | inactif | active la série agents Parquet dans l'ingestion |
| `--cell-timeout` | `600` s | abandon d'une cellule bloquée |
| `--database` | temp | réutilise une base SQLite entre les cellules |
| `--output-dir` | `echos/data/benchmarks` | répertoire des rapports |

Sorties : `benchmark-<horodatage>.csv` (brut, toutes les cellules), `.md` (moyennes par
entités × ticks + détail seed par seed), `-summary.json` (synthèse + ratios overhead).
Le répertoire de travail `.bench-work-<horodatage>/` conserve les configs injectées et la
base temporaire ; il est reproductible (`--quick` ≠ supplément de hasard, seeds fixes
default `12345,999,7`).

## 4. Lecture des résultats

Deux colonnes disent l'essentiel :

- **`tps`** : débit intrinsèque de chaque chaîne. SYNE headless dépasse couramment le
  millier de ticks/s ; la pipeline ECHOS est bornée par `--ticks-per-second` (le
  « coût ECHOS (×) » mêle donc pacing et overhead — comparer les t/s en priorité).
- **`peak_rss_kb`** : l'écart RPG-observabilité. Sur une machine de référence (ex. grille
  `--quick`, seed 12345) : SYNE seul ≈ **50 Mo**, pile ECHOS complète ≈ **250–280 Mo**
  répartis entre API (~95–115 Mo), ingestion (~80 Mo) et serveur SYNE observé (~75–85 Mo).

> Les chiffres ci-dessus et exemples sont **indicatifs** (dépendent du matériel, de
> `.NET` et de la charge). La grille complète est rejouable à volonté via le script.

## 5. Bonnes pratiques

- **Compiler en Release avant de mesurer** ; ne pas comparer deux builds différents.
- Libérer les ports `5000/5180/5181` (le script assume des ports libres).
- Laisser tourner sans UI sur la machine pour ne pas fausser la RAM.
- Pour isoler le coût *analyse* vs coût *ingestion*, comparer `--analysis-every 1` et
  `--analysis-every 10` à ticks-per-second identique.
- Le scénario `syne` et le scénario `echos` exécutent **exactement le même monde**
  (mêmes entités/ticks/seed) : tout écart de t/s est imputable à l'observabilité.