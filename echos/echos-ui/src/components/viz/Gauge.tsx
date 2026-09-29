interface GaugeProps {
  label: string
  value: number | null
  min?: number
  max?: number
  /**
   * Seuil bas : en dessous, la jauge passe en alerte. Utilisé quand une valeur
   * faible est anormale (vitesse de diffusion, densité sociale).
   */
  warnBelow?: number
  /** Seuil haut : au-dessus, la jauge passe en alerte. */
  warnAbove?: number
  /** Rendu lorsque la valeur est un repli neutre (API `measured: false`). */
  fallback?: boolean
}

/**
 * Jauge radiale. Les deux seuils sont optionnels et cumulables : `warnBelow`
 * pour « trop peu », `warnAbove` pour « trop beaucoup ».
 *
 * L'aiguille ne bouge pas pour une valeur non finie : `NaN` vient des ticks
 * non observés d'une série fusionnée, pas d'une mesure à zéro.
 */
export function Gauge({ label, value, min = 0, max = 1, warnBelow, warnAbove, fallback = false }: GaugeProps) {
  const known = typeof value === 'number' && Number.isFinite(value)
  const ratio = known ? Math.max(0, Math.min(1, (value - min) / (max - min))) : 0
  // Un repli neutre ne déclenche jamais une alerte de seuil : sa valeur est
  // celle du moteur faute de données, pas une observation en dessous du seuil.
  const isWarn =
    known &&
    !fallback &&
    ((warnBelow !== undefined && value < warnBelow) ||
      (warnAbove !== undefined && value > warnAbove))
  const color = isWarn ? 'var(--danger)' : fallback ? 'var(--text-secondary)' : 'var(--accent)'

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
          <span className="mono">{known ? value.toFixed(3) : '—'}</span>
        </div>
      </div>
      <div style={{ fontSize: 11, color: 'var(--text-secondary)', fontVariantNumeric: 'tabular-nums' }}>
        {min.toFixed(0)} … {max.toFixed(0)}
        {warnBelow !== undefined ? ` · alerte sous ${warnBelow}` : ''}
        {warnAbove !== undefined ? ` · alerte au-dessus de ${warnAbove}` : ''}
      </div>
      {fallback ? (
        <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>
          Repli neutre — métrique non mesurée sur ce tick
        </div>
      ) : null}
    </div>
  )
}
