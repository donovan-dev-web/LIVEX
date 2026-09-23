import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'

vi.mock('./ws/realtime', () => ({
  connect: vi.fn(),
  disconnect: vi.fn(),
}))

vi.mock('echarts-for-react', () => ({
  default: () => <div aria-label="echart" />,
}))

const json = (body: unknown) => ({ ok: true, status: 200, json: async () => body })

function mockFetch(route: string) {
  if (route.includes('/metrics')) {
    return Promise.resolve(
      json({
        run_id: 'run-1',
        engine: null,
        metric: null,
        every: 1,
        ticks: [1, 2, 3],
        values: { EmergenceIndicators: { EmergenceScore: [0.5, 0.52, 0.51] } },
        latest: { EmergenceIndicators: { EmergenceScore: 0.51 } },
      }),
    )
  }
  if (route.includes('/groups')) {
    return Promise.resolve(json({ run_id: 'run-1', tick: 3, groups: [{ label: 'A', members: ['A', 'B'], size: 2 }] }))
  }
  if (route.includes('/emergent-phenomena')) {
    return Promise.resolve(
      json({
        run_id: 'run-1',
        tick: 3,
        phenomena: [
          { identifier: 'InformationBottleneck', label: 'Goulot d’information', signals: [] },
        ],
        disclaimer: 'ECHOS ne doit jamais transformer une métrique en vérité scientifique.',
      }),
    )
  }
  if (route.includes('/api/compare')) {
    return Promise.resolve(
      json({
        run_a: { run_id: 'run-1', version: '0.1.0', seed: '1' },
        run_b: { run_id: 'run-2', version: '0.1.0', seed: '1' },
        same_seed: true,
        same_version: true,
        bit_identical: true,
        is_reproducible: true,
        reproducibility_score: 1,
        cognitive_diff: 0,
        social_diff: 0,
        series: [{ tick: 1, engine: 'E', metric: 'M', run_a_value: 1, run_b_value: 1, diff: 0 }],
        format: 'json',
      }),
    )
  }
  if (route.includes('/causal-chains/')) {
    return Promise.resolve(
      json({
        run_id: 'run-1',
        agent_id: 'A',
        tick: 3,
        depth_requested: 7,
        depth_served: 7,
        chain: [{ layer: 'Action', tick: 3, label: 'SeekFood', detail: {} }],
        cycle: false,
        cycles: [],
        truncated: false,
      }),
    )
  }
  return Promise.resolve(json({ runs: [{ run_id: 'run-1', version: '0.1.0', seed: '1', ticks_count: 1200, first_tick: 1, last_tick: 1200 }] }))
}

describe('App', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    globalThis.fetch = vi.fn((input: RequestInfo | URL) =>
  mockFetch(String(input)),
) as unknown as typeof fetch
  })

  it("affiche le titre de l'application", async () => {
    render(<App />)
    await waitFor(() => expect(screen.getByText(/ECHOS/i)).toBeInTheDocument())
    expect(await screen.findByRole('heading', { name: 'Tableau de bord' })).toBeInTheDocument()
  })

  it('navigue vers un écran via la barre latérale', async () => {
    const user = userEvent.setup()
    render(<App />)
    await user.click(await screen.findByText(/Pilotage & calibration/))
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Pilotage & calibration' })).toBeInTheDocument(),
    )
  })

  it('navigue vers exploration', async () => {
    const user = userEvent.setup()
    render(<App />)
    await user.click(await screen.findByText(/Exploration/))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Exploration' })).toBeInTheDocument())
  })
})