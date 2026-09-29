import { API_BASE, CONTROL_BASE } from '../config'
import type {
  BeliefsResponse,
  CausalChainResponse,
  CompareCsvResponse,
  CompareResponse,
  DecisionsResponse,
  ExportCsvResponse,
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
 * Fichier prêt à télécharger, texte normalisé.
 *
 * L'API ECHOS renvoie **toujours du JSON**, y compris pour ``format=csv`` : le
 * CSV est transporté dans une enveloppe ``{content_type, body}``. Le client
 * convertit cette enveloppe en contenu texte, sinon un fichier ``.csv``
 * téléchargé contenait du JSON et aucun tableur ne pouvait l'ouvrir.
 */
export interface ExportFile {
  filename: string
  mime: string
  content: string
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

  /**
   * Export d'un run, normalisé en fichier téléchargeable.
   *
   * Aucun en-tête ``Accept: text/csv`` n'est envoyé : la route renvoie du JSON
   * dans les deux cas, et un ``Accept`` de CSV ne faisait qu'annoncer un
   * contenu qui n'arrivait jamais.
   */
  async exportRun(runId: string, format: 'json' | 'csv' = 'json'): Promise<ExportFile> {
    const envelope = await request<ExportJsonResponse | ExportCsvResponse>(
      `/api/runs/${runId}/export?format=${format}`,
    )
    if ('body' in envelope) {
      return {
        filename: `${runId}.${format}`,
        mime: envelope.content_type,
        content: envelope.body,
      }
    }
    return {
      filename: `${runId}.${format}`,
      mime: 'application/json',
      content: JSON.stringify(envelope, null, 2),
    }
  },

  decisions: (runId: string) => request<DecisionsResponse>(`/api/runs/${runId}/decisions`),

  causalChain: (runId: string, agentId: string, opts: { tick?: number; depth?: number } = {}) => {
    const params = new URLSearchParams()
    if (opts.tick !== undefined) params.set('tick', String(opts.tick))
    if (opts.depth !== undefined) params.set('depth', String(opts.depth))
    const query = params.toString() ? `?${params.toString()}` : ''
    return request<CausalChainResponse>(`/api/runs/${runId}/causal-chains/${agentId}${query}`)
  },

  /**
   * ``runId`` est obligatoire : sans lui, l'API résout « le run le plus
   * récent » et l'interface affichait ces données sous le run sélectionné dans
   * la barre latérale — deux runs confondus sans aucun signal visuel.
   */
  beliefs: (agentId: string, runId?: string) =>
    request<BeliefsResponse>(`/api/beliefs/${agentId}${withRunId(runId)}`),

  relationships: (agentId: string, runId?: string) =>
    request<RelationshipsResponse>(`/api/relationships/${agentId}${withRunId(runId)}`),

  groups: (opts: { runId?: string } = {}) => {
    const query = opts.runId ? `?run_id=${encodeURIComponent(opts.runId)}` : ''
    return request<GroupsResponse>(`/api/groups${query}`)
  },

  phenomena: (runId?: string) =>
    request<PhenomenaResponse>(`/api/emergent-phenomena${withRunId(runId)}`),

  compare: (a: string, b: string, format: 'json' | 'csv' = 'json') =>
    request<CompareResponse | CompareCsvResponse>(
      `/api/compare?run_a=${encodeURIComponent(a)}&run_b=${encodeURIComponent(b)}&format=${format}`,
    ),

  /** Export comparatif CSV, converti en fichier téléchargeable. */
  async compareCsv(a: string, b: string): Promise<ExportFile> {
    const envelope = await request<CompareCsvResponse>(
      `/api/compare?run_a=${encodeURIComponent(a)}&run_b=${encodeURIComponent(b)}&format=csv`,
    )
    return {
      filename: `compare-${a}-${b}.csv`,
      mime: envelope.content_type ?? 'text/csv',
      content: envelope.body,
    }
  },
}

function withRunId(runId?: string): string {
  return runId ? `?run_id=${encodeURIComponent(runId)}` : ''
}

/**
 * Contrôle relayé : l'interface n'invoque jamais SYNE en direct (règle d'or
 * FRONTEND_VISION §2) — elle passe par ECHOS qui relaie vers le contrat HTTP
 * :5181 de SYNE (API_CONTRACTS.md §3).
 */
export const controlClient = {
  async command(action: 'start' | 'pause' | 'resume' | 'stop' | 'reset', body: Record<string, unknown> = {}) {
    const response = await fetch(`${CONTROL_BASE}/api/control/${action}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
    if (!response.ok) {
      let message = `Contrôle « ${action} » refusé (HTTP ${response.status})`
      try {
        const body = (await response.json()) as { detail?: string }
        if (body.detail) message = body.detail
      } catch {
        /* réponse non JSON */
      }
      throw new ApiError(response.status, message)
    }
    return response
  },
}