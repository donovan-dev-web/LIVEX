import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { WorldGrid } from './WorldGrid'

describe('WorldGrid', () => {
  it('renders obstacles and agents from a snapshot', () => {
    render(
      <WorldGrid
        world={{ obstacles: [{ id: 'rock', x: 0, y: 0, radius: 1 }] }}
        agents={[{ id: 'a', positionX: 1, positionY: 0 }]}
      />,
    )
    expect(screen.getByRole('img', { name: 'Grille 2D du monde' })).toBeInTheDocument()
    expect(screen.getByTitle('0,0 · obstacle')).toBeInTheDocument()
    expect(screen.getByTitle('23,0 · agents: 1')).toBeInTheDocument()
  })

  it('renders agents using the nested SYNE snapshot position', () => {
    render(<WorldGrid agents={[{ id: 'a', position: { x: 12, y: 8 } }]} />)
    expect(screen.getByTitle('23,15 · agents: 1')).toBeInTheDocument()
  })
})
