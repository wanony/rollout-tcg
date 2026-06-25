import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react'
import { fetchAutocompleteSuggestions } from '../api/pokemontcg'

interface AutocompleteDropdownProps {
  query: string
  onSelect: (name: string) => void
  onClose: () => void
}

export interface AutocompleteDropdownHandle {
  handleKeyDown: (e: React.KeyboardEvent) => void
}

const AutocompleteDropdown = forwardRef<AutocompleteDropdownHandle, AutocompleteDropdownProps>(
  function AutocompleteDropdown({ query, onSelect, onClose }, ref) {
    const [suggestions, setSuggestions] = useState<string[]>([])
    const [activeIndex, setActiveIndex] = useState(-1)
    const listRef = useRef<HTMLUListElement>(null)

    // Fix 2: debounce requests — fire only after 300ms of no typing
    useEffect(() => {
      if (query.length < 2) { setSuggestions([]); return }
      let cancelled = false
      const timer = setTimeout(() => {
        fetchAutocompleteSuggestions(query).then(results => {
          if (!cancelled) { setSuggestions(results); setActiveIndex(-1) }
        })
      }, 300)
      return () => { cancelled = true; clearTimeout(timer) }
    }, [query])

    // Fix 1: expose handleKeyDown via ref so the parent input can forward events
    function handleKeyDown(e: React.KeyboardEvent) {
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

    useImperativeHandle(ref, () => ({ handleKeyDown }))

    if (suggestions.length === 0) return null

    return (
      <div className="absolute left-0 right-0 top-full z-30 mt-1">
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
)

export default AutocompleteDropdown
