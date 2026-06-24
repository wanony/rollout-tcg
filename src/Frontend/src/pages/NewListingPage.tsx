import { useState, FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { createListing } from '../api/marketplace'

export default function NewListingPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ cardName: '', condition: 'NearMint', askingPriceUsd: '' })

  const create = useMutation({
    mutationFn: createListing,
    onSuccess: () => navigate('/marketplace'),
  })

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    create.mutate({ sellerId: user!.profile.sub, cardId: crypto.randomUUID(), cardName: form.cardName, condition: form.condition, askingPriceUsd: Number(form.askingPriceUsd) })
  }

  return (
    <div className="mx-auto max-w-md">
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">List a Card</h1>
      <form onSubmit={handleSubmit} className="space-y-3 rounded-xl border border-slate-700/50 bg-slate-900/80 p-4 backdrop-blur-sm sm:p-6">
        <div>
          <label className="mb-1.5 block text-xs font-medium text-slate-400">Card name</label>
          <input required value={form.cardName} onChange={e => setForm(f => ({ ...f, cardName: e.target.value }))}
            className="w-full rounded-lg border border-slate-700 bg-slate-800 px-3 py-2.5 text-sm text-slate-100 placeholder-slate-500 focus:border-violet-500 focus:outline-none" />
        </div>
        <div>
          <label className="mb-1.5 block text-xs font-medium text-slate-400">Condition</label>
          <select value={form.condition} onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}
            className="w-full rounded-lg border border-slate-700 bg-slate-800 px-3 py-2.5 text-sm text-slate-100 focus:border-violet-500 focus:outline-none">
            {['Mint', 'NearMint', 'LightlyPlayed', 'Played', 'HeavilyPlayed'].map(c => <option key={c}>{c}</option>)}
          </select>
        </div>
        <div>
          <label className="mb-1.5 block text-xs font-medium text-slate-400">Asking price (USD)</label>
          <input required type="number" step="0.01" min="0.01" value={form.askingPriceUsd}
            onChange={e => setForm(f => ({ ...f, askingPriceUsd: e.target.value }))}
            className="w-full rounded-lg border border-slate-700 bg-slate-800 px-3 py-2.5 text-sm text-slate-100 placeholder-slate-500 focus:border-violet-500 focus:outline-none" />
        </div>
        <button type="submit" disabled={create.isPending}
          className="w-full rounded-lg bg-violet-600 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-violet-500 disabled:opacity-50">
          {create.isPending ? 'Listing…' : 'Create Listing'}
        </button>
      </form>
    </div>
  )
}
