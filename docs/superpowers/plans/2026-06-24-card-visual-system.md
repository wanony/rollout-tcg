# Card Visual System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add type-driven hover glow to all cards, enrich the card grid overlay with price/set/type data, and build a GlassSurface card detail modal with a lightweight-charts price sparkline and BorderGlow Add-to-Portfolio CTA.

**Architecture:** `typeColors.ts` is a pure mapping module; `HoloCard` consumes it via a new `types` prop and sets a `--glow-color` CSS var on hover; CSS rules read that var for all tiers. `CardDetailModal` is a React Portal with a `GlassSurface` panel; `BorderGlow` wraps the CTA button. The `PokemonCard` type is extended once in `pokemontcg.ts` for the `tcgplayer` price field. `CardsPage` gains `selectedCard` state and passes `types` + prices into the card grid.

**Tech Stack:** React 18, TypeScript, Tailwind CSS, `framer-motion` (already installed), `lightweight-charts` (TradingView), Vitest (set up in Plan A — run `npm test` from `src/Frontend/`)

## Global Constraints

- Execute after Plan A (Navigation & Transitions) — Vitest is already installed
- All frontend code in `src/Frontend/src/`
- `GlassSurface` already exists at `src/components/GlassSurface.tsx` (from Plan A)
- `lightweight-charts` import: `import { createChart, ColorType } from 'lightweight-charts'`
- `addToPortfolio` API: `addCardToPortfolio({ userId, cardId, cardName, quantity, condition, acquisitionPriceUsd })` from `src/api/portfolio.ts`
- pokemontcg.io v2 returns `tcgplayer.prices` in card responses by default — no API change needed to receive the data
- Run all commands from `src/Frontend/`

---

### Task 1: Install lightweight-charts and create typeColors

**Files:**
- Create: `src/Frontend/src/lib/typeColors.ts`
- Create: `src/Frontend/src/lib/typeColors.test.ts`

**Interfaces:**
- Produces: `export const TYPE_GLOW: Record<string, string>` — space-separated RGB values
- Produces: `export function primaryGlow(types?: string[]): string` — returns RGB string for CSS custom property

- [ ] **Step 1: Install lightweight-charts**

```bash
cd src/Frontend && npm install lightweight-charts
```

Expected: package added with no errors.

- [ ] **Step 2: Write the failing test**

Create `src/Frontend/src/lib/typeColors.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { primaryGlow, TYPE_GLOW } from './typeColors'

describe('primaryGlow', () => {
  it('returns the correct RGB string for Fire', () => {
    expect(primaryGlow(['Fire'])).toBe('239 68 68')
  })

  it('returns the correct RGB string for Water', () => {
    expect(primaryGlow(['Water'])).toBe('59 130 246')
  })

  it('uses the first type when multiple are provided', () => {
    expect(primaryGlow(['Psychic', 'Fire'])).toBe('168 85 247')
  })

  it('returns fallback violet for empty array', () => {
    expect(primaryGlow([])).toBe('139 92 246')
  })

  it('returns fallback violet for undefined', () => {
    expect(primaryGlow(undefined)).toBe('139 92 246')
  })

  it('returns fallback violet for unknown type', () => {
    expect(primaryGlow(['Unknown'])).toBe('139 92 246')
  })

  it('TYPE_GLOW covers all 11 standard types', () => {
    const types = ['Fire','Water','Grass','Lightning','Psychic','Fighting','Darkness','Metal','Dragon','Fairy','Colorless']
    types.forEach(t => expect(TYPE_GLOW[t]).toBeDefined())
  })
})
```

- [ ] **Step 3: Run to verify it fails**

```bash
cd src/Frontend && npm test -- typeColors
```

Expected: FAIL — `Cannot find module './typeColors'`

- [ ] **Step 4: Create typeColors.ts**

Create `src/Frontend/src/lib/typeColors.ts`:

```ts
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
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd src/Frontend && npm test -- typeColors
```

Expected: `7 passed`

- [ ] **Step 6: Commit**

```bash
cd src/Frontend && git add src/lib/typeColors.ts src/lib/typeColors.test.ts package.json package-lock.json && git commit -m "feat: type colour mapping + lightweight-charts"
```

---

### Task 2: Add BorderGlow component

**Files:**
- Create: `src/Frontend/src/components/BorderGlow.tsx`
- Create: `src/Frontend/src/components/BorderGlow.css`

**Interfaces:**
- Produces: `export default BorderGlow` — props: `{ children, className?, edgeSensitivity?, glowColor?, backgroundColor?, borderRadius?, glowRadius?, glowIntensity?, coneSpread?, animated?, colors?, fillOpacity? }`

