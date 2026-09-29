import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { ExploreScreen } from './ExploreScreen'
import { useLiveStore, useRunsStore } from '../store'

function stubApi(groups: Array<{ label: string; members: string[]; size: number }>) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/api/groups')) {
        return Promise.resolve({ ok: true, json: async () => ({ run_id: 'run-1', tick: 3, groups }) })
      }
      return Promise.resolve({
        ok: true,
        json: async () => ({
          agent_id: 'A',
          run_id: 'run-1',
          tick: 3,
          beliefs: [{ subject: 'eau', predicate: 'proche', value: 'oui', confidence: 0.7 }],
          trust: [],
          count: 0,
        }),
      })
    }),
  )
}

function renderScreen() {
  return render(
    <MemoryRouter>
      <ExploreScreen />
    </MemoryRouter>,
  )
}

describe('ExploreScreen', () => {
  beforeEach(() => {
    useRunsStore.setState({ runs: [{ run_id: 'run-1', version: '0.1.0', seed: '1', ticks_count: 3, first_tick: 1, last_tick: 3 }], selectedRunId: 'run-1', loading: false, error: null })
    useLiveStore.setState({ wsState: 'connected', wsError: null, live: null })
  })
  afterEach(() => vi.unstubAllGlobals())

  it('liste les entités isolées, absentes des groupes', async () => {
    // Régression : la liste ne contenait que les membres de groupe, alors que
    // `_groups_of` exclut volontairement les singletons. Une entité seule était
    // donc impossible à inspecter depuis l'interface.
    stubApi([{ label: 'A', members: ['A', 'B'], size: 2 }])
    renderScreen()

    // Les deux membres de groupe d'abord.
    expect(await screen.findByText(/Entités \(2\)/)).toBeInTheDocument()

    // 'C' n'appartient à aucun groupe mais est observé dans le flux.
    act(() => {
      useLiveStore.setState({
        live: { tick: 4, agentCount: 3, messageCount: 0, agents: [{ id: 'A' }, { id: 'C' }] },
      })
    })

    expect(
      await screen.findByRole('heading', { name: /Entités \(3\) · 1 isolée/ }),
    ).toBeInTheDocument()
  })

  it('inspecte une entité isolée sélectionnée au clavier', async () => {
    const user = userEvent.setup()
    stubApi([])
    act(() => {
      useLiveStore.setState({
        live: { tick: 4, agentCount: 2, messageCount: 0, agents: [{ id: 'A' }, { id: 'B' }] },
      })
    })
    renderScreen()

    expect(await screen.findByText(/Entités \(2\)/)).toBeInTheDocument()
    const option = screen.getAllByRole('option')[0]
    option.focus()
    await user.keyboard('{Enter}')

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Entité A' })).toBeInTheDocument(),
    )
    expect(option).toHaveAttribute('aria-selected', 'true')
  })

  it('affiche un message d’erreur si les groupes sont indisponibles', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve({ ok: false, status: 500, json: async () => ({ detail: 'store corrompu' }) })),
    )
    renderScreen()

    expect(await screen.findByText('store corrompu')).toBeInTheDocument()
  })
})
