import { useEffect, useMemo, useState } from 'react'
import { useGroups } from '../hooks/useData'
import { useLiveStore, useSelectedRunId } from '../store'
import { AgentInspector } from '../components/agents/AgentInspector'
import { EntityBadge, GroupChip } from '../components/agents/badges'

type Tab = 'entities' | 'groups' | 'communications'

export function ExploreScreen() {
  const runId = useSelectedRunId()
  const { groups, error } = useGroups(runId)
  const liveAgents = useLiveStore((s) => s.live?.agents)
  const [tab, setTab] = useState<Tab>('entities')
  const [selected, setSelected] = useState<string | null>(null)

  /**
   * Entités affichées = membres de groupe ∪ agents vus dans le flux.
   *
   * `_groups_of` (côté ECHOS) exclut volontairement les singletons : une
   * entité isolée n'est pas une communauté. La liste des entités n'en lisait
   * que les membres, donc une entité seule n'était pas inspectable — la liste
   * pouvait afficher « 0 entité » pendant que le tableau de bord en annonçait
   * 40.
   */
  const entityIds = useMemo<string[]>(() => {
    const set = new Set<string>()
    for (const g of groups?.groups ?? []) for (const m of g.members) set.add(m)
    for (const agent of liveAgents ?? []) if (agent?.id != null) set.add(String(agent.id))
    return [...set].sort((a, b) => a.localeCompare(b, 'fr', { numeric: true }))
  }, [groups, liveAgents])

  const groupedIds = useMemo(
    () => new Set((groups?.groups ?? []).flatMap((g) => g.members)),
    [groups],
  )

  // Une entité qui quitte le run affiché ne doit pas rester ouverte sur des
  // données devenues hors sujet.
  useEffect(() => {
    if (selected && entityIds.length > 0 && !entityIds.includes(selected)) setSelected(null)
  }, [entityIds, selected])

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
              <h3 className="mb-3">
                Entités ({entityIds.length}) · {entityIds.length - groupedIds.size} isolée(s)
              </h3>
              <div className="agent-list" role="listbox" aria-label="Entités observées">
                {entityIds.length ? (
                  entityIds.map((id) => (
                    <div
                      key={id}
                      className={`agent-row ${selected === id ? 'agent-row--selected' : ''}`}
                      onClick={() => setSelected(id)}
                      role="option"
                      aria-selected={selected === id}
                      tabIndex={0}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault()
                          setSelected(id)
                        }
                      }}
                    >
                      <EntityBadge agentId={id} />
                      {!groupedIds.has(id) ? <span className="tag">isolée</span> : null}
                    </div>
                  ))
                ) : (
                  <div className="empty">
                    Aucune entité observée — flux temps réel déconnecté ou aucun groupe actif.
                  </div>
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
            <AgentInspector agentId={selected} runId={runId} pollMs={500} />
          ) : (
            <div className="empty">Sélectionnez une entité pour l'inspecter (sondage 500 ms).</div>
          )}
        </div>
      </div>
    </div>
  )
}

export default ExploreScreen