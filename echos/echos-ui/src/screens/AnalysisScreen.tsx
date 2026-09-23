import { useEffect, useState } from 'react'
import { TimelineChart } from '../components/viz/TimelineChart'
import { useLoadRuns, useMetrics } from '../hooks/useData'
import { useRuns, useSelectedRunId } from '../store'
import { client } from '../api/client'
import { DEFAULT_DEPTH } from '../config'
import type { CausalChainResponse, CompareResponse } from '../api/types'

type Tab = 'metrics' | 'causal' | 'compare'

export function AnalysisScreen() {
  useLoadRuns()
  const runId = useSelectedRunId()
  const runs = useRuns()
  const [tab, setTab] = useState<Tab>('metrics')
  const [every, setEvery] = useState(2)

  return (
    <div>
      <h2 className="mb-4">Analyse & causalité</h2>
      <div className="tabs">
        {(
          [
            ['metrics', 'Métriques'],
            ['causal', 'Causale'],
            ['compare', 'Comparaison'],
          ] as Array<[Tab, string]>
        ).map(([id, label]) => (
          <button
            key={id}
            className={`tab ${tab === id ? 'tab--active' : ''}`}
            onClick={() => setTab(id)}
            type="button"
          >
            {label}
          </button>
        ))}
      </div>

      {tab === 'metrics' && <MetricsTab runId={runId} every={every} setEvery={setEvery} />}
      {tab === 'causal' && <CausalTab runId={runId} />}
      {tab === 'compare' && <CompareTab runs={runs.map((r) => r.run_id)} />}
    </div>
  )
}

