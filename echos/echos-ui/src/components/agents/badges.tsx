const GROUP_COLORS = [
  '#00d4a0',
  '#58a6ff',
  '#f0883e',
  '#bc8cff',
  '#f778ba',
  '#3fb950',
  '#e3b341',
  '#ff7b72',
]

export function colorForGroup(label: string): string {
  let hash = 0
  for (let i = 0; i < label.length; i += 1) {
    hash = (hash * 31 + label.charCodeAt(i)) >>> 0
  }
  return GROUP_COLORS[hash % GROUP_COLORS.length]
}

export function GroupChip({ label }: { label: string }) {
  return (
    <span className="chip" style={{ backgroundColor: colorForGroup(label) }}>
      {label}
    </span>
  )
}

export function EntityBadge({ agentId, group }: { agentId: string; group?: string }) {
  return (
    <span className="badge">
      <span
        style={{
          width: 8,
          height: 8,
          borderRadius: '50%',
          backgroundColor: group ? colorForGroup(group) : 'var(--text-secondary)',
          display: 'inline-block',
        }}
      />
      {agentId}
    </span>
  )
}