import { useEffect, useState } from 'react'
import { client } from '../api/client'
import { useRunsStore } from '../store'
import { useLiveStore } from '../store'
import type { MetricsResponse, PhenomenaResponse } from '../api/types'

const cacheKey = (kind: string, id: string) => `echos:${kind}:${id}`
function readCache<T>(key: string): T | null {
  try { return JSON.parse(localStorage.getItem(key) ?? 'null') as T | null } catch { return null }
}
function writeCache(key: string, value: unknown) {
  try { localStorage.setItem(key, JSON.stringify(value)) } catch { /* storage is optional */ }
}

function mergeMetrics(previous: MetricsResponse | null, next: MetricsResponse): MetricsResponse {
  if (!previous || previous.run_id !== next.run_id) return next
  const ticks = [...new Set([...previous.ticks, ...next.ticks])].sort((a, b) => a - b)
  const values: MetricsResponse['values'] = { ...previous.values }
  for (const [engine, metrics] of Object.entries(next.values)) {
    values[engine] = { ...(values[engine] ?? {}) }
    for (const [metric, data] of Object.entries(metrics)) {
      const previousData = previous.values[engine]?.[metric] ?? []
      const byTick = new Map<number, number>()
      previous.ticks.forEach((tick, i) => {
        if (typeof previousData[i] === 'number') byTick.set(tick, previousData[i])
      })
      next.ticks.forEach((tick, i) => byTick.set(tick, data[i]))
      values[engine][metric] = ticks.map((tick) => byTick.get(tick) ?? NaN)
    }
  }
  return { ...next, ticks, values }
}

export function useLoadRuns() {
  const setRuns = useRunsStore((s) => s.setRuns)
  const setLoading = useRunsStore((s) => s.setLoading)
  const setError = useRunsStore((s) => s.setError)

  useEffect(() => {
    setLoading(true)
    client
      .runs()
      .then((res) => setRuns(res.runs))
      .catch((err) => setError(err instanceof Error ? err.message : String(err)))
      .finally(() => setLoading(false))
  }, [setRuns, setLoading, setError])
}

export function useRunDetail(runId: string | null) {
  const [detail, setDetail] = useState<Awaited<ReturnType<typeof client.run>> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!runId) return
    let cancelled = false
    client
      .run(runId)
      .then((d) => !cancelled && setDetail(d))
      .catch((err) => !cancelled && setError(err instanceof Error ? err.message : String(err)))
    return () => {
      cancelled = true
    }
  }, [runId])

  return { detail, error }
}

export function useMetrics(runId: string | null, every = 1) {
  const [metrics, setMetrics] = useState<Awaited<ReturnType<typeof client.metrics>> | null>(() => runId ? readCache(cacheKey('metrics', runId)) : null)
  const [error, setError] = useState<string | null>(null)
  const [refreshedAt, setRefreshedAt] = useState<number | null>(null)

  useEffect(() => {
    if (!runId) {
      setMetrics(null)
      setError(null)
      setRefreshedAt(null)
      return
    }
    let cancelled = false
    const refresh = () => {
      client
        .metrics(runId, { every })
        .then((m) => {
          if (cancelled) return
          setMetrics((current) => {
            const merged = mergeMetrics(current ?? readCache<MetricsResponse>(cacheKey('metrics', runId)), m)
            writeCache(cacheKey('metrics', runId), merged)
            return merged
          })
          setError(null)
          setRefreshedAt(Date.now())
        })
        .catch((err) => {
          if (!cancelled) setError(err instanceof Error ? err.message : String(err))
        })
    }
    refresh()
    const unsubscribe = useLiveStore.subscribe((state, previous) => {
      if (state.live?.tick !== previous.live?.tick) refresh()
    })
    // Keep a bounded fallback for runs whose stream is temporarily quiet;
    // tick notifications remain the primary refresh trigger.
    const timer = window.setInterval(refresh, 1000)
    return () => {
      cancelled = true
      unsubscribe()
      window.clearInterval(timer)
    }
  }, [runId, every])

  return { metrics, error, refreshedAt }
}

export function useGroups(runId: string | null) {
  const [groups, setGroups] = useState<Awaited<ReturnType<typeof client.groups>> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!runId) return
    let cancelled = false
    client
      .groups({ runId })
      .then((g) => !cancelled && setGroups(g))
      .catch((err) => !cancelled && setError(err instanceof Error ? err.message : String(err)))
    return () => {
      cancelled = true
    }

  }, [runId])

  return { groups, error }
}

export function usePhenomena(runId: string | null) {
  const [phenomena, setPhenomena] = useState<PhenomenaResponse | null>(() => runId ? readCache(cacheKey('phenomena', runId)) : null)
  useEffect(() => {
    if (!runId) return
    let cancelled = false
    const refresh = () => client.phenomena().then((next) => {
      if (cancelled) return
      setPhenomena((current) => {
        const merged = current
          ? { ...next, phenomena: [...new Map([...current.phenomena, ...next.phenomena].map((p) => [p.identifier, p])).values()] }
          : next
        writeCache(cacheKey('phenomena', runId), merged)
        return merged
      })
    }).catch(() => undefined)
    refresh()
    const unsubscribe = useLiveStore.subscribe((state, previous) => {
      if (state.live?.tick !== previous.live?.tick) refresh()
    })
    return () => { cancelled = true; unsubscribe() }
  }, [runId])
  return phenomena
}