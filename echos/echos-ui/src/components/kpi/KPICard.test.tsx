import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { KPICard } from './KPICard'

describe('KPICard', () => {
  it('affiche titre et valeur', () => {
    render(<KPICard title="Entités actives" value={12} />)
    expect(screen.getByText('Entités actives')).toBeInTheDocument()
    expect(screen.getByText('12')).toBeInTheDocument()
  })

  it('affiche un delta hausse coloré', () => {
    render(<KPICard title="T" value={1} delta={0.5} />)
    expect(screen.getByText('▲ 0.50')).toBeInTheDocument()
  })

  it('affiche un delta baisse', () => {
    render(<KPICard title="T" value={1} delta={-0.25} />)
    expect(screen.getByText('▼ 0.25')).toBeInTheDocument()
  })

  it('affiche un hint', () => {
    render(<KPICard title="Score" value="0.5" hint="pas une preuve" />)
    expect(screen.getByText('pas une preuve')).toBeInTheDocument()
  })

  it('affiche une valeur manquante', () => {
    render(<KPICard title="T" value={null} />)
    expect(screen.getByText('—')).toBeInTheDocument()
  })
})