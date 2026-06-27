import { useRef, useState, useCallback, useEffect, useMemo } from 'react'
import { createPortal } from 'react-dom'
import { motion } from 'framer-motion'
import { useQuery } from '@tanstack/react-query'
import BorderGlow from './BorderGlow'
import { primaryGlow, TYPE_GLOW } from '../lib/typeColors'
import { PokemonCard, fetchCardById } from '../api/pokemontcg'
import { addCardToPortfolio } from '../api/portfolio'
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

function PriceSparkline({ glowColor }: { glowColor: string }) {
  // ponytail: no backend price history; mock until card-ID mapping lands
  const data = useMemo(
    () => Array.from({ length: 30 }, (_, i) => 5 + Math.sin(i * 0.4) * 2 + Math.random() * 0.5),
    [],
  )
  const W = 400, H = 100, PX = 4, PY = 6
  const min = Math.min(...data), max = Math.max(...data)
  const px = (i: number) => PX + (i / (data.length - 1)) * (W - PX * 2)
  const py = (v: number) => H - PY - ((v - min) / (max - min || 1)) * (H - PY * 2)
  const pts = data.map((v, i) => `${i === 0 ? 'M' : 'L'}${px(i).toFixed(1)},${py(v).toFixed(1)}`).join(' ')
  const area = `${pts} L${px(data.length - 1).toFixed(1)},${H} L${px(0).toFixed(1)},${H} Z`
  const color = `rgb(${glowColor})`
  const gradId = `sg-${glowColor.replace(/\s/g, '')}`

  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="w-full h-[100px]" preserveAspectRatio="none">
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.35} />
          <stop offset="100%" stopColor={color} stopOpacity={0} />
        </linearGradient>
      </defs>
      <path d={area} fill={`url(#${gradId})`} />
      <path d={pts} fill="none" stroke={color} strokeWidth="2" strokeLinejoin="round" />
    </svg>
  )
}

export default function CardDetailModal({ card: cardProp, onClose }: CardDetailModalProps) {
  const { user } = useAuth()
  const [addState, setAddState] = useState<'idle' | 'loading' | 'done'>('idle')

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
      setAddState('idle')
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
        className="relative z-10 w-full max-w-3xl max-h-[90dvh] overflow-y-auto rounded-3xl"
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
              </div>

              {/* Type badges */}
              {card.types && card.types.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {card.types.map((t) => (
                    <TypeBadge key={t} type={t} />
                  ))}
                </div>
              )}

              {/* Price sparkline */}
              <div>
                <p className="mb-1 text-xs font-medium uppercase tracking-wider text-slate-500">
                  Price trend (30d)
                </p>
                <PriceSparkline glowColor={glowColor} />
              </div>

              {/* TCGPlayer price tiers */}
              {hasPrices && (
                <div>
                  <p className="mb-2 text-xs font-medium uppercase tracking-wider text-slate-500">
                    TCGPlayer prices
                  </p>
                  <dl className="grid grid-cols-3 gap-3">
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

              {/* Add to Portfolio CTA */}
              {user && (
                <div className="mt-auto pt-2">
                  <BorderGlow
                    glowColor="59 130 246"
                    backgroundColor="transparent"
                    borderRadius={12}
                    colors={['#60a5fa', '#38bdf8', '#38bdf8']}
                    className="w-full"
                  >
                    <button
                      onClick={handleAddToPortfolio}
                      disabled={addState === 'loading'}
                      className="w-full rounded-xl px-4 py-2.5 text-sm font-semibold text-slate-100 transition-colors disabled:opacity-50"
                    >
                      {addState === 'done'
                        ? 'Added ✓'
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
