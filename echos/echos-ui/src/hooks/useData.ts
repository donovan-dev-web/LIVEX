import { useCallback, useEffect, useRef, useState } from 'react'
import { client } from '../api/client'
import { useRunsStore, useLiveStore } from '../store'
import type { MetricsResponse, PhenomenaResponse } from '../api/types'

/**
 * Cache de lecture, borné au navigateur.
 *
 * La clé inclut le sous-échantillonnage `every` : deux cadences sur le même run
 * produisent des séries de longueurs et de ticks différents, et les fusionner
 * produisait des trous (`NaN`) au lieu de deux séries cohérentes. Le tableau de
 * bord (`every=1`) et l'écran Analyse (`every=2,5,10…`) partageaient la même clé
 * et se corrompaient mutuellement au premier changement de cadence.
 */
const cacheKey = (kind: string, id: string, variant = '') =>
  `echos:${kind}:${id}${variant ? `:${variant}` : ''}`

function readCache<T>(key: string): T | null {
  try { return JSON.parse(localStorage.getItem(key) ?? 'null') as T | null } catch { return null }
}
function writeCache(key: string, value: unknown) {
  try { localStorage.setItem(key, JSON.stringify(value)) } catch { /* storage is optional */ }
}

function message(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

function mergeMetrics(previous: MetricsResponse | null, next: MetricsResponse): MetricsResponse {
  if (!previous || previous.run_id !== next.run_id) return next
  if (previous.every !== next.every) return next
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
      next.ticks.forEach((tick, i) => {
        if (typeof data[i] === 'number') byTick.set(tick, data[i])
      })
      // Un tick sans valeur reste `NaN` : il n'a pas été observé, ce qui est
      // différent d'une valeur nulle. Les graphiques le traitent comme un trou.
      values[engine][metric] = ticks.map((tick) => byTick.get(tick) ?? NaN)
    }
  }
  const previousLatestTick = previous.latest_tick ?? null
  const nextLatestTick = next.latest_tick ?? null
  const latestTick =
    Math.max(
      previousLatestTick ?? 0,
      nextLatestTick ?? 0,
      ticks[ticks.length - 1] ?? 0,
    ) || null
  return {
    ...previous,
    ...next,
    ticks,
    values,
    measured: next.measured ?? previous.measured,
    // `latest` ne recule jamais : une réponse plus ancienne (course entre deux
    // rafraîchissements) ne doit pas remplacer une valeur plus avancée.
    latest:
      previous.latest && nextLatestTick !== null && previousLatestTick !== null && nextLatestTick < previousLatestTick
        ? previous.latest
        : next.latest,
    latest_tick: latestTick,
  }
}

/**
 * Charge la liste des runs. Appelé une seule fois par `AppShell` : appelé dans
 * chaque écran, il déclenchait une requête `/api/runs` par écran monté, et le
 * sélecteur de run ne pouvait pas exister avant que chaque écran ait fini de
 * charger.
 */
export function useLoadRuns() {
  const setRuns = useRunsStore((s) => s.setRuns)
  const setLoading = useRunsStore((s) => s.setLoading)
  const setError = useRunsStore((s) => s.setError)
  const inFlight = useRef<Promise<unknown> | null>(null)

  const refresh = useCallback(() => {
    // Requêtes concurrentes partagées : un re-rendu ne relance pas la requête.
    if (inFlight.current) return inFlight.current
    setLoading(true)
    const request = client
      .runs()
      .then((res) => {
        setRuns(res.runs)
        setError(null)
      })
      .catch((err) => setError(message(err)))
      .finally(() => {
        inFlight.current = null
        setLoading(false)
      })
    inFlight.current = request
    return request
  }, [setRuns, setError, setLoading])

  useEffect(() => {
    void refresh()
  }, [refresh])

  return { refresh }
}

export function useRunDetail(runId: string | null) {
  const [detail, setDetail] = useState<Awaited<ReturnType<typeof client.run>> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!runId) return
    let cancelled = false
    setDetail(null)
    setError(null)
    client
      .run(runId)
      .then((d) => !cancelled && setDetail(d))
      .catch((err) => !cancelled && setError(message(err)))
    return () => {
      cancelled = true
    }
  }, [runId])

  return { detail, error }
}

export function useMetrics(runId: string | null, every = 1) {
  const key = runId ? cacheKey('metrics', runId, `every-${every}`) : null
  const [metrics, setMetrics] = useState<MetricsResponse | null>(() =>
    key ? readCache<MetricsResponse>(key) : null,
  )
  const [error, setError] = useState<string | null>(null)
  const [refreshedAt, setRefreshedAt] = useState<number | null>(null)

  useEffect(() => {
    if (!runId || !key) {
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
            const merged = mergeMetrics(current ?? readCache<MetricsResponse>(key), m)
            writeCache(key, merged)
            return merged
          })
          setError(null)
          setRefreshedAt(Date.now())
        })
        .catch((err) => {
          if (!cancelled) setError(message(err))
        })
    }
    setMetrics(readCache<MetricsResponse>(key))
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
  }, [runId, key, every])

  return { metrics, error, refreshedAt }
}

export function useGroups(runId: string | null) {
  const [groups, setGroups] = useState<Awaited<ReturnType<typeof client.groups>> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!runId) {
      setGroups(null)
      setError(null)
      return
    }
    let cancelled = false
    const refresh = () =>
      client
        .groups({ runId })
        .then((g) => {
          if (cancelled) return
          setGroups(g)
          setError(null)
        })
        .catch((err) => {
          if (!cancelled) setError(message(err))
        })
    // Le groupe affiché doit suivre le tick observé : sans rafraîchissement, le
    // compteur « Groupes actifs » restait figé sur le tick du montage.
    setGroups(null)
    refresh()
    const unsubscribe = useLiveStore.subscribe((state, previous) => {
      if (state.live?.tick !== previous.live?.tick) refresh()
    })
    return () => {
      cancelled = true
      unsubscribe()
    }
  }, [runId])

  return { groups, error }
}

export function usePhenomena(runId: string | null) {
  const key = runId ? cacheKey('phenomena', runId) : null
  const [phenomena, setPhenomena] = useState<PhenomenaResponse | null>(() =>
    key ? readCache<PhenomenaResponse>(key) : null,
  )
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!runId || !key) {
      setPhenomena(null)
      setError(null)
      return
    }
    let cancelled = false
    const refresh = () =>
      client
        .phenomena(runId)
        .then((next) => {
          if (cancelled) return
          setPhenomena((current) => {
            const merged = current
              ? {
                  ...next,
                  phenomena: [
                    ...new Map(
                      [...current.phenomena, ...next.phenomena].map((p) => [p.identifier, p]),
                    ).values(),
                  ],
                }
              : next
            writeCache(key, merged)
            return merged
          })
          setError(null)
        })
        .catch((err) => {
          // L'erreur était avalée : un échec réseau affichait « Aucun phénomène
          // détecté », soit un constat faux présenté comme une observation.
          if (!cancelled) setError(message(err))
        })
    refresh()
    const unsubscribe = useLiveStore.subscribe((state, previous) => {
      if (state.live?.tick !== previous.live?.tick) refresh()
    })
    return () => {
      cancelled = true
      unsubscribe()
    }
  }, [runId, key])

  return { phenomena, error }
}
