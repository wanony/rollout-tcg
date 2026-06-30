import { useState, useEffect, useRef, FormEvent } from 'react'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion, AnimatePresence } from 'framer-motion'
import { useAuth } from '../auth/useAuth'
import { getPortfolioCards, getPortfolioSummary, addCardToPortfolio, removeCardFromPortfolio } from '../api/portfolio'
import { PokemonCard, searchPokemonCards, fetchCardById } from '../api/pokemontcg'
import CardTile from '../components/CardTile'
import CardDetailModal from '../components/CardDetailModal'

const CONDITIONS = ['Mint', 'NearMint', 'LightlyPlayed', 'Played', 'HeavilyPlayed']

function CardSearch({ onPick }: { onPick: (id: string, name: string) => void }) {
  const [query, setQuery] = useState('')
  const [debouncedQuery, setDebouncedQuery] = useState('')
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const t = setTimeout(() => setDebouncedQuery(query), 300)
    return () => clearTimeout(t)
  }, [query])

  const { data, isFetching } = useQuery({
    queryKey: ['card-search-pick', debouncedQuery],
    queryFn: () => searchPokemonCards({ name: debouncedQuery }, 1, 8),
    enabled: debouncedQuery.length >= 2,
    staleTime: 60_000,
  })

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const results = data?.data ?? []

  return (
    <div ref={ref} className="relative col-span-2">
      <input
        value={query}
        onChange={e => { setQuery(e.target.value); setOpen(true) }}
        onFocus={() => setOpen(true)}
        placeholder="Search for a card…"
        autoComplete="off"
        className="w-full rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 focus:border-blue-500 focus:outline-none"
      />
      {open && debouncedQuery.length >= 2 && (
        <div className="absolute top-full z-20 mt-1 w-full rounded-lg border border-slate-700 bg-slate-900 shadow-xl">
          {isFetching && results.length === 0 && (
            <div className="px-3 py-2 text-xs text-slate-500">Searching…</div>
          )}
          {!isFetching && results.length === 0 && (
            <div className="px-3 py-2 text-xs text-slate-500">No cards found.</div>
          )}
          {results.map(card => (
            <button
              key={card.id}
              type="button"
              onMouseDown={() => { onPick(card.id, card.name); setQuery(''); setOpen(false) }}
              className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-slate-200 hover:bg-slate-800"
            >
              {card.images.small && (
                <img src={card.images.small} alt="" className="h-8 w-auto rounded" />
              )}
              <span className="truncate">{card.name}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

export default function PortfolioPage() {
  const { user } = useAuth()
  const userId = user!.profile.sub
  const qc = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [selectedCard, setSelectedCard] = useState<PokemonCard | null>(null)
  const [pickedCard, setPickedCard] = useState<{ id: string; name: string } | null>(null)
  const [form, setForm] = useState({ quantity: '1', condition: 'NearMint', acquisitionPriceUsd: '' })

  const { data: cards } = useQuery({ queryKey: ['portfolio', userId], queryFn: () => getPortfolioCards(userId) })
  const { data: summary } = useQuery({ queryKey: ['portfolio-summary', userId], queryFn: () => getPortfolioSummary(userId) })

  // Portfolio only stores cardId/cardName — enrich each item with real TCGdex art/rarity/types,
  // same lookup CardDetailModal already does, so the cache is shared and the modal opens instantly.
  const cardQueries = useQueries({
    queries: (cards ?? []).map(item => ({
      queryKey: ['card', item.cardId],
      queryFn: () => fetchCardById(item.cardId),
      enabled: !!item.cardId,
      staleTime: 24 * 60 * 60 * 1000,
    })),
  })

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['portfolio', userId] })
    qc.invalidateQueries({ queryKey: ['portfolio-summary', userId] })
  }

  const addMutation = useMutation({
    mutationFn: addCardToPortfolio,
    onSuccess: () => {
      invalidate()
      setShowForm(false)
      setPickedCard(null)
      setForm({ quantity: '1', condition: 'NearMint', acquisitionPriceUsd: '' })
    },
  })
  const removeMutation = useMutation({ mutationFn: removeCardFromPortfolio, onSuccess: invalidate })

  const handleAdd = (e: FormEvent) => {
    e.preventDefault()
    if (!pickedCard) return
    addMutation.mutate({
      userId,
      cardId: pickedCard.id,
      cardName: pickedCard.name,
      quantity: Number(form.quantity),
      condition: form.condition,
      acquisitionPriceUsd: Number(form.acquisitionPriceUsd),
    })
  }

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">My Portfolio</h1>

      {/* Summary */}
      {summary && (
        <div className="mb-4 grid grid-cols-2 gap-2 rounded-xl border border-slate-700/50 bg-slate-900/80 p-3 backdrop-blur-sm sm:p-4">
          {[
            { label: 'Cards', value: summary.totalCards },
            { label: 'Cost', value: `$${summary.totalAcquisitionCostUsd.toFixed(2)}` },
          ].map(s => (
            <div key={s.label} className="text-center">
              <div className="text-lg font-bold text-blue-300 sm:text-2xl">{s.value}</div>
              <div className="text-xs text-slate-500">{s.label}</div>
            </div>
          ))}
        </div>
      )}

      {/* Add card toggle */}
      <button
        onClick={() => { setShowForm(f => !f); setPickedCard(null) }}
        className="mb-4 w-full rounded-xl border border-dashed border-slate-700 py-2.5 text-sm text-slate-400 transition-colors hover:border-blue-500 hover:text-blue-400"
      >
        {showForm ? '− Cancel' : '+ Add Card'}
      </button>

      {/* Add card form */}
      {showForm && (
        <motion.form
          initial={{ opacity: 0, y: -8 }}
          animate={{ opacity: 1, y: 0 }}
          onSubmit={handleAdd}
          className="mb-4 grid grid-cols-2 gap-2 rounded-xl border border-slate-700/50 bg-slate-900/80 p-3 backdrop-blur-sm sm:grid-cols-4"
        >
          {pickedCard ? (
            <div className="col-span-2 flex items-center gap-2 rounded-lg border border-blue-500/40 bg-blue-950/30 px-3 py-2">
              <span className="flex-1 truncate text-sm text-slate-100">{pickedCard.name}</span>
              <button type="button" onClick={() => setPickedCard(null)} className="text-slate-500 hover:text-slate-300">×</button>
            </div>
          ) : (
            <CardSearch onPick={(id, name) => setPickedCard({ id, name })} />
          )}

          <input required type="number" min="1" placeholder="Qty" value={form.quantity}
            onChange={e => setForm(f => ({ ...f, quantity: e.target.value }))}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 focus:border-blue-500 focus:outline-none" />
          <input required type="number" step="0.01" placeholder="Price paid" value={form.acquisitionPriceUsd}
            onChange={e => setForm(f => ({ ...f, acquisitionPriceUsd: e.target.value }))}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 focus:border-blue-500 focus:outline-none" />
          <select value={form.condition} onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}
            className="col-span-2 rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-100 focus:border-blue-500 focus:outline-none">
            {CONDITIONS.map(c => <option key={c}>{c}</option>)}
          </select>
          <button type="submit" disabled={addMutation.isPending || !pickedCard}
            className="col-span-2 rounded-lg bg-blue-600 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-500 disabled:opacity-40">
            {addMutation.isPending ? 'Adding…' : 'Add'}
          </button>
        </motion.form>
      )}

      {/* Card grid — same tile as /cards, enriched with real TCGdex art */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        {cards?.map((item, i) => {
          const cardData = cardQueries[i]?.data
          if (!cardData) {
            return (
              <div key={item.id} className="animate-pulse rounded-[4.5%/3.5%] bg-slate-800/60" style={{ aspectRatio: '5/7' }} />
            )
          }
          return (
            <div key={item.id} className="group relative">
              <CardTile
                card={cardData}
                index={i}
                onClick={() => setSelectedCard(cardData)}
                footer={
                  <div className="mt-0.5 truncate text-[10px] text-slate-300 opacity-80">
                    Qty {item.quantity} · {item.condition}
                  </div>
                }
              />
              <button
                onClick={e => { e.stopPropagation(); removeMutation.mutate(item.id) }}
                className="absolute right-1.5 top-1.5 rounded-full bg-black/60 px-1.5 py-0.5 text-[10px] text-slate-300 opacity-0 transition-opacity hover:bg-red-900/70 hover:text-red-200 group-hover:opacity-100"
              >
                ✕
              </button>
            </div>
          )
        })}
      </div>
      {cards?.length === 0 && <div className="mt-16 text-center text-slate-500">No cards yet.</div>}

      <AnimatePresence>
        {selectedCard && (
          <CardDetailModal card={selectedCard} onClose={() => setSelectedCard(null)} />
        )}
      </AnimatePresence>
    </div>
  )
}
