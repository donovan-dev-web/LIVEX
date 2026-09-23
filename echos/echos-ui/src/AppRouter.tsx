import { lazy, Suspense, type ReactNode } from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'

const DashboardScreen = lazy(() => import('./screens/DashboardScreen'))
const ExploreScreen = lazy(() => import('./screens/ExploreScreen'))
const SocialGraphScreen = lazy(() => import('./screens/SocialGraphScreen'))
const AnalysisScreen = lazy(() => import('./screens/AnalysisScreen'))
const ControlScreen = lazy(() => import('./screens/ControlScreen'))
const LogScreen = lazy(() => import('./screens/LogScreen'))

function withSuspense(node: ReactNode) {
  return <Suspense fallback={<div className="spinner">Chargement…</div>}>{node}</Suspense>
}

/**
 * Écrans définis dans UI_DESIGN.md §2 — A Dashboard, B Exploration,
 * C Graphe social, D Analyse & causalité, E Pilotage & calibration,
 * F Journal & limites.
 */
export function AppRouter() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={withSuspense(<DashboardScreen />)} />
        <Route path="/explore" element={withSuspense(<ExploreScreen />)} />
        <Route path="/social-graph" element={withSuspense(<SocialGraphScreen />)} />
        <Route path="/analysis" element={withSuspense(<AnalysisScreen />)} />
        <Route path="/control" element={withSuspense(<ControlScreen />)} />
        <Route path="/log" element={withSuspense(<LogScreen />)} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Route>
    </Routes>
  )
}