import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
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
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-100 sm:text-2xl">Marketplace</h1>
        {user && (
          <Link
            to="/marketplace/new"
            className="rounded-lg bg-violet-600 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-violet-500 active:scale-95"
          >
            + List Card
          </Link>
        )}
      </div>

      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-16 animate-pulse rounded-xl bg-slate-800/60" />
          ))}
        </div>
      )}

      <div className="space-y-2">
        {listings?.map((listing, i) => (
          <motion.div
            key={listing.id}
            initial={{ opacity: 0, x: -10 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: i * 0.04 }}
            className="flex items-center gap-3 rounded-xl border border-slate-700/50 bg-slate-900/80 px-4 py-3 backdrop-blur-sm"
          >
            <div className="flex-1 min-w-0">
              <div className="font-medium text-slate-100 truncate">{listing.cardName}</div>
              <div className="text-xs text-slate-500">{listing.condition}</div>
            </div>
            <div className="font-bold text-green-400 whitespace-nowrap">${listing.askingPriceUsd.toFixed(2)}</div>
            {user && user.profile.sub !== listing.sellerId && (
              <button
                onClick={() => purchase.mutate({ id: listing.id, buyerId: user.profile.sub })}
                disabled={purchase.isPending}
                className="rounded-lg bg-violet-600 px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-violet-500 disabled:opacity-50 active:scale-95 whitespace-nowrap"
              >
                Buy
              </button>
            )}
          </motion.div>
        ))}
        {!isLoading && listings?.length === 0 && (
          <div className="mt-16 text-center text-slate-500">No active listings.</div>
        )}
      </div>
    </div>
  )
}
