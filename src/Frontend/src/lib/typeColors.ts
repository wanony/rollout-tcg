export const TYPE_GLOW: Record<string, string> = {
  Fire:       '239 68 68',
  Water:      '59 130 246',
  Grass:      '34 197 94',
  Lightning:  '234 179 8',
  Psychic:    '168 85 247',
  Fighting:   '249 115 22',
  Darkness:   '100 116 139',
  Metal:      '148 163 184',
  Dragon:     '99 102 241',
  Fairy:      '236 72 153',
  Colorless:  '203 213 225',
}

const FALLBACK = '139 92 246'

export function primaryGlow(types?: string[]): string {
  if (!types?.length) return FALLBACK
  return TYPE_GLOW[types[0]] ?? FALLBACK
}