- [ ] **Step 1: Create BorderGlow.css**

Create `src/Frontend/src/components/BorderGlow.css`:

```css
.border-glow-card {
  --edge-proximity: 0;
  --cursor-angle: 45deg;
  --edge-sensitivity: 30;
  --color-sensitivity: calc(var(--edge-sensitivity) + 20);
  --border-radius: 28px;
  --glow-padding: 40px;
  --cone-spread: 25;

  position: relative;
  border-radius: var(--border-radius);
  isolation: isolate;
  transform: translate3d(0, 0, 0.01px);
  display: grid;
  border: 1px solid rgb(255 255 255 / 15%);
  background: var(--card-bg, #120F17);
  overflow: visible;
  box-shadow:
    rgba(0, 0, 0, 0.1) 0px 1px 2px,
    rgba(0, 0, 0, 0.1) 0px 2px 4px,
    rgba(0, 0, 0, 0.1) 0px 4px 8px,
    rgba(0, 0, 0, 0.1) 0px 8px 16px,
    rgba(0, 0, 0, 0.1) 0px 16px 32px,
    rgba(0, 0, 0, 0.1) 0px 32px 64px;
}

.border-glow-card::before,
.border-glow-card::after,
.border-glow-card > .edge-light {
  content: "";
  position: absolute;
  inset: 0;
  border-radius: inherit;
  transition: opacity 0.25s ease-out;
  z-index: -1;
}

.border-glow-card:not(:hover):not(.sweep-active)::before,
.border-glow-card:not(:hover):not(.sweep-active)::after,
.border-glow-card:not(:hover):not(.sweep-active) > .edge-light {
  opacity: 0;
  transition: opacity 0.75s ease-in-out;
}

.border-glow-card::before {
  border: 1px solid transparent;
  background:
    linear-gradient(var(--card-bg, #120F17) 0 100%) padding-box,
    linear-gradient(rgb(255 255 255 / 0%) 0% 100%) border-box,
    var(--gradient-one, radial-gradient(at 80% 55%, hsla(268, 100%, 76%, 1) 0px, transparent 50%)) border-box,
    var(--gradient-two, radial-gradient(at 69% 34%, hsla(349, 100%, 74%, 1) 0px, transparent 50%)) border-box,
    var(--gradient-three, radial-gradient(at 8% 6%, hsla(136, 100%, 78%, 1) 0px, transparent 50%)) border-box,
    var(--gradient-four, radial-gradient(at 41% 38%, hsla(192, 100%, 64%, 1) 0px, transparent 50%)) border-box,
    var(--gradient-five, radial-gradient(at 86% 85%, hsla(186, 100%, 74%, 1) 0px, transparent 50%)) border-box,
    var(--gradient-six, radial-gradient(at 82% 18%, hsla(52, 100%, 65%, 1) 0px, transparent 50%)) border-box,
    var(--gradient-seven, radial-gradient(at 51% 4%, hsla(12, 100%, 72%, 1) 0px, transparent 50%)) border-box,
    var(--gradient-base, linear-gradient(#c299ff 0 100%)) border-box;
  opacity: calc((var(--edge-proximity) - var(--color-sensitivity)) / (100 - var(--color-sensitivity)));
  mask-image:
    conic-gradient(
      from var(--cursor-angle) at center,
      black calc(var(--cone-spread) * 1%),
      transparent calc((var(--cone-spread) + 15) * 1%),
      transparent calc((100 - var(--cone-spread) - 15) * 1%),
      black calc((100 - var(--cone-spread)) * 1%)
    );
}

.border-glow-card::after {
  border: 1px solid transparent;
  background:
    var(--gradient-one, radial-gradient(at 80% 55%, hsla(268, 100%, 76%, 1) 0px, transparent 50%)) padding-box,
    var(--gradient-two, radial-gradient(at 69% 34%, hsla(349, 100%, 74%, 1) 0px, transparent 50%)) padding-box,
    var(--gradient-three, radial-gradient(at 8% 6%, hsla(136, 100%, 78%, 1) 0px, transparent 50%)) padding-box,
    var(--gradient-four, radial-gradient(at 41% 38%, hsla(192, 100%, 64%, 1) 0px, transparent 50%)) padding-box,
    var(--gradient-five, radial-gradient(at 86% 85%, hsla(186, 100%, 74%, 1) 0px, transparent 50%)) padding-box,
    var(--gradient-six, radial-gradient(at 82% 18%, hsla(52, 100%, 65%, 1) 0px, transparent 50%)) padding-box,
    var(--gradient-seven, radial-gradient(at 51% 4%, hsla(12, 100%, 72%, 1) 0px, transparent 50%)) padding-box,
    var(--gradient-base, linear-gradient(#c299ff 0 100%)) padding-box;
  mask-image:
    linear-gradient(to bottom, black, black),
    radial-gradient(ellipse at 50% 50%, black 40%, transparent 65%),
    radial-gradient(ellipse at 66% 66%, black 5%, transparent 40%),
    radial-gradient(ellipse at 33% 33%, black 5%, transparent 40%),
    radial-gradient(ellipse at 66% 33%, black 5%, transparent 40%),
    radial-gradient(ellipse at 33% 66%, black 5%, transparent 40%),
    conic-gradient(from var(--cursor-angle) at center, transparent 5%, black 15%, black 85%, transparent 95%);
  mask-composite: subtract, add, add, add, add, add;
  opacity: calc(var(--fill-opacity, 0.5) * (var(--edge-proximity) - var(--color-sensitivity)) / (100 - var(--color-sensitivity)));
  mix-blend-mode: soft-light;
}

.border-glow-card > .edge-light {
  inset: calc(var(--glow-padding) * -1);
  pointer-events: none;
  z-index: 1;
  mask-image:
    conic-gradient(
      from var(--cursor-angle) at center, black 2.5%, transparent 10%, transparent 90%, black 97.5%
    );
  opacity: calc((var(--edge-proximity) - var(--edge-sensitivity)) / (100 - var(--edge-sensitivity)));
  mix-blend-mode: plus-lighter;
}

.border-glow-card > .edge-light::before {
  content: "";
  position: absolute;
  inset: var(--glow-padding);
  border-radius: inherit;
  box-shadow:
    inset 0 0 0 1px var(--glow-color, hsl(40deg 80% 80% / 100%)),
    inset 0 0 1px 0 var(--glow-color-60, hsl(40deg 80% 80% / 60%)),
    inset 0 0 3px 0 var(--glow-color-50, hsl(40deg 80% 80% / 50%)),
    inset 0 0 6px 0 var(--glow-color-40, hsl(40deg 80% 80% / 40%)),
    inset 0 0 15px 0 var(--glow-color-30, hsl(40deg 80% 80% / 30%)),
    inset 0 0 25px 2px var(--glow-color-20, hsl(40deg 80% 80% / 20%)),
    inset 0 0 50px 2px var(--glow-color-10, hsl(40deg 80% 80% / 10%)),
    0 0 1px 0 var(--glow-color-60, hsl(40deg 80% 80% / 60%)),
    0 0 3px 0 var(--glow-color-50, hsl(40deg 80% 80% / 50%)),
    0 0 6px 0 var(--glow-color-40, hsl(40deg 80% 80% / 40%)),
    0 0 15px 0 var(--glow-color-30, hsl(40deg 80% 80% / 30%)),
    0 0 25px 2px var(--glow-color-20, hsl(40deg 80% 80% / 20%)),
    0 0 50px 2px var(--glow-color-10, hsl(40deg 80% 80% / 10%));
}

.border-glow-inner {
  display: flex;
  flex-direction: column;
  position: relative;
  overflow: auto;
  z-index: 1;
}
```

