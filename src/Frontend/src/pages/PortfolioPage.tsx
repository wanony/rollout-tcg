import { useState } from 'react'
import { FormEvent } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import {
  getPortfolioCards, getPortfolioSummary,
  addCardToPortfolio, removeCardFromPortfolio
} from '../api/portfolio'

export default function PortfolioPage() {
  const { user } = useAuth()
  const userId = user!.profile.sub
  const qc = useQueryClient()

  const { data: cards } = useQuery({
    queryKey: ['portfolio', userId],
    queryFn: () => getPortfolioCards(userId),
  })

  const { data: summary } = useQuery({
    queryKey: ['portfolio-summary', userId],
    queryFn: () => getPortfolioSummary(userId),
  })

  const addMutation = useMutation({
    mutationFn: addCardToPortfolio,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['portfolio', userId] })
      qc.invalidateQueries({ queryKey: ['portfolio-summary', userId] })
    },
  })

  const removeMutation = useMutation({
    mutationFn: removeCardFromPortfolio,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['portfolio', userId] })
      qc.invalidateQueries({ queryKey: ['portfolio-summary', userId] })
    },
  })

  const [form, setForm] = useState({
    cardId: '', cardName: '', quantity: '1',
    condition: 'NearMint', acquisitionPriceUsd: ''
  })

  const handleAdd = (e: FormEvent) => {
    e.preventDefault()
    addMutation.mutate({
      userId,
      cardId: form.cardId || crypto.randomUUID(),
      cardName: form.cardName,
      quantity: Number(form.quantity),
      condition: form.condition,
      acquisitionPriceUsd: Number(form.acquisitionPriceUsd),
    })
    setForm({ cardId: '', cardName: '', quantity: '1', condition: 'NearMint', acquisitionPriceUsd: '' })
  }

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">My Portfolio</h1>

      {summary && (
        <div className="bg-white border rounded p-4 mb-6 flex gap-8">
          <div><div className="text-sm text-gray-500">Items</div><div className="text-xl font-bold">{summary.totalItems}</div></div>
          <div><div className="text-sm text-gray-500">Total cards</div><div className="text-xl font-bold">{summary.totalCards}</div></div>
          <div><div className="text-sm text-gray-500">Acquisition cost</div><div className="text-xl font-bold">${summary.totalAcquisitionCostUsd.toFixed(2)}</div></div>
        </div>
      )}

      <form onSubmit={handleAdd} className="bg-white border rounded p-4 mb-6 flex flex-wrap gap-2">
        <input required placeholder="Card name" value={form.cardName}
          onChange={e => setForm(f => ({ ...f, cardName: e.target.value }))}
          className="border rounded px-2 py-1" />
        <input required type="number" min="1" placeholder="Qty" value={form.quantity}
          onChange={e => setForm(f => ({ ...f, quantity: e.target.value }))}
          className="border rounded px-2 py-1 w-20" />
        <input required type="number" step="0.01" placeholder="Price paid" value={form.acquisitionPriceUsd}
          onChange={e => setForm(f => ({ ...f, acquisitionPriceUsd: e.target.value }))}
          className="border rounded px-2 py-1 w-28" />
        <select value={form.condition}
          onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}
          className="border rounded px-2 py-1">
          {['Mint', 'NearMint', 'LightlyPlayed', 'Played', 'HeavilyPlayed'].map(c =>
            <option key={c}>{c}</option>)}
        </select>
        <button type="submit"
          className="px-4 py-1 bg-green-600 text-white rounded hover:bg-green-700">
          Add Card
        </button>
      </form>

      <div className="space-y-2">
        {cards?.map(item => (
          <div key={item.id} className="bg-white border rounded px-4 py-3 flex items-center gap-4">
            <div className="flex-1">
              <div className="font-medium">{item.cardName}</div>
              <div className="text-sm text-gray-500">Qty: {item.quantity} · {item.condition} · ${item.acquisitionPriceUsd}/ea</div>
            </div>
            <button
              onClick={() => removeMutation.mutate(item.id)}
              className="text-sm text-red-600 hover:text-red-800"
            >Remove</button>
          </div>
        ))}
        {cards?.length === 0 && <p className="text-gray-500">No cards yet. Add one above.</p>}
      </div>
    </div>
  )
}
