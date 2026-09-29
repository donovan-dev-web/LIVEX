import { act, render, screen } from '@testing-library/react'
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import { useGroups, useMetrics, usePhenomena } from './useData'

function Probe() {
  const { metrics, error } = useMetrics('run-1')
  return <output>{error ?? metrics?.latest_tick ?? 'loading'}</output>
}

describe('useMetrics', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          json: async () => ({
            run_id: 'run-1',
            engine: null,
            metric: null,
            every: 1,
            ticks: [1],
            values: {},
            latest: {},
            latest_tick: 1,
          }),
        }),
      ),
    )
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('refreshes metrics periodically while a run is selected', async () => {
    render(<Probe />)
    await act(async () => {})
    expect(fetch).toHaveBeenCalledTimes(1)

    await act(async () => {
      vi.advanceTimersByTime(1000)
      await Promise.resolve()
    })
    expect(fetch).toHaveBeenCalledTimes(2)
    expect(screen.getByRole('status')).toHaveTextContent('1')
  })

  it('exposes the last request error and clears it after recovery', async () => {
    const request = vi
      .fn()
      .mockRejectedValueOnce(new Error('API indisponible'))
      .mockResolvedValue({
        ok: true,
        json: async () => ({
          run_id: 'run-1',
          engine: null,
          metric: null,
          every: 1,
          ticks: [2],
          values: {},
          latest: {},
          latest_tick: 2,
        }),
      })
    vi.stubGlobal('fetch', request)
    render(<Probe />)
    await act(async () => {})
    expect(screen.getByRole('status')).toHaveTextContent('API indisponible')

    await act(async () => {
      vi.advanceTimersByTime(1000)
      await Promise.resolve()
    })
    expect(screen.getByRole('status')).toHaveTextContent('2')
  })

  it('does not move the displayed tick backwards when a stale response arrives', async () => {
    const request = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          run_id: 'run-1', engine: null, metric: null, every: 1,
          ticks: [1, 2], values: {}, latest: {}, latest_tick: 2,
        }),
      })
      .mockResolvedValue({
        ok: true,
        json: async () => ({
          run_id: 'run-1', engine: null, metric: null, every: 1,
          ticks: [1], values: {}, latest: {}, latest_tick: 1,
        }),
      })
    vi.stubGlobal('fetch', request)
    render(<Probe />)
    await act(async () => {})
    expect(screen.getByRole('status')).toHaveTextContent('2')
    await act(async () => {
      vi.advanceTimersByTime(1000)
      await Promise.resolve()
    })
    expect(screen.getByRole('status')).toHaveTextContent('2')
  })
})

/** Réponse `/metrics` minimale pour un run donné. */
function metricsBody(runId: string, ticks: number[], every = 1) {
  return {
    run_id: runId,
    engine: null,
    metric: null,
    every,
    ticks,
    values: { EmergenceIndicators: { EmergenceScore: ticks.map((t) => t / 10) } },
    latest: { EmergenceIndicators: { EmergenceScore: (ticks[ticks.length - 1] ?? 0) / 10 } },
    measured: { EmergenceIndicators: { EmergenceScore: true } },
    latest_tick: ticks[ticks.length - 1] ?? null,
  }
}

function EveryProbe({ every }: { every: number }) {
  const { metrics } = useMetrics('run-1', every)
  const values = metrics?.values.EmergenceIndicators?.EmergenceScore ?? []
  return (
    <div>
      <output data-testid="ticks">{metrics?.ticks.join(',') ?? '—'}</output>
      <output data-testid="values">{values.map((v) => (Number.isFinite(v) ? v : 'x')).join(',')}</output>
      <output data-testid="every">{metrics?.every ?? '—'}</output>
    </div>
  )
}

describe('useMetrics — isolation des cadences', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.useFakeTimers()
  })
  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it("ne mélange pas deux sous-échantillonnages sur le même run", async () => {
    // Régression : la clé de cache ne portait pas `every`. Le tableau de bord
    // (every=1) et l'écran Analyse (every=2) partageaient la même entrée, et la
    // fusion intercalait des ticks absents de la réponse.
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        const every = url.includes('every=2') ? 2 : 1
        const ticks = every === 2 ? [1, 3, 5] : [1, 2, 3, 4, 5]
        return Promise.resolve({ ok: true, json: async () => metricsBody('run-1', ticks, every) })
      }),
    )

    const { rerender } = render(<EveryProbe every={1} />)
    await act(async () => {})
    expect(screen.getByTestId('ticks')).toHaveTextContent('1,2,3,4,5')

    rerender(<EveryProbe every={2} />)
    await act(async () => {})
    expect(screen.getByTestId('every')).toHaveTextContent('2')
    expect(screen.getByTestId('ticks')).toHaveTextContent('1,3,5')
    // Aucune valeur creuse : chaque tick affiché a bien été observé.
    expect(screen.getByTestId('values')).toHaveTextContent('0.1,0.3,0.5')
  })
})

describe('usePhenomena — erreurs', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('expose une erreur au lieu d’afficher « aucun phénomène »', async () => {
    // L'erreur était avalée (`.catch(() => undefined)`) : un échec réseau se
    // présentait comme une absence de phénomène, soit un constat faux.
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new Error('API indisponible'))))

    function Probe() {
      const { phenomena, error } = usePhenomena('run-1')
      return <output>{error ?? phenomena?.phenomena.length ?? 0}</output>
    }

    render(<Probe />)
    await act(async () => {})

    expect(screen.getByRole('status')).toHaveTextContent('API indisponible')
  })
})

describe('useGroups — rotation de run', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('vide les groupes du run précédent au changement de run', async () => {
    // Sans réinitialisation, l'écran affichait les groupes du run précédent
    // sous le nom du nouveau run, jusqu'à la fin de la requête.
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const runId = String(input).includes('run-b') ? 'run-b' : 'run-a'
        return Promise.resolve({
          ok: true,
          json: async () => ({
            run_id: runId,
            tick: 3,
            groups: [{ label: runId, members: ['A', 'B'], size: 2 }],
          }),
        })
      }),
    )

    function Probe({ runId }: { runId: string }) {
      const { groups } = useGroups(runId)
      return <output data-testid="groups">{groups?.groups.map((g) => g.label).join(',') ?? '—'}</output>
    }

    const { rerender } = render(<Probe runId="run-a" />)
    await act(async () => {})
    expect(screen.getByTestId('groups')).toHaveTextContent('run-a')

    rerender(<Probe runId="run-b" />)
    expect(screen.getByTestId('groups')).toHaveTextContent('—')
    await act(async () => {})
    expect(screen.getByTestId('groups')).toHaveTextContent('run-b')
  })
})
