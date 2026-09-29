import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { DashboardScreen } from './DashboardScreen'
import { useLiveStore, useRunsStore } from '../store'

vi.mock('echarts-for-react', () => ({ default: () => <div aria-label="echart" /> }))

function stubApi(measured: Record<string, Record<string, boolean>>, diffusion: number) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/metrics')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            run_id: 'run-1',
            engine: null,
            metric: null,
            every: 1,
            ticks: [1, 2],
            values: {
              EmergenceIndicators: { EmergenceScore: [0, 0] },
              InformationPropagationMetrics: { InformationDiffusionSpeed: [diffusion, diffusion] },
            },
            latest: {
              EmergenceIndicators: { EmergenceScore: 0 },
              InformationPropagationMetrics: { InformationDiffusionSpeed: diffusion },
              CognitiveDiversityMetrics: { BeliefDiversity: 0.5, GoalDiversity: 0.5 },
              SocialComplexityMetrics: { ClusteringCoefficient: 0.2 },
            },
            measured,
            latest_tick: 2,
          }),
        })
      }
      if (url.includes('/emergent-phenomena')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({ run_id: 'run-1', tick: 2, phenomena: [], disclaimer: '' }),
        })
      }
      return Promise.resolve({ ok: true, json: async () => ({ run_id: 'run-1', tick: 2, groups: [] }) })
    }),
  )
}

function renderDashboard() {
  return render(
    <MemoryRouter>
      <DashboardScreen />
    </MemoryRouter>,
  )
}

describe('DashboardScreen — provenance des métriques', () => {
  beforeEach(() => {
    useRunsStore.setState({
      runs: [{ run_id: 'run-1', version: '0.1.0', seed: '1', ticks_count: 2, first_tick: 1, last_tick: 2 }],
      selectedRunId: 'run-1',
      loading: false,
      error: null,
    })
    useLiveStore.setState({ wsState: 'connected', wsError: null, live: null })
  })
  afterEach(() => vi.unstubAllGlobals())

  it('présente un repli neutre comme non mesuré', async () => {
    // Sans ce drapeau, le 0.0 d'un moteur sans fenêtre s'affichait
    // indissociable d'un 0.0 réellement observé.
    stubApi({ EmergenceIndicators: { EmergenceScore: false } }, 0)
    renderDashboard()

    expect(await screen.findByText(/Repli neutre/)).toBeInTheDocument()
    expect(screen.getByText('0.000')).toBeInTheDocument()
  })

  it('signale une information qui ne se propage plus', async () => {
    stubApi({ EmergenceIndicators: { EmergenceScore: true } }, 0)
    renderDashboard()

    expect(await screen.findByText(/alerte sous 1/)).toBeInTheDocument()
  })

  it('ne signale pas une diffusion rapide', async () => {
    stubApi({ EmergenceIndicators: { EmergenceScore: true } }, 80)
    renderDashboard()

    await waitFor(() => expect(screen.getByText('80.000')).toBeInTheDocument())
    // Un seuil haut résiduel transformerait une bonne propagation en alerte.
    expect(screen.queryByText(/alerte au-dessus/)).not.toBeInTheDocument()
  })
})
