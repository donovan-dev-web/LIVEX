import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { GroupChip, EntityBadge, colorForGroup } from './badges'

describe('badges', () => {
  it('GroupChip affiche l’étiquette', () => {
    render(<GroupChip label="Alpha" />)
    expect(screen.getByText('Alpha')).toBeInTheDocument()
  })

  it('EntityBadge affiche l’identifiant', () => {
    render(<EntityBadge agentId="A" />)
    expect(screen.getByText('A')).toBeInTheDocument()
  })

  it('colorForGroup respecte l’opacité du hash', () => {
    expect(colorForGroup('Alpha')).toMatch(/^#[0-9a-f]{6}$/i)
  })
})