import { useState } from 'react'
import { SocialGraph } from '../components/viz/SocialGraph'
import { AgentInspector } from '../components/agents/AgentInspector'
import { useLoadRuns } from '../hooks/useData'
import { useSelectedRunId } from '../store'

export function SocialGraphScreen() {
  useLoadRuns()
  const runId = useSelectedRunId()
  const [selected, setSelected] = useState<string | null>(null)

  return (
    <div>
      <h2 className="mb-4">Graphe social</h2>
      <div className="grid grid--2">
        <SocialGraph runId={runId} onSelectAgent={setSelected} />
        {selected ? (
          <AgentInspector agentId={selected} pollMs={2000} />
        ) : (
          <div className="empty">Cliquez sur un nœud pour ouvrir son inspecteur.</div>
        )}
      </div>
    </div>
  )
}

export default SocialGraphScreen