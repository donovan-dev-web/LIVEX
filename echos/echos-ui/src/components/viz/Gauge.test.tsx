import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Gauge } from './Gauge'

describe('Gauge', () => {
  it("affiche la valeur en décimales", () => {
    render(<Gauge label="Diversité croyances" value={0.75} max={1} />)
    expect(screen.getByText('0.750')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Diversité croyances' })).toBeInTheDocument()
  })

  it('affiche une valeur nulle', () => {
    render(<Gauge label="Clustering" value={null} />)
    expect(screen.getByText('—')).toBeInTheDocument()
  })
})