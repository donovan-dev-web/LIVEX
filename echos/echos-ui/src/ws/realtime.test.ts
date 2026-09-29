import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { connect, disconnect } from './realtime'
import { useLiveStore } from '../store'

/** WebSocket contrôlable : le module `realtime` n'a pas d'export d'injection. */
class FakeSocket {
  static instances: FakeSocket[] = []
  static readonly CONNECTING = 0
  static readonly OPEN = 1
  static readonly CLOSING = 2
  static readonly CLOSED = 3

  readyState = FakeSocket.CONNECTING
  url: string
  closed = false
  private listeners = new Map<string, Set<(event: unknown) => void>>()

  constructor(url: string) {
    this.url = url
    FakeSocket.instances.push(this)
  }

  addEventListener(type: string, handler: (event: unknown) => void) {
    if (!this.listeners.has(type)) this.listeners.set(type, new Set())
    this.listeners.get(type)!.add(handler)
  }

  emit(type: string, event: unknown = {}) {
    for (const handler of this.listeners.get(type) ?? []) handler(event)
  }

  close() {
    this.closed = true
    this.readyState = FakeSocket.CLOSED
    this.emit('close')
  }
}

describe('ws/realtime', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    FakeSocket.instances = []
    vi.stubGlobal('WebSocket', FakeSocket as unknown as typeof WebSocket)
    useLiveStore.setState({ wsState: 'disconnected', wsError: null, live: null })
  })

  afterEach(() => {
    disconnect()
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('se connecte et publie les agents du snapshot', () => {
    connect()
    const socket = FakeSocket.instances[0]
    socket.readyState = FakeSocket.OPEN
    socket.emit('open')
    expect(useLiveStore.getState().wsState).toBe('connected')

    socket.emit('message', {
      data: JSON.stringify({ type: 'snapshot', tick: 12, agents: [{ id: 'A' }] }),
    })
    vi.advanceTimersByTime(20)

    expect(useLiveStore.getState().live).toMatchObject({
      tick: 12,
      agentCount: 1,
      agents: [{ id: 'A' }],
    })
  })

  it('reconnecte après une fermeture distante', () => {
    connect()
    FakeSocket.instances[0].emit('close')
    expect(useLiveStore.getState().wsState).toBe('disconnected')

    vi.advanceTimersByTime(2000)
    expect(FakeSocket.instances).toHaveLength(2)
  })

  it('ne se reconnecte pas après une déconnexion volontaire', () => {
    // Régression : `disconnect()` fermait la socket, dont l'événement `close`
    // replanifiait une connexion 2 s plus tard. L'interface continuait donc de
    // sonder un flux qu'on venait de fermer explicitement.
    connect()
    expect(FakeSocket.instances).toHaveLength(1)

    disconnect()
    expect(FakeSocket.instances[0].closed).toBe(true)

    vi.advanceTimersByTime(10_000)
    expect(FakeSocket.instances).toHaveLength(1)
    expect(useLiveStore.getState().wsState).toBe('disconnected')
  })

  it('ignore les messages d’une socket détruite', () => {
    // Une socket fermée peut encore émettre avant l\'effet de la fermeture : ces
    // messages continuaient d'alimenter l'état affiché après le démontage.
    connect()
    const stale = FakeSocket.instances[0]
    disconnect()
    connect()
    expect(FakeSocket.instances).toHaveLength(2)

    stale.emit('message', {
      data: JSON.stringify({ type: 'snapshot', tick: 99, agents: [{ id: 'Z' }] }),
    })
    vi.advanceTimersByTime(20)

    expect(useLiveStore.getState().live).toBeNull()
  })

  it('n’ouvre qu’une seule socket tant que la précédente est ouverte', () => {
    connect()
    FakeSocket.instances[0].readyState = FakeSocket.OPEN
    connect()
    expect(FakeSocket.instances).toHaveLength(1)
  })
})
