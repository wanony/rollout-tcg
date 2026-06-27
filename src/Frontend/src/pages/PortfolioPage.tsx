import { useState, FormEvent } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { useAuth } from '../auth/useAuth'
import { getPortfolioCards, getPortfolioSummary, addCardToPortfolio, removeCardFromPortfolio } from '../api/portfolio'

const CONDITIONS = ['Mint', 'NearMint', 'LightlyPlayed', 'Played', 'HeavilyPlayed']

export default function PortfolioPage() {
  const { user } = useAuth()
  const userId = user!.profile.sub
  const qc = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({ cardName: '', quantity: '1', condition: 'NearMint', acquisitionPriceUsd: '' })

  const { data: cards } = useQuery({ queryKey: ['portfolio', userId], queryFn: () => getPortfolioCards(userId) })
  const { data: summary } = useQuery({ queryKey: ['portfolio-summary', userId], queryFn: () => getPortfolioSummary(userId) })

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['portfolio', userId] })
    qc.invalidateQueries({ queryKey: ['portfolio-summary', userId] })
  }

  const addMutation = useMutation({ mutationFn: addCardToPortfolio, onSuccess: () => { invalidate(); setShowForm(false); setForm({ cardName: '', quantity: '1', condition: 'NearMint', acquisitionPriceUsd: '' }) } })
  const removeMutation = useMutation({ mutationFn: removeCardFromPortfolio, onSuccess: invalidate })

  const handleAdd = (e: FormEvent) => {
    e.preventDefault()
    addMutation.mutate({ userId, cardId: crypto.randomUUID(), cardName: form.cardName, quantity: Number(form.quantity), condition: form.condition, acquisitionPriceUsd: Number(form.acquisitionPriceUsd) })
  }

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">My Portfolio</h1>

      {/* Summary */}
      {summary && (
        <div className="mb-4 grid grid-cols-3 gap-2 rounded-xl border border-slate-700/50 bg-slate-900/80 p-3 backdrop-blur-sm sm:p-4">
          {[
            { label: 'Items', value: summary.totalItems },
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
        onClick={() => setShowForm(f => !f)}
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
          <input required placeholder="Card name" value={form.cardName}
            onChange={e => setForm(f => ({ ...f, cardName: e.target.value }))}
            className="col-span-2 rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 focus:border-blue-500 focus:outline-none sm:col-span-2" />
          <input required type="number" min="1" placeholder="Qty" value={form.quantity}
            onChange={e => setForm(f => ({ ...f, quantity: e.target.value }))}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 focus:border-blue-500 focus:outline-none" />
          <input required type="number" step="0.01" placeholder="Price" value={form.acquisitionPriceUsd}
            onChange={e => setForm(f => ({ ...f, acquisitionPriceUsd: e.target.value }))}
            className="rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 focus:border-blue-500 focus:outline-none" />
          <select value={form.condition} onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}
            className="col-span-2 rounded-lg border border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-100 focus:border-blue-500 focus:outline-none">
            {CONDITIONS.map(c => <option key={c}>{c}</option>)}
          </select>
          <button type="submit" disabled={addMutation.isPending}
            className="col-span-2 rounded-lg bg-blue-600 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-500 disabled:opacity-50">
            {addMutation.isPending ? 'Adding…' : 'Add'}
          </button>
        </motion.form>
      )}

      {/* Card list */}
      <div className="space-y-2">
        {cards?.map((item, i) => (
          <motion.div key={item.id} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: i * 0.03 }}
            className="flex items-center gap-3 rounded-xl border border-slate-700/50 bg-slate-900/80 px-4 py-3 backdrop-blur-sm">
            <div className="flex-1 min-w-0">
              <div className="font-medium text-slate-100 truncate">{item.cardName}</div>
              <div className="text-xs text-slate-500">Qty {item.quantity} · {item.condition} · ${item.acquisitionPriceUsd}/ea</div>
            </div>
            <button onClick={() => removeMutation.mutate(item.id)}
              className="shrink-0 rounded-lg px-2.5 py-1.5 text-xs text-slate-500 transition-colors hover:bg-red-900/30 hover:text-red-400">
              Remove
            </button>
          </motion.div>
        ))}
        {cards?.length === 0 && <div className="mt-16 text-center text-slate-500">No cards yet.</div>}
      </div>
    </div>
  )
}
