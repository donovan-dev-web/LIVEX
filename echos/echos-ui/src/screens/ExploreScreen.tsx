import { useMemo, useState } from 'react'
import { useGroups, useLoadRuns } from '../hooks/useData'
import { useRuns, useSelectedRunId } from '../store'
import { AgentInspector } from '../components/agents/AgentInspector'
import { EntityBadge, GroupChip } from '../components/agents/badges'

type Tab = 'entities' | 'groups' | 'communications'

export function ExploreScreen() {
  useLoadRuns()
  const runId = useSelectedRunId()
  const runs = useRuns()
  const { groups, error } = useGroups(runId)
  const [tab, setTab] = useState<Tab>('entities')
  const [selected, setSelected] = useState<string | null>(null)

  const memberIds = useMemo<string[]>(() => {
    const set = new Set<string>()
    for (const g of groups?.groups ?? []) for (const m of g.members) set.add(m)
    return [...set].sort()
  }, [groups])

  return (
    <div>
      <h2 className="mb-4">Exploration</h2>
      {error ? <div className="error banner mb-4">{error}</div> : null}

      <div className="tabs">
        {(
          [
            ['entities', 'Entités'],
            ['groups', 'Groupes'],
            ['communications', 'Communications'],
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

      <div className="grid grid--2">
        <div>
          {tab === 'entities' && (
            <div className="card">
              <h3 className="mb-3">Entités ({memberIds.length})</h3>
              <div className="agent-list">
                {memberIds.length ? (
                  memberIds.map((id) => (
                    <div
                      key={id}
                      className={`agent-row ${selected === id ? 'agent-row--selected' : ''}`}
                      onClick={() => setSelected(id)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={(e) => e.key === 'Enter' && setSelected(id)}
                    >
                      <EntityBadge agentId={id} />
                    </div>
                  ))
                ) : (
                  <div className="empty">Aucune entité (groupes vides).</div>
                )}
              </div>
            </div>
          )}

          {tab === 'groups' && (
            <div className="card">
              <h3 className="mb-3">Groupes ({groups?.groups.length ?? 0})</h3>
              {groups && groups.groups.length > 0 ? (
                groups.groups.map((g) => (
                  <div key={g.label} className="card mb-3">
                    <GroupChip label={g.label} />
                    <span className="tag" style={{ marginLeft: 8 }}>
                      {g.size} membre{g.size > 1 ? 's' : ''}
                    </span>
                    <div className="mono mt-3" style={{ fontSize: 13 }}>
                      {g.members.join(', ')}
                    </div>
                  </div>
                ))
              ) : (
                <div className="empty">Aucun groupe actif.</div>
              )}
            </div>
          )}

          {tab === 'communications' && (
            <div className="card">
              <h3 className="mb-3">Communications</h3>
              <div className="banner">
                La matrice entité×entité de communication (`/api/communication-heatmap`) est au
                périmètre UI, hors V0.1 (API_REST §Points restés ouverts). Les flux d'événements
                restent visibles dans le journal.
              </div>
            </div>
          )}
        </div>

        <div>
          {selected ? (
            <AgentInspector agentId={selected} pollMs={500} />
          ) : (
            <div className="empty">Sélectionnez une entité pour l'inspecter (sondage 500 ms).</div>
          )}
        </div>
      </div>

      {runs.length === 0 && (
        <div className="empty mt-4">Aucun run enregistré.</div>
      )}
    </div>
  )
}

export default ExploreScreen