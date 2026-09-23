interface GaugeProps {
  label: string
  value: number | null
  min?: number
  max?: number
  warnAbove?: number
}

export function Gauge({ label, value, min = 0, max = 1, warnAbove }: GaugeProps) {
  const ratio = value === null ? 0 : Math.max(0, Math.min(1, (value - min) / (max - min)))
  const isWarn = value !== null && warnAbove !== undefined && value > warnAbove
  const color = isWarn ? 'var(--danger)' : 'var(--accent)'

  return (
    <div className="card" style={{ textAlign: 'center' }}>
      <div style={{ fontSize: 13, color: 'var(--text-secondary)' }}>{label}</div>
      <div style={{ position: 'relative', margin: '8px 0' }}>
        <svg viewBox="0 0 120 70" width="140" height="82" role="img" aria-label={label}>
          <path
            d="M 10 66 A 50 50 0 0 1 110 66"
            fill="none"
            stroke="var(--border)"
            strokeWidth="8"
            strokeLinecap="round"
          />
          <path
            d="M 10 66 A 50 50 0 0 1 110 66"
            fill="none"
            stroke={color}
            strokeWidth="8"
            strokeLinecap="round"
            strokeDasharray={`${ratio * 157} 157`}
          />
        </svg>
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            alignItems: 'flex-end',
            justifyContent: 'center',
            paddingBottom: 4,
            fontVariantNumeric: 'tabular-nums',
          }}
        >
          <span className="mono">{value === null ? '—' : value.toFixed(3)}</span>
        </div>
      </div>
      <div style={{ fontSize: 11, color: 'var(--text-secondary)', fontVariantNumeric: 'tabular-nums' }}>
        {min.toFixed(0)} … {warnAbove !== undefined ? `seuil ${warnAbove} · ` : ''}
        {max.toFixed(0)}
      </div>
    </div>
  )
}