import { useRef, useState, useCallback, useEffect, useMemo } from 'react'
import { createPortal } from 'react-dom'
import { motion } from 'framer-motion'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import BorderGlow from './BorderGlow'
import { primaryGlow, TYPE_GLOW, typeGradientColors } from '../lib/typeColors'
import { PokemonCard, CardmarketPricing, fetchCardById } from '../api/pokemontcg'
import { addCardToPortfolio } from '../api/portfolio'
import { createAlert, getAlerts, deleteAlert } from '../api/alerts'
import { useAuth } from '../auth/useAuth'

interface CardDetailModalProps {
  card: PokemonCard
  onClose: () => void
}

function TypeBadge({ type }: { type: string }) {
  const rgb = TYPE_GLOW[type] ?? '100 116 139'
  return (
    <span
      className="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
      style={{
        background: `rgb(${rgb} / 0.2)`,
        border: `1px solid rgb(${rgb} / 0.4)`,
        color: `rgb(${rgb})`,
      }}
    >
      {type}
    </span>
  )
}

function PriceSparkline({ glowColor, cardmarket }: { glowColor: string; cardmarket?: CardmarketPricing }) {
  const data = useMemo(() => {
    // Use real Cardmarket rolling averages when available (avg30→avg7→avg1→trend)
    const cm = cardmarket
    if (cm?.avg30 != null && cm?.avg7 != null && cm?.avg1 != null) {
      const anchors: [number, number][] = [
        [0,  cm.avg30],
        [23, cm.avg7],
        [29, cm.avg1],
        [30, cm.trend ?? cm.avg ?? cm.avg1],
      ]
      return Array.from({ length: 31 }, (_, i) => {
        for (let a = 1; a < anchors.length; a++) {
          const [x0, y0] = anchors[a - 1], [x1, y1] = anchors[a]
          if (i <= x1) return y0 + (y1 - y0) * ((i - x0) / (x1 - x0))
        }
        return anchors[anchors.length - 1][1]
      })
    }
    // Fallback: neutral flat line while data loads
    return Array.from({ length: 30 }, () => 1)
  }, [cardmarket])

  const W = 400, H = 100, PX = 4, PY = 6
  const min = Math.min(...data), max = Math.max(...data)
  const flat = max === min
  const px = (i: number) => PX + (i / (data.length - 1)) * (W - PX * 2)
  const py = (v: number) => flat ? H / 2 : H - PY - ((v - min) / (max - min)) * (H - PY * 2)
  const pts = data.map((v, i) => `${i === 0 ? 'M' : 'L'}${px(i).toFixed(1)},${py(v).toFixed(1)}`).join(' ')
  const area = `${pts} L${px(data.length - 1).toFixed(1)},${H} L${px(0).toFixed(1)},${H} Z`
  const color = `rgb(${glowColor})`
  const gradId = `sg-${glowColor.replace(/\s/g, '')}`

  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="w-full h-[100px]" preserveAspectRatio="none">
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={flat ? 0.1 : 0.35} />
          <stop offset="100%" stopColor={color} stopOpacity={0} />
        </linearGradient>
      </defs>
      <path d={area} fill={`url(#${gradId})`} />
      <path d={pts} fill="none" stroke={color} strokeWidth="2" strokeLinejoin="round" strokeOpacity={flat ? 0.3 : 1} />
    </svg>
  )
}

