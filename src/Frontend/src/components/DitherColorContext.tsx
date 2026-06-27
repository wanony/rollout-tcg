import { createContext, useContext } from 'react'

type RGB = [number, number, number]
export const DitherColorContext = createContext<(c: RGB | null) => void>(() => {})
export const useDitherOverride = () => useContext(DitherColorContext)

/** Convert a TYPE_GLOW string ("239 68 68") to a 0-1 dither colour, dimmed for dark bg */
export function typeGlowToDither(rgb: string): RGB {
  const [r, g, b] = rgb.split(' ').map(Number)
  return [r / 255 * 0.65, g / 255 * 0.65, b / 255 * 0.65]
}
