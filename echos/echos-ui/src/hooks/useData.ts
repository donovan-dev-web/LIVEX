import { useCallback, useEffect, useRef, useState } from 'react'
import { client } from '../api/client'
import { useRunsStore, useLiveStore } from '../store'
import type { MetricsResponse, PhenomenaResponse, RunsResponse } from '../api/types'

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

/**
 * Cadence de rafraîchissement de la liste des runs (ms). Un run d'analyse
 * n'existe dans la base qu'après le premier message ingéré — il ne peut pas
 * être connu au montage : sans re-poll, la liste restait figée sur l'état
 * initial et aucun écran d'analyse ne montrait jamais le run en cours.
 */
const RUNS_REFRESH_MS = 5000

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
 * Charge la liste des runs et la maintient à jour.
 *
 * Le premier chargement a lieu au montage d'`AppShell` (une seule requête
 * partagée : appeler le hook dans chaque écran déclenchait une requête
 * `/api/runs` par écran monté, et le sélecteur de run ne pouvait pas exister
 * avant que chaque écran ait fini de charger). Ensuite, la liste est re-pollée
 * périodiquement : un run lancé **après** l'ouverture de l'application (cas
 * normal du shell bureau, où l'utilisateur démarre SYNE depuis l'écran de
 * pilotage) doit apparaître dans le sélecteur sans rechargement manuel —
 * `setRuns` conserve la sélection courante tant qu'elle existe toujours.
 */
export function useLoadRuns() {
  const refresh = useRunsRefresh()

  useEffect(() => {
    void refresh()
    const timer = window.setInterval(() => void refresh(), RUNS_REFRESH_MS)
    return () => window.clearInterval(timer)
  }, [refresh])

  return { refresh }
}

/**
 * Callback de rafraîchissement sans cycle de vie : pour les écrans qui doivent
 * provoquer un rechargement immédiat (démarrage d'un run depuis le pilotage)
 * sans monter leur propre timer — le polling périodique reste la propriété
 * exclusive d'`AppShell` via `useLoadRuns`. La promesse résout avec la réponse
 * (void en cas d'échec) pour que l'appelant puisse exploiter les données qu'il
 * vient de faire charger dans le store.
 */
export function useRunsRefresh() {
  const setRuns = useRunsStore((s) => s.setRuns)
  const setLoading = useRunsStore((s) => s.setLoading)
  const setError = useRunsStore((s) => s.setError)
  const inFlight = useRef<Promise<RunsResponse | undefined> | null>(null)

  const refresh = useCallback(() => {
    // Requêtes concurrentes partagées : un re-rendu ou un tick de timer ne
    // relance pas la requête si la précédente n'est pas terminée.
    if (inFlight.current) return inFlight.current
    setLoading(true)
    const request = client
      .runs()
      .then((res) => {
        setRuns(res.runs)
        setError(null)
        return res
      })
      .catch((err) => {
        setError(message(err))
        return undefined
      })
      .finally(() => {
        inFlight.current = null
        setLoading(false)
      })
    inFlight.current = request
    return request
  }, [setRuns, setError, setLoading])

  return refresh
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

export type MetricsMode = 'live' | 'manual'

/**
 * Cadence minimale entre deux requêtes `/metrics` en mode live (ms). Les
 * séries sont servies **en entier** à chaque appel : à 10 rafraîchissements par
 * seconde (un par tick), les réponses s'empilaient plus vite qu'elles
 * n'aboutissaient, l'affichage décrochait du run (« retard N ticks ») et le
 * rendu des graphes se figeait. Le flux WS reste le détecteur d'activité ; la
 * cadence API, elle, reste lisible par le service.
 */
const METRICS_THROTTLE_MS = 2000

export function useMetrics(runId: string | null, every = 1) {
  const key = runId ? cacheKey('metrics', runId, `every-${every}`) : null
  const [metrics, setMetrics] = useState<MetricsResponse | null>(() =>
    key ? readCache<MetricsResponse>(key) : null,
  )
  const [error, setError] = useState<string | null>(null)
  const [refreshedAt, setRefreshedAt] = useState<number | null>(null)
  const [mode, setMode] = useState<MetricsMode>('live')
  // Le mode vit dans un ref : le basculer ne doit ni re-monter l'effet (qui
  // rafraîchirait immédiatement, annulant le gel demandé) ni relancer la
  // souscription — il ne change que la politique de déclenchement.
  const modeRef = useRef<MetricsMode>('live')
  useEffect(() => {
    modeRef.current = mode
  }, [mode])
  // Handle de la dernière fermeture de rafraîchissement : exposé à l'appelant
  // pour l'actualisation manuelle (le `refresh` interne vit dans l'effet).
  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    if (!runId || !key) {
      setMetrics(null)
      setError(null)
      setRefreshedAt(null)
      return
    }
    let cancelled = false
    let lastFetch = 0
    let inFlight = false
    const refresh = () => {
      // Une seule requête à la fois : en cas de saturation, on saute un tour
      // plutôt que d'empiler des réponses périmées.
      if (cancelled || inFlight) return
      inFlight = true
      lastFetch = Date.now()
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
        .finally(() => {
          inFlight = false
        })
    }
    setMetrics(readCache<MetricsResponse>(key))
    refreshRef.current = refresh
    refresh()
    const unsubscribe = useLiveStore.subscribe((state, previous) => {
      if (modeRef.current === 'manual') return
      if (state.live?.tick !== previous.live?.tick && Date.now() - lastFetch >= METRICS_THROTTLE_MS) refresh()
    })
    // Filet de sécurité pour les runs dont le flux WS est momentanément
    // silencieux ; en mode live uniquement — un gel manuel doit rester gelé.
    const timer = window.setInterval(() => {
      if (modeRef.current === 'live') refresh()
    }, METRICS_THROTTLE_MS)
    return () => {
      cancelled = true
      unsubscribe()
      window.clearInterval(timer)
    }
  }, [runId, key, every])

  return { metrics, error, refreshedAt, mode, setMode, refresh: () => refreshRef.current() }
}

