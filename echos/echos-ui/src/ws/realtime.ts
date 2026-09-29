import { WS_URL } from '../config'
import { useLiveStore } from '../store'
import type { LiveAgent, WsMessage } from '../api/types'

const RECONNECT_DELAY_MS = 2000

let socket: WebSocket | null = null
let reconnectTimer: ReturnType<typeof setTimeout> | null = null
let shouldReconnect = true
let pendingSnapshot: { type: 'snapshot'; tick: number; agents: LiveAgent[]; world?: import('../api/types').WorldSnapshot } | null = null
let pendingEventCount = 0
let flushScheduled = false

function flushLiveUpdate() {
  flushScheduled = false
  const store = useLiveStore.getState()
  if (pendingSnapshot) {
    store.setLive({
      tick: pendingSnapshot.tick ?? 0,
      agentCount: Array.isArray(pendingSnapshot.agents) ? pendingSnapshot.agents.length : 0,
      messageCount: pendingEventCount,
      agents: pendingSnapshot.agents,
      world: pendingSnapshot.world,
    })
    pendingSnapshot = null
    pendingEventCount = 0
    return
  }
  if (store.live && pendingEventCount > 0) {
    store.setLive({ ...store.live, messageCount: store.live.messageCount + pendingEventCount })
    pendingEventCount = 0
  }
}

function scheduleLiveUpdate() {
  if (flushScheduled) return
  flushScheduled = true
  requestAnimationFrame(flushLiveUpdate)
}

function scheduleReconnect() {
  if (!shouldReconnect) return
  clearTimeout(reconnectTimer ?? undefined)
  reconnectTimer = setTimeout(connect, RECONNECT_DELAY_MS)
}

/**
 * Consommation temps réel du flux SYNE (WebSocket :5180). Le client est
 * non-intrusif : il n'écrit rien dans le monde observé (règle d'or) — il
 * alimente les vues live de l'interface (tick courant, comptages d'affichage).
 *
 * La reconnexion automatique est armée par `connect()` et désarmée par
 * `disconnect()` : `close` est émis par le navigateur dans les deux cas, donc
 * une déconnexion volontaire se reconnectait 2 s plus tard.
 */
export function connect(): void {
  shouldReconnect = true
  if (socket && socket.readyState !== WebSocket.CLOSED) return

  useLiveStore.getState().setWsState('connecting')
  useLiveStore.getState().setWsError(null)

  let current: WebSocket
  try {
    current = new WebSocket(WS_URL)
  } catch (error) {
    useLiveStore.getState().setWsError(error instanceof Error ? error.message : String(error))
    useLiveStore.getState().setWsState('disconnected')
    scheduleReconnect()
    return
  }
  socket = current

  current.addEventListener('open', () => {
    if (socket !== current) return
    useLiveStore.getState().setWsState('connected')
  })

  current.addEventListener('message', (event: MessageEvent) => {
    // Une socket détruite peut encore émettre des messages pendant sa fermeture :
    // sans ce garde, un flux sortant continuait d'alimenter l'état affiché.
    if (socket !== current) return
    try {
      const message = JSON.parse(String(event.data)) as WsMessage
      if (message.type === 'snapshot') {
        if (!Array.isArray(message.agents)) return
        pendingSnapshot = {
          type: 'snapshot',
          tick: message.tick ?? 0,
          agents: message.agents,
          world: {
            resources: Array.isArray(message.resources) ? message.resources as import('../api/types').WorldResource[] : undefined,
            obstacles: Array.isArray(message.obstacles) ? message.obstacles as import('../api/types').WorldObstacle[] : undefined,
            season: typeof message.season === 'string' ? message.season : undefined,
            seasonIndex: typeof message.seasonIndex === 'number' ? message.seasonIndex : undefined,
          },
        }
        pendingEventCount = 0
        scheduleLiveUpdate()
      } else if (message.type === 'event') {
        pendingEventCount += 1
        scheduleLiveUpdate()
      }
    } catch {
      /* message non JSON : ignoré */
    }
  })

  current.addEventListener('close', () => {
    if (socket !== current) return
    socket = null
    useLiveStore.getState().setWsState('disconnected')
    scheduleReconnect()
  })

  current.addEventListener('error', () => {
    if (socket !== current) return
    useLiveStore.getState().setWsError('Erreur de connexion WebSocket')
  })
}

export function disconnect(): void {
  shouldReconnect = false
  clearTimeout(reconnectTimer ?? undefined)
  reconnectTimer = null
  const current = socket
  socket = null
  if (current) current.close()
  pendingSnapshot = null
  pendingEventCount = 0
  useLiveStore.getState().setWsState('disconnected')
}