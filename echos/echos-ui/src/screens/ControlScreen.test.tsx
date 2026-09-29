import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { ControlScreen } from './ControlScreen'
import { useRunsStore } from '../store'

const CSV_BODY = 'run_id,tick,engine,metric,value\r\nrun-1,1,E,M,0.5\r\n'

describe('ControlScreen — export', () => {
  let downloads: Array<{ filename: string; href: string }>
  let blobs: Array<{ content: string; type: string }>

  beforeEach(() => {
    downloads = []
    blobs = []
    useRunsStore.setState({
      runs: [{ run_id: 'run-1', version: '0.1.0', seed: '1', ticks_count: 3, first_tick: 1, last_tick: 3 }],
      selectedRunId: 'run-1',
      loading: false,
      error: null,
    })
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes('format=csv')) {
          return Promise.resolve({
            ok: true,
            json: async () => ({ run_id: 'run-1', content_type: 'text/csv', body: CSV_BODY }),
          })
        }
        return Promise.resolve({
          ok: true,
          json: async () => ({ run_id: 'run-1', format: 'json', rows: [] }),
        })
      }),
    )
    // jsdom n'implémente ni `Blob.text` ni `URL.createObjectURL` : on enregistre
    // le contenu réellement remis au téléchargement, c'est lui qui prouve que le
    // fichier produit est du CSV et non une enveloppe JSON.
    vi.stubGlobal(
      'Blob',
      class {
        constructor(parts: unknown[], options?: { type?: string }) {
          blobs.push({ content: String(parts[0]), type: options?.type ?? '' })
        }
      },
    )
    Object.defineProperty(globalThis.URL, 'createObjectURL', {
      value: () => 'blob:mock',
      configurable: true,
    })
    Object.defineProperty(globalThis.URL, 'revokeObjectURL', {
      value: () => undefined,
      configurable: true,
    })
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      downloads.push({ filename: this.download, href: this.href })
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  const renderScreen = () =>
    render(
      <MemoryRouter>
        <ControlScreen />
      </MemoryRouter>,
    )

  it('télécharge un fichier .csv contenant du CSV', async () => {
    // Régression : l'export « CSV » téléchargeait l'enveloppe JSON et la
    // nommageait `run-1.csv` — un fichier qu'aucun tableur ne pouvait ouvrir.
    const user = userEvent.setup()
    renderScreen()

    await user.click(await screen.findByRole('button', { name: /^CSV/ }))

    await waitFor(() => expect(downloads).toHaveLength(1))
    expect(downloads[0].filename).toBe('run-1.csv')
    expect(blobs[0].content).toBe(CSV_BODY)
    expect(blobs[0].type).toContain('text/csv')
    expect(await screen.findByText(/exporté/)).toBeInTheDocument()
  })

  it('télécharge un export JSON lisible', async () => {
    const user = userEvent.setup()
    renderScreen()

    await user.click(await screen.findByRole('button', { name: /^JSON/ }))

    await waitFor(() => expect(downloads[0]?.filename).toBe('run-1.json'))
    expect(JSON.parse(blobs[0].content)).toMatchObject({ format: 'json' })
  })

  it('remonte un échec d’export sans annoncer un succès', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve({ ok: false, status: 404, json: async () => ({ detail: 'run inconnu : run-1' }) }),
      ),
    )
    const user = userEvent.setup()
    renderScreen()

    await user.click(await screen.findByRole('button', { name: /^CSV/ }))

    expect(await screen.findByText(/run inconnu : run-1/)).toBeInTheDocument()
    expect(downloads).toHaveLength(0)
  })
})
