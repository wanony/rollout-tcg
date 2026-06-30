import { motion } from 'framer-motion'
import HoloCard from './HoloCard'
import { TYPE_GLOW } from '../lib/typeColors'
import { PokemonCard } from '../api/pokemontcg'

function TypeBadge({ type }: { type: string }) {
  const rgb = TYPE_GLOW[type] ?? '100 116 139'
  return (
    <span
      className="inline-flex rounded-full px-1.5 py-0.5 text-[9px] font-medium leading-none"
      style={{
        background: `rgb(${rgb} / 0.2)`,
        border: `1px solid rgb(${rgb} / 0.35)`,
        color: `rgb(${rgb})`,
      }}
    >
      {type}
    </span>
  )
}

interface CardTileProps {
  card: PokemonCard
  index?: number
  price?: string | null
  onClick?: () => void
  footer?: React.ReactNode
}

export default function CardTile({ card, index = 0, price, onClick, footer }: CardTileProps) {
  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.92 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ delay: Math.min((index % 20) * 0.04, 0.5), duration: 0.25 }}
      style={{ aspectRatio: '5/7' }}
      onClick={onClick}
      className={onClick ? 'cursor-pointer' : undefined}
    >
      <HoloCard rarity={card.rarity} types={card.types} className="h-full w-full">
        <div className="relative h-full w-full overflow-hidden rounded-[4.5%/3.5%]">
          <img
            src={card.images.small}
            alt={card.name}
            className="h-full w-full object-cover"
            loading="lazy"
            draggable={false}
          />
          <div className="absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/80 to-transparent px-2 pb-1.5 pt-4">
            <div className="truncate text-xs font-semibold text-white">{card.name}</div>
            <div className="flex items-center justify-between gap-1">
              <div className="truncate text-[10px] text-slate-300 opacity-80">{card.set.name}</div>
              {price && (
                <span className="flex-shrink-0 text-[10px] font-semibold text-emerald-400">{price}</span>
              )}
            </div>
            {card.types && card.types.length > 0 && (
              <div className="mt-0.5 flex flex-wrap gap-0.5">
                {card.types.map(t => <TypeBadge key={t} type={t} />)}
              </div>
            )}
            {footer}
          </div>
        </div>
      </HoloCard>
    </motion.div>
  )
}
