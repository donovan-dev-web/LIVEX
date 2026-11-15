# Fermer G5 — procédure du run R3

**Objet** : produire le run de référence R3 (100 agents SYNE × 5 000 ticks, deux passes) et savoir, sans ambiguïté, si la porte G5 est franchie.
**Date** : 14 novembre 2026
**Durée** : 1 à 3 h de machine selon le coût moyen du tick, dont 1 à 2 min de build.
**Auteur de la mesure** : vous, en local. Aucun agent n'a besoin d'attendre : le script écrit le relevé et un brouillon de rapport, il ne reste que la rédaction.

---

## 1. Ce que R3 doit produire

| Critère de G5 | Ce que R3 doit montrer | Où c'est vérifié |
| :-- | :-- | :-- |
| `Simulation.Core` sans classe de monde | déjà établi (`PrismWorldAdapter` seule implémentation de `IWorld`) | `docs/prism/ROADMAP.md`, tests d'architecture |
| ≥ 3 chaînes sans plan scripté | le client imprime `production : N chaîne(s) terminée(s) [..]` | ligne de synthèse du client |
| **R3 reproductible** | 2 passes de 100 agents × 5 000 ticks, **même empreinte**, `5000/5000 ticks OK`, `100/100 esprits`, déplacements > 0 | ce run |
| Budget SYNE tenu | coût **propre à SYNE** (délibération + application) sous 50 ms/tick | profil client du run |

Trois précisions qui évitent de conclure à tort :

