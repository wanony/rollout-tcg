import { useRef, useCallback } from 'react'

const HOLO_TIER: Record<string, 'none' | 'holo' | 'ultra'> = {
  Common: 'none',
  Uncommon: 'none',
  Rare: 'holo',
  'Rare Holo': 'holo',
  'Rare Holo EX': 'holo',
  'Rare Holo GX': 'holo',
  'Rare Holo V': 'holo',
  'Rare Holo VMAX': 'ultra',
  'Rare Holo VSTAR': 'ultra',
  'Double Rare': 'ultra',
  'Ultra Rare': 'ultra',
  'Hyper Rare': 'ultra',
  'Shiny Rare': 'ultra',
  'Shiny Ultra Rare': 'ultra',
  'Special Illustration Rare': 'ultra',
  'Illustration Rare': 'holo',
  'ACE SPEC Rare': 'ultra',
}

function holoTier(rarity: string): 'none' | 'holo' | 'ultra' {
  return HOLO_TIER[rarity] ?? (rarity.toLowerCase().includes('rare') ? 'holo' : 'none')
}

interface HoloCardProps {
  children: React.ReactNode
  rarity?: string
  className?: string
}

export default function HoloCard({ children, rarity = 'Common', className = '' }: HoloCardProps) {
  const cardRef = useRef<HTMLDivElement>(null)
  const tier = holoTier(rarity)

  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    const card = cardRef.current
    if (!card || tier === 'none') return
    const rect = card.getBoundingClientRect()
    const x = e.clientX - rect.left
    const y = e.clientY - rect.top
    const cx = rect.width / 2
    const cy = rect.height / 2
    const rotateY = ((x - cx) / cx) * 15
    const rotateX = -((y - cy) / cy) * 15
    card.style.setProperty('--pointer-x', `${(x / rect.width) * 100}%`)
    card.style.setProperty('--pointer-y', `${(y / rect.height) * 100}%`)
    card.style.setProperty('--rotate-x', `${rotateX}deg`)
    card.style.setProperty('--rotate-y', `${rotateY}deg`)
    card.style.setProperty('--shine-opacity', tier === 'ultra' ? '1' : '0.6')
    card.style.setProperty('--foil-opacity', tier === 'ultra' ? '0.7' : '0.35')
  }, [tier])

  const handleMouseLeave = useCallback(() => {
    const card = cardRef.current
    if (!card) return
    card.style.setProperty('--rotate-x', '0deg')
    card.style.setProperty('--rotate-y', '0deg')
    card.style.setProperty('--shine-opacity', '0')
    card.style.setProperty('--foil-opacity', '0')
  }, [])

  return (
    <div
      ref={cardRef}
      className={`holo-card ${className}`}
      data-tier={tier}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
    >
      {children}
    </div>
  )
}
