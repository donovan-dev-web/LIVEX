import { Link } from 'react-router-dom'
import { useState } from 'react'
import { KPICard } from '../components/kpi/KPICard'
import { Gauge } from '../components/viz/Gauge'
import { TimelineChart } from '../components/viz/TimelineChart'
import { useGroups, useLoadRuns, useMetrics, usePhenomena } from '../hooks/useData'
import { useLiveStore, useRuns, useSelectedRunId } from '../store'
import type { MetricLatest, MetricValues } from '../api/types'
import { WorldGrid } from '../components/viz/WorldGrid'

export function DashboardScreen() {
  useLoadRuns()
  const runs = useRuns()
  const runId = useSelectedRunId()
  const live = useLiveStore((s) => s.live)

  const { metrics, error, refreshedAt } = useMetrics(runId, 1)
  const { groups } = useGroups(runId)
  const phenomena = usePhenomena(runId)
  const [selectedSeries, setSelectedSeries] = useState<string>()

  const latest = metrics?.latest ?? {}
  const emScore =
    latest.EmergenceIndicators?.['EmergenceScore'] ?? null

  const gauges: Array<{ label: string; value: number | null; max: number; warnAbove?: number }> = [
    { label: 'Diversité croyances', value: pick(latest, 'CognitiveDiversityMetrics', 'BeliefDiversity'), max: 1 },
    { label: 'Diversité objectifs', value: pick(latest, 'CognitiveDiversityMetrics', 'GoalDiversity'), max: 1 },
    { label: 'Clustering', value: pick(latest, 'SocialComplexityMetrics', 'ClusteringCoefficient'), max: 1 },
    { label: 'Vitesse de diffusion', value: pick(latest, 'InformationPropagationMetrics', 'InformationDiffusionSpeed'), max: 100, warnAbove: 50 },
  ]

  const { series } = flattenSeries(metrics?.values ?? {})
  const metricsOk = metrics && Array.isArray(metrics.ticks) && metrics.ticks.length > 0
  const metricsTick =
    metrics?.latest_tick ?? (metrics && metrics.ticks.length > 0 ? metrics.ticks[metrics.ticks.length - 1] : null)
  const metricsLag = live && metricsTick !== null ? Math.max(0, live.tick - metricsTick) : null

  return (
    <div>
      <h2 className="mb-4">Tableau de bord</h2>
      <div className={`banner mb-4 ${error ? 'error' : ''}`} role="status">
        {error
          ? `Métriques indisponibles : ${error}`
          : metrics
            ? `Métriques rafraîchies ${refreshedAt ? new Date(refreshedAt).toLocaleTimeString() : '…'} · tick ${metricsTick ?? '—'}${metricsLag !== null ? ` · retard ${metricsLag} tick${metricsLag > 1 ? 's' : ''}` : ''}`
            : 'Chargement des métriques…'}
      </div>

      <div className="grid grid--kpi mb-4">
        <KPICard title="Entités actives" value={live?.agentCount ?? null} hint="Sondage temps réel (WebSocket)" />
        <KPICard title="Score émergence" value={emScore !== null ? emScore.toFixed(4) : null} hint="Vue d'analyse, pas une preuve" />
        <KPICard title="Groupes actifs" value={groups?.groups.length ?? null} hint="Communautés détectées" />
        <KPICard title="Messages reçus" value={live?.messageCount ?? null} hint="Événements reçus depuis le dernier snapshot" />
      </div>

      <div className="grid grid--3 mb-4">
        {gauges.map((g) => (
          <Gauge
            key={g.label}
            label={g.label}
            value={g.value}
            max={g.max}
            warnAbove={g.warnAbove}
          />
        ))}
      </div>

      <div className="card mb-4">
        <h3 className="mb-3">Évolution temporelle (sous-échantillonnée)</h3>
        {metricsOk ? (
          <TimelineChart ticks={metrics.ticks} series={series} selected={selectedSeries} onSelect={setSelectedSeries} />
        ) : (
          <div className="empty">Aucune série disponible pour le run sélectionné.</div>
        )}
      </div>

      <div className="card mb-4">
        <h3 className="mb-3">Monde (tick {live?.tick ?? '—'})</h3>
        <WorldGrid agents={live?.agents ?? []} world={live?.world} />
        {live?.world?.resources?.length ? (
          <div className="world-resources mt-3">
            {live.world.resources.map((resource) => (
              <span className="badge" key={resource.type}>
                {resource.type}: {resource.quantity}
              </span>
            ))}
          </div>
        ) : null}
        <div className="banner mt-4">Les agents et obstacles sont positionnés dans la grille normalisée. SYNE expose actuellement les ressources comme stocks globaux, sans coordonnées individuelles.</div>
      </div>

      <div className="card">
        <h3 className="mb-3">Phénomènes détectés</h3>
        {phenomena && phenomena.phenomena.length > 0 ? (
          <ul>
            {phenomena.phenomena.map((p) => (
              <li key={p.identifier} className="mb-3">
                <span className="badge">
                  <Link to="/analysis">{p.label}</Link>
                </span>
                <div className="mono" style={{ fontSize: 12, color: 'var(--text-secondary)' }}>
                  {p.identifier}
                </div>
                {p.description ? <div>{p.description}</div> : null}
                {p.firstTick !== undefined ? (
                  <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>
                    Ticks {p.firstTick}–{p.lastTick} · {p.occurrences} occurrence(s)
                  </div>
                ) : null}
              </li>
            ))}
          </ul>
        ) : (
          <div className="empty">Aucun phénomène détecté.</div>
        )}
        {phenomena?.disclaimer ? (
          <div className="banner mt-4">{phenomena.disclaimer}</div>
        ) : null}
      </div>

      {runs.length === 0 && <div className="empty mt-4">Aucun run enregistré — API ECHOS répondue sans données.</div>}
    </div>
  )
}

function pick(
  latest: MetricLatest | undefined,
  engine: string,
  metric: string,
): number | null {
  const value = latest?.[engine]?.[metric]
  return typeof value === 'number' ? value : null
}

function flattenSeries(values: MetricValues) {
  const series: Record<string, number[]> = {}
  for (const [engine, metrics] of Object.entries(values)) {
    for (const [metric, data] of Object.entries(metrics)) {
      series[`${engine} · ${metric}`] = data
    }
  }
  return { series }
}

export default DashboardScreen