function MetricsTab({
  runId,
  every,
  setEvery,
}: {
  runId: string | null
  every: number
  setEvery: (n: number) => void
}) {
  const { metrics, error } = useMetrics(runId, every)
  const series: Record<string, number[]> = {}
  for (const [engine, m] of Object.entries(metrics?.values ?? {})) {
    for (const [metric, data] of Object.entries(m)) series[`${engine} · ${metric}`] = data
  }

  return (
    <div>
      <div className="mb-4" style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <label className="tag" htmlFor="every">
          Sous-échantillonnage every=
        </label>
        <select
          id="every"
          className="select"
          value={every}
          onChange={(e) => setEvery(Number(e.target.value))}
        >
          {[1, 2, 5, 10, 20, 50].map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
      </div>
      {error ? <div className="error banner mb-3">{error}</div> : null}
      {metrics && metrics.ticks.length > 0 ? (
        <div className="card">
          <TimelineChart ticks={metrics.ticks} series={series} />
        </div>
      ) : (
        <div className="empty">Aucune série.</div>
      )}
    </div>
  )
}

function CausalTab({ runId }: { runId: string | null }) {
  const [agentId, setAgentId] = useState('')
  const [depth, setDepth] = useState(DEFAULT_DEPTH)
  const [chain, setChain] = useState<CausalChainResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  const run = async () => {
    if (!runId || !agentId.trim()) return
    setChain(null)
    setError(null)
    try {
      const res = await client.causalChain(runId, agentId.trim(), { depth })
      setChain(res)
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  return (
    <div>
      <div className="mb-4" style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <input
          className="input"
          placeholder="agentId (ex : A)"
          value={agentId}
          onChange={(e) => setAgentId(e.target.value)}
        />
        <label className="tag" htmlFor="depth">
          profondeur
        </label>
        <select id="depth" className="select" value={depth} onChange={(e) => setDepth(Number(e.target.value))}>
          {[3, 5, 7, 10, 12].map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
        <button className="btn btn--accent" onClick={() => void run()} type="button" disabled={!runId}>
          Reconstruire la chaîne
        </button>
      </div>
      {error ? <div className="error banner mb-3">{error}</div> : null}
      {chain && (
        <div className="card metric-list">
          {chain.chain.map((node) => (
            <div key={node.layer} className="card">
              <div className="tag">{node.layer}</div>
              <div className="mono mt-3" style={{ fontSize: 15 }}>
                {node.label}
              </div>
              {node.detail && Object.keys(node.detail).length > 0 ? (
                <pre className="mono" style={{ fontSize: 12, color: 'var(--text-secondary)', whiteSpace: 'pre-wrap' }}>
                  {JSON.stringify(node.detail, null, 2)}
                </pre>
              ) : null}
            </div>
          ))}
          <div className="banner">
            tick {chain.tick} · profondeur {chain.depth_served}/{chain.depth_requested} · cycle :{' '}
            {chain.cycle ? 'oui' : 'non'} · tronquée : {chain.truncated ? 'oui' : 'non'}
          </div>
        </div>
      )}
    </div>
  )
}

function CompareTab({ runs }: { runs: string[] }) {
  const [a, setA] = useState(runs[0] ?? '')
  const [b, setB] = useState(runs.length > 1 ? runs[1] ?? runs[0] ?? '' : runs[0] ?? '')
  const [result, setResult] = useState<CompareResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  const run = async () => {
    if (!a || !b) return
    setError(null)
    try {
      setResult(await client.compare(a, b))
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  // Pré-remplir si les runs arrivent après le montage (store asynchrone).
  useEffect(() => {
    if (runs.length < 1) return
    setA((prev) => prev || runs[0])
    setB((prev) => prev || (runs.length > 1 ? runs[1] : runs[0]))
  }, [runs])

  const metric = (x: number | null | undefined) => (x === null || x === undefined ? '—' : x.toFixed(4))

  return (
    <div>
      <div className="mb-4" style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <select className="select" value={a} onChange={(e) => setA(e.target.value)} aria-label="run A">
          <option value="">run A…</option>
          {runs.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
        <span className="tag">vs</span>
        <select className="select" value={b} onChange={(e) => setB(e.target.value)} aria-label="run B">
          <option value="">run B…</option>
          {runs.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
        <button className="btn btn--accent" onClick={() => void run()} type="button" disabled={!a || !b}>
          Comparer
        </button>
      </div>
      {error ? <div className="error banner mb-3">{error}</div> : null}
      {result && (
        <div className="card">
          <div className="grid grid--kpi mb-4">
            <div className="kpi">
              <div className="kpi__title">Reproductible</div>
              <div className="kpi__value">{result.is_reproducible ? 'OUI' : 'NON'}</div>
            </div>
            <div className="kpi">
              <div className="kpi__title">Bit-à-bit</div>
              <div className="kpi__value">{result.bit_identical ? 'OUI' : 'NON'}</div>
            </div>
            <div className="kpi">
              <div className="kpi__title">Score reproductibilité</div>
              <div className="kpi__value">{metric(result.reproducibility_score)}</div>
            </div>
            <div className="kpi">
              <div className="kpi__title">Δ cognitive (L2)</div>
              <div className="kpi__value">{metric(result.cognitive_diff)}</div>
            </div>
            <div className="kpi">
              <div className="kpi__title">Δ sociale (L2)</div>
              <div className="kpi__value">{metric(result.social_diff)}</div>
            </div>
          </div>
          <h4 className="tag mb-3">Séries comparatives (alignées)</h4>
          {result.series.length > 0 ? (
            <table className="table">
              <thead>
                <tr>
                  <th>tick</th>
                  <th>moteur</th>
                  <th>métrique</th>
                  <th>run A</th>
                  <th>run B</th>
                  <th>diff (B−A)</th>
                </tr>
              </thead>
              <tbody>
                {result.series.slice(0, 200).map((row, i) => (
                  <tr key={i}>
                    <td>{row.tick}</td>
                    <td className="mono">{row.engine}</td>
                    <td className="mono">{row.metric}</td>
                    <td className="mono">{metric(row.run_a_value)}</td>
                    <td className="mono">{metric(row.run_b_value)}</td>
                    <td className="mono">{metric(row.diff)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <div className="empty">Aucune série commune.</div>
          )}
        </div>
      )}
    </div>
  )
}

export default AnalysisScreen