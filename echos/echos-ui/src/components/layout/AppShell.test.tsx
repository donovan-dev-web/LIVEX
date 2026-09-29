import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppShell } from './AppShell'
import { useRunsStore } from '../../store'

const connect = vi.fn()
const disconnect = vi.fn()

vi.mock('../../ws/realtime', () => ({
  connect: () => connect(),
  disconnect: () => disconnect(),
}))

const RUNS = [
  { run_id: 'run-1', version: '0.1.0', seed: '1', ticks_count: 100, first_tick: 1, last_tick: 100 },
  { run_id: 'run-2', version: '0.1.0', seed: '2', ticks_count: 50, first_tick: 1, last_tick: 50 },
]

function StubScreen() {
  return <p>écran</p>
}

function renderShell() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<StubScreen />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('AppShell', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    useRunsStore.setState({ runs: [], selectedRunId: null, loading: false, error: null })
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve({ ok: true, status: 200, json: async () => ({ runs: RUNS }) })),
    )
  })

  afterEach(() => vi.unstubAllGlobals())

  it('ne charge la liste des runs qu’une fois pour toute la coquille', async () => {
    // Régression : `useLoadRuns()` était appelé par chaque écran, donc une
    // requête `/api/runs` par écran monté et par navigation.
    renderShell()
    await waitFor(() => expect(screen.getByText('écran')).toBeInTheDocument())

    const urls = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls.map((c) =>
      String(c[0]),
    )
    expect(urls.filter((url) => url.includes('/api/runs') && !url.includes('run_id'))).toHaveLength(1)
  })

  it('sélectionne le run affiché et le propage au store', async () => {
    const user = userEvent.setup()
    renderShell()
    const picker = await screen.findByLabelText('Run affiché')

    await waitFor(() => expect(useRunsStore.getState().runs).toHaveLength(2))
    expect((picker as HTMLSelectElement).value).toBe('run-2')

    await user.selectOptions(picker, 'run-1')
    expect(useRunsStore.getState().selectedRunId).toBe('run-1')
  })

  it('ferme le flux temps réel au démontage', async () => {
    // Sans nettoyage, la socket et son minuteur de reconnexion survivaient au
    // démontage de la coquille.
    const { unmount } = renderShell()
    await waitFor(() => expect(connect).toHaveBeenCalledTimes(1))

    unmount()

    expect(disconnect).toHaveBeenCalledTimes(1)
  })

  it('affiche l’erreur de chargement des runs', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve({ ok: false, status: 503, json: async () => ({ detail: 'store fermé' }) })),
    )
    renderShell()

    expect(await screen.findByText(/Runs indisponibles : store fermé/)).toBeInTheDocument()
  })
})
