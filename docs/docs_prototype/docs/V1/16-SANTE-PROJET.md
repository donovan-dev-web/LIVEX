# Document de santé du projet — Tests & Couverture

**But** : document vivant servant de tableau de bord de la santé du code (tests, couverture,
det dans le temps tout au long du projet. À mettre à jour à chaque phase / PR notable
(rejouer les commandes de mesure et ajouter une ligne dans l'*Historique*).

**Cible de couverture** : **≥ 80 % de lignes** sur les bibliothèques métier
(`Simulation.Core`, `Analyzer.Core`). Les projets hôtes (`Simulation.Console`,
`Analyzer.Service`) et les projets de test sont hors cible (points d'entrée, pas de
logique métier).

---

## 1. Comment mesurer (reproductible)

Les filtres `ci/coverlet.sim.runsettings` / `ci/coverlet.an.runsettings` isolent chaque
bibliothèque et **excluent** les projets hôtes et de test (sinon la couverture est noyée
par `Analyzer.Service` / `Simulation.Console`, non unit-testés).

```bash
# Simulation.Core
dotnet test simulation-core/Simulation.Core.Tests/Simulation.Core.Tests.csproj \
  --settings ci/coverlet.sim.runsettings --collect:"XPlat Code Coverage"

# Analyzer.Core
dotnet test analyzer/Analyzer.Tests/Analyzer.Tests.csproj \
  --settings ci/coverlet.an.runsettings --collect:"XPlat Code Coverage"
```

Le rapport `coverage.cobertura.xml` est généré dans `*/TestResults/*/`. La couverture
branches est lue depuis l'attribut `branch-rate`.

Web UI : 11 tests `Vitest` (`react/vitest`, jsdom) couvrant `ControlPanel`,
`EmergenceReportPanel`, `useControl`, `useEmergence`, `useRunMetrics` (polling
temps réel vérifié) — voir §2. Lint ESLint + format Prettier (`npm run lint` /
`npm run format:check`).

---

## 2. Snapshot — 2026-08-29 (mise à jour)

| Bibliothèque      | Tests | Lignes couv. | Branches couv. | Cible ≥80 % ? | Statut |
|-------------------|------:|-------------:|---------------:|:-------------:|--------|
| `Simulation.Core` |    38 | 90,3 % (909/1007) | 76,4 % (359/470) | ✅ 90,3 ≥ 80 | **OK** |
| `Analyzer.Core`    |    18 | 85,0 % (518/609) | 67,9 % (182/268) | ✅ 85,0 ≥ 80 | OK |
| **Total C#**       |  **56** | — | — | — | — |

Web UI (TypeScript) : typecheck ✅, **tests automatisés ✅ (11 tests Vitest)** couvrant
`ControlPanel`, `EmergenceReportPanel`, `useControl`, `useEmergence`, `useRunMetrics`
(polling temps réel vérifié).

---

## 3. Points à renforcer (`Simulation.Core`, 90,3 %)

Les principaux points faibles identifiés le 2026-08-29 sont **désormais couverts** par
des tests unitaires ciblés (`ActionSystemTests`, `WebSocketServerControlTests`) :
`ActionSystem` (boire/attaquer/manger/parler/explorer/fuir/…) et les branches de
contrôle `StartRun`/`ResetRun` (seed fournie/absente, runId fourni/vide).

Classes restant à couverture modérée (non bloquant, total déjà ≥ 80 % lignes) :

| Classe | Fichier | Note |
|--------|---------|------|
| `Vector2` | `Types.cs` | opérateurs/helpers (distance, normalisation) |
| `PhysiologySystem` | `Systems/PhysiologySystem.cs` | |
| `WebSocketServer.WsClient` | `Transport/WebSocketServer.cs` | réception/erreurs de frame (couvert en intégration via `TransportServerTests`) |
| Types événementiels + obstacles (`CircleObstacle`, `RectObstacle`) | `Events.cs` / `Model.cs` | records de données triviaux ; `AgentDiedEvent`/`ResourceDepletedEvent` désormais émis par `ActionSystemTests` |

→ La cible **lignes ≥ 80 %** est atteinte. La couverture **branches** (76,4 % sur
`Simulation.Core`) reste perfectible : cibler les branchements de décision dans
`ActionSystem`/`PhysiologySystem`/`DecisionSystem` et le handshake WebSocket.

---

## 4. Nouveau code de ce cycle et sa couverture

| Ajout | Couverture | Commentaire |
|-------|-----------|-------------|
| Contrôle moteur REST (`WebSocketServer.StartRun`/`ResetRun`/`Pause`/`Resume`/`GetState`) | partielle | `TransportServerTests` appelle `StartRun` ; branches `seed`/`reset` non unit-testées |
| `WorldSnapshot.RunId` + indexation dynamique `SimClient` | intégration | exercée en bout-en-bout (sim ↔ analyzer), pas d'unit test dédié |
| `EmergenceAnalyzer` (rapport d'émergence) | 2 tests | `convergence` + `empty` ; les autres détecteurs exercés en intégration, pas unit-testés |
| `ControlPanel` / `useControl` / `useEmergence` / `useRunMetrics` (Web UI) | **11 tests Vitest** | rendu + hooks, polling temps réel vérifié (`ControlPanel.test.tsx`, `EmergenceReportPanel.test.tsx`, `useControl.test.ts`, `useRuns.test.ts`) |

---

## 5. Recommandations

1. **`Simulation.Core` → ≥ 80 %** : ✅ atteint (90,3 %). Tests ciblés ajoutés
    (`ActionSystemTests`, `WebSocketServerControlTests`) couvrant `Drink` (régression
    `DrinkThirstReduction`), `Attack`, `Eat`, `Gather`, `Talk`, `Rest`, `Flee`, `MoveTo`
    et les branches `StartRun`/`ResetRun`.
2. **Branches (~76 % sur `Simulation.Core`)** : cibler les branchements de décision dans
    les systèmes (`ActionSystem`, `PhysiologySystem`, `DecisionSystem`) et le handshake WebSocket.
3. **Web UI** : ✅ `Vitest` ajouté (11 tests) couvrant `ControlPanel`, `EmergenceReportPanel`,
    `useControl`, `useEmergence`, `useRunMetrics` (polling vérifié). Optionnel : ajouter
    `@vitest/coverage-v8` pour une mesure de couverture UI.
4. **CI** (Phase 11) : ✅ automatisé — `.github/workflows/ci.yml` exécute
    `dotnet test --collect:"XPlat Code Coverage"` sur les deux solutions avec **seuil
    `Threshold=80` lignes** (`ThresholdStat=total`) dans `ci/coverlet.*.runsettings` :
    le build échoue si `Simulation.Core` ou `Analyzer.Core` < 80 % de lignes. Ce seuil a
    été validé localement (90,3 % et 85,0 %, ≥ 80 % → passage).

---

## 6. Historique des mesures

| Date       | `Simulation.Core` (lignes) | `Analyzer.Core` (lignes) | Tests totaux | Branches (S/A) | Note |
|------------|---------------------------:|--------------------------:|-------------:|---------------:|------|
| 2026-08-29 | 79,3 % | 85,0 % | 36 | 65,5 % / 67,9 % | Phase 9 + contrôle UI (API REST) + rapport d'émergence (`EmergenceAnalyzer`) |
| 2026-08-29 | 90,3 % | 85,0 % | 56 (+11 UI) | 76,4 % / 67,9 % | Tests ciblés C# `ActionSystem`/`WebSocketServer` + **Vitest UI (11)** → `Simulation.Core` franchit 80 % |
| 2026-08-30 | 90,6 % | ≥ 80 % ✅ | 56 (+11 UI) | — | **Phase 11 CI/CD** : couverture seuillée ≥ 80 % (CI), lint/format C# + TS, Docker, release (couverture inchangée — `dotnet format` de style uniquement ; +0,3 pt lignes suite à la normalisation/historique) |

*Règle de mise à jour* : à chaque phase, rejouer §1, reporter les chiffres dans le
snapshot §2 et ajouter une ligne ici (avec le delta vs la mesure précédente et la cause
du changement : nouveau code, nouveau test, refactor).
