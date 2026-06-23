import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { createListing } from '../api/marketplace'

export default function NewListingPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({
    cardName: '', cardId: '', condition: 'NearMint', askingPriceUsd: ''
  })

  const create = useMutation({
    mutationFn: createListing,
    onSuccess: () => navigate('/marketplace'),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    create.mutate({
      sellerId: user!.profile.sub,
      cardId: form.cardId || crypto.randomUUID(),
      cardName: form.cardName,
      condition: form.condition,
      askingPriceUsd: Number(form.askingPriceUsd),
    })
  }

  return (
    <div className="max-w-md">
      <h1 className="text-2xl font-bold mb-4">List a Card for Sale</h1>
      <form onSubmit={handleSubmit} className="bg-white border rounded p-6 space-y-4">
        <div>
          <label className="block text-sm font-medium mb-1">Card name</label>
          <input required value={form.cardName}
            onChange={e => setForm(f => ({ ...f, cardName: e.target.value }))}
            className="border rounded px-3 py-2 w-full" />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Condition</label>
          <select value={form.condition}
            onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}
            className="border rounded px-3 py-2 w-full">
            {['Mint', 'NearMint', 'LightlyPlayed', 'Played', 'HeavilyPlayed'].map(c =>
              <option key={c}>{c}</option>)}
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Asking price (USD)</label>
          <input required type="number" step="0.01" min="0.01" value={form.askingPriceUsd}
            onChange={e => setForm(f => ({ ...f, askingPriceUsd: e.target.value }))}
            className="border rounded px-3 py-2 w-full" />
        </div>
        <button type="submit" disabled={create.isPending}
          className="w-full py-2 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50">
          {create.isPending ? 'Creating...' : 'Create Listing'}
        </button>
      </form>
    </div>
  )
}
