export const API_BASE = (import.meta.env.VITE_API_BASE as string | undefined) ?? ''

export const WS_URL =
  (import.meta.env.VITE_WS_URL as string | undefined) ?? 'ws://127.0.0.1:5180'

export const CONTROL_BASE = (import.meta.env.VITE_CONTROL_BASE as string | undefined) ?? ''

export const DEFAULT_DEPTH = 7
export const MAX_DEPTH = 12