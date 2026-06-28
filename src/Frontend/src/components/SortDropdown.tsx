import { CardFilters } from '../api/pokemontcg'

interface SortDropdownProps {
  filters: CardFilters
  totalCount: number
  onChange: (filters: CardFilters) => void
}

export default function SortDropdown({ filters, totalCount, onChange }: SortDropdownProps) {
  return (
    <div className="mb-3 flex items-center justify-between">
      <span className="text-sm text-slate-500">
        {totalCount > 0 ? `${totalCount.toLocaleString()} cards` : ''}
      </span>
      <select
        value={filters.sort ?? 'newest'}
        onChange={e => onChange({ ...filters, sort: e.target.value as CardFilters['sort'] })}
        className="rounded-lg border border-slate-700/60 bg-slate-900 px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-blue-500"
      >
        <option value="newest">Newest</option>
        <option value="price-desc">Price: High → Low</option>
        <option value="price-asc">Price: Low → High</option>
        <option value="name">Name A → Z</option>
      </select>
    </div>
  )
}
