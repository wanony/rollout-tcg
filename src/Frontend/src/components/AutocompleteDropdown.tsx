import { useEffect, useRef, useState, KeyboardEvent } from 'react'
import { fetchAutocompleteSuggestions } from '../api/pokemontcg'

interface AutocompleteDropdownProps {
  query: string
  onSelect: (name: string) => void
  onClose: () => void
}

export default function AutocompleteDropdown({ query, onSelect, onClose }: AutocompleteDropdownProps) {
  const [suggestions, setSuggestions] = useState<string[]>([])
  const [activeIndex, setActiveIndex] = useState(-1)
  const listRef = useRef<HTMLUListElement>(null)

  useEffect(() => {
    if (query.length < 2) { setSuggestions([]); return }
    let cancelled = false
    fetchAutocompleteSuggestions(query).then(results => {
      if (!cancelled) { setSuggestions(results); setActiveIndex(-1) }
    })
    return () => { cancelled = true }
  }, [query])

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActiveIndex(i => Math.min(i + 1, suggestions.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActiveIndex(i => Math.max(i - 1, -1))
    } else if (e.key === 'Enter' && activeIndex >= 0) {
      e.preventDefault()
      onSelect(suggestions[activeIndex])
    } else if (e.key === 'Escape') {
      onClose()
    }
  }

  if (suggestions.length === 0) return null

  return (
    <div
      className="absolute left-0 right-0 top-full z-30 mt-1"
      onKeyDown={handleKeyDown}
    >
      <ul
        ref={listRef}
        className="overflow-hidden rounded-xl border border-slate-700/60 bg-slate-900 shadow-xl"
        role="listbox"
      >
        {suggestions.map((name, i) => (
          <li
            key={name}
            role="option"
            aria-selected={i === activeIndex}
            className={`cursor-pointer px-4 py-2.5 text-sm transition-colors
              ${i === activeIndex
                ? 'bg-violet-500/20 text-violet-200'
                : 'text-slate-200 hover:bg-violet-500/10 hover:text-slate-100'}`}
            onMouseDown={(e) => { e.preventDefault(); onSelect(name) }}
            onMouseEnter={() => setActiveIndex(i)}
          >
            {name}
          </li>
        ))}
      </ul>
    </div>
  )
}