1. **« Reproductible » = deux passes identiques sur la même machine, même binaire, même graine.** L'interplateforme est hors périmètre (reportée en fin de projet, elle relève de G2).
2. **Le budget publié est joint** (`PERFORMANCE.md` §1 : « ≈ 50 ms/tick pour SYNE + PRISM »). Au 14 novembre après les optimisations d'observation, à 100 agents : aller-retour **357,4 ms**, dont **348,0 ms de tick hôte** et **23,5 ms de coût SYNE** (6,3 ms de délibération + 17,3 ms d'application). Autrement dit, la cognition tient le budget ; c'est l'hôte qui le dépasse, et cela relève de G1. R3 mesure les deux pour que la décision de porte soit prise sur des chiffres, pas sur une impression.
3. **Le run R3 n'est pas un benchmark de performance.** On ne l'instrumente pas en JSONL par tick (cela ajouterait de l'écriture disque sur 5 000 ticks) : le script relève la durée murale par passe et le profil client, ce qui suffit à situer SYNE par rapport à son budget.

---

## 2. Préparation (5 minutes)

```bash
# 1. Aucun hôte PRISM ne doit tourner : la build Release échouerait sur les binaires verrouillés.
tasklist //FI "IMAGENAME eq prism-host.exe"
# s'il en reste :
taskkill //F //IM prism-host.exe

# 2. Être sur la branche et l'état de travail attendus
git -C . status --short --branch
git -C . rev-parse --short HEAD

# 3. Binaires Release à jour (le script le fait aussi, sauf --no-build)
dotnet build prism/Prism.Host/Prism.Host.csproj -c Release --nologo -v q
dotnet build syne/Simulation.Console/Simulation.Console.csproj -c Release --nologo -v q
```

À vérifier avant de lancer :

- **Espace disque** : quelques centaines de Mo (`prism/build/g5-r3/`), supprimés à la fin sauf `--keep-work`.
- **Machine au repos** : pas de build, pas de benchmark, pas de conteneur lourd. Le run n'a pas besoin d'être rapide, mais une machine saturée peut faire expirer une passe.
- **Pas de veille** : Windows en veille coupe le run. Désactivez la veille ou gardez la machine éveillée pendant l'exécution.
- **Ne fermez pas le terminal** : le script imprime une ligne par passe et garde l'hôte en vie jusqu'au bout.

---

## 3. Option A — une commande (recommandée)

```bash
python scripts/prism-g5-r3.py
```

Ce que le script fait, dans l'ordre :

1. refuse de construire si un `prism-host.exe` tourne (sauf `--kill-host`) ;
2. construit les deux binaires Release ;
3. pour chaque passe : démarre un hôte neuf sur un port libre, attend `/health/live`, lance le client couplé avec les paramètres de R3, coupe l'hôte proprement ;
4. vérifie `5000/5000 ticks`, l'empreinte, l'incarnation (`100/100` esprits, déplacements > 0), et l'égalité des deux empreintes ;
5. écrit le relevé machine et un **brouillon de rapport** déjà rempli.

Paramètres utilisés (ceux des campagnes PRISM-106 et de la forme réduite de R3) :

| Paramètre | Valeur |
| :-- | :-- |
| Agents / ticks | `100` / `5000` |
| Graine | `7` |
| Contenu | `prism/content/minimal`, hash `793A8996…AA82` |
| Production | `stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace` |
| Checkpoint d'arrêt | désactivé (`PRISM_CHECKPOINT_ON_STOP=0`) : il n'entre ni dans l'empreinte ni dans le comportement |
| Profil client | activé (`SYNE_COUPLED_PROFILE=1`), documenté comme neutre sur l'empreinte |

Options utiles :

```bash
python scripts/prism-g5-r3.py --dry-run          # affiche les commandes, ne lance rien
python scripts/prism-g5-r3.py --no-build         # binaires déjà construits
python scripts/prism-g5-r3.py --keep-work        # garde journaux et répertoires de travail
python scripts/prism-g5-r3.py --runs 3           # une passe de plus (défaut : 2)
python scripts/prism-g5-r3.py --timeout 10800    # 3 h max par passe si la machine est lente
```

Sorties :

| Fichier | Contenu |
| :-- | :-- |
| `docs/prism/benchmarks/G5-R3-<date>-v1.json` | relevé machine : deux passes, empreintes, ticks, durées, profil client, provenance git |
| `docs/prism/G5-R3-REPORT-<date>.md` | brouillon de rapport : tableaux remplis, sections « À RÉDIGER » marquées |
| `prism/build/g5-r3/<horodatage>/passN/` | `client.out`, `client.err`, `client-profile.json`, `host.out`, `host.err`, `logs/` (supprimés sauf `--keep-work`) |

Codes de sortie :

| Code | Signification | Suite |
| --: | :-- | :-- |
| `0` | R3 produite : deux passes complètes, même empreinte | compléter le brouillon (§5) |
| `1` | échec technique (build, hôte, client) | lire `host.err` / `client.err`, relancer |
| `2` | empreintes divergentes ou run incomplet | relancer avec `--keep-work`, **ne rien publier** |
| `3` | délai dépassé | relancer avec `--timeout` plus grand, machine au repos |

Temps attendu : la mesure du 14 novembre donne ≈ 355–368 ms par aller-retour à 100 agents, soit **≈ 30 min par passe** si le coût reste stable sur 5 000 ticks. Il peut monter : les agents entrent en production et se déplacent davantage. **Comptez 30 à 90 min par passe**, donc 1 à 3 h pour les deux. Le script affiche la durée réelle de chaque passe.

---

## 4. Option B — à la main (si le script pose problème)

Deux terminaux, deux fois. Les commandes ci-dessous sont celles du script, exécutées telles quelles.

### Passe 1

**Terminal 1 — l'hôte**

```bash
cd prism
export PRISM_CHECKPOINT_ON_STOP=0
export LIVEX_SESSION_TOKEN=g5-r3-livex
dotnet prism/Prism.Host/bin/Release/net10.0/prism-host.dll \
  --control-port 8131 \
  --work-dir "$PWD/build/g5-r3/p1/work" \
  --instance-id r3-p1 \
  --log-dir "$PWD/build/g5-r3/p1/logs" \
  --correlation-id prism-g5-r3-p1
```

Attendez que l'hôte réponde (`http://127.0.0.1:8131/health/live`).

**Terminal 2 — le client couplé**

```bash
cd syne
export LIVEX_SESSION_TOKEN=g5-r3-livex
export SYNE_COUPLED_PROFILE=1
export SYNE_COUPLED_PROFILE_JSON=../prism/build/g5-r3/p1/client-profile.json
dotnet Simulation.Console/bin/Release/net10.0/Simulation.Console.dll \
  --prism-url http://127.0.0.1:8131 \
  --prism-agents 100 --prism-ticks 5000 --prism-seed 7 \
  --prism-content-hash 793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82 \
  --prism-content-dir ../prism/content/minimal \
  --production stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace \
  2>&1 | tee ../prism/build/g5-r3/p1/client.out
```

**Terminal 3 (ou le même que le terminal 1)** — arrêter l'hôte proprement :

```bash
curl.exe -s -X POST \
  -H "Content-Type: application/json" \
  -H "X-LIVEX-Session-Token: g5-r3-livex" \
  -d "{}" http://127.0.0.1:8131/control/shutdown
```

L'empreinte est la ligne `SYNE — empreinte couplée : <hex>` :

```bash
grep -oE "empreinte [^:]*: [0-9a-f]+" ../prism/build/g5-r3/p1/client.out
```

> Les journaux du client sont écrits en page de code Windows (cp1252). Pour les lire confortablement : `iconv -f CP1252 -t UTF-8 client.out`. Les motifs `grep` ci-dessus restent en ASCII exprès.

### Passe 2

Identique avec `p2`, `r3-p2` et le port `8132` (ou `8131` une fois l'hôte arrêté). **Même graine, mêmes paramètres, binaires inchangés.**

### Vérification manuelle

| Contrôle | Attendu |
| :-- | :-- |
| Fin de passe | `SYNE — boucle couplée terminée : 5000/5000 ticks OK.` |
| Incarnation | `couplage effectif : … , … déplacement(s), 100/100 esprit(s).` |
| Chaînes | `production : N chaîne(s) terminée(s) […]` avec N ≥ 3 |
| Reproductibilité | les deux `empreinte couplée` **identiques** |

Si l'un des deux runs n'atteint pas 5 000 ticks, **le run ne vaut rien pour la porte** : relancez-le. Ne reprenez pas depuis un checkpoint intermédiaire pour « finir » une passe — l'empreinte dépend de l'horizon et la porte demande deux passes pleines.

---

## 5. Après le run — ce qu'il reste à faire

### 5.1 Compléter le rapport (30 à 45 min)

Ouvrez `docs/prism/G5-R3-REPORT-<date>.md`. Les tableaux, empreintes, durées, issues et provenance sont déjà remplis. Trois sections sont marquées `À RÉDIGER` :

| Section | Ce qu'il faut y mettre |
| :-- | :-- |
| §4 Reproductibilité | ce que la comparaison prouve (même hôte, même binaire) et ce qu'elle ne prouve pas (interplateforme, reportée) |
| §5 Write-back | lecture des issues (`source_not_found`, `path_not_found`, `action_already_active`), chaînes terminées et abandons, cohérence avec la forme réduite `233d76e194c897cf` |
| §7 Décision de porte | une seule des deux options ci-dessous |

### 5.2 Trancher le critère de budget

R3 règle le critère « R3 reproductible ». Reste le quatrième : le budget. Deux issues seulement, toutes deux défendables, mais il faut choisir et l'écrire :

| Option | Formulation | Conséquence |
| :-- | :-- | :-- |
| **(a) G5 franchie pour la capacité** | « G5 franchie : R3 reproductible, 100/100 esprits, N chaînes ; coût propre à SYNE X ms/tick (< 50 ms) ; le tick hôte (≈ 348 ms) est porté par G1, avec l'ADR de budget correspondante. » | P9 se clôt, G1 reste la seule porte de performance ; il faut rédiger l'ADR qui sépare les deux budgets |
| **(b) G5 maintenue ouverte** | « G5 non franchie : le budget joint de 50 ms/tick n'est pas tenu, R3 est produite et conservée comme référence. » | P9 reste ouverte jusqu'à ce que G1 tienne le budget ; aucune ADR nécessaire |

La recommandation, au vu des mesures : **(a)**, à condition d'écrire l'ADR qui dit noir sur blanc que le budget de 50 ms est **joint** et que la part SYNE est tenue. Sans cette ADR, (b) est la lecture littérale de `PERFORMANCE.md` §1.

### 5.3 Mettre à jour le suivi (si G5 est franchie)

| Fichier | Ligne à changer |
| :-- | :-- |
| `docs/prism/G5-R3-REPORT.md` | remplacer le statut « porte non franchie » par le renvoi au nouveau rapport |
| `docs/prism/ROADMAP.md` | §« État des portes » : G5 franchie, avec l'empreinte R3 et la décision de budget ; ligne P9 du suivi d'exécution |
| `docs/prism/TESTING.md` | ligne R3 du tableau §4 : « produite le … , empreintes … × 2 » |
| `docs/governance/LIVEX-CURRENT-STATUS.md` | état des portes G1/G5 |
| `docs/prism/ISSUES.md` | PRISM-109 / PRISM-143 selon la décision ; entrée datée dans « Mises à jour » |
| Backlog | ticket #54 (PRISM-119) : la dépendance R3 est levée ; #56 (PRISM-140) devient déblocable |

Aucune empreinte PRISM n'est ré-épinglée par ce run : R3 mesure, il ne change pas le comportement.

---

## 6. Si ça échoue

| Symptôme | Cause probable | Action |
| :-- | :-- | :-- |
| `Echec de la build … fichier verrouillé` | un `prism-host.exe` tourne | `taskkill //F //IM prism-host.exe`, ou `--kill-host` |
| `l'hote PRISM n'a pas repondu a /health/live` | port pris, démarrage lent, antivirus | relancer, `--startup-timeout 300` |
| client : `path_not_found` massifs dès les premiers ticks | contenu ou hash non concordants | vérifier `--content-dir` et le hash, ne pas improviser |
| `5000/4999 ticks` ou run coupé | machine saturée, veille, mémoire | machine au repos, veille désactivée, relancer la passe |
| empreintes différentes entre les deux passes | instrumentation ajoutée, binaire modifié entre les passes, graine changée | refaire les deux passes sur le **même** binaire, sans variable d'environnement supplémentaire |
| délai dépassé | coût moyen supérieur à l'estimation | `--timeout 10800`, ou `--keep-work` pour lire où le run s'est arrêté |
| profil client absent | `SYNE_COUPLED_PROFILE` non vu par le client | relancer avec la variable exportée (le script le fait seul) |

---

## 7. Ce qu'il ne faut pas faire

- **Ne pas lancer `prism-coupled-benchmark.py` pour R3** : il jette une chauffe, exige deux runs mesurés et écrit un JSONL par tick — inutile ici, et il ajoute 30 min de chauffe.
- **Ne pas activer `PRISM_BENCHMARK_JSON`** sur ce run : 5 000 lignes de JSON par passe, pour un chiffre déjà connu par PRISM-106.
- **Ne pas changer la graine, le contenu ou la production entre les deux passes.** La reproductibilité n'a de sens qu'à paramètres identiques.
- **Ne pas ré-épingler d'empreinte dorée** après le run : R3 est une mesure, pas un changement de comportement.
- **Ne pas publier un run incomplet** comme « R3 produite ». Un `4999/5000` n'est pas R3.

---

## 8. Résumé exécutif

```bash
taskkill //F //IM prism-host.exe        # si un hôte traîne
python scripts/prism-g5-r3.py          # 1 à 3 h, sans intervention
# puis : compléter 3 sections du brouillon et trancher le budget (§5.2)
```

Deux fichiers sortent du run : le relevé machine (`docs/prism/benchmarks/G5-R3-<date>-v1.json`) et le brouillon de rapport (`docs/prism/G5-R3-REPORT-<date>.md`). Le reste est de la rédaction, pas de l'attente.
