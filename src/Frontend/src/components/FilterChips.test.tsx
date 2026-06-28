import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FilterChips from './FilterChips'
import { CardFilters, PokemonSet } from '../api/pokemontcg'

const noSets: PokemonSet[] = []

describe('FilterChips', () => {
  it('renders type chips for all 11 types', () => {
    render(<FilterChips filters={{}} sets={noSets} onChange={vi.fn()} />)
    expect(screen.getByText('Fire')).toBeInTheDocument()
    expect(screen.getByText('Water')).toBeInTheDocument()
    expect(screen.getByText('Psychic')).toBeInTheDocument()
  })

  it('clicking a type chip calls onChange with that type', async () => {
    const onChange = vi.fn()
    render(<FilterChips filters={{}} sets={noSets} onChange={onChange} />)
    await userEvent.click(screen.getByText('Fire'))
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ type: 'Fire' }))
  })

  it('clicking an active type chip deselects it (calls onChange without type)', async () => {
    const onChange = vi.fn()
    const filters: CardFilters = { type: 'Fire' }
    render(<FilterChips filters={filters} sets={noSets} onChange={onChange} />)
    await userEvent.click(screen.getByText('Fire'))
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ type: undefined }))
  })

  it('clicking a rarity chip calls onChange with that rarity', async () => {
    const onChange = vi.fn()
    render(<FilterChips filters={{}} sets={noSets} onChange={onChange} />)
    await userEvent.click(screen.getByText('Ultra Rare'))
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ rarity: 'Ultra Rare' }))
  })

  it('shows Clear filters button when a filter is active', () => {
    render(<FilterChips filters={{ type: 'Fire' }} sets={noSets} onChange={vi.fn()} />)
    expect(screen.getByText('× Clear filters')).toBeInTheDocument()
  })

  it('does not show Clear filters when only sort is set', () => {
    render(<FilterChips filters={{ sort: 'newest' }} sets={noSets} onChange={vi.fn()} />)
    expect(screen.queryByText('× Clear filters')).toBeNull()
  })
})
