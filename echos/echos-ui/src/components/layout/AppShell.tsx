import { useEffect } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useLiveStore, useRuns, useRunsStore, useSelectedRunId } from '../../store'
import { connect, disconnect } from '../../ws/realtime'
import { useLoadRuns } from '../../hooks/useData'

const NAV_ITEMS = [
  { to: '/dashboard', label: 'Tableau de bord' },
  { to: '/explore', label: 'Exploration' },
  { to: '/social-graph', label: 'Graphe social' },
  { to: '/analysis', label: 'Analyse & causalité' },
  { to: '/control', label: 'Pilotage & calibration' },
  { to: '/log', label: 'Journal & limites' },
]

export function AppShell() {
  const wsState = useLiveStore((s) => s.wsState)
  const wsError = useLiveStore((s) => s.wsError)
  const live = useLiveStore((s) => s.live)
  const runs = useRuns()
  const runId = useSelectedRunId()
  const runsError = useRunsStore((s) => s.error)
  const selectRun = useRunsStore((s) => s.selectRun)
  const navigate = useNavigate()

  // Chargement unique : la liste des runs est la propriété de la coquille, pas
  // de chaque écran (une requête par écran monté auparavant).
  useLoadRuns()

  useEffect(() => {
    connect()
    // Sans ce nettoyage, la socket et son timer de reconnexion survivaient au
    // démontage et l'interface continuait de sonder en arrière-plan.
    return () => disconnect()
  }, [])

  const wsClass = wsState === 'connected' ? 'ok' : wsState === 'connecting' ? 'warn' : 'err'
  const wsLabel =
    wsState === 'connected'
      ? `WS connecté · tick ${live?.tick ?? '—'}`
      : wsState === 'connecting'
        ? 'WS connexion…'
        : 'WS déconnecté'

  return (
    <div className="app-shell">
      <header className="topbar">
        <button className="btn topbar__brand" onClick={() => navigate('/dashboard')} type="button">
          ECHOS
        </button>
        <span className="topbar__tick">Observation LIVEX · interface V0.1</span>
        <label className="topbar__run" htmlFor="run-picker">
          <span className="visually-hidden">Run affiché</span>
          <select
            id="run-picker"
            className="select mono"
            value={runId ?? ''}
            disabled={runs.length === 0}
            onChange={(event) => selectRun(event.target.value)}
          >
            {runs.length === 0 ? <option value="">Aucun run</option> : null}
            {runs.map((run) => (
              <option key={run.run_id} value={run.run_id}>
                {run.run_id} · {run.ticks_count} ticks
              </option>
            ))}
          </select>
        </label>
        <span className="topbar__ws" title={wsError ?? undefined}>
          <span className={`dot dot--${wsClass}`} />
          {wsLabel}
          {live ? ` · ${live.agentCount} entités` : ''}
        </span>
      </header>
      {(runsError || wsError) && (
        <div className="error banner" role="status">
          {runsError ? `Runs indisponibles : ${runsError}` : null}
          {wsError && !runsError ? `Flux temps réel : ${wsError}` : null}
        </div>
      )}
      <div className="app-body">
        <nav className="sidenav">
          {NAV_ITEMS.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => (isActive ? 'active' : '')}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
