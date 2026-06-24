import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { motion, AnimatePresence } from 'framer-motion'
import { searchPokemonCards, PokemonCard } from '../api/pokemontcg'
import { TYPE_GLOW } from '../lib/typeColors'
import HoloCard from '../components/HoloCard'
import CardDetailModal from '../components/CardDetailModal'

function TypeBadge({ type }: { type: string }) {
  const rgb = TYPE_GLOW[type] ?? '139 92 246'
  return (
    <span
      className="inline-flex rounded-full px-1.5 py-0.5 text-[9px] font-medium leading-none"
      style={{
        background: `rgb(${rgb} / 0.2)`,
        border: `1px solid rgb(${rgb} / 0.35)`,
        color: `rgb(${rgb})`,
      }}
    >
      {type}
    </span>
  )
}

function marketPrice(card: PokemonCard): string | null {
  const p = card.tcgplayer?.prices
  const val = p?.holofoil?.market ?? p?.normal?.market ?? p?.reverseHolofoil?.market
  return val != null ? `$${val.toFixed(2)}` : null
}

export default function CardsPage() {
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [selectedCard, setSelectedCard] = useState<PokemonCard | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['pokemon-cards', search],
    queryFn: () => searchPokemonCards({ name: search }, 1, 20),
    staleTime: 5 * 60 * 1000,
  })

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">Card Catalog</h1>

      <div className="mb-6 flex gap-2">
        <input
          value={q}
          onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && setSearch(q)}
          placeholder="Search Pokémon cards…"
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
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: 10 }).map((_, i) => (
            <div key={i} className="animate-pulse rounded-[4.5%/3.5%] bg-slate-800/60" style={{ aspectRatio: '5/7' }} />
          ))}
        </div>
      )}

      {data && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {data.data.map((card, i) => {
            const price = marketPrice(card)
            return (
              <motion.div
                key={card.id}
                initial={{ opacity: 0, scale: 0.92 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ delay: Math.min(i * 0.04, 0.5), duration: 0.25 }}
                style={{ aspectRatio: '5/7' }}
                onClick={() => setSelectedCard(card)}
                className="cursor-pointer"
              >
                <HoloCard rarity={card.rarity} types={card.types} className="h-full w-full">
                  <div className="relative h-full w-full overflow-hidden rounded-[4.5%/3.5%]">
                    <img
                      src={card.images.small}
                      alt={card.name}
                      className="h-full w-full object-cover"
                      loading="lazy"
                      draggable={false}
                    />
                    {/* Enriched overlay */}
                    <div className="absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/80 to-transparent px-2 pb-1.5 pt-4">
                      <div className="truncate text-xs font-semibold text-white">{card.name}</div>
                      <div className="flex items-center justify-between gap-1">
                        <div className="truncate text-[10px] text-slate-300 opacity-80">{card.set.name}</div>
                        {price && (
                          <span className="flex-shrink-0 text-[10px] font-semibold text-emerald-400">{price}</span>
                        )}
                      </div>
                      {card.types && card.types.length > 0 && (
                        <div className="mt-0.5 flex flex-wrap gap-0.5">
                          {card.types.map(t => <TypeBadge key={t} type={t} />)}
                        </div>
                      )}
                    </div>
                  </div>
                </HoloCard>
              </motion.div>
            )
          })}
        </div>
      )}

      {data?.data.length === 0 && (
        <div className="mt-16 text-center text-slate-500">No cards found.</div>
      )}

      <AnimatePresence>
        {selectedCard && (
          <CardDetailModal
            card={selectedCard}
            onClose={() => setSelectedCard(null)}
          />
        )}
      </AnimatePresence>
    </div>
  )
}
