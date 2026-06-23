import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { searchCards } from '../api/cards'

export default function CardsPage() {
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['cards', search],
    queryFn: () => searchCards(search || undefined),
  })

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Card Catalog</h1>
      <div className="flex gap-2 mb-6">
        <input
          value={q}
          onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && setSearch(q)}
          placeholder="Search cards..."
          className="border rounded px-3 py-2 w-64"
        />
        <button
          onClick={() => setSearch(q)}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
        >
          Search
        </button>
      </div>

      {isLoading && <p>Loading...</p>}
      {data && (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {data.items.map(card => (
            <div key={card.id} className="border rounded p-4 bg-white shadow-sm">
              <div className="font-semibold">{card.name}</div>
              <div className="text-sm text-gray-600">{card.set}</div>
              <div className="text-sm text-gray-500">{card.rarity} · {card.type}</div>
              <p className="text-sm mt-2">{card.text}</p>
            </div>
          ))}
        </div>
      )}
      {data?.items.length === 0 && <p className="text-gray-500">No cards found.</p>}
    </div>
  )
}
