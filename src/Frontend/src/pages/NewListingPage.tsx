import { useState, FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { createListing } from '../api/marketplace'
import { getPortfolioCards } from '../api/portfolio'

export default function NewListingPage() {
  const { user } = useAuth()
  const userId = user!.profile.sub
  const navigate = useNavigate()
  const location = useLocation()
  const preselectedItemId = (location.state as { itemId?: string } | null)?.itemId
  const [selectedItemId, setSelectedItemId] = useState(preselectedItemId ?? '')
  const [askingPriceUsd, setAskingPriceUsd] = useState('')

  const { data: cards, isLoading } = useQuery({ queryKey: ['portfolio', userId], queryFn: () => getPortfolioCards(userId) })
  const selectedItem = cards?.find(c => c.id === selectedItemId)

  const create = useMutation({
    mutationFn: createListing,
    onSuccess: () => navigate('/marketplace'),
  })

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!selectedItem) return
    create.mutate({
      sellerId: userId,
      cardId: selectedItem.cardId,
      cardName: selectedItem.cardName,
      condition: selectedItem.condition,
      askingPriceUsd: Number(askingPriceUsd),
    })
  }

  return (
    <div className="mx-auto max-w-md">
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">List a Card</h1>
      <form onSubmit={handleSubmit} className="space-y-3 rounded-xl border border-slate-700/50 bg-slate-900/80 p-4 backdrop-blur-sm sm:p-6">
        <div>
          <label className="mb-1.5 block text-xs font-medium text-slate-400">Card from your portfolio</label>
          <select required value={selectedItemId} onChange={e => setSelectedItemId(e.target.value)}
            className="w-full rounded-lg border border-slate-700 bg-slate-800 px-3 py-2.5 text-sm text-slate-100 focus:border-blue-500 focus:outline-none">
            <option value="" disabled>
              {isLoading ? 'Loading…' : 'Select a card…'}
            </option>
            {cards?.map(c => (
              <option key={c.id} value={c.id}>
                {c.cardName} · {c.condition} · Qty {c.quantity}
              </option>
            ))}
          </select>
          {!isLoading && cards?.length === 0 && (
            <p className="mt-1.5 text-xs text-slate-500">You don't have any cards in your portfolio to list yet.</p>
          )}
        </div>
        {selectedItem && (
          <div className="rounded-lg border border-slate-700 bg-slate-800/60 px-3 py-2 text-xs text-slate-400">
            Condition: <span className="text-slate-200">{selectedItem.condition}</span>
          </div>
        )}
        <div>
          <label className="mb-1.5 block text-xs font-medium text-slate-400">Asking price (USD)</label>
          <input required type="number" step="0.01" min="0.01" value={askingPriceUsd}
            onChange={e => setAskingPriceUsd(e.target.value)}
            className="w-full rounded-lg border border-slate-700 bg-slate-800 px-3 py-2.5 text-sm text-slate-100 placeholder-slate-500 focus:border-blue-500 focus:outline-none" />
        </div>
        <button type="submit" disabled={create.isPending || !selectedItem}
          className="w-full rounded-lg bg-blue-600 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-blue-500 disabled:opacity-50">
          {create.isPending ? 'Listing…' : 'Create Listing'}
        </button>
      </form>
    </div>
  )
}
