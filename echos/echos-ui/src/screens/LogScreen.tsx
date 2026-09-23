import { useState } from 'react'
import { useLiveStore } from '../store'
import { useLoadRuns } from '../hooks/useData'

const LIMITS = [
  { domain: 'Affichage', wording: 'L’interface montre, l’analyse démontre — jamais l’inverse.' },
  { domain: 'Vues live', wording: 'Sondage borné (500 ms inspecteur, 2 s graphe) pour ne pas dégrader SYNE.' },
  { domain: 'Métriques', wording: 'Les vues n’affichent que ce que l’API ECHOS fournit ; aucune recomputation front.' },
  { domain: 'Pilotage', wording: 'Toujours via le relais ECHOS → SYNE :5181, jamais en direct.' },
  { domain: 'Causalité', wording: 'Chaînes reconstruites hors ligne depuis decision_traces (ADR-002) — pas de causalité temps réel.' },
  { domain: 'Graphe social', wording: 'Les arêtes reflètent la confiance observée, pas des liens « réels ».' },
  { domain: 'Volumétrie', wording: 'Sous-échantillonnage ?every=N à la lecture.' },
]

type Level = 'structured' | 'traces' | 'text'

export function LogScreen() {
  useLoadRuns()
  const live = useLiveStore((s) => s.live)
  const wsState = useLiveStore((s) => s.wsState)
  const [level, setLevel] = useState<Level>('structured')

  return (
    <div>
      <h2 className="mb-4">Journal & limites</h2>

      <div className="card mb-4">
        <h3 className="mb-3">Console de débogage (3 niveaux)</h3>
        <div className="tabs">
          {(
            [
              ['structured', 'Structuré'],
              ['traces', 'Traces'],
              ['text', 'Texte'],
            ] as Array<[Level, string]>
          ).map(([id, label]) => (
            <button
              key={id}
              className={`tab ${level === id ? 'tab--active' : ''}`}
              onClick={() => setLevel(id)}
              type="button"
            >
              {label}
            </button>
          ))}
        </div>
        <div className="mono" style={{ fontSize: 12, background: '#0d1117', padding: 12, borderRadius: 6, minHeight: 240, maxHeight: 320, overflow: 'auto' }}>
          {level === 'structured' && (
            <span>
              Flux structuré (WebSocket) — état SYNE: {wsState}, tick courant: {live?.tick ?? '—'}, entités: {live?.agentCount ?? '—'}.
              Les journaux JSONL déterministes (structuré-&lt;run&gt;.jsonl, profilage, decision-traces) sont écrits par ECHOS (LOGGING_INSTRUMENTATION.md), hors flux navigateur.
            </span>
          )}
          {level === 'traces' && (
            <span>
              {`Traces de décision (decision_traces, schéma v3) disponibles via GET /api/runs/{run_id}/decisions — voir l'écran Analyse.`}
            </span>
          )}
          {level === 'text' && (
            <span>
              Logs texte quotidiens taggés [SSE-V2] — côté serveur ECHOS (répertoire ECHOS_LOG_DIR), hors périmètre navigateur V0.1.
            </span>
          )}
        </div>
      </div>

      <div className="card">
        <h3 className="mb-3">Limites de validité (contrepoint de l'écran Analyse)</h3>
        <ul>
          {LIMITS.map((l) => (
            <li key={l.domain} className="mb-3">
              <div className="tag">{l.domain}</div>
              <div style={{ fontSize: 14 }}>{l.wording}</div>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

export default LogScreen