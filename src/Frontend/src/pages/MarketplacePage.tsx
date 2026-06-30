import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { useAuth } from '../auth/useAuth'
import { getListings, purchaseListing, getOffers, makeOffer, acceptOffer, rejectOffer } from '../api/marketplace'
import { Listing } from '../types/api'

function ListingRow({ listing, index, userId }: { listing: Listing; index: number; userId?: string }) {
  const qc = useQueryClient()
  const isOwnListing = userId === listing.sellerId
  const isActive = listing.status === 'Active'
  const [showOffers, setShowOffers] = useState(false)
  const [offerAmount, setOfferAmount] = useState('')

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['listings'] })
    qc.invalidateQueries({ queryKey: ['offers', listing.id] })
  }

  const purchase = useMutation({
    mutationFn: (buyerId: string) => purchaseListing(listing.id, buyerId),
    onSuccess: invalidate,
  })

  const { data: offers } = useQuery({
    queryKey: ['offers', listing.id],
    queryFn: () => getOffers(listing.id),
    enabled: showOffers,
  })
  const pendingOffers = offers?.filter(o => o.status === 'Pending') ?? []

  const submitOffer = useMutation({
    mutationFn: () => makeOffer(listing.id, userId!, Number(offerAmount)),
    onSuccess: () => { setOfferAmount(''); invalidate() },
  })
  const accept = useMutation({ mutationFn: (id: string) => acceptOffer(id), onSuccess: invalidate })
  const reject = useMutation({ mutationFn: (id: string) => rejectOffer(id), onSuccess: invalidate })

  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.04 }}
      className="rounded-xl border border-slate-700/50 bg-slate-900/80 backdrop-blur-sm"
    >
      <div className="flex items-center gap-3 px-4 py-3">
        <div className="flex-1 min-w-0">
          <div className="font-medium text-slate-100 truncate">{listing.cardName}</div>
          <div className="text-xs text-slate-500">{listing.condition}</div>
        </div>
        <div className="font-bold text-green-400 whitespace-nowrap">${listing.askingPriceUsd.toFixed(2)}</div>
        {userId && !isOwnListing && isActive && (
          <button
            onClick={() => purchase.mutate(userId)}
            disabled={purchase.isPending}
            className="rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-blue-500 disabled:opacity-50 active:scale-95 whitespace-nowrap"
          >
            Buy
          </button>
        )}
        {userId && isOwnListing && isActive && (
          <button
            onClick={() => setShowOffers(s => !s)}
            className="rounded-lg border border-slate-700 px-3 py-1.5 text-xs font-semibold text-slate-300 transition-colors hover:border-slate-600 hover:text-slate-100 whitespace-nowrap"
          >
            {showOffers ? 'Hide Offers' : 'View Offers'}
          </button>
        )}
      </div>

      {userId && !isOwnListing && isActive && (
        <div className="flex items-center gap-2 border-t border-slate-800 px-4 py-2">
          <input
            type="number"
            min="0.01"
            step="0.01"
            placeholder="Your offer ($)"
            value={offerAmount}
            onChange={e => setOfferAmount(e.target.value)}
            className="w-32 rounded-lg border border-slate-700 bg-slate-800 px-2 py-1 text-xs text-slate-100 placeholder-slate-500 focus:border-blue-500 focus:outline-none"
          />
          <button
            onClick={() => submitOffer.mutate()}
            disabled={submitOffer.isPending || !offerAmount}
            className="rounded-lg border border-blue-600/60 px-3 py-1 text-xs font-semibold text-blue-400 transition-colors hover:bg-blue-600/10 disabled:opacity-40 whitespace-nowrap"
          >
            {submitOffer.isPending ? 'Sending…' : 'Make Offer'}
          </button>
          {submitOffer.isSuccess && <span className="text-xs text-emerald-400">Offer sent ✓</span>}
        </div>
      )}

      {showOffers && (
        <div className="space-y-1.5 border-t border-slate-800 px-4 py-2">
          {pendingOffers.length === 0 && (
            <div className="text-xs text-slate-500">No pending offers.</div>
          )}
          {pendingOffers.map(offer => (
            <div key={offer.id} className="flex items-center gap-2 rounded-lg bg-slate-800/60 px-2.5 py-1.5">
              <span className="flex-1 text-xs text-slate-300">${offer.offeredPriceUsd.toFixed(2)}</span>
              <button
                onClick={() => accept.mutate(offer.id)}
                disabled={accept.isPending || reject.isPending}
                className="rounded-md bg-emerald-600/80 px-2 py-0.5 text-[11px] font-semibold text-white transition-colors hover:bg-emerald-500 disabled:opacity-40"
              >
                Accept
              </button>
              <button
                onClick={() => reject.mutate(offer.id)}
                disabled={accept.isPending || reject.isPending}
                className="rounded-md border border-red-700/60 px-2 py-0.5 text-[11px] font-semibold text-red-400 transition-colors hover:bg-red-900/30 disabled:opacity-40"
              >
                Reject
              </button>
            </div>
          ))}
        </div>
      )}
    </motion.div>
  )
}

export default function MarketplacePage() {
  const { user } = useAuth()

  const { data: listings, isLoading } = useQuery({
    queryKey: ['listings'],
    queryFn: getListings,
  })

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-100 sm:text-2xl">Marketplace</h1>
        {user && (
          <Link
            to="/marketplace/new"
            className="rounded-lg bg-blue-600 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-500 active:scale-95"
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
          <ListingRow key={listing.id} listing={listing} index={i} userId={user?.profile.sub} />
        ))}
        {!isLoading && listings?.length === 0 && (
          <div className="mt-16 text-center text-slate-500">No active listings.</div>
        )}
      </div>
    </div>
  )
}
