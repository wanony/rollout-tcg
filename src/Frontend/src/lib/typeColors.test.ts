import { describe, it, expect } from 'vitest'
import { primaryGlow, TYPE_GLOW } from './typeColors'

describe('primaryGlow', () => {
  it('returns the correct RGB string for Fire', () => {
    expect(primaryGlow(['Fire'])).toBe('239 68 68')
  })

  it('returns the correct RGB string for Water', () => {
    expect(primaryGlow(['Water'])).toBe('59 130 246')
  })

  it('uses the first type when multiple are provided', () => {
    expect(primaryGlow(['Psychic', 'Fire'])).toBe('168 85 247')
  })

  it('returns fallback violet for empty array', () => {
    expect(primaryGlow([])).toBe('139 92 246')
  })

  it('returns fallback violet for undefined', () => {
    expect(primaryGlow(undefined)).toBe('139 92 246')
  })

  it('returns fallback violet for unknown type', () => {
    expect(primaryGlow(['Unknown'])).toBe('139 92 246')
  })

  it('TYPE_GLOW covers all 11 standard types', () => {
    const types = ['Fire','Water','Grass','Lightning','Psychic','Fighting','Darkness','Metal','Dragon','Fairy','Colorless']
    types.forEach(t => expect(TYPE_GLOW[t]).toBeDefined())
  })
})
