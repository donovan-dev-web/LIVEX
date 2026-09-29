import { useEffect, useMemo, useRef, useState } from 'react'
import ReactECharts from 'echarts-for-react'
import { client } from '../../api/client'
import { colorForGroup } from '../agents/badges'
import type { GroupsResponse, RelationshipsResponse } from '../../api/types'
import { useLiveStore } from '../../store'

/**
 * Nombre d'appels `/api/relationships/{id}` menés de front. Le chemin
 * `/api/beliefs|relationships/{agent_id}` n'a pas d'équivalent « tous les
 * agents » : la confiance est un attribut d'agent dans le contexte `agents`.
 */
const RELATIONSHIP_CONCURRENCY = 6

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

/** Descend une liste de tâches par lots, sans jamais dépasser `limit` en vol. */
async function pooled<T, R>(
  items: T[],
  limit: number,
  task: (item: T) => Promise<R>,
): Promise<R[]> {
  const results: R[] = new Array(items.length)
  let cursor = 0
  const workers = Array.from({ length: Math.min(limit, items.length) }, async () => {
    while (cursor < items.length) {
      const index = cursor
      cursor += 1
      results[index] = await task(items[index])
    }
  })
  await Promise.all(workers)
  return results
}

export function SocialGraph({ runId, onSelectAgent }: SocialGraphProps) {
  const [groups, setGroups] = useState<GroupsResponse | null>(null)
  const [relations, setRelations] = useState<Record<string, RelationshipsResponse>>({})
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const tick = useLiveStore((state) => state.live?.tick)
  // Mémorise le dernier tick interrogé : le graphe n'est reconstruit que sur
  // changement réel de tick, pas à chaque rendu de l'écran.
  const lastTick = useRef<number | undefined>(undefined)
  const runRef = useRef<string | null>(null)

  useEffect(() => {
    if (!runId) {
      setGroups(null)
      setRelations({})
      return
    }
    if (runRef.current === runId && lastTick.current === tick) return
    runRef.current = runId
    lastTick.current = tick

    let cancelled = false
    setLoading(true)
    client
      .groups({ runId })
      .then(async (g) => {
        if (cancelled) return
        setGroups(g)
        setError(null)
        const members = [...new Set(g.groups.flatMap((group) => group.members))].sort()
        if (members.length === 0) {
          if (!cancelled) {
            setRelations({})
            setLoading(false)
          }
          return
        }
        // Avant : une requête par membre, en série, relancée à chaque tick.
        // Sur 40 entités à 1 tick/2 s, c'était 40 requêtes par tick en série.
        const fetched = await pooled(members, RELATIONSHIP_CONCURRENCY, (id) =>
          client
            .relationships(id, runId)
            .catch(() => null),
        )
        if (cancelled) return
        const rel: Record<string, RelationshipsResponse> = {}
        fetched.forEach((response, index) => {
          if (response) rel[members[index]] = response
        })
        setRelations(rel)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [runId, tick])

  const { nodes, edges } = useMemo(() => {
    const groupByMember = new Map<string, string>()
    for (const group of groups?.groups ?? []) {
      for (const member of group.members) groupByMember.set(member, group.label)
    }

    const nodeIds = new Set<string>()
    for (const id of groupByMember.keys()) nodeIds.add(id)
    for (const rel of Object.values(relations)) for (const t of rel.trust) nodeIds.add(t.peerId)

    const graphNodes: GraphNode[] = [...nodeIds].sort().map((id) => ({
      id,
      name: id,
      itemStyle: { color: colorForGroup(groupByMember.get(id) ?? '') },
    }))

    const edgeKey = new Set<string>()
    const graphEdges: GraphEdge[] = []
    for (const [agentId, rel] of Object.entries(relations)) {
      for (const t of rel.trust) {
        const key = [agentId, t.peerId].sort().join('|')
        if (edgeKey.has(key)) continue
        edgeKey.add(key)
        graphEdges.push({
          source: agentId,
          target: t.peerId,
          lineStyle: { opacity: Math.max(0.15, t.trust), width: 1 + t.trust * 2 },
        })
      }
    }
    return { nodes: graphNodes, edges: graphEdges }
  }, [groups, relations])

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
          notMerge
          onEvents={{
            click: (params: { data?: { name?: string } }) => {
              if (params?.data?.name) onSelectAgent(params.data.name)
            },
          }}
        />
        <div className="banner" role="status">
          {loading
            ? 'Reconstruction du graphe…'
            : `tick ${groups?.tick ?? '—'} · ${nodes.length} nœud${nodes.length > 1 ? 's' : ''} · ${edges.length} lien${edges.length > 1 ? 's' : ''}`}
        </div>
      </article>
      <p className="banner mt-3">
        Graphe force-directed, sondage ~2 s. Opacité/largeur des arêtes = niveau de confiance
        observé — il ne faut pas inférer de liens « réels » (FRONTEND_VISION).
      </p>
    </div>
  )
}