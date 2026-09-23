import { useEffect, useState } from 'react'
import { client } from '../api/client'
import { useRunsStore } from '../store'

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
  const [metrics, setMetrics] = useState<Awaited<ReturnType<typeof client.metrics>> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!runId) return
    let cancelled = false
    client
      .metrics(runId, { every })
      .then((m) => !cancelled && setMetrics(m))
      .catch((err) => !cancelled && setError(err instanceof Error ? err.message : String(err)))
    return () => {
      cancelled = true
    }
  }, [runId, every])

  return { metrics, error }
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