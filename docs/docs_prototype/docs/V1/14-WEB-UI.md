# Web UI (Phase 6)

Interface web de visualisation et de rapport, consommant le Simulation Core et
l'Analyzer. Techno retenue : **React + TypeScript**.

## 1. Choix technologique (justifié)

- **React + TypeScript** (et non Vue/Angular) : écosystème maximal pour les
  tableaux de bord, support natif de `canvas`/`SVG` pour la vue monde 2D, bonne
  prise en charge des WebSockets, et typage fort qui réutilise le contrat JSON
  `camelCase` du transport.
- **Charting** : Recharts ou Chart.js pour les séries temporelles (consommation,
  population, métriques d'émergence).
- **Client WebSocket** natif (ou lib légère) pour la vue live.
- **Client HTTP** (fetch) pour l'API REST de l'Analyzer.
- Aucune logique métier : tout provient du flux live ou de l'API Analyzer.

## 2. Double flux (décision Phase 6)

Le Web UI consomme deux sources :

```text
Simulation Core --(WebSocket: snapshot + events)--> Web UI  (vue live : positions, actions)
Analyzer (.NET) --(REST API : métriques, émergence)--> Web UI  (rapports, graphes)
```

- **Flux direct (WebSocket simulation)** : reconstruction temps réel du monde
  (positions des agents, ressources, actions en cours) — utile pour la vue live.
- **Flux Analyzer (REST)** : métriques agrégées, mesures d'émergence, rapports
  et comparaison de runs — calculées côté Analyzer pour ne pas surcharger le client.

## 3. Implémentation (Phase 6 — terminée)

Stack : **Vite + React 18 + TypeScript + Recharts**. Aucune logique métier.

```text
web-ui/
├── package.json, tsconfig.json, vite.config.ts, index.html
└── src/
    ├── types.ts                     # miroir des DTO transport + réponses Analyzer
    ├── config.ts                    # SIM_URL / ANALYZER_URL (env Vite surchargeables)
    ├── hooks/
    │   ├── useSimulationSocket.ts   # flux direct : WebSocket → WorldSnapshot/Event
    │   └── useRuns.ts               # flux Analyzer : /api/runs, /api/runs/{id}
    ├── components/
    │   ├── WorldView.tsx            # rendu canvas temps réel (agents colorés par action)
    │   ├── MetricsPanel.tsx         # graphes Recharts (population, entropie, besoins, déplétion)
    │   └── RunComparison.tsx        # /api/compare (reproductibilité)
    └── App.tsx                      # assemblage + saisie des URLs / run id
```

- **Vue monde 2D** (`WorldView`, canvas) : agents (couleur = action), ressources
  (eau/ nourriture), bornée dynamiquement depuis le snapshot.
- **Panneau de rapports** (`MetricsPanel`) : population & entropie de comportement,
  besoins moyens, **déplétion des ressources** (barres) — via l'API Analyzer.
- **Comparateur de runs** (`RunComparison`) : deux run id → `/api/compare`.
- **Contrôles** : champs URL WebSocket, URL Analyzer et run id (ou `.env`
  `VITE_SIM_URL` / `VITE_ANALYZER_URL`).

Lancement :
```bash
cd web-ui
npm install
npm run dev      # http://localhost:5173
npm run build    # dist/ (vérifié : tsc --noEmit + vite build OK)
```

## 4. Contraintes

- Aucune décision de comportement : la Web UI observe et rapporte uniquement.
- Découplée du renderer 3D (Godot, Phase 10) : elle partage le même flux de
  transport mais reste une application web autonome.
- Réutilise le contrat `WorldSnapshot` / `ExternalEvent` (`docs/V1/09-EVENTS-API.md`)
  et l'API REST de l'Analyzer (`docs/V1/13-ANALYZER.md`).

## 5. Hors périmètre

- Logique de simulation, calcul de métriques lourdes (délégués à l'Analyzer).
- Rendu 3D (cf. Phase 10 Godot).
