import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import { SocialGraph } from './SocialGraph'
import { useLiveStore } from '../../store'

vi.mock('echarts-for-react', () => ({
  default: () => <div aria-label="echart" />,
}))

const MEMBERS = Array.from({ length: 20 }, (_, i) => `A${i}`)

const GROUPS = {
  run_id: 'run-1',
  tick: 7,
  groups: [{ label: 'A0', members: MEMBERS, size: MEMBERS.length }],
}

function stubApi() {
  let inFlight = 0
  let peak = 0
  const calls: string[] = []
  const request = vi.fn((input: RequestInfo | URL) => {
    const url = String(input)
    calls.push(url)
    if (url.includes('/api/groups')) {
      return Promise.resolve({ ok: true, json: async () => GROUPS })
    }
    inFlight += 1
    peak = Math.max(peak, inFlight)
    const agent = decodeURIComponent(url.split('/api/relationships/')[1]?.split('?')[0] ?? '')
    const peer = agent === 'A19' ? 'A0' : `A${Number(agent.slice(1)) + 1}`
    return Promise.resolve({
      ok: true,
      json: async () => {
        inFlight -= 1
        return { run_id: 'run-1', tick: 7, trust: [{ peerId: peer, trust: 0.8 }], count: 1 }
      },
    })
  })
  vi.stubGlobal('fetch', request)
  return { calls, get peak() { return peak } }
}

function pushTick(tick: number) {
  act(() => {
    useLiveStore.setState({
      live: { tick, agentCount: 2, messageCount: 0, agents: [{ id: 'A' }] },
    })
  })
}

describe('SocialGraph', () => {
  beforeEach(() => {
    useLiveStore.setState({ wsState: 'connected', wsError: null, live: null })
  })
  afterEach(() => vi.unstubAllGlobals())

  it('borne les requêtes de confiance et les mène de front', async () => {
    // Régression : une requête par membre, en série, relancée à chaque tick.
    // Sur 20 entités, c'était 20 requêtes séquentielles par tick.
    const api = stubApi()

    render(<SocialGraph runId="run-1" onSelectAgent={() => {}} />)
    await waitFor(() =>
      expect(api.calls.filter((u) => u.includes('/api/relationships/'))).toHaveLength(20),
    )
    expect(api.peak).toBeGreaterThan(1)
    expect(api.peak).toBeLessThanOrEqual(6)

    await act(async () => {
      await Promise.resolve()
    })
  })

  it('reconstruit une seule fois par tick, pas à chaque rendu', async () => {
    const api = stubApi()
    const { rerender } = render(<SocialGraph runId="run-1" onSelectAgent={() => {}} />)
    await waitFor(() =>
      expect(api.calls.filter((u) => u.includes('/api/relationships/'))).toHaveLength(20),
    )
    expect(api.calls.filter((u) => u.includes('/api/groups'))).toHaveLength(1)

    // Re-rendus sur le même tick : aucune requête supplémentaire.
    rerender(<SocialGraph runId="run-1" onSelectAgent={() => {}} />)
    rerender(<SocialGraph runId="run-1" onSelectAgent={() => {}} />)
    await act(async () => {
      await Promise.resolve()
    })
    expect(api.calls.length).toBe(21)

    // Un nouveau tick déclenche exactement un cycle supplémentaire.
    pushTick(9)
    await waitFor(() =>
      expect(api.calls.filter((u) => u.includes('/api/groups'))).toHaveLength(2),
    )
    expect(await screen.findByText(/tick 7/)).toBeInTheDocument()
  })

  it('transmet le run sélectionné aux requêtes de confiance', async () => {
    const api = stubApi()
    render(<SocialGraph runId="run-42" onSelectAgent={() => {}} />)
    await waitFor(() =>
      expect(api.calls.filter((u) => u.includes('/api/relationships/')).length).toBeGreaterThan(0),
    )
    for (const url of api.calls) {
      expect(url).toContain('run_id=run-42')
    }
  })

  it('ignore un run absent', async () => {
    const api = stubApi()
    render(<SocialGraph runId={null} onSelectAgent={() => {}} />)
    await act(async () => {
      await Promise.resolve()
    })
    expect(api.calls).toHaveLength(0)
  })
})
