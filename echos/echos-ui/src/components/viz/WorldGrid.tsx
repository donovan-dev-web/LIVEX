import type React from 'react'
import type { WorldSnapshot } from '../../api/types'

interface WorldGridProps {
  agents: Record<string, unknown>[]
  world?: WorldSnapshot
}

function numberOf(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

export function WorldGrid({ agents, world }: WorldGridProps) {
  const positions = agents.flatMap((agent) => {
    const x = numberOf(agent.positionX)
    const y = numberOf(agent.positionY)
    return x !== null && y !== null ? [{ x, y }] : []
  })
  const obstacles = world?.obstacles ?? []
  const allX = [...positions.map((p) => p.x), ...obstacles.map((o) => o.x)]
  const allY = [...positions.map((p) => p.y), ...obstacles.map((o) => o.y)]
  const minX = Math.min(0, ...allX)
  const minY = Math.min(0, ...allY)
  const maxX = Math.max(1, ...allX)
  const maxY = Math.max(1, ...allY)
  const width = 24
  const height = 16
  const cellKey = (x: number, y: number) => {
    const column = Math.max(0, Math.min(width - 1, Math.round(((x - minX) / (maxX - minX || 1)) * (width - 1))))
    const row = Math.max(0, Math.min(height - 1, Math.round(((y - minY) / (maxY - minY || 1)) * (height - 1))))
    return `${column},${row}`
  }
  const agentByKey = new Map<string, number>()
  positions.forEach((position) => {
    const key = cellKey(position.x, position.y)
    agentByKey.set(key, (agentByKey.get(key) ?? 0) + 1)
  })
  const obstacleKeys = new Set(obstacles.map((obstacle) => cellKey(obstacle.x, obstacle.y)))
  return <div className="world-grid" style={{ '--world-columns': width } as React.CSSProperties} role="img" aria-label="Grille 2D du monde">
    {Array.from({ length: width * height }, (_, i) => {
      const key = `${i % width},${Math.floor(i / width)}`
      const agentCount = agentByKey.get(key) ?? 0
      const obstacle = obstacleKeys.has(key)
      return <div key={key} className={`world-cell terrain-${obstacle ? 'obstacle' : 'unknown'}`} title={`${key}${agentCount ? ` · agents: ${agentCount}` : ''}${obstacle ? ' · obstacle' : ''}`}>
        {agentCount ? <span className="world-agent">{agentCount > 1 ? agentCount : '●'}</span> : obstacle ? <span className="world-resource">◆</span> : null}
      </div>
    })}
  </div>
}
