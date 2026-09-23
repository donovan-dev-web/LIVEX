import { useEffect, useState } from 'react'
import ReactECharts from 'echarts-for-react'
import { client } from '../../api/client'
import { colorForGroup } from '../agents/badges'
import type { GroupsResponse, RelationshipsResponse } from '../../api/types'

interface SocialGraphProps {
  runId: string | null
  onSelectAgent: (agentId: string | null) => void
}

interface GraphNode {
  id: string
  name: string
  itemStyle: { color: string }
}

interface GraphEdge {
  source: string
  target: string
  lineStyle: { opacity: number; width: number }
}

export function SocialGraph({ runId, onSelectAgent }: SocialGraphProps) {
  const [groups, setGroups] = useState<GroupsResponse | null>(null)
  const [relations, setRelations] = useState<Record<string, RelationshipsResponse>>({})
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!runId) return
    let cancelled = false
    client
      .groups({ runId })
      .then(async (g) => {
        if (cancelled) return
        setGroups(g)
        const rel: Record<string, RelationshipsResponse> = {}
        for (const group of g.groups) {
          for (const member of group.members) {
            if (rel[member]) continue
            const ok = await client
              .relationships(member)
              .catch(() => null)
            if (ok && !cancelled) rel[member] = ok
          }
        }
        if (!cancelled) setRelations(rel)
      })
      .catch((err) => !cancelled && setError(err instanceof Error ? err.message : String(err)))
    return () => {
      cancelled = true
    }
  }, [runId])

  const groupByMember = new Map<string, string>()
  for (const group of groups?.groups ?? []) {
    for (const member of group.members) groupByMember.set(member, group.label)
  }

  const nodeIds = new Set<string>()
  for (const id of groupByMember.keys()) nodeIds.add(id)
  for (const rel of Object.values(relations)) for (const t of rel.trust) nodeIds.add(t.peerId)

  const nodes: GraphNode[] = [...nodeIds].map((id) => ({
    id,
    name: id,
    itemStyle: { color: colorForGroup(groupByMember.get(id) ?? '') },
  }))

  const edgeKey = new Set<string>()
  const edges: GraphEdge[] = []
  for (const [agentId, rel] of Object.entries(relations)) {
    for (const t of rel.trust) {
      const key = [agentId, t.peerId].sort().join('|')
      if (edgeKey.has(key)) continue
      edgeKey.add(key)
      edges.push({
        source: agentId,
        target: t.peerId,
        lineStyle: { opacity: Math.max(0.15, t.trust), width: 1 + t.trust * 2 },
      })
    }
  }

  const option = {
    tooltip: {},
    series: [
      {
        type: 'graph' as const,
        layout: 'force' as const,
        data: nodes,
        links: edges,
        roam: true,
        label: { show: true, color: '#e6edf3' },
        force: { repulsion: 120, edgeLength: [60, 140] },
        emphasis: { focus: 'adjacency' as const },
      },
    ],
  }

  return (
    <div>
      {error ? <div className="error banner mb-3">{error}</div> : null}
      <article
        aria-label="Graphe social"
        onMouseLeave={() => onSelectAgent(null)}
        style={{ border: '1px solid var(--border)', borderRadius: 6 }}
      >
        <ReactECharts
          option={option}
          style={{ height: 480 }}
          onEvents={{
            click: (params: { data?: { name?: string } }) => {
              if (params?.data?.name) onSelectAgent(params.data.name)
            },
          }}
        />
      </article>
      <p className="banner mt-3">
        Graphe force-directed, sondage ~2 s. Opacité/largeur des arêtes = niveau de confiance
        observé — il ne faut pas inférer de liens « réels » (FRONTEND_VISION).
      </p>
    </div>
  )
}