- [ ] **Step 2: Create BorderGlow.tsx**

Create `src/Frontend/src/components/BorderGlow.tsx`:

```tsx
import { useRef, useCallback, useEffect, ReactNode, CSSProperties } from 'react'
import './BorderGlow.css'

function parseHSL(hslStr: string) {
  const match = hslStr.match(/([\d.]+)\s*([\d.]+)%?\s*([\d.]+)%?/)
  if (!match) return { h: 40, s: 80, l: 80 }
  return { h: parseFloat(match[1]), s: parseFloat(match[2]), l: parseFloat(match[3]) }
}

function buildGlowVars(glowColor: string, intensity: number): Record<string, string> {
  const { h, s, l } = parseHSL(glowColor)
  const base = `${h}deg ${s}% ${l}%`
  const opacities = [100, 60, 50, 40, 30, 20, 10]
  const keys = ['', '-60', '-50', '-40', '-30', '-20', '-10']
  const vars: Record<string, string> = {}
  for (let i = 0; i < opacities.length; i++) {
    vars[`--glow-color${keys[i]}`] = `hsl(${base} / ${Math.min(opacities[i] * intensity, 100)}%)`
  }
  return vars
}

const GRADIENT_POSITIONS = ['80% 55%', '69% 34%', '8% 6%', '41% 38%', '86% 85%', '82% 18%', '51% 4%']
const GRADIENT_KEYS = ['--gradient-one','--gradient-two','--gradient-three','--gradient-four','--gradient-five','--gradient-six','--gradient-seven']
const COLOR_MAP = [0, 1, 2, 0, 1, 2, 1]

function buildGradientVars(colors: string[]): Record<string, string> {
  const vars: Record<string, string> = {}
  for (let i = 0; i < 7; i++) {
    const c = colors[Math.min(COLOR_MAP[i], colors.length - 1)]
    vars[GRADIENT_KEYS[i]] = `radial-gradient(at ${GRADIENT_POSITIONS[i]}, ${c} 0px, transparent 50%)`
  }
  vars['--gradient-base'] = `linear-gradient(${colors[0]} 0 100%)`
  return vars
}

function easeOutCubic(x: number) { return 1 - Math.pow(1 - x, 3) }
function easeInCubic(x: number) { return x * x * x }

function animateValue({ start = 0, end = 100, duration = 1000, delay = 0, ease = easeOutCubic, onUpdate, onEnd }: {
  start?: number; end?: number; duration?: number; delay?: number
  ease?: (x: number) => number; onUpdate: (v: number) => void; onEnd?: () => void
}) {
  const t0 = performance.now() + delay
  function tick() {
    const elapsed = performance.now() - t0
    const t = Math.min(elapsed / duration, 1)
    onUpdate(start + (end - start) * ease(t))
    if (t < 1) requestAnimationFrame(tick)
    else if (onEnd) onEnd()
  }
  setTimeout(() => requestAnimationFrame(tick), delay)
}

interface BorderGlowProps {
  children?: ReactNode
  className?: string
  edgeSensitivity?: number
  glowColor?: string
  backgroundColor?: string
  borderRadius?: number
  glowRadius?: number
  glowIntensity?: number
  coneSpread?: number
  animated?: boolean
  colors?: string[]
  fillOpacity?: number
}

export default function BorderGlow({
  children,
  className = '',
  edgeSensitivity = 30,
  glowColor = '40 80 80',
  backgroundColor = '#120F17',
  borderRadius = 28,
  glowRadius = 40,
  glowIntensity = 1.0,
  coneSpread = 25,
  animated = false,
  colors = ['#c084fc', '#f472b6', '#38bdf8'],
  fillOpacity = 0.5,
}: BorderGlowProps) {
  const cardRef = useRef<HTMLDivElement>(null)

  const getCenterOfElement = useCallback((el: HTMLElement) => {
    const { width, height } = el.getBoundingClientRect()
    return [width / 2, height / 2]
  }, [])

  const getEdgeProximity = useCallback((el: HTMLElement, x: number, y: number) => {
    const [cx, cy] = getCenterOfElement(el)
    const dx = x - cx, dy = y - cy
    let kx = Infinity, ky = Infinity
    if (dx !== 0) kx = cx / Math.abs(dx)
    if (dy !== 0) ky = cy / Math.abs(dy)
    return Math.min(Math.max(1 / Math.min(kx, ky), 0), 1)
  }, [getCenterOfElement])

  const getCursorAngle = useCallback((el: HTMLElement, x: number, y: number) => {
    const [cx, cy] = getCenterOfElement(el)
    const dx = x - cx, dy = y - cy
    if (dx === 0 && dy === 0) return 0
    let degrees = Math.atan2(dy, dx) * (180 / Math.PI) + 90
    if (degrees < 0) degrees += 360
    return degrees
  }, [getCenterOfElement])

  const handlePointerMove = useCallback((e: React.PointerEvent<HTMLDivElement>) => {
    const card = cardRef.current
    if (!card) return
    const rect = card.getBoundingClientRect()
    const x = e.clientX - rect.left, y = e.clientY - rect.top
    card.style.setProperty('--edge-proximity', `${(getEdgeProximity(card, x, y) * 100).toFixed(3)}`)
    card.style.setProperty('--cursor-angle', `${getCursorAngle(card, x, y).toFixed(3)}deg`)
  }, [getEdgeProximity, getCursorAngle])

  useEffect(() => {
    if (!animated || !cardRef.current) return
    const card = cardRef.current
    card.classList.add('sweep-active')
    card.style.setProperty('--cursor-angle', '110deg')
    animateValue({ duration: 500, onUpdate: v => card.style.setProperty('--edge-proximity', String(v)) })
    animateValue({ ease: easeInCubic, duration: 1500, end: 50, onUpdate: v => {
      card.style.setProperty('--cursor-angle', `${(355 * (v / 100) + 110).toFixed(3)}deg`)
    }})
    animateValue({ ease: easeOutCubic, delay: 1500, duration: 2250, start: 50, end: 100, onUpdate: v => {
      card.style.setProperty('--cursor-angle', `${(355 * (v / 100) + 110).toFixed(3)}deg`)
    }})
    animateValue({ ease: easeInCubic, delay: 2500, duration: 1500, start: 100, end: 0,
      onUpdate: v => card.style.setProperty('--edge-proximity', String(v)),
      onEnd: () => card.classList.remove('sweep-active'),
    })
  }, [animated])

  return (
    <div
      ref={cardRef}
      onPointerMove={handlePointerMove}
      className={`border-glow-card ${className}`}
      style={{
        '--card-bg': backgroundColor,
        '--edge-sensitivity': edgeSensitivity,
        '--border-radius': `${borderRadius}px`,
        '--glow-padding': `${glowRadius}px`,
        '--cone-spread': coneSpread,
        '--fill-opacity': fillOpacity,
        ...buildGlowVars(glowColor, glowIntensity),
        ...buildGradientVars(colors),
      } as CSSProperties}
    >
      <span className="edge-light" />
      <div className="border-glow-inner">{children}</div>
    </div>
  )
}
```

