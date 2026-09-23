import ReactECharts from 'echarts-for-react'

interface TimelineChartProps {
  ticks: number[]
  series: Record<string, number[]>
}

/**
 * Évolutions temporelles des métriques (Écran A / D). Les séries sont servies
 * par l'API ECHOS (`/metrics`), déjà sous-échantillonnées côté service — le
 * front affiche sans recalculer. Survolez pour la valeur exacte.
 */
export function TimelineChart({ ticks, series }: TimelineChartProps) {
  const option = {
    tooltip: { trigger: 'axis' as const },
    legend: { textStyle: { color: '#8b949e' }, top: 0 },
    grid: { left: 48, right: 16, top: 32, bottom: 32 },
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
      splitLine: { lineStyle: { color: '#30363d' } },
    },
    series: Object.entries(series).map(([name, data]) => ({
      name,
      type: 'line' as const,
      data,
      showSymbol: false,
      lineStyle: { width: 2 },
    })),
  }

  return <ReactECharts option={option} style={{ height: 280 }} notMerge />
}