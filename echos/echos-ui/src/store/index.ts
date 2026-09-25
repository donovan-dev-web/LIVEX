import { create } from 'zustand'
import type { RunMeta, WorldSnapshot } from '../api/types'

export type WsState = 'connected' | 'connecting' | 'disconnected'

export interface LiveSnapshot {
  tick: number
  agentCount: number
  messageCount: number
  agents: Record<string, unknown>[]
  world?: WorldSnapshot
}

export interface LiveState {
  wsState: WsState
  wsError: string | null
  live: LiveSnapshot | null
  setWsState: (state: WsState) => void
  setWsError: (error: string | null) => void
  setLive: (snapshot: LiveSnapshot | null) => void
}

export interface RunsState {
  runs: RunMeta[]
  selectedRunId: string | null
  loading: boolean
  error: string | null
  setRuns: (runs: RunMeta[]) => void
  selectRun: (runId: string) => void
  setLoading: (loading: boolean) => void
  setError: (error: string | null) => void
}

export const useLiveStore = create<LiveState>((set) => ({
  wsState: 'disconnected',
  wsError: null,
  live: null,
  setWsState: (wsState) => set({ wsState }),
  setWsError: (wsError) => set({ wsError }),
  setLive: (live) => set({ live }),
}))

export const isWsConnected = () => useLiveStore.getState().wsState === 'connected'

export const useRunsStore = create<RunsState>((set) => ({
  runs: [],
  selectedRunId: null,
  loading: false,
  error: null,
  setRuns: (runs) =>
    set((state) => ({
      runs,
      selectedRunId:
        state.selectedRunId && runs.some((r) => r.run_id === state.selectedRunId)
          ? state.selectedRunId
          : runs.length > 0
            ? runs[runs.length - 1].run_id
            : null,
    })),
  selectRun: (runId) => set({ selectedRunId: runId }),
  setLoading: (loading) => set({ loading }),
  setError: (error) => set({ error }),
}))

export const useRuns = () => useRunsStore((s) => s.runs)
export const useSelectedRunId = () => useRunsStore((s) => s.selectedRunId)