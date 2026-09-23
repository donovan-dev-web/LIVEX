interface KPICardProps {
  title: string
  value: string | number | null
  delta?: number | null
  hint?: string
  formatDelta?: (delta: number) => string
}

export function KPICard({ title, value, delta, hint, formatDelta }: KPICardProps) {
  const isDeltaUp = delta !== null && delta !== undefined && delta > 0
  const isDeltaDown = delta !== null && delta !== undefined && delta < 0
  const prettyDelta =
    delta !== null && delta !== undefined && delta !== 0
      ? formatDelta
        ? formatDelta(delta)
        : `${isDeltaUp ? '▲' : '▼'} ${Math.abs(delta).toFixed(2)}`
      : null

  return (
    <div className="kpi">
      <div className="kpi__title">{title}</div>
      <div className="kpi__value">{value ?? '—'}</div>
      {prettyDelta ? (
        <div
          className={`kpi__delta ${
            isDeltaUp ? 'kpi__delta--up' : isDeltaDown ? 'kpi__delta--down' : ''
          }`}
        >
          {prettyDelta}
        </div>
      ) : null}
      {hint ? <div className="kpi__hint">{hint}</div> : null}
    </div>
  )
}