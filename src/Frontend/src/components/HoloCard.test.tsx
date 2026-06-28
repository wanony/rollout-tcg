import { describe, it, expect } from 'vitest'
import { render, fireEvent } from '@testing-library/react'
import HoloCard from './HoloCard'

describe('HoloCard', () => {
  it('renders children', () => {
    const { getByText } = render(<HoloCard>Test content</HoloCard>)
    expect(getByText('Test content')).toBeInTheDocument()
  })

  it('sets --glow-color to Fire RGB on mousemove with Fire type', () => {
    const { container } = render(
      <HoloCard types={['Fire']} rarity="Rare Holo">
        <span>card</span>
      </HoloCard>,
    )
    const card = container.firstChild as HTMLElement

    // Mock getBoundingClientRect
    card.getBoundingClientRect = () => ({
      left: 0, top: 0, width: 200, height: 280,
      right: 200, bottom: 280, x: 0, y: 0, toJSON: () => {},
    })

    fireEvent.mouseMove(card, { clientX: 100, clientY: 140 })

    expect(card.style.getPropertyValue('--glow-color')).toBe('239 68 68')
  })

  it('sets --glow-color to fallback violet on mousemove with no types', () => {
    const { container } = render(
      <HoloCard>
        <span>card</span>
      </HoloCard>,
    )
    const card = container.firstChild as HTMLElement

    card.getBoundingClientRect = () => ({
      left: 0, top: 0, width: 200, height: 280,
      right: 200, bottom: 280, x: 0, y: 0, toJSON: () => {},
    })

    fireEvent.mouseMove(card, { clientX: 100, clientY: 140 })

    expect(card.style.getPropertyValue('--glow-color')).toBe('139 92 246')
  })

  it('sets --glow-color on mouseleave (keeps colour after hover)', () => {
    const { container } = render(
      <HoloCard types={['Water']} rarity="Ultra Rare">
        <span>card</span>
      </HoloCard>,
    )
    const card = container.firstChild as HTMLElement

    card.getBoundingClientRect = () => ({
      left: 0, top: 0, width: 200, height: 280,
      right: 200, bottom: 280, x: 0, y: 0, toJSON: () => {},
    })

    fireEvent.mouseLeave(card)

    expect(card.style.getPropertyValue('--glow-color')).toBe('59 130 246')
  })

  it('applies data-tier="ultra" for Ultra Rare rarity', () => {
    const { container } = render(<HoloCard rarity="Ultra Rare"><span /></HoloCard>)
    expect((container.firstChild as HTMLElement).dataset.tier).toBe('ultra')
  })

  it('applies data-tier="holo" for Rare Holo rarity', () => {
    const { container } = render(<HoloCard rarity="Rare Holo"><span /></HoloCard>)
    expect((container.firstChild as HTMLElement).dataset.tier).toBe('holo')
  })

  it('applies data-tier="none" for Common rarity', () => {
    const { container } = render(<HoloCard rarity="Common"><span /></HoloCard>)
    expect((container.firstChild as HTMLElement).dataset.tier).toBe('none')
  })
})
