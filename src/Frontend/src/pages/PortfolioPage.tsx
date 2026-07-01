import { useState, useEffect, useRef, useMemo, FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion, AnimatePresence } from 'framer-motion'
import { useAuth } from '../auth/useAuth'
import { getPortfolioCards, getPortfolioSummary, addCardToPortfolio, removeCardFromPortfolio } from '../api/portfolio'
import { getListings } from '../api/marketplace'
import { PokemonCard, PokemonSet, searchPokemonCards, fetchCardById, fetchSets } from '../api/pokemontcg'
import CardTile from '../components/CardTile'
import CardDetailModal from '../components/CardDetailModal'

const CONDITIONS = ['Mint', 'NearMint', 'LightlyPlayed', 'Played', 'HeavilyPlayed']

function marketValue(card?: PokemonCard): number {
  if (!card) return 0
  const p = card.tcgplayer?.prices
  return p?.holofoil?.market ?? p?.normal?.market ?? p?.reverseHolofoil?.market ?? card.cardmarket?.avg ?? 0
}
const formatUsd = (n: number) => `$${n.toFixed(2)}`

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
        <div className="absolute top-full z-30 mt-1 w-full rounded-lg border border-slate-700 bg-slate-900 shadow-xl">
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

function ConfirmDialog({ open, title, body, onConfirm, onCancel, pending }: {
  open: boolean
  title: string
  body: string
  onConfirm: () => void
  onCancel: () => void
  pending: boolean
}) {
  if (!open) return null
  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/60 p-4" onClick={onCancel}>
      <div onClick={e => e.stopPropagation()} className="w-full max-w-sm rounded-xl border border-slate-700/50 bg-slate-900 p-4">
        <div className="mb-1 font-semibold text-slate-100">{title}</div>
        <div className="mb-4 text-sm text-slate-400">{body}</div>
        <div className="flex justify-end gap-2">
          <button onClick={onCancel} className="rounded-lg px-3 py-1.5 text-sm text-slate-400 hover:text-slate-200">
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={pending}
            className="rounded-lg bg-red-600 px-3 py-1.5 text-sm font-semibold text-white transition-colors hover:bg-red-500 disabled:opacity-50"
          >
            {pending ? 'Removing…' : 'Remove'}
          </button>
        </div>
      </div>
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
  const [showMissing, setShowMissing] = useState(false)
  const [selectMode, setSelectMode] = useState(false)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [confirmTarget, setConfirmTarget] = useState<'bulk' | string | null>(null)

  const { data: cards } = useQuery({ queryKey: ['portfolio', userId], queryFn: () => getPortfolioCards(userId) })
  const { data: summary } = useQuery({ queryKey: ['portfolio-summary', userId], queryFn: () => getPortfolioSummary(userId) })
  const { data: sets = [] } = useQuery({ queryKey: ['pokemon-sets'], queryFn: fetchSets, staleTime: 24 * 60 * 60 * 1000 })
  const { data: listings } = useQuery({ queryKey: ['listings'], queryFn: getListings })

  // Portfolio only stores cardId/cardName — enrich each item with real TCGdex art/rarity/types/pricing,
  // same lookup CardDetailModal already does, so the cache is shared and the modal opens instantly.
  const cardQueries = useQueries({
    queries: (cards ?? []).map(item => ({
      queryKey: ['card', item.cardId],
      queryFn: () => fetchCardById(item.cardId),
      enabled: !!item.cardId,
      staleTime: 24 * 60 * 60 * 1000,
    })),
  })

  const listedCardIds = useMemo(
    () => new Set((listings ?? []).filter(l => l.sellerId === userId && l.status === 'Active').map(l => l.cardId)),
    [listings, userId],
  )

  const enriched = (cards ?? []).map((item, i) => ({ item, cardData: cardQueries[i]?.data }))
  const loadingCount = enriched.filter(e => !e.cardData).length

  const setsById = useMemo(() => new Map(sets.map(s => [s.id, s])), [sets])

  type Group = { setId: string; setName: string; entries: typeof enriched }
  const groups = useMemo(() => {
    const map = new Map<string, Group>()
    for (const e of enriched) {
      if (!e.cardData) continue
      const setId = e.cardData.set.id || 'unknown'
      const setName = e.cardData.set.name || 'Unknown Set'
      if (!map.has(setId)) map.set(setId, { setId, setName, entries: [] })
      map.get(setId)!.entries.push(e)
    }
    return [...map.values()].sort((a, b) => b.entries.length - a.entries.length || a.setName.localeCompare(b.setName))
  }, [enriched])

  const setIds = useMemo(() => groups.map(g => g.setId).filter(id => id !== 'unknown'), [groups])
  const missingQueries = useQueries({
    queries: setIds.map(setId => ({
      queryKey: ['set-cards', setId],
      queryFn: () => searchPokemonCards({ setId, sort: 'name' }, 1, 300),
      enabled: showMissing,
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

  const openAddForm = (id: string, name: string) => {
    setPickedCard({ id, name })
    setShowForm(true)
  }

  const toggleSelected = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  const handleConfirmRemove = async () => {
    if (confirmTarget === 'bulk') {
      await Promise.all([...selectedIds].map(id => removeMutation.mutateAsync(id)))
      setSelectedIds(new Set())
      setSelectMode(false)
    } else if (confirmTarget) {
      await removeMutation.mutateAsync(confirmTarget)
    }
    setConfirmTarget(null)
  }

  const totalValue = enriched.reduce((sum, e) => sum + e.item.quantity * marketValue(e.cardData), 0)

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-100 sm:text-2xl">My Portfolio</h1>
        <button
          onClick={() => { setSelectMode(m => !m); setSelectedIds(new Set()) }}
          className="rounded-lg border border-slate-700 px-3 py-1.5 text-xs font-semibold text-slate-300 transition-colors hover:border-slate-600 hover:text-slate-100"
        >
          {selectMode ? 'Cancel' : 'Select'}
        </button>
      </div>

      {/* Summary */}
      {summary && (
        <div className="mb-4 grid grid-cols-2 gap-2 rounded-xl border border-slate-700/50 bg-slate-900/80 p-3 backdrop-blur-sm sm:p-4">
          {[
            { label: 'Cards', value: summary.totalCards },
            { label: 'Value', value: formatUsd(totalValue) },
          ].map(s => (
            <div key={s.label} className="text-center">
              <div className="text-lg font-bold text-blue-300 sm:text-2xl">{s.value}</div>
              <div className="text-xs text-slate-500">{s.label}</div>
            </div>
          ))}
        </div>
      )}

      {/* Add card toggle */}
      <div className="mb-4 flex gap-2">
        <button
          onClick={() => { setShowForm(f => !f); setPickedCard(null) }}
          className="flex-1 rounded-xl border border-dashed border-slate-700 py-2.5 text-sm text-slate-400 transition-colors hover:border-blue-500 hover:text-blue-400"
        >
          {showForm ? '− Cancel' : '+ Add Card'}
        </button>
        <button
          onClick={() => setShowMissing(m => !m)}
          className={`rounded-xl border px-3 py-2.5 text-sm transition-colors ${
            showMissing
              ? 'border-blue-500 text-blue-400'
              : 'border-slate-700 text-slate-400 hover:border-slate-600 hover:text-slate-200'
          }`}
        >
          {showMissing ? 'Hide missing' : 'Show missing'}
        </button>
      </div>

      {/* Add card form */}
      {showForm && (
        <motion.form
          initial={{ opacity: 0, y: -8 }}
          animate={{ opacity: 1, y: 0 }}
          onSubmit={handleAdd}
          className="relative z-20 mb-4 grid grid-cols-2 gap-2 rounded-xl border border-slate-700/50 bg-slate-900/80 p-3 backdrop-blur-sm sm:grid-cols-4"
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

      {/* Cards still enriching */}
      {loadingCount > 0 && (
        <div className="mb-6 grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: loadingCount }).map((_, i) => (
            <div key={i} className="animate-pulse rounded-[4.5%/3.5%] bg-slate-800/60" style={{ aspectRatio: '5/7' }} />
          ))}
        </div>
      )}

      {/* Grouped by set */}
      {groups.map((group, gi) => {
        const setMeta = setsById.get(group.setId)
        const sectionValue = group.entries.reduce((sum, e) => sum + e.item.quantity * marketValue(e.cardData), 0)
        const ownedCardIds = new Set(group.entries.map(e => e.item.cardId))
        const missingIdx = setIds.indexOf(group.setId)
        const missingCards = showMissing && missingIdx >= 0
          ? (missingQueries[missingIdx]?.data?.data ?? []).filter(c => !ownedCardIds.has(c.id))
          : []

        return (
          <div key={group.setId} className="mb-6">
            <div className="mb-2 flex items-baseline justify-between">
              <h2 className="text-sm font-semibold text-slate-200">
                {setMeta?.name ?? group.setName}
                <span className="ml-2 text-xs font-normal text-slate-500">{group.entries.length} card{group.entries.length === 1 ? '' : 's'}</span>
              </h2>
              <span className="text-xs font-semibold text-emerald-400">{formatUsd(sectionValue)}</span>
            </div>
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
              {group.entries.map(({ item, cardData }, i) => {
                if (!cardData) return null
                const price = marketValue(cardData) > 0 ? formatUsd(marketValue(cardData)) : null
                const isSelected = selectedIds.has(item.id)
                return (
                  <div key={item.id} className="group relative">
                    <CardTile
                      card={cardData}
                      index={gi * 20 + i}
                      price={price}
                      onClick={() => selectMode ? toggleSelected(item.id) : setSelectedCard(cardData)}
                      footer={
                        <div className="mt-0.5 flex items-center justify-between gap-1 text-[10px] text-slate-300 opacity-80">
                          <span className="truncate">Qty {item.quantity} · {item.condition}</span>
                          {!selectMode && !listedCardIds.has(item.cardId) && (
                            <Link
                              to="/marketplace/new"
                              state={{ itemId: item.id }}
                              onClick={e => e.stopPropagation()}
                              className="flex-shrink-0 text-blue-400 hover:text-blue-300"
                            >
                              List
                            </Link>
                          )}
                        </div>
                      }
                    />
                    {listedCardIds.has(item.cardId) && (
                      <span className="absolute left-1.5 top-1.5 rounded-full bg-blue-600/90 px-1.5 py-0.5 text-[9px] font-semibold text-white">
                        Listed
                      </span>
                    )}
                    {selectMode ? (
                      <button
                        onClick={e => { e.stopPropagation(); toggleSelected(item.id) }}
                        className={`absolute right-1.5 top-1.5 flex h-5 w-5 items-center justify-center rounded-full border text-[10px] font-bold transition-colors ${
                          isSelected ? 'border-blue-400 bg-blue-500 text-white' : 'border-slate-400 bg-black/60 text-transparent'
                        }`}
                      >
                        ✓
                      </button>
                    ) : (
                      <button
                        onClick={e => { e.stopPropagation(); setConfirmTarget(item.id) }}
                        className="absolute right-1.5 top-1.5 rounded-full bg-black/60 px-1.5 py-0.5 text-[10px] text-slate-300 opacity-0 transition-opacity hover:bg-red-900/70 hover:text-red-200 group-hover:opacity-100"
                      >
                        ✕
                      </button>
                    )}
                  </div>
                )
              })}
              {missingCards.map(card => (
                <div key={card.id} className="group relative opacity-40 grayscale transition-opacity hover:opacity-70">
                  <CardTile card={card} />
                  <button
                    onClick={() => openAddForm(card.id, card.name)}
                    className="absolute inset-0 flex items-center justify-center rounded-[4.5%/3.5%] bg-black/40 text-2xl font-bold text-white opacity-0 transition-opacity group-hover:opacity-100"
                  >
                    +
                  </button>
                </div>
              ))}
            </div>
          </div>
        )
      })}

      {cards?.length === 0 && <div className="mt-16 text-center text-slate-500">No cards yet.</div>}

      {/* Bulk select bar */}
      {selectMode && selectedIds.size > 0 && (
        <div className="fixed inset-x-0 bottom-4 z-30 flex justify-center px-4">
          <div className="flex items-center gap-3 rounded-xl border border-slate-700 bg-slate-900 px-4 py-2.5 shadow-xl">
            <span className="text-sm text-slate-300">{selectedIds.size} selected</span>
            <button
              onClick={() => setConfirmTarget('bulk')}
              className="rounded-lg bg-red-600 px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-red-500"
            >
              Remove Selected
            </button>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={confirmTarget !== null}
        title={confirmTarget === 'bulk' ? `Remove ${selectedIds.size} cards?` : 'Remove this card?'}
        body="This can't be undone. The card will be removed from your portfolio."
        onConfirm={handleConfirmRemove}
        onCancel={() => setConfirmTarget(null)}
        pending={removeMutation.isPending}
      />

      <AnimatePresence>
        {selectedCard && (
          <CardDetailModal card={selectedCard} onClose={() => setSelectedCard(null)} />
        )}
      </AnimatePresence>
    </div>
  )
}
