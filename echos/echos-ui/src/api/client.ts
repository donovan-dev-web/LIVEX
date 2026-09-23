import { API_BASE, CONTROL_BASE } from '../config'
import type {
  BeliefsResponse,
  CausalChainResponse,
  CompareResponse,
  DecisionsResponse,
  ExportJsonResponse,
  GroupsResponse,
  MetricsResponse,
  PhenomenaResponse,
  RelationshipsResponse,
  RunDetail,
  RunsResponse,
} from './types'

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: { Accept: 'application/json' },
    ...init,
  })
  if (!response.ok) {
    let message = `HTTP ${response.status}`
    try {
      const body = (await response.json()) as { detail?: string }
      if (body.detail) message = body.detail
    } catch {
      /* réponse non JSON */
    }
    throw new ApiError(response.status, message)
  }
  return (await response.json()) as T
}

/**
 * API REST ECHOS (:5000, lecture seule). Chaque méthode correspond à un
 * contrat publié dans API_REST.md — l'interface ne calcule jamais de métrique
 * scientifique, elle affiche ce que le service fournit.
 */
export const client = {
  health: () => request<{ status: string }>('/health'),

  runs: () => request<RunsResponse>('/api/runs'),

  run: (runId: string) => request<RunDetail>(`/api/runs/${runId}`),

  metrics: (runId: string, opts: { engine?: string; metric?: string; every?: number } = {}) => {
    const params = new URLSearchParams()
    if (opts.engine) params.set('engine', opts.engine)
    if (opts.metric) params.set('metric', opts.metric)
    if (opts.every && opts.every > 1) params.set('every', String(opts.every))
    const query = params.toString() ? `?${params.toString()}` : ''
    return request<MetricsResponse>(`/api/runs/${runId}/metrics${query}`)
  },

  exportRun: (runId: string, format: 'json' | 'csv' = 'json') =>
    request<ExportJsonResponse | Blob>(
      `/api/runs/${runId}/export?format=${format}`,
      format === 'csv' ? { headers: { Accept: 'text/csv' } } : undefined,
    ),

  decisions: (runId: string) => request<DecisionsResponse>(`/api/runs/${runId}/decisions`),

  causalChain: (runId: string, agentId: string, opts: { tick?: number; depth?: number } = {}) => {
    const params = new URLSearchParams()
    if (opts.tick !== undefined) params.set('tick', String(opts.tick))
    if (opts.depth !== undefined) params.set('depth', String(opts.depth))
    const query = params.toString() ? `?${params.toString()}` : ''
    return request<CausalChainResponse>(`/api/runs/${runId}/causal-chains/${agentId}${query}`)
  },

  beliefs: (agentId: string) => request<BeliefsResponse>(`/api/beliefs/${agentId}`),

  relationships: (agentId: string) => request<RelationshipsResponse>(`/api/relationships/${agentId}`),

  groups: (opts: { runId?: string } = {}) => {
    const query = opts.runId ? `?run_id=${opts.runId}` : ''
    return request<GroupsResponse>(`/api/groups${query}`)
  },

  phenomena: () => request<PhenomenaResponse>('/api/emergent-phenomena'),

  compare: (a: string, b: string, format: 'json' | 'csv' = 'json') =>
    request<CompareResponse>(`/api/compare?run_a=${a}&run_b=${b}&format=${format}`),
}

/**
 * Contrôle relayé : l'interface n'invoque jamais SYNE en direct (règle d'or
 * FRONTEND_VISION §2) — elle passe par ECHOS qui relaie vers le contrat HTTP
 * :5181 de SYNE (API_CONTRACTS.md §3).
 */
export const controlClient = {
  async command(action: 'start' | 'pause' | 'resume' | 'reset', body: Record<string, unknown> = {}) {
    const response = await fetch(`${CONTROL_BASE}/api/control/${action}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
    if (!response.ok) {
      throw new ApiError(response.status, `Contrôle « ${action} » refusé (HTTP ${response.status})`)
    }
    return response
  },
}