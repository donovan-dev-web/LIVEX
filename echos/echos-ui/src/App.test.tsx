import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import App from './App'

describe('App', () => {
  it("affiche le titre d'observation", () => {
    render(<App />)
    expect(screen.getByRole('heading', { name: /ECHOS — Observation/i })).toBeInTheDocument()
  })

  it('mentionne la connexion SYNE aux jalons suivants', () => {
    render(<App />)
    expect(screen.getByText(/WebSocket :5180/i)).toBeInTheDocument()
  })
})