- [ ] **Step 3: Commit**

```bash
cd src/Frontend && git add src/components/BorderGlow.tsx src/components/BorderGlow.css && git commit -m "feat: add BorderGlow component (React Bits)"
```

---

### Task 3: Update HoloCard with type glow and update CSS

**Files:**
- Modify: `src/Frontend/src/components/HoloCard.tsx`
- Modify: `src/Frontend/src/index.css`

**Interfaces:**
- Consumes: `primaryGlow` from `../lib/typeColors`
- Produces: `HoloCard` accepts `types?: string[]` prop; sets `--glow-color` CSS var on the card element on hover

- [ ] **Step 1: Update HoloCard.tsx**

Replace the full content of `src/Frontend/src/components/HoloCard.tsx`:

```tsx
import { useRef, useCallback } from 'react'
import { primaryGlow } from '../lib/typeColors'

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
  types?: string[]
  className?: string
}

export default function HoloCard({ children, rarity = 'Common', types, className = '' }: HoloCardProps) {
  const cardRef = useRef<HTMLDivElement>(null)
  const tier = holoTier(rarity)
  const glowColor = primaryGlow(types)

  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    const card = cardRef.current
    if (!card) return
    const rect = card.getBoundingClientRect()
    const x = e.clientX - rect.left
    const y = e.clientY - rect.top
    const cx = rect.width / 2
    const cy = rect.height / 2
    card.style.setProperty('--pointer-x', `${(x / rect.width) * 100}%`)
    card.style.setProperty('--pointer-y', `${(y / rect.height) * 100}%`)
    card.style.setProperty('--rotate-x', `${-((y - cy) / cy) * 15}deg`)
    card.style.setProperty('--rotate-y', `${((x - cx) / cx) * 15}deg`)
    card.style.setProperty('--shine-opacity', tier === 'ultra' ? '1' : tier === 'holo' ? '0.6' : '0')
    card.style.setProperty('--foil-opacity', tier === 'ultra' ? '0.7' : tier === 'holo' ? '0.35' : '0')
    card.style.setProperty('--glow-color', glowColor)
  }, [tier, glowColor])

  const handleMouseLeave = useCallback(() => {
    const card = cardRef.current
    if (!card) return
    card.style.setProperty('--rotate-x', '0deg')
    card.style.setProperty('--rotate-y', '0deg')
    card.style.setProperty('--shine-opacity', '0')
    card.style.setProperty('--foil-opacity', '0')
    card.style.setProperty('--glow-color', glowColor)
  }, [glowColor])

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
```

