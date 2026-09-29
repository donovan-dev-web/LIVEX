import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { TimelineChart } from './TimelineChart'

/** Capture l'`option` transmise à ECharts, pour inspecter la série rendue. */
const option = vi.fn()
vi.mock('echarts-for-react', () => ({
  default: ({ option: value }: { option: unknown }) => {
    option(value)
    return <div aria-label="echart" />
  },
}))

function lastOption<T>(): T {
  const calls = option.mock.calls as unknown as Array<[T]>
  return calls[calls.length - 1][0]
}

function seriesOf(values: number[]) {
  render(<TimelineChart ticks={values.map((_, i) => i + 1)} series={{ M: values }} />)
  return lastOption<{ series: Array<{ data: Array<number | null> }> }>().series[0].data
}

describe('TimelineChart', () => {
  it('convertit NaN en trou au lieu de le laisser atteindre ECharts', () => {
    // Régression : `NaN` (tick non observé après fusion de cadences) était
    // passé tel quel à ECharts, qui l'interprétait comme une valeur et traçait
    // un effondrement numérique au lieu d'un creux.
    expect(seriesOf([1, NaN, 3])).toEqual([1, null, 3])
  })

  it('signale les ticks sans valeur observée', () => {
    render(<TimelineChart ticks={[1, 2, 3]} series={{ M: [1, NaN, 3] }} />)
    expect(screen.getByRole('status')).toHaveTextContent('1 tick(s) sans valeur observée')
  })

  it('ne signale rien quand toutes les valeurs sont observées', () => {
    render(<TimelineChart ticks={[1, 2]} series={{ M: [0.1, 0.2] }} />)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('retombe sur une série disponible si la sélection a disparu', () => {
    // Changer de run supprimait la série sélectionnée : le graphique vidait.
    render(<TimelineChart ticks={[1, 2]} series={{ A: [1, 2] }} selected="B" />)
    expect(lastOption<{ series: Array<{ name: string }> }>().series[0].name).toBe('A')
  })
})
