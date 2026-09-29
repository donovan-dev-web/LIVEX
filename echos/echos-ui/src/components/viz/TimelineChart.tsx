import ReactECharts from 'echarts-for-react'

interface TimelineChartProps {
  ticks: number[]
  series: Record<string, number[]>
  selected?: string
  onSelect?: (name: string) => void
}

/**
 * Évolutions temporelles des métriques (Écran A / D). Les séries sont servies
 * par l'API ECHOS (`/metrics`), déjà sous-échantillonnées côté service — le
 * front affiche sans recalculer. Survolez pour la valeur exacte.
 */
export function TimelineChart({ ticks, series, selected, onSelect }: TimelineChartProps) {
  const names = Object.keys(series)
  // `selected` peut désigner une série disparue (changement de run) : on retombe
  // alors sur la première disponible plutôt que d'afficher une courbe vide.
  const active = selected && series[selected] ? selected : names[0]
  // ECharts ne connaît pas `NaN` : un tick non observé (trou de fusion) était
  // rendu comme un 0, ce qui transformait un creux de série en effondrement
  // chiffré. `null` est le trou explicite d'un `connectNulls: false`.
  const data = (active ? series[active] : []).map((value) =>
    typeof value === 'number' && Number.isFinite(value) ? value : null,
  )
  const observed = data.filter((value) => value !== null).length
  const option = {
    tooltip: { trigger: 'axis' as const },
    legend: { textStyle: { color: '#8b949e' }, bottom: 0, data: active ? [active] : [] },
    grid: { left: 56, right: 16, top: 24, bottom: 56 },
    xAxis: {
      type: 'category' as const,
      data: ticks,
      name: 'tick',
      nameTextStyle: { color: '#8b949e' },
      axisLabel: { color: '#8b949e' },
    },
    yAxis: {
      type: 'value' as const,
      axisLabel: { color: '#8b949e' },
      name: active ?? 'valeur',
      nameTextStyle: { color: '#8b949e' },
      splitLine: { lineStyle: { color: '#30363d' } },
    },
    series: active ? [{
      name: active,
      type: 'line' as const,
      data,
      showSymbol: false,
      connectNulls: false,
      lineStyle: { width: 2 },
    }] : [],
  }

  return <div>
    {onSelect && names.length > 1 ? (
      <label className="chart-selector">Série <select className="select" value={active} onChange={(e) => onSelect(e.target.value)}>
        {names.map((name) => <option key={name}>{name}</option>)}
      </select></label>
    ) : null}
    {active && observed < data.length ? (
      <div className="banner" role="status">
        {data.length - observed} tick(s) sans valeur observée — affichés en creux, pas à 0.
      </div>
    ) : null}
    <ReactECharts option={option} style={{ height: 280 }} notMerge />
  </div>
}