import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { getListings, purchaseListing } from '../api/marketplace'

export default function MarketplacePage() {
  const { user } = useAuth()
  const qc = useQueryClient()

  const { data: listings, isLoading } = useQuery({
    queryKey: ['listings'],
    queryFn: getListings,
  })

  const purchase = useMutation({
    mutationFn: ({ id, buyerId }: { id: string; buyerId: string }) =>
      purchaseListing(id, buyerId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['listings'] }),
  })

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-bold">Marketplace</h1>
        {user && (
          <Link to="/marketplace/new"
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">
            + List a Card
          </Link>
        )}
      </div>

      {isLoading && <p>Loading...</p>}

      <div className="space-y-3">
        {listings?.map(listing => (
          <div key={listing.id} className="bg-white border rounded px-4 py-3 flex items-center gap-4">
            <div className="flex-1">
              <div className="font-medium">{listing.cardName}</div>
              <div className="text-sm text-gray-500">{listing.condition} · Seller: {listing.sellerId.slice(0, 8)}…</div>
            </div>
            <div className="font-bold text-green-700">${listing.askingPriceUsd.toFixed(2)}</div>
            {user && user.profile.sub !== listing.sellerId && (
              <button
                onClick={() => purchase.mutate({ id: listing.id, buyerId: user.profile.sub })}
                disabled={purchase.isPending}
                className="px-3 py-1 bg-blue-600 text-white text-sm rounded hover:bg-blue-700 disabled:opacity-50"
              >
                Buy
              </button>
            )}
          </div>
        ))}
        {listings?.length === 0 && <p className="text-gray-500">No active listings.</p>}
      </div>
    </div>
  )
}