export function useGroups(runId: string | null, live: MetricsMode = 'live') {
  const [groups, setGroups] = useState<Awaited<ReturnType<typeof client.groups>> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const refreshRef = useRef<() => void>(() => {})
  const liveRef = useRef<MetricsMode>('live')
  useEffect(() => {
    liveRef.current = live
  }, [live])

  useEffect(() => {
    if (!runId) {
      setGroups(null)
      setError(null)
      return
    }
    let cancelled = false
    let lastFetch = 0
    let inFlight = false
    const refresh = () => {
      if (cancelled || inFlight) return
      inFlight = true
      lastFetch = Date.now()
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
        .finally(() => {
          inFlight = false
        })
    }
    // Le groupe affiché doit suivre le tick observé : sans rafraîchissement, le
    // compteur « Groupes actifs » restait figé sur le tick du montage.
    setGroups(null)
    refreshRef.current = refresh
    refresh()
    const unsubscribe = useLiveStore.subscribe((state, previous) => {
      if (liveRef.current === 'manual') return
      if (state.live?.tick !== previous.live?.tick && Date.now() - lastFetch >= METRICS_THROTTLE_MS) refresh()
    })
    return () => {
      cancelled = true
      unsubscribe()
    }
  }, [runId])

  return { groups, error, refresh: () => refreshRef.current() }
}

export function usePhenomena(runId: string | null, live: MetricsMode = 'live') {
  const key = runId ? cacheKey('phenomena', runId) : null
  const [phenomena, setPhenomena] = useState<PhenomenaResponse | null>(() =>
    key ? readCache<PhenomenaResponse>(key) : null,
  )
  const [error, setError] = useState<string | null>(null)
  const refreshRef = useRef<() => void>(() => {})
  const liveRef = useRef<MetricsMode>('live')
  useEffect(() => {
    liveRef.current = live
  }, [live])

  useEffect(() => {
    if (!runId || !key) {
      setPhenomena(null)
      setError(null)
      return
    }
    let cancelled = false
    let lastFetch = 0
    let inFlight = false
    const refresh = () => {
      if (cancelled || inFlight) return
      inFlight = true
      lastFetch = Date.now()
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
        .finally(() => {
          inFlight = false
        })
    }
    refreshRef.current = refresh
    refresh()
    const unsubscribe = useLiveStore.subscribe((state, previous) => {
      if (liveRef.current === 'manual') return
      if (state.live?.tick !== previous.live?.tick && Date.now() - lastFetch >= METRICS_THROTTLE_MS) refresh()
    })
    return () => {
      cancelled = true
      unsubscribe()
    }
  }, [runId, key])

  return { phenomena, error, refresh: () => refreshRef.current() }
}
