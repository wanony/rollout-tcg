import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { searchCards } from '../api/cards'
import HoloCard from '../components/HoloCard'

const RARITY_BADGE: Record<string, string> = {
  Common: 'bg-slate-700 text-slate-300',
  Uncommon: 'bg-green-900/60 text-green-300',
  Rare: 'bg-blue-900/60 text-blue-300',
  SecretRare: 'bg-violet-900/60 text-violet-300',
}

export default function CardsPage() {
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['cards', search],
    queryFn: () => searchCards(search || undefined),
  })

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">Card Catalog</h1>

      {/* Search */}
      <div className="mb-6 flex gap-2">
        <input
          value={q}
          onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && setSearch(q)}
          placeholder="Search cards…"
          className="flex-1 rounded-lg border border-slate-700/60 bg-slate-900/80 px-3 py-2.5 text-sm text-slate-100 placeholder-slate-500 backdrop-blur-sm focus:border-violet-500 focus:outline-none"
        />
        <button
          onClick={() => setSearch(q)}
          className="rounded-lg bg-violet-600 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-violet-500 active:scale-95"
        >
          Search
        </button>
      </div>

      {isLoading && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="h-48 animate-pulse rounded-xl bg-slate-800/60" />
          ))}
        </div>
      )}

      {data && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {data.items.map((card, i) => (
            <motion.div
              key={card.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: i * 0.05, duration: 0.3 }}
            >
              <HoloCard rarity={card.rarity} className="h-full rounded-xl">
                <div className="rounded-xl border border-slate-700/50 bg-slate-900/80 p-3 backdrop-blur-sm h-full flex flex-col">
                  {/* Card art placeholder */}
                  <div className="mb-3 flex h-28 items-center justify-center rounded-lg bg-gradient-to-br from-slate-800 to-slate-900 text-4xl sm:h-36">
                    {card.type === 'Spell' ? '✨' : '🐉'}
                  </div>
                  <div className="flex-1">
                    <div className="mb-1 font-semibold text-slate-100 text-sm leading-tight">{card.name}</div>
                    <div className="mb-2 text-xs text-slate-400">{card.set}</div>
                    <div className="flex flex-wrap gap-1">
                      <span className={`rounded px-1.5 py-0.5 text-xs font-medium ${RARITY_BADGE[card.rarity] ?? RARITY_BADGE.Common}`}>
                        {card.rarity}
                      </span>
                      <span className="rounded bg-slate-800 px-1.5 py-0.5 text-xs text-slate-400">
                        {card.type}
                      </span>
                    </div>
                    {card.text && (
                      <p className="mt-2 text-xs text-slate-500 line-clamp-2">{card.text}</p>
                    )}
                  </div>
                </div>
              </HoloCard>
            </motion.div>
          ))}
        </div>
      )}

      {data?.items.length === 0 && (
        <div className="mt-16 text-center text-slate-500">No cards found.</div>
      )}
    </div>
  )
}
