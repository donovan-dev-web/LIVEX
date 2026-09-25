export interface RunMeta {
  run_id: string
  version: string
  seed: string
  ticks_count: number
  first_tick: number
  last_tick: number
}

export interface RunsResponse {
  runs: RunMeta[]
}

export type MetricValues = Record<string, Record<string, number[]>>

/** Dernières valeurs par moteur/métrique (valeurs scalaires). */
export type MetricLatest = Record<string, Record<string, number>>

export interface MetricsResponse {
  run_id: string
  engine: string | null
  metric: string | null
  every: number
  ticks: number[]
  values: MetricValues
  latest: MetricLatest
  /** Highest tick currently persisted in the metrics store, if any. */
  latest_tick?: number | null
}

export interface WorldCell {
  x: number
  y: number
  terrain?: string
  resource?: string | number
}

export interface WorldResource {
  type: string
  quantity: number
}

export interface WorldObstacle {
  id: string
  x: number
  y: number
  radius: number
}

export interface WorldSnapshot {
  width?: number
  height?: number
  resources?: WorldResource[]
  obstacles?: WorldObstacle[]
  season?: string
  seasonIndex?: number
}

export interface LiveAgent {
  id: string
  position?: { x: number; y: number }
  positionX?: number
  positionY?: number
  [key: string]: unknown
}

export type EngineData = Record<string, number>

export interface RunDetail {
  run_id: string
  version: string
  seed: string
  ticks_count: number
  first_tick: number
  last_tick: number
  metrics: Record<string, EngineData>
  phenomena: PhenomenaResponse | null
}

export interface ExportRow {
  tick: number
  engine: string
  metric: string
  value: number
}

export interface ExportJsonResponse {
  run_id: string
  format: 'json'
  rows: ExportRow[]
}

export interface Belief {
  subject: string
  predicate: string
  value: string
  confidence: number
}

export interface BeliefsResponse {
  agent_id: string
  run_id: string
  tick: number
  beliefs: Belief[]
}

export interface TrustEdge {
  peerId: string
  trust: number
}

export interface RelationshipsResponse {
  agent_id: string
  run_id: string
  tick: number
  trust: TrustEdge[]
  count: number
}

export interface Group {
  label: string
  members: string[]
  size: number
}

export interface GroupsResponse {
  run_id: string
  tick: number
  groups: Group[]
}

export interface PhenomenonSignal {
  metric: string
  value: number
  threshold: number
}

export interface Phenomenon {
  identifier: string
  label: string
  description?: string
  firstTick?: number
  lastTick?: number
  occurrences?: number
  signals: PhenomenonSignal[]
}

export interface PhenomenaResponse {
  run_id: string
  tick: number
  phenomena: Phenomenon[]
  disclaimer: string
}

export interface Decision {
  tick: number
  agent_id: string
  chosen_action: string
  utility: number
  deliberated: boolean
  interrupted: boolean
  cause: string
  beliefs_count: number
  goals_count: number
  memory_count: number
  needs: Record<string, number>
}

export interface DecisionsResponse {
  run_id: string
  decisions: Decision[]
}

export interface ChainNode {
  layer: string
  tick: number
  label: string
  detail: Record<string, unknown>
}

export interface CausalChainResponse {
  run_id: string
  agent_id: string
  tick: number
  depth_requested: number
  depth_served: number
  chain: ChainNode[]
  cycle: boolean
  cycles: Array<{ layer: string; label: string; ticks: number[] }>
  truncated: boolean
}

export interface CompareRunMeta {
  run_id: string
  version: string
  seed: string
}

export interface CompareSeriesRow {
  tick: number
  engine: string
  metric: string
  run_a_value: number | null
  run_b_value: number | null
  diff: number | null
}

export interface CompareResponse {
  run_a: CompareRunMeta
  run_b: CompareRunMeta
  same_seed: boolean
  same_version: boolean
  bit_identical: boolean
  is_reproducible: boolean
  reproducibility_score: number
  cognitive_diff: number
  social_diff: number
  series: CompareSeriesRow[]
  format: string
}

export interface ApiErrorPayload {
  detail?: string
}

export type WsMessage =
  | {
      type: 'snapshot'
      tick: number
      agents: LiveAgent[]
      resources?: WorldResource[]
      obstacles?: WorldObstacle[]
      season?: string
      seasonIndex?: number
    }
  | { type: 'event'; tick: number; event_type: string; agent_id?: string }
  | { type: string; tick?: number; [key: string]: unknown }