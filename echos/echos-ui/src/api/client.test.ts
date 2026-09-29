import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { client, controlClient, ApiError } from './client'

const CSV_BODY = 'run_id,tick,engine,metric,value\r\nrun-1,1,E,M,0.5\r\n'

function stubFetch(handler: (url: string, init?: RequestInit) => unknown) {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
    Promise.resolve(handler(String(input), init)),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body })

describe('client.exportRun', () => {
  beforeEach(() => vi.clearAllMocks())
  afterEach(() => vi.unstubAllGlobals())

  it('retourne le CSV brut, pas une enveloppe JSON', async () => {
    // Régression : la route renvoie toujours du JSON, le CSV est dans `body`.
    // Le client traitait la réponse comme un `Blob` jamais atteint par la
    // branche CSV, et le fichier `.csv` téléchargé contenait du JSON.
    const fetchMock = stubFetch(() =>
      ok({ run_id: 'run-1', content_type: 'text/csv', body: CSV_BODY }),
    )

    const file = await client.exportRun('run-1', 'csv')

    expect(file).toEqual({
      filename: 'run-1.csv',
      mime: 'text/csv',
      content: CSV_BODY,
    })
    // L'en-tête `Accept: text/csv` annonçait un contenu qui n'arrivait jamais.
    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect(init.headers).toEqual({ Accept: 'application/json' })
  })

  it('sérialise l’export JSON en texte indenté', async () => {
    stubFetch(() =>
      ok({
        run_id: 'run-1',
        format: 'json',
        rows: [{ tick: 1, engine: 'E', metric: 'M', value: 0.5 }],
      }),
    )

    const file = await client.exportRun('run-1', 'json')

    expect(file.mime).toBe('application/json')
    expect(JSON.parse(file.content)).toMatchObject({ run_id: 'run-1', format: 'json' })
  })

  it('expose le message `detail` d’une erreur HTTP', async () => {
    stubFetch(() => ({
      ok: false,
      status: 400,
      json: async () => ({ detail: 'format inconnu : xml' }),
    }))

    await expect(client.exportRun('run-1', 'json')).rejects.toThrow('format inconnu : xml')
  })
})

describe('client — run explicite', () => {
  beforeEach(() => vi.clearAllMocks())
  afterEach(() => vi.unstubAllGlobals())

  it.each([
    ['beliefs', (id: string, runId?: string) => client.beliefs(id, runId)],
    ['relationships', (id: string, runId?: string) => client.relationships(id, runId)],
  ])('%s transmet le run sélectionné', async (_name, call) => {
    // Sans `run_id`, l'API résout « le run le plus récent » : l'interface
    // affichait ces données sous le run choisi dans la barre latérale.
    const fetchMock = stubFetch(() => ok({}))

    await call('A', 'run-42')

    expect(String(fetchMock.mock.calls[0][0])).toContain('run_id=run-42')
  })

  it('encode les identifiants de run dans la requête', async () => {
    const fetchMock = stubFetch(() => ok({}))

    await client.phenomena('run 7/b')

    expect(String(fetchMock.mock.calls[0][0])).toContain('run_id=run%207%2Fb')
  })

  it('omet run_id quand aucun run n’est sélectionné', async () => {
    const fetchMock = stubFetch(() => ok({}))

    await client.phenomena()

    expect(String(fetchMock.mock.calls[0][0])).not.toContain('run_id')
  })
})

describe('client.compareCsv', () => {
  beforeEach(() => vi.clearAllMocks())
  afterEach(() => vi.unstubAllGlobals())

  it('retourne le corps CSV et le nom de fichier dérivé des deux runs', async () => {
    stubFetch(() => ok({ summary: {}, content_type: 'text/csv', body: 'tick,engine\n1,E\n' }))

    const file = await client.compareCsv('run-a', 'run-b')

    expect(file.filename).toBe('compare-run-a-run-b.csv')
    expect(file.content).toBe('tick,engine\n1,E\n')
  })
})

describe('controlClient', () => {
  beforeEach(() => vi.clearAllMocks())
  afterEach(() => vi.unstubAllGlobals())

  it('remonte une erreur de relais avec son message', async () => {
    stubFetch(() => ({
      ok: false,
      status: 502,
      json: async () => ({ detail: 'SYNE injoignable' }),
    }))

    const failure = await controlClient.command('start', { seed: 1 }).catch((e) => e)

    expect(failure).toBeInstanceOf(ApiError)
    expect((failure as ApiError).status).toBe(502)
    expect((failure as ApiError).message).toBe('SYNE injoignable')
  })
})
