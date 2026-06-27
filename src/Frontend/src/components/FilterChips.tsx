import { CardFilters, PokemonSet } from '../api/pokemontcg'
import { TYPE_GLOW } from '../lib/typeColors'

const TYPES = ['Fire','Water','Grass','Lightning','Psychic','Fighting','Darkness','Metal','Dragon','Fairy','Colorless']
const RARITIES = ['Common', 'Uncommon', 'Rare', 'Ultra Rare']

interface FilterChipsProps {
  filters: CardFilters
  sets: PokemonSet[]
  onChange: (filters: CardFilters) => void
}

export default function FilterChips({ filters, onChange, sets }: FilterChipsProps) {
  const hasActiveFilter = !!(filters.name || filters.type || filters.rarity || filters.setId)

  function setType(type: string) {
    onChange({ ...filters, type: filters.type === type ? undefined : type })
  }

  function setRarity(rarity: string) {
    onChange({ ...filters, rarity: filters.rarity === rarity ? undefined : rarity })
  }

  function clearAll() {
    onChange({ sort: filters.sort })
  }

  return (
    <div className="mb-4 space-y-2">
      {/* Type chips */}
      <div className="flex gap-1.5 overflow-x-auto pb-1">
        {TYPES.map(type => {
          const rgb = TYPE_GLOW[type] ?? '139 92 246'
          const active = filters.type === type
          return (
            <button
              key={type}
              onClick={() => setType(type)}
              className="flex-shrink-0 rounded-full px-2.5 py-1 text-xs font-medium transition-all"
              style={active
                ? { background: `rgb(${rgb} / 0.2)`, border: `1px solid rgb(${rgb} / 0.5)`, color: `rgb(${rgb})` }
                : { background: 'rgb(30 41 59 / 0.6)', border: '1px solid rgb(71 85 105 / 0.4)', color: '#94a3b8' }}
            >
              {type}
            </button>
          )
        })}
      </div>

      {/* Rarity chips + Set dropdown + Clear */}
      <div className="flex flex-wrap items-center gap-2">
        {RARITIES.map(rarity => {
          const active = filters.rarity === rarity
          return (
            <button
              key={rarity}
              onClick={() => setRarity(rarity)}
              className={`rounded-full px-2.5 py-1 text-xs font-medium transition-all
                ${active
                  ? 'bg-blue-500/20 border border-blue-500/50 text-blue-300'
                  : 'bg-slate-800/60 border border-slate-700/40 text-slate-400 hover:text-slate-200'}`}
            >
              {rarity}
            </button>
          )
        })}

        {sets.length > 0 && (
          <select
            value={filters.setId ?? ''}
            onChange={e => onChange({ ...filters, setId: e.target.value || undefined })}
            className="rounded-lg border border-slate-700/60 bg-slate-900 px-3 py-1 text-xs text-slate-200 focus:outline-none focus:border-blue-500"
          >
            <option value="">All sets</option>
            {sets.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        )}

        {hasActiveFilter && (
          <button
            onClick={clearAll}
            className="text-xs text-slate-500 hover:text-slate-300 transition-colors"
          >
            × Clear filters
          </button>
        )}
      </div>
    </div>
  )
}
