import { useState } from 'react'
import { client, controlClient } from '../api/client'
import { triggerDownload } from '../api/download'
import { useRunsRefresh } from '../hooks/useData'
import { useLiveStore, useRuns, useRunsStore, useSelectedRunId } from '../store'

export function ControlScreen() {
  const runs = useRuns()
  const runId = useSelectedRunId()
  const live = useLiveStore((s) => s.live)
  const wsState = useLiveStore((s) => s.wsState)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [pending, setPending] = useState<string | null>(null)
  const [seed, setSeed] = useState('12345')

  const refreshRuns = useRunsRefresh()
  const selectRun = useRunsStore((s) => s.selectRun)

  const send = async (action: 'start' | 'pause' | 'resume' | 'stop' | 'reset') => {
    setPending(action)
    setFeedback(null)
    try {
      // La seed n'est portée que par `start` et `reset` : la valider pour
      // `pause`/`resume` rejetait des commandes qui ne l'utilisent pas.
      const parsedSeed = Number(seed)
      if ((action === 'start' || action === 'reset') && !Number.isSafeInteger(parsedSeed)) {
        throw new Error('La seed doit être un entier valide.')
      }
      const response = (await controlClient.command(
        action,
        action === 'start' || action === 'reset' ? { seed: parsedSeed } : {},
      ).then((r) => r.json())) as { runId?: string }
      setFeedback(`Commande « ${action} » relayée à SYNE (:5181).`)
      // Un run d'analyse n'apparaît dans la base qu'après le premier tick
      // ingéré : le Start répond avant, donc on recharge la liste et on attend
      // (borné) que le run démarré soit visible avant de le sélectionner —
      // sinon tous les écrans d'analyse restaient sur le run précédent, le
      // sélecteur ne listant jamais le nouveau run sans rechargement manuel.
      if (action === 'start' || action === 'reset') {
        const started = response.runId
        // Repli si le run démarré n'apparaît jamais (ingestion arrêtée) : le run
        // le plus avancé, jamais « le dernier de la liste » (ordre alphabétique
        // de l'API ≠ plus récent).
        const mostAdvanced = (runs: { run_id: string; last_tick: number }[]) =>
          runs.length > 0 ? runs.reduce((a, b) => (b.last_tick > a.last_tick ? b : a)) : undefined
        let selected = false
        for (let attempt = 0; attempt < 10 && !selected; attempt++) {
          const runsResponse = await refreshRuns()
          const known = started
            ? runsResponse?.runs.find((r) => r.run_id === started)
            : undefined
          if (known) {
            selectRun(known.run_id)
            selected = true
          } else {
            await new Promise((resolve) => setTimeout(resolve, 500))
          }
        }
        if (!selected) {
          const runsResponse = await refreshRuns()
          const fallback = runsResponse ? mostAdvanced(runsResponse.runs) : undefined
          if (fallback) selectRun(fallback.run_id)
        }
      }
    } catch (err) {
      setFeedback(err instanceof Error ? err.message : String(err))
    } finally {
      setPending(null)
    }
  }

  return (
    <div>
      <h2 className="mb-4">Pilotage & calibration</h2>

      <div className="banner mb-4">
        Le pilotage est <strong>relayé</strong> : l'interface n'invoque jamais SYNE en direct —
        ECHOS relaie vers HTTP :5181 (FRONTEND_VISION §2). Le serveur SYNE `--serve` doit être démarré. État WebSocket : {wsState} · tick :{' '}
        {live?.tick ?? '—'}.
      </div>

      {feedback ? (
        <div className={`banner mb-4 ${pending ? '' : 'ok'}`} role="status">
          {feedback}
        </div>
      ) : null}

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
                <th>issue</th>
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
                    <OutcomeBadge outcome={r.outcome} extinctionTick={r.extinction_tick} />
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

/**
 * Badge du résultat de population (A3/D4) : « éteint à tN » sur la liste des
 * runs, sans recalcul côté interface. ``unknown`` (run sans tick) s'affiche
 * neutre ; l'absence du champ (API antérieure) vaut ``surviving`` inconnu →
 * aussi neutre.
 */
function OutcomeBadge({
  outcome,
  extinctionTick,
}: {
  outcome?: 'extinct' | 'surviving' | 'unknown'
  extinctionTick?: number | null
}) {
  if (outcome === 'extinct') {
    return (
      <span className="badge badge--danger" title={`Extinction au tick ${extinctionTick ?? '—'}`}>
        éteint{extinctionTick != null ? ` à t${extinctionTick}` : ''}
      </span>
    )
  }
  if (outcome === 'surviving') {
    return <span className="badge" title="Population vivante au dernier tick observé">vivant</span>
  }
  return <span className="tag">—</span>
}

/**
 * Export d'un run. Le service renvoie toujours du JSON, y compris pour
 * ``format=csv`` (le CSV est une chaîne dans ``body``) : sans normalisation,
 * le fichier ``.csv`` téléchargé contenait du JSON.
 */
function ExportButton({ runId, format }: { runId: string; format: 'json' | 'csv' }) {
  const [status, setStatus] = useState<string | null>(null)

  const download = async () => {
    setStatus('…')
    try {
      const file = await client.exportRun(runId, format)
      triggerDownload(file.filename, file.mime, file.content)
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