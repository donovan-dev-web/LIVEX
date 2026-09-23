import { useEffect, useState } from 'react'
import { client } from '../../api/client'
import type { BeliefsResponse, RelationshipsResponse } from '../../api/types'

interface AgentInspectorProps {
  agentId: string
  /** Sondage 500 ms — lecture seule (règle d'or : l'interface n'écrit rien). */
  pollMs?: number
}

export function AgentInspector({ agentId, pollMs }: AgentInspectorProps) {
  const [beliefs, setBeliefs] = useState<BeliefsResponse | null>(null)
  const [relationships, setRelationships] = useState<RelationshipsResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    const load = async () => {
      try {
        const [b, r] = await Promise.all([client.beliefs(agentId), client.relationships(agentId)])
        if (!cancelled) {
          setBeliefs(b)
          setRelationships(r)
          setError(null)
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err))
      }
    }
    load()
    if (pollMs) {
      const timer = setInterval(load, pollMs)
      return () => {
        clearInterval(timer)
        cancelled = true
      }
    }
    return () => {
      cancelled = true
    }
  }, [agentId, pollMs])

  return (
    <div className="card">
      <h3 className="mono mb-3">Entité {agentId}</h3>
      {error ? <div className="error banner">{error}</div> : null}

      <h4 className="tag mb-3">Croyances</h4>
      {beliefs && beliefs.beliefs.length > 0 ? (
        <table className="table">
          <thead>
            <tr>
              <th>Sujet</th>
              <th>Prédicat</th>
              <th>Valeur</th>
              <th>Confiance</th>
            </tr>
          </thead>
          <tbody>
            {beliefs.beliefs.map((b, i) => (
              <tr key={i}>
                <td className="mono">{b.subject}</td>
                <td className="mono">{b.predicate}</td>
                <td className="mono">{b.value}</td>
                <td className="mono">{b.confidence.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <div className="empty">Aucune croyance observée</div>
      )}

      <h4 className="tag mt-4 mb-3">Confiance</h4>
      {relationships && relationships.trust.length > 0 ? (
        <table className="table">
          <thead>
            <tr>
              <th>Pair</th>
              <th>Confiance</th>
            </tr>
          </thead>
          <tbody>
            {relationships.trust.map((t) => (
              <tr key={t.peerId}>
                <td className="mono">{t.peerId}</td>
                <td className="mono">{t.trust.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <div className="empty">Aucune relation observée</div>
      )}
    </div>
  )
}