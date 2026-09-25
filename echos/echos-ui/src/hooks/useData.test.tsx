import { act, render, screen } from '@testing-library/react'
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import { useMetrics } from './useData'

function Probe() {
  const { metrics, error } = useMetrics('run-1')
  return <output>{error ?? metrics?.latest_tick ?? 'loading'}</output>
}

describe('useMetrics', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          json: async () => ({
            run_id: 'run-1',
            engine: null,
            metric: null,
            every: 1,
            ticks: [1],
            values: {},
            latest: {},
            latest_tick: 1,
          }),
        }),
      ),
    )
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('refreshes metrics periodically while a run is selected', async () => {
    render(<Probe />)
    await act(async () => {})
    expect(fetch).toHaveBeenCalledTimes(1)

    await act(async () => {
      vi.advanceTimersByTime(1000)
      await Promise.resolve()
    })
    expect(fetch).toHaveBeenCalledTimes(2)
    expect(screen.getByRole('status')).toHaveTextContent('1')
  })

  it('exposes the last request error and clears it after recovery', async () => {
    const request = vi
      .fn()
      .mockRejectedValueOnce(new Error('API indisponible'))
      .mockResolvedValue({
        ok: true,
        json: async () => ({
          run_id: 'run-1',
          engine: null,
          metric: null,
          every: 1,
          ticks: [2],
          values: {},
          latest: {},
          latest_tick: 2,
        }),
      })
    vi.stubGlobal('fetch', request)
    render(<Probe />)
    await act(async () => {})
    expect(screen.getByRole('status')).toHaveTextContent('API indisponible')

    await act(async () => {
      vi.advanceTimersByTime(1000)
      await Promise.resolve()
    })
    expect(screen.getByRole('status')).toHaveTextContent('2')
  })

  it('does not move the displayed tick backwards when a stale response arrives', async () => {
    const request = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          run_id: 'run-1', engine: null, metric: null, every: 1,
          ticks: [1, 2], values: {}, latest: {}, latest_tick: 2,
        }),
      })
      .mockResolvedValue({
        ok: true,
        json: async () => ({
          run_id: 'run-1', engine: null, metric: null, every: 1,
          ticks: [1], values: {}, latest: {}, latest_tick: 1,
        }),
      })
    vi.stubGlobal('fetch', request)
    render(<Probe />)
    await act(async () => {})
    expect(screen.getByRole('status')).toHaveTextContent('2')
    await act(async () => {
      vi.advanceTimersByTime(1000)
      await Promise.resolve()
    })
    expect(screen.getByRole('status')).toHaveTextContent('2')
  })
})
