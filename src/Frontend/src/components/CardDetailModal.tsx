import { useEffect, useRef, useState, useCallback } from 'react'
import { createPortal } from 'react-dom'
import { motion } from 'framer-motion'
import { createChart, ColorType, AreaSeries } from 'lightweight-charts'
import GlassSurface from './GlassSurface'
import BorderGlow from './BorderGlow'
import { primaryGlow, TYPE_GLOW } from '../lib/typeColors'
import { PokemonCard } from '../api/pokemontcg'
import { addCardToPortfolio } from '../api/portfolio'
import { useAuth } from '../auth/useAuth'

interface CardDetailModalProps {
  card: PokemonCard
  onClose: () => void
}

function TypeBadge({ type }: { type: string }) {
  const rgb = TYPE_GLOW[type] ?? '139 92 246'
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
  const chartRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!chartRef.current) return
    const chart = createChart(chartRef.current, {
      width: chartRef.current.clientWidth,
      height: 120,
      layout: {
        background: { type: ColorType.Solid, color: 'transparent' },
        textColor: '#94a3b8',
      },
      grid: {
        vertLines: { color: 'rgba(148, 163, 184, 0.1)' },
        horzLines: { color: 'rgba(148, 163, 184, 0.1)' },
      },
      rightPriceScale: { borderColor: 'rgba(148, 163, 184, 0.2)' },
      timeScale: { borderColor: 'rgba(148, 163, 184, 0.2)', timeVisible: false },
      crosshair: { mode: 1 },
    })

    // ponytail: no backend sparkline data (pokemontcg IDs aren't GUIDs); mock until card-ID mapping is added
    const series = chart.addSeries(AreaSeries, {
      lineColor: `rgb(${glowColor})`,
      topColor: `rgb(${glowColor} / 0.3)`,
      bottomColor: `rgb(${glowColor} / 0.0)`,
      lineWidth: 2,
    })

    const today = Math.floor(Date.now() / 1000)
    const DAY = 86400
    const mockData = Array.from({ length: 30 }, (_, i) => ({
      time: (today - (29 - i) * DAY) as unknown as number,
      value: 5 + Math.sin(i * 0.4) * 2 + Math.random() * 0.5,
    }))
    series.setData(mockData as Parameters<typeof series.setData>[0])
    chart.timeScale().fitContent()

    const obs = new ResizeObserver(() => {
      if (chartRef.current) {
        chart.applyOptions({ width: chartRef.current.clientWidth })
      }
    })
    obs.observe(chartRef.current)

    return () => {
      chart.remove()
      obs.disconnect()
    }
  }, [glowColor])

  return <div ref={chartRef} className="w-full h-[120px]" />
}

export default function CardDetailModal({ card, onClose }: CardDetailModalProps) {
  const { user } = useAuth()
  const [addState, setAddState] = useState<'idle' | 'loading' | 'done'>('idle')
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
        className="relative z-10 w-full max-w-3xl"
      >
        <GlassSurface
          width="100%"
          height="auto"
          borderRadius={24}
          blur={16}
          brightness={15}
          opacity={0.95}
        >
          <div className="flex w-full flex-col gap-6 p-6 sm:flex-row">
            {/* Card image */}
            <div className="flex flex-shrink-0 justify-center sm:justify-start">
              <img
                src={card.images.large}
                alt={card.name}
                className="max-h-[420px] w-auto rounded-xl object-contain shadow-2xl"
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
                    glowColor="139 92 246"
                    backgroundColor="transparent"
                    borderRadius={12}
                    colors={['#c084fc', '#818cf8', '#38bdf8']}
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
        </GlassSurface>
      </motion.div>
    </div>,
    document.body,
  )
}
