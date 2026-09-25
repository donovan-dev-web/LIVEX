import { useState } from 'react'
import { controlClient } from '../api/client'
import { useLiveStore, useRuns, useSelectedRunId } from '../store'
import { useLoadRuns } from '../hooks/useData'
import { client } from '../api/client'

export function ControlScreen() {
  useLoadRuns()
  const runs = useRuns()
  const runId = useSelectedRunId()
  const live = useLiveStore((s) => s.live)
  const wsState = useLiveStore((s) => s.wsState)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [pending, setPending] = useState<string | null>(null)

  const send = async (action: 'start' | 'pause' | 'resume' | 'stop' | 'reset') => {
    setPending(action)
    setFeedback(null)
    try {
      const parsedSeed = Number(seed)
      if ((action === 'start' || action === 'reset') && !Number.isSafeInteger(parsedSeed)) {
        throw new Error('La seed doit être un entier valide.')
      }
      await controlClient.command(
        action,
        action === 'start' || action === 'reset' ? { seed: parsedSeed } : {},
      )
      setFeedback(`Commande « ${action} » relayée à SYNE (:5181).`)
    } catch (err) {
      setFeedback(err instanceof Error ? err.message : String(err))
    } finally {
      setPending(null)
    }
  }

  const [seed, setSeed] = useState('12345')

  return (
    <div>
      <h2 className="mb-4">Pilotage & calibration</h2>

      <div className="banner mb-4">
        Le pilotage est <strong>relayé</strong> : l'interface n'invoque jamais SYNE en direct —
        ECHOS relaie vers HTTP :5181 (FRONTEND_VISION §2). Le serveur SYNE `--serve` doit être démarré. État WebSocket : {wsState} · tick :{' '}
        {live?.tick ?? '—'}.
      </div>

      {feedback ? <div className="banner mb-4">{feedback}</div> : null}

      <div className="card mb-4">
        <h3 className="mb-3">SimulationControls</h3>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button className="btn btn--accent" type="button" disabled={pending !== null} onClick={() => void send('start')}>
            {pending === 'start' ? '…' : '▶ Start'}
          </button>
          <button className="btn" type="button" disabled={pending !== null} onClick={() => void send('pause')}>
            {pending === 'pause' ? '…' : '⏸ Pause'}
          </button>
          <button className="btn" type="button" disabled={pending !== null} onClick={() => void send('resume')}>
            {pending === 'resume' ? '…' : '▶ Resume'}
          </button>
          <button className="btn btn--danger" type="button" disabled={pending !== null} onClick={() => void send('stop')}>
            {pending === 'stop' ? '…' : '■ Stop'}
          </button>
          <button className="btn btn--danger" type="button" disabled={pending !== null} onClick={() => void send('reset')}>
            {pending === 'reset' ? '…' : '↺ Reset'}
          </button>
        </div>
        <div className="mt-4" style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <label className="tag" htmlFor="seed">
            seed
          </label>
          <input id="seed" className="input mono" value={seed} onChange={(e) => setSeed(e.target.value)} />
        </div>
      </div>

      <div className="card mb-4">
        <h3 className="mb-3">Calibration</h3>
        <p className="banner">CalibrationForm — paramètres ECHOS relayés à SYNE (jamais appliqués en direct).</p>
      </div>

      <div className="card">
        <h3 className="mb-3">RecordingPanel — runs enregistrés ({runs.length})</h3>
        {runs.length > 0 ? (
          <table className="table">
            <thead>
              <tr>
                <th>run</th>
                <th>version</th>
                <th>seed</th>
                <th>ticks</th>
                <th>export</th>
              </tr>
            </thead>
            <tbody>
              {runs.map((r) => (
                <tr key={r.run_id}>
                  <td className="mono">{r.run_id}</td>
                  <td className="mono">{r.version}</td>
                  <td className="mono">{r.seed}</td>
                  <td className="mono">
                    {r.first_tick} … {r.last_tick} ({r.ticks_count})
                  </td>
                  <td>
                    <ExportButton runId={r.run_id} format="json" />
                    <ExportButton runId={r.run_id} format="csv" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="empty">Aucun run — ECHOS n'a pas encore enregistré de runs.</div>
        )}
        {runId ? <div className="tag mt-4">Sélection courante : {runId}</div> : null}
      </div>
    </div>
  )
}

function ExportButton({ runId, format }: { runId: string; format: 'json' | 'csv' }) {
  const [status, setStatus] = useState<string | null>(null)

  const download = async () => {
    setStatus('…')
    try {
      const data = await client.exportRun(runId, format)
      if (data instanceof Blob) {
        const url = URL.createObjectURL(data)
        const a = document.createElement('a')
        a.href = url
        a.download = `${runId}.${format}`
        a.click()
        URL.revokeObjectURL(url)
      } else {
        const a = document.createElement('a')
        a.href = `data:application/json;charset=utf-8,${encodeURIComponent(JSON.stringify(data, null, 2))}`
        a.download = `${runId}.${format}`
        a.click()
      }
      setStatus('exporté')
    } catch (err) {
      setStatus(err instanceof Error ? err.message : String(err))
    }
  }

  return (
    <button className="btn" type="button" onClick={() => void download()}>
      {format.toUpperCase()} {status ?? ''}
    </button>
  )
}

export default ControlScreen