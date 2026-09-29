import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Gauge } from './Gauge'

/** L'aiguille est le chemin `stroke={color}` : sa couleur porte l'alerte. */
function needleColor(container: HTMLElement): string {
  const paths = container.querySelectorAll('path')
  return paths[1]?.getAttribute('stroke') ?? ''
}

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

  it("ne déclenche pas l'alerte sur une valeur haute quand le seuil est bas", () => {
    // Régression : la vitesse de diffusion portait `warnAbove: 50`, ce qui
    // peignait en rouge un monde qui propageait l'information correctement.
    const { container } = render(
      <Gauge label="Vitesse de diffusion" value={80} max={100} warnBelow={1} />,
    )
    expect(needleColor(container)).toBe('var(--accent)')
  })

  it("déclenche l'alerte sous le seuil bas", () => {
    const { container } = render(
      <Gauge label="Vitesse de diffusion" value={0} max={100} warnBelow={1} />,
    )
    expect(needleColor(container)).toBe('var(--danger)')
    expect(screen.getByText(/alerte sous 1/)).toBeInTheDocument()
  })

  it('déclenche l alerte au-dessus du seuil haut', () => {
    const { container } = render(<Gauge label="Densité" value={0.9} max={1} warnAbove={0.7} />)
    expect(needleColor(container)).toBe('var(--danger)')
  })

  it('traite NaN comme une valeur inconnue, pas comme un zéro', () => {
    // NaN vient des ticks non observés d'une série fusionnée : `toFixed` y
    // renvoyait « NaN » et l'aiguille se vidait à 0 sans explication.
    const { container } = render(<Gauge label="EmergenceScore" value={NaN} max={1} warnBelow={0.1} />)
    expect(screen.getByText('—')).toBeInTheDocument()
    expect(needleColor(container)).toBe('var(--accent)')
    expect(container.querySelectorAll('path')[1]).toHaveAttribute(
      'stroke-dasharray',
      '0 157',
    )
  })

  it("signale un repli neutre sans déclencher l'alerte de seuil", () => {
    // Une valeur 0.0 non mesurée ne doit pas être peinte comme une valeur
    // observée hors seuil.
    const { container } = render(
      <Gauge label="LoopStrength" value={0} max={1} warnBelow={0.1} fallback />,
    )
    expect(needleColor(container)).toBe('var(--text-secondary)')
    expect(screen.getByText(/Repli neutre/)).toBeInTheDocument()
  })
})
