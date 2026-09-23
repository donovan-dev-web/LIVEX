import { useEffect } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useLiveStore } from '../../store'
import { connect } from '../../ws/realtime'

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
  const live = useLiveStore((s) => s.live)
  const navigate = useNavigate()

  useEffect(() => {
    connect()
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
        <span className="topbar__ws">
          <span className={`dot dot--${wsClass}`} />
          {wsLabel}
          {live ? ` · ${live.agentCount} entités` : ''}
        </span>
      </header>
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