import { WS_URL } from '../config'
import { useLiveStore } from '../store'
import type { LiveAgent, WsMessage } from '../api/types'

const RECONNECT_DELAY_MS = 2000

let socket: WebSocket | null = null
let reconnectTimer: ReturnType<typeof setTimeout> | null = null
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
  clearTimeout(reconnectTimer!)
  reconnectTimer = setTimeout(connect, RECONNECT_DELAY_MS)
}

/**
 * Consommation temps réel du flux SYNE (WebSocket :5180). Le client est
 * non-intrusif : il n'écrit rien dans le monde observé (règle d'or) — il
 * alimente les vues live de l'interface (tick courant, comptages d'affichage).
 */
export function connect(): void {
  if (socket && socket.readyState !== WebSocket.CLOSED) return

  useLiveStore.getState().setWsState('connecting')
  useLiveStore.getState().setWsError(null)

  try {
    socket = new WebSocket(WS_URL)
  } catch (error) {
    useLiveStore.getState().setWsError(error instanceof Error ? error.message : String(error))
    useLiveStore.getState().setWsState('disconnected')
    scheduleReconnect()
    return
  }

  socket.addEventListener('open', () => {
    useLiveStore.getState().setWsState('connected')
  })

  socket.addEventListener('message', (event: MessageEvent) => {
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

  socket.addEventListener('close', () => {
    useLiveStore.getState().setWsState('disconnected')
    scheduleReconnect()
  })

  socket.addEventListener('error', () => {
    useLiveStore.getState().setWsError('Erreur de connexion WebSocket')
  })
}

export function disconnect(): void {
  if (reconnectTimer) clearTimeout(reconnectTimer)
  if (socket) {
    socket.close()
    socket = null
  }
  useLiveStore.getState().setWsState('disconnected')
}