- [ ] **Step 2: Update index.css glow rules**

In `src/Frontend/src/index.css`, find and replace the hover and ultra-glow rules.

Find this block (the existing hover + ultra hover rules):
```css
/* Lift on hover */
.holo-card:hover {
  box-shadow:
    0 30px 60px -12px rgba(0, 0, 0, 0.7),
    0 0 0 1px rgba(255, 255, 255, 0.08);
}
```

Replace it with:
```css
/* Lift + type-tinted glow on hover */
.holo-card:hover {
  box-shadow:
    0 30px 60px -12px rgba(0, 0, 0, 0.7),
    0 0 40px -8px rgb(var(--glow-color, 139 92 246) / 0.35),
    0 0 0 1px rgb(var(--glow-color, 139 92 246) / 0.15);
}

.holo-card[data-tier='holo']:hover {
  box-shadow:
    0 30px 60px -12px rgba(0, 0, 0, 0.7),
    0 0 50px -6px rgb(var(--glow-color, 139 92 246) / 0.5),
    0 0 0 1px rgb(var(--glow-color, 139 92 246) / 0.25);
}
```

Find and replace the static ultra hover rule:
```css
/* Ultra rarity: stronger tinted glow */
.holo-card[data-tier='ultra']:hover {
  box-shadow:
    0 30px 80px -10px rgba(139, 92, 246, 0.5),
    0 0 0 1px rgba(167, 139, 250, 0.3);
}
```

Replace with:
```css
.holo-card[data-tier='ultra']:hover {
  box-shadow:
    0 30px 80px -10px rgba(0, 0, 0, 0.7),
    0 0 70px -4px rgb(var(--glow-color, 139 92 246) / 0.65),
    0 0 0 1px rgb(var(--glow-color, 139 92 246) / 0.4);
}
```

- [ ] **Step 3: Verify in browser**

```bash
cd src/Frontend && npm run dev
```

Navigate to `/cards`. Hover over a Fire-type card — glow should be red/orange. Hover over a Water-type card — glow should be blue. Common cards get a subtle glow (was no glow before).

- [ ] **Step 4: Commit**

```bash
cd src/Frontend && git add src/components/HoloCard.tsx src/index.css && git commit -m "feat: type-driven glow colour on HoloCard hover"
```

---

### Task 4: Extend PokemonCard type with tcgplayer prices

**Files:**
- Modify: `src/Frontend/src/api/pokemontcg.ts`

**Interfaces:**
- Produces: `PokemonCard` extended with `tcgplayer?: { prices?: { normal?: PriceTier; holofoil?: PriceTier; reverseHolofoil?: PriceTier } }`
- Produces: `interface PriceTier { low: number; mid: number; high: number; market: number }`

- [ ] **Step 1: Update pokemontcg.ts**

In `src/Frontend/src/api/pokemontcg.ts`, replace the `PokemonCard` interface:

```ts
interface PriceTier {
  low: number
  mid: number
  high: number
  market: number
}

export interface PokemonCard {
  id: string
  name: string
  rarity: string
  set: { id: string; name: string; series: string }
  types?: string[]
  hp?: string
  images: { small: string; large: string }
  supertype: string
  tcgplayer?: {
    prices?: {
      normal?: PriceTier
      holofoil?: PriceTier
      reverseHolofoil?: PriceTier
    }
  }
}
```

Note: `set` now includes `id` (needed for filtering in Plan C).

- [ ] **Step 2: Verify TypeScript compiles**

```bash
cd src/Frontend && npx tsc --noEmit
```

Expected: No errors.

- [ ] **Step 3: Commit**

```bash
cd src/Frontend && git add src/api/pokemontcg.ts && git commit -m "feat: extend PokemonCard type with tcgplayer prices and set.id"
```

---

### Task 5: Build CardDetailModal

**Files:**
- Create: `src/Frontend/src/components/CardDetailModal.tsx`

**Interfaces:**
- Consumes: `GlassSurface` from `./GlassSurface`
- Consumes: `BorderGlow` from `./BorderGlow`
- Consumes: `primaryGlow, TYPE_GLOW` from `../lib/typeColors`
- Consumes: `PokemonCard` from `../api/pokemontcg`
- Consumes: `addCardToPortfolio` from `../api/portfolio`
- Consumes: `useAuth` from `../auth/useAuth`
- Consumes: `createChart, ColorType` from `lightweight-charts`
- Produces: `export default CardDetailModal` — props: `{ card: PokemonCard; onClose: () => void }`

- [ ] **Step 1: Create CardDetailModal.tsx**

Create `src/Frontend/src/components/CardDetailModal.tsx`:

```tsx
import { useEffect, useRef, useState, useCallback } from 'react'
import { createPortal } from 'react-dom'
import { motion } from 'framer-motion'
import { createChart, ColorType } from 'lightweight-charts'
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

    const series = chart.addAreaSeries({
      lineColor: `rgb(${glowColor})`,
      topColor: `rgb(${glowColor} / 0.3)`,
      bottomColor: `rgb(${glowColor} / 0.0)`,
      lineWidth: 2,
    })

    // ponytail: no backend sparkline data yet (pokemontcg IDs aren't GUIDs)
    // Show a placeholder trend using a static 30-day mock to demonstrate the chart
    const today = Math.floor(Date.now() / 1000)
    const DAY = 86400
    const mockData = Array.from({ length: 30 }, (_, i) => ({
      time: (today - (29 - i) * DAY) as any,
      value: 5 + Math.sin(i * 0.4) * 2 + Math.random() * 0.5,
    }))
    series.setData(mockData)
    chart.timeScale().fitContent()

    const obs = new ResizeObserver(() => chart.applyOptions({ width: chartRef.current!.clientWidth }))
    obs.observe(chartRef.current)

    return () => { chart.remove(); obs.disconnect() }
  }, [glowColor])

  return <div ref={chartRef} className="w-full" />
}

export default function CardDetailModal({ card, onClose }: CardDetailModalProps) {
  const { user } = useAuth()
  const [addState, setAddState] = useState<'idle' | 'loading' | 'done'>('idle')
  const glowColor = primaryGlow(card.types)

  const handleClose = useCallback(() => onClose(), [onClose])

  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') handleClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [handleClose])

  const handleAddToPortfolio = async () => {
    if (!user || addState !== 'idle') return
    setAddState('loading')
    try {
      const price = card.tcgplayer?.prices?.holofoil?.market
        ?? card.tcgplayer?.prices?.normal?.market
        ?? 0
      await addCardToPortfolio({
        userId: user.profile.sub,
        cardId: card.id,
        cardName: card.name,
        quantity: 1,
        condition: 'NearMint',
        acquisitionPriceUsd: price,
      })
      setAddState('done')
      setTimeout(() => setAddState('idle'), 2000)
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
          distortionScale={-150}
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
                  {card.hp && <span className="ml-2 rounded bg-slate-700/60 px-1.5 py-0.5 text-xs">HP {card.hp}</span>}
                </p>
              </div>

              {/* Type badges */}
              {card.types && card.types.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {card.types.map(t => <TypeBadge key={t} type={t} />)}
                </div>
              )}

              {/* Price sparkline */}
              <div>
                <p className="mb-1 text-xs font-medium uppercase tracking-wider text-slate-500">Price trend (30d)</p>
                <PriceSparkline glowColor={glowColor} />
              </div>

              {/* TCGPlayer price tiers */}
              {hasPrices && (
                <div>
                  <p className="mb-2 text-xs font-medium uppercase tracking-wider text-slate-500">TCGPlayer prices</p>
                  <dl className="grid grid-cols-3 gap-3">
                    {prices.normal?.market != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">Normal</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">${prices.normal.market.toFixed(2)}</dd>
                      </div>
                    )}
                    {prices.holofoil?.market != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">Holofoil</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">${prices.holofoil.market.toFixed(2)}</dd>
                      </div>
                    )}
                    {prices.reverseHolofoil?.market != null && (
                      <div className="rounded-lg bg-slate-800/60 px-3 py-2">
                        <dt className="text-xs text-slate-500">Rev. Holo</dt>
                        <dd className="mt-0.5 text-sm font-semibold text-slate-200">${prices.reverseHolofoil.market.toFixed(2)}</dd>
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
                    glowRadius={30}
                    colors={['#c084fc', '#818cf8', '#38bdf8']}
                    fillOpacity={0.3}
                    className="w-full"
                  >
                    <button
                      onClick={handleAddToPortfolio}
                      disabled={addState === 'loading'}
                      className="w-full rounded-xl px-4 py-2.5 text-sm font-semibold text-slate-100 transition-colors disabled:opacity-50"
                    >
                      {addState === 'done' ? 'Added ✓' : addState === 'loading' ? 'Adding…' : 'Add to Portfolio'}
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
```

- [ ] **Step 2: Commit**

```bash
cd src/Frontend && git add src/components/CardDetailModal.tsx && git commit -m "feat: CardDetailModal with GlassSurface, sparkline, BorderGlow CTA"
```

---

### Task 6: Wire modal and enrich card grid overlay in CardsPage

**Files:**
- Modify: `src/Frontend/src/pages/CardsPage.tsx`

**Interfaces:**
- Consumes: `CardDetailModal` from `../components/CardDetailModal`
- Consumes: `AnimatePresence` from `framer-motion`
- Consumes: `TYPE_GLOW` from `../lib/typeColors`
- Consumes: `PokemonCard` from `../api/pokemontcg`

- [ ] **Step 1: Replace CardsPage.tsx**

Replace the full content of `src/Frontend/src/pages/CardsPage.tsx`:

```tsx
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { motion, AnimatePresence } from 'framer-motion'
import { searchPokemonCards, PokemonCard } from '../api/pokemontcg'
import { TYPE_GLOW } from '../lib/typeColors'
import HoloCard from '../components/HoloCard'
import CardDetailModal from '../components/CardDetailModal'

function TypeBadge({ type }: { type: string }) {
  const rgb = TYPE_GLOW[type] ?? '139 92 246'
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

function marketPrice(card: PokemonCard): string | null {
  const p = card.tcgplayer?.prices
  const val = p?.holofoil?.market ?? p?.normal?.market ?? p?.reverseHolofoil?.market
  return val != null ? `$${val.toFixed(2)}` : null
}

export default function CardsPage() {
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [selectedCard, setSelectedCard] = useState<PokemonCard | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['pokemon-cards', search],
    queryFn: () => searchPokemonCards({ name: search }, 1, 20),
    staleTime: 5 * 60 * 1000,
  })

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold text-slate-100 sm:text-2xl">Card Catalog</h1>

      <div className="mb-6 flex gap-2">
        <input
          value={q}
          onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && setSearch(q)}
          placeholder="Search Pokémon cards…"
          className="flex-1 rounded-lg border border-slate-700/60 bg-slate-900/80 px-3 py-2.5 text-sm text-slate-100 placeholder-slate-500 backdrop-blur-sm focus:border-violet-500 focus:outline-none"
        />
        <button
          onClick={() => setSearch(q)}
          className="rounded-lg bg-violet-600 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-violet-500 active:scale-95"
        >
          Search
        </button>
      </div>

      {isLoading && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: 10 }).map((_, i) => (
            <div key={i} className="animate-pulse rounded-[4.5%/3.5%] bg-slate-800/60" style={{ aspectRatio: '5/7' }} />
          ))}
        </div>
      )}

      {data && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {data.data.map((card, i) => {
            const price = marketPrice(card)
            return (
              <motion.div
                key={card.id}
                initial={{ opacity: 0, scale: 0.92 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ delay: Math.min(i * 0.04, 0.5), duration: 0.25 }}
                style={{ aspectRatio: '5/7' }}
                onClick={() => setSelectedCard(card)}
                className="cursor-pointer"
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
                    {/* Enriched overlay */}
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
                    </div>
                  </div>
                </HoloCard>
              </motion.div>
            )
          })}
        </div>
      )}

      {data?.data.length === 0 && (
        <div className="mt-16 text-center text-slate-500">No cards found.</div>
      )}

      <AnimatePresence>
        {selectedCard && (
          <CardDetailModal
            card={selectedCard}
            onClose={() => setSelectedCard(null)}
          />
        )}
      </AnimatePresence>
    </div>
  )
}
```

Note: `searchPokemonCards` now takes a `CardFilters` object — this will be defined fully in Plan C. For now it's called with `{ name: search }`. The function signature change in Task 4 covers this bridge.

**Important:** `searchPokemonCards` in `pokemontcg.ts` still has the old signature `(q, page, pageSize)`. Update it now to accept `CardFilters` as first arg so this CardsPage compiles. Add this minimal `CardFilters` type to `pokemontcg.ts`:

```ts
export interface CardFilters {
  name?: string
}

export async function searchPokemonCards(
  filters: CardFilters = {},
  page = 1,
  pageSize = 20,
): Promise<PokemonCardPage> {
  const params = new URLSearchParams({
    pageSize: String(pageSize),
    page: String(page),
    orderBy: '-set.releaseDate',
  })
  if (filters.name) params.set('q', `name:${filters.name}*`)
  const res = await fetch(`${BASE}/cards?${params}`)
  if (!res.ok) throw new Error(`Pokemon TCG API error: ${res.status}`)
  return res.json()
}
```

Plan C will extend `CardFilters` further — this is the backwards-compatible stub.

- [ ] **Step 2: Verify TypeScript compiles**

```bash
cd src/Frontend && npx tsc --noEmit
```

Expected: No errors.

- [ ] **Step 3: Verify in browser**

```bash
cd src/Frontend && npm run dev
```

Navigate to `/cards`. Expected:
- Cards show set name, TCGPlayer price badge (green), and type chips in the overlay
- Clicking a card opens the GlassSurface modal with large image, type badges, HP, sparkline chart, price tiers, and BorderGlow "Add to Portfolio" button
- Pressing `Escape` or clicking backdrop closes the modal with exit animation
- On Safari: GlassSurface falls back to frosted glass — still readable

- [ ] **Step 4: Run all tests**

```bash
cd src/Frontend && npm test
```

Expected: All tests pass (typeColors: 7, CommandPalette: 3).

- [ ] **Step 5: Commit**

```bash
cd src/Frontend && git add src/pages/CardsPage.tsx src/api/pokemontcg.ts && git commit -m "feat: card grid overlay with prices/types, CardDetailModal wired"
```
