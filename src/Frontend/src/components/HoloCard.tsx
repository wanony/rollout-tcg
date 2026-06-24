import { useRef, useCallback } from 'react'

interface HoloCardProps {
  children: React.ReactNode
  rarity?: string
  className?: string
}

export default function HoloCard({ children, rarity = 'Common', className = '' }: HoloCardProps) {
  const cardRef = useRef<HTMLDivElement>(null)

  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    const card = cardRef.current
    if (!card) return
    const rect = card.getBoundingClientRect()
    const x = e.clientX - rect.left
    const y = e.clientY - rect.top
    const cx = rect.width / 2
    const cy = rect.height / 2
    const rotateY = ((x - cx) / cx) * 12
    const rotateX = -((y - cy) / cy) * 12
    const px = (x / rect.width) * 100
    const py = (y / rect.height) * 100
    card.style.setProperty('--pointer-x', `${px}%`)
    card.style.setProperty('--pointer-y', `${py}%`)
    card.style.setProperty('--rotate-x', `${rotateX}deg`)
    card.style.setProperty('--rotate-y', `${rotateY}deg`)
    card.style.setProperty('--card-opacity', '1')
  }, [])

  const handleMouseLeave = useCallback(() => {
    const card = cardRef.current
    if (!card) return
    card.style.setProperty('--rotate-x', '0deg')
    card.style.setProperty('--rotate-y', '0deg')
    card.style.setProperty('--card-opacity', '0')
  }, [])

  const rarityClass = `rarity-${rarity.toLowerCase().replace(/\s/g, '')}`

  return (
    <div
      ref={cardRef}
      className={`holo-card ${rarityClass} ${className}`}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
    >
      {children}
    </div>
  )
}