function PriceAlertControl({ userId, providerCardId, cardName }: { userId: string; providerCardId: string; cardName: string }) {
  const queryClient = useQueryClient()
  const [direction, setDirection] = useState<'Below' | 'Above'>('Below')
  const [targetPrice, setTargetPrice] = useState('')
  const [recurring, setRecurring] = useState(false)
  const [createState, setCreateState] = useState<'idle' | 'loading' | 'error'>('idle')

  const { data: alerts } = useQuery({
    queryKey: ['alerts', userId],
    queryFn: () => getAlerts(userId),
  })
  const cardAlerts = (alerts ?? []).filter((a) => a.providerCardId === providerCardId)

  const handleCreate = async () => {
    const price = parseFloat(targetPrice)
    if (!Number.isFinite(price) || price <= 0 || createState === 'loading') return
    setCreateState('loading')
    try {
      await createAlert({
        userId,
        providerCardId,
        cardName,
        direction,
        targetPriceUsd: price,
        recurring,
      })
      setTargetPrice('')
      setRecurring(false)
      setCreateState('idle')
      await queryClient.invalidateQueries({ queryKey: ['alerts', userId] })
    } catch {
      setCreateState('error')
      setTimeout(() => setCreateState('idle'), 2000)
    }
  }

  const handleDelete = async (id: string) => {
    await deleteAlert(id, userId)
    await queryClient.invalidateQueries({ queryKey: ['alerts', userId] })
  }

  return (
    <div>
      <p className="mb-2 text-xs font-medium uppercase tracking-wider text-slate-500">
        Price alert
      </p>
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex overflow-hidden rounded-lg border border-white/[0.08]">
          {(['Below', 'Above'] as const).map((d) => (
            <button
              key={d}
              type="button"
              onClick={() => setDirection(d)}
              className={`px-3 py-1.5 text-xs font-medium transition-colors ${
                direction === d
                  ? 'bg-slate-700 text-slate-100'
                  : 'bg-slate-800/60 text-slate-400 hover:text-slate-200'
              }`}
            >
              {d}
            </button>
          ))}
        </div>
        <input
          type="number"
          min="0"
          step="0.01"
          value={targetPrice}
          onChange={(e) => setTargetPrice(e.target.value)}
          placeholder="Target price (USD)"
          className="w-36 rounded-lg bg-slate-800/60 px-3 py-1.5 text-sm text-slate-200 placeholder:text-slate-500 focus:outline-none"
        />
        <label className="flex items-center gap-1.5 text-xs text-slate-400">
          <input
            type="checkbox"
            checked={recurring}
            onChange={(e) => setRecurring(e.target.checked)}
            className="rounded border-white/[0.08]"
          />
          Recurring
        </label>
        <button
          type="button"
          onClick={handleCreate}
          disabled={createState === 'loading' || !targetPrice}
          className="rounded-lg bg-slate-700 px-3 py-1.5 text-xs font-semibold text-slate-100 transition-colors hover:bg-slate-600 disabled:opacity-50"
        >
          {createState === 'loading' ? 'Creating…' : createState === 'error' ? 'Failed — retry' : 'Create alert'}
        </button>
      </div>

      {cardAlerts.length > 0 && (
        <ul className="mt-2 flex flex-col gap-1.5">
          {cardAlerts.map((a) => (
            <li
              key={a.id}
              className="flex items-center justify-between rounded-lg bg-slate-800/60 px-3 py-2 text-xs"
            >
              <span className="text-slate-300">
                {a.direction} ${a.targetPriceUsd.toFixed(2)}
                {a.recurring && <span className="ml-1 text-slate-500">(recurring)</span>}
                {!a.isActive && <span className="ml-1 text-slate-500">(fired)</span>}
              </span>
              <button
                type="button"
                onClick={() => handleDelete(a.id)}
                className="text-slate-500 transition-colors hover:text-red-400"
              >
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

export default function CardDetailModal({ card: cardProp, onClose }: CardDetailModalProps) {
  const { user } = useAuth()
  const [addState, setAddState] = useState<'idle' | 'loading' | 'done' | 'error'>('idle')

  // Enrich with full card data (list endpoint only has name + image)
  const { data: fullCard } = useQuery({
    queryKey: ['card', cardProp.id],
    queryFn: () => fetchCardById(cardProp.id),
    staleTime: 24 * 60 * 60 * 1000,
  })
  const card = fullCard ?? cardProp

  const glowColor = primaryGlow(card.types)

  const handleClose = useCallback(() => onClose(), [onClose])

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') handleClose()
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [handleClose])

  const handleAddToPortfolio = async () => {
    if (!user || addState !== 'idle') return
    setAddState('loading')
    try {
      const price =
        card.tcgplayer?.prices?.holofoil?.market ??
        card.tcgplayer?.prices?.normal?.market ??
        0
      await addCardToPortfolio({
        userId: user.profile.sub,
        cardId: card.id,
        cardName: card.name,
        quantity: 1,
        condition: 'NearMint',
        acquisitionPriceUsd: price,
      })
      setAddState('done')
      setTimeout(() => setAddState('idle'), 1500)
    } catch {
      setAddState('error')
      setTimeout(() => setAddState('idle'), 2000)
    }
  }

  const prices = card.tcgplayer?.prices
  const hasPrices = prices && (prices.normal || prices.holofoil || prices.reverseHolofoil)

  return createPortal(
    <div className="fixed inset-0 z-40 flex items-center justify-center p-4">
      <div
        className="absolute inset-0 bg-slate-950/70 backdrop-blur-sm"
        onClick={handleClose}
      />
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        transition={{ duration: 0.2, ease: 'easeOut' }}
        className="relative z-10 w-full max-w-3xl max-h-[90dvh] overflow-y-auto overflow-x-hidden rounded-3xl modal-scroll"
      >
        <div className="rounded-3xl border border-white/[0.08] bg-slate-900/95 backdrop-blur-xl shadow-2xl">
          <div className="flex w-full flex-col gap-6 p-6 sm:flex-row">
            {/* Card image */}
            <div className="flex flex-shrink-0 justify-center sm:justify-start">
              <img
                src={card.images.large}
                alt={card.name}
                className="max-h-[220px] w-auto rounded-xl object-contain shadow-2xl sm:max-h-[420px]"
                onError={(e) => {
                  const img = e.currentTarget
                  img.style.display = 'none'
                  img.nextElementSibling?.removeAttribute('hidden')
                }}
              />
              <div hidden className="h-64 w-44 rounded-xl bg-slate-800" />
            </div>

            {/* Details */}
            <div className="flex flex-1 flex-col gap-4 min-w-0">
              {/* Header */}
              <div>
                <h2 className="text-2xl font-bold text-slate-100">{card.name}</h2>
                <p className="mt-0.5 text-sm text-slate-400">
                  {card.set.name} · {card.rarity}
                  {card.hp && (
                    <span className="ml-2 rounded bg-slate-700/60 px-1.5 py-0.5 text-xs">
                      HP {card.hp}
                    </span>
                  )}
                </p>
                {card.illustrator && (
                  <p className="mt-1 text-xs text-slate-500">Illus. {card.illustrator}</p>
                )}
              </div>

              {/* Type badges */}
              {card.types && card.types.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {card.types.map((t) => (
                    <TypeBadge key={t} type={t} />
                  ))}
                </div>
              )}

              {/* Price sparkline — real Cardmarket 30d data when available */}
              <div>
                <p className="mb-1 text-xs font-medium uppercase tracking-wider text-slate-500">
                  {card.cardmarket ? 'Cardmarket trend (30d · EUR)' : 'Price trend (30d)'}
                </p>
                <PriceSparkline glowColor={glowColor} cardmarket={card.cardmarket} />
              </div>

              {/* Price alert */}
              {user && (
                <PriceAlertControl userId={user.profile.sub} providerCardId={card.id} cardName={card.name} />
              )}

              {/* TCGPlayer USD prices */}
              {hasPrices && (
                <div>
                  <p className="mb-2 text-xs font-medium uppercase tracking-wider text-slate-500">
                    TCGPlayer (USD)
                  </p>
                  <dl className="grid grid-cols-3 gap-2">
                    {prices.normal?.market != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">Normal</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">
                          ${prices.normal.market.toFixed(2)}
                        </dd>
                      </div>
                    )}
                    {prices.holofoil?.market != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">Holofoil</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">
                          ${prices.holofoil.market.toFixed(2)}
                        </dd>
                      </div>
                    )}
                    {prices.reverseHolofoil?.market != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">Rev. Holo</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">
                          ${prices.reverseHolofoil.market.toFixed(2)}
                        </dd>
                      </div>
                    )}
                  </dl>
                </div>
              )}

              {/* Cardmarket EUR prices */}
              {card.cardmarket && (
                <div>
                  <p className="mb-2 text-xs font-medium uppercase tracking-wider text-slate-500">
                    Cardmarket (EUR)
                  </p>
                  <dl className="grid grid-cols-3 gap-2">
                    {card.cardmarket.trend != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">Trend</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">
                          €{card.cardmarket.trend.toFixed(2)}
                        </dd>
                      </div>
                    )}
                    {card.cardmarket.avg7 != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">7d avg</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">
                          €{card.cardmarket.avg7.toFixed(2)}
                        </dd>
                      </div>
                    )}
                    {card.cardmarket.avg30 != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">30d avg</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">
                          €{card.cardmarket.avg30.toFixed(2)}
                        </dd>
                      </div>
                    )}
                  </dl>
                </div>
              )}

              {/* Add to Portfolio CTA */}
              {user && (
                <div className="mt-auto pt-2">
                  <BorderGlow
                    glowColor={glowColor}
                    backgroundColor="transparent"
                    borderRadius={12}
                    colors={typeGradientColors(card.types)}
                    fillOpacity={0}
                    className="w-full"
                  >
                    <button
                      onClick={handleAddToPortfolio}
                      disabled={addState === 'loading'}
                      className="w-full rounded-xl px-4 py-2.5 text-sm font-semibold text-slate-100 transition-colors disabled:opacity-50"
                    >
                      {addState === 'done'
                        ? 'Added ✓'
                        : addState === 'error'
                        ? 'Failed — try again'
                        : addState === 'loading'
                        ? 'Adding…'
                        : 'Add to Portfolio'}
                    </button>
                  </BorderGlow>
                </div>
              )}
            </div>
          </div>
        </div>
      </motion.div>
    </div>,
    document.body,
  )
}
