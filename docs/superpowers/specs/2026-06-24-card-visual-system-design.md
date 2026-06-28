# Card Visual System — Design Spec
**Date:** 2026-06-24  
**Status:** Approved

## Overview

Three layered improvements to the card visual system:
1. Type-driven glow colours on hover (replaces static violet ultra glow)
2. Richer card grid overlay (price, set, type badges)
3. Card detail modal with GlassSurface, lightweight-charts sparkline, and BorderGlow CTA

## Architecture

### New files
- `src/components/CardDetailModal.tsx` — portal modal, GlassSurface panel
- `src/components/BorderGlow.tsx` — React Bits BorderGlow, copied verbatim
- `src/components/BorderGlow.css` — accompanying CSS
- `src/lib/typeColors.ts` — Pokémon type → colour mapping

### Modified files
- `src/components/HoloCard.tsx` — accept `types?: string[]`, drive `--glow-color` CSS var
- `src/index.css` — replace static ultra glow with `var(--glow-color, ...)`
- `src/pages/CardsPage.tsx` — card click opens modal, pass TCGPlayer prices to overlay
- `src/api/pokemontcg.ts` — extend `PokemonCard` type with `tcgplayer` field

### New dependencies
- `lightweight-charts` — TradingView canvas charts for price sparkline

## Type Colour Mapping (`src/lib/typeColors.ts`)

```ts
export const TYPE_GLOW: Record<string, string> = {
  Fire:       '239 68 68',   // red-500
  Water:      '59 130 246',  // blue-500
  Grass:      '34 197 94',   // green-500
  Lightning:  '234 179 8',   // yellow-500
  Psychic:    '168 85 247',  // purple-500
  Fighting:   '249 115 22',  // orange-500
  Darkness:   '100 116 139', // slate-500
  Metal:      '148 163 184', // slate-400
  Dragon:     '99 102 241',  // indigo-500
  Fairy:      '236 72 153',  // pink-500
  Colorless:  '203 213 225', // slate-300
}

export function primaryGlow(types?: string[]): string {
  if (!types?.length) return '139 92 246' // fallback violet
  return TYPE_GLOW[types[0]] ?? '139 92 246'
}
```

## HoloCard (`src/components/HoloCard.tsx`)

### Prop change
```ts
interface HoloCardProps {
  children: React.ReactNode
  rarity?: string
  types?: string[]      // ← new
  className?: string
}
```

### Behaviour
On `mousemove`, set `--glow-color` from `primaryGlow(types)` in addition to existing CSS vars:
```ts
card.style.setProperty('--glow-color', primaryGlow(types))
```

On `mouseleave`, clear it:
```ts
card.style.setProperty('--glow-color', primaryGlow(types)) // keep colour, just dim via opacity
```

### CSS changes (`src/index.css`)
Replace the static ultra glow:
```css
/* Before */
.holo-card[data-tier='ultra']:hover {
  box-shadow:
    0 30px 80px -10px rgba(139, 92, 246, 0.5),
    0 0 0 1px rgba(167, 139, 250, 0.3);
}

/* After */
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

.holo-card[data-tier='ultra']:hover {
  box-shadow:
    0 30px 80px -10px rgba(0, 0, 0, 0.7),
    0 0 70px -4px rgb(var(--glow-color, 139 92 246) / 0.65),
    0 0 0 1px rgb(var(--glow-color, 139 92 246) / 0.4);
}
```

Common/Uncommon cards (`data-tier='none'`) get the dimmer 0.35 glow — previously no glow at all.

## Card Grid Overlay

In `CardsPage.tsx`, extend the existing bottom label to include:
- Price badge: `card.tcgplayer?.prices?.holofoil?.market ?? card.tcgplayer?.prices?.normal?.market` — displayed as `$4.99`. Falls back to `—` if no pricing data.
- Set name: already in `card.set.name`, add truncated below rarity
- Type badges: coloured `<span>` pills (background uses `TYPE_GLOW` colours at 20% opacity, border at 40% opacity)

The overlay remains a `bg-gradient-to-t from-black/80` — no structural change.

`PokemonCard` type extended:
```ts
tcgplayer?: {
  prices?: {
    normal?: { low: number; mid: number; high: number; market: number }
    holofoil?: { low: number; mid: number; high: number; market: number }
    reverseHolofoil?: { low: number; mid: number; high: number; market: number }
  }
}
```

`searchPokemonCards` adds `select=*` or no change needed — pokemontcg.io v2 returns tcgplayer prices by default in the response.

## Card Detail Modal (`src/components/CardDetailModal.tsx`)

### Trigger
Cards in the grid get an `onClick` → sets `selectedCard` state in `CardsPage.tsx` → `<CardDetailModal card={selectedCard} onClose={...} />` renders.

### Portal structure
```tsx
createPortal(
  <div className="fixed inset-0 z-40 flex items-center justify-center p-4">
    {/* Backdrop */}
    <div className="absolute inset-0 bg-slate-950/70 backdrop-blur-sm" onClick={onClose} />
    {/* Panel */}
    <motion.div initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }} exit={{ ...}}>
      <GlassSurface width="100%" height="auto" borderRadius={24} blur={16} brightness={15} opacity={0.95}>
        {/* content */}
      </GlassSurface>
    </motion.div>
  </div>,
  document.body
)
```

`<AnimatePresence>` in CardsPage.tsx wraps the conditional modal render for exit animations.

### Panel layout
Two-column on desktop, stacked on mobile (`flex flex-col sm:flex-row gap-6 p-6`):

**Left column** — card image: `<img src={card.images.large} />` at natural aspect ratio, `max-h-[480px]`.

**Right column:**
- Card name: `text-2xl font-bold text-slate-100`
- Type badges: same pills as grid overlay, slightly larger
- Rarity + Set: `text-sm text-slate-400`
- HP: if present, `HP {card.hp}` badge

**Price sparkline:**
- lightweight-charts `createChart` in a `useEffect`, `addAreaSeries`
- Endpoint: `GET /api/prices/{cardId}/history?granularity=day&from={ISO}&to={ISO}` — route uses `{cardId:guid}` constraint
- pokemontcg.io card IDs are strings like `base1-1`, not GUIDs — the backend route constraint will reject them. The frontend skips the backend call entirely and shows TCGPlayer current price tiers instead (see below). A future backend task to map pokemontcg.io IDs to internal GUIDs is out of scope here.
- When backend data is available (GUID card IDs in the seeded dataset): `from = 30 days ago as ISO string`, `to = now as ISO string`
- Chart sizing: `100% × 120px`, dark theme (`background: 'transparent'`, `textColor: '#94a3b8'`, `lineColor: rgb(var(--glow-color))`)

**TCGPlayer price tiers:**
```
Normal       $4.99
Holofoil     $12.50
Rev. Holo    $3.20
```
Displayed as a simple `<dl>` grid.

**Add to Portfolio button:**
```tsx
<BorderGlow
  glowColor="139 92 246"
  backgroundColor="transparent"
  borderRadius={12}
  colors={['#c084fc', '#818cf8', '#38bdf8']}
>
  <button onClick={handleAddToPortfolio}>Add to Portfolio</button>
</BorderGlow>
```
`handleAddToPortfolio` calls existing `portfolio.ts` API. Shows a brief success state ("Added ✓") then resets.

### Close
Backdrop click or Escape key closes the modal.

## Error Handling & Edge Cases
- No TCGPlayer pricing: show `—` in grid, omit price section in modal
- No Pokémon types (Trainer/Energy cards): `primaryGlow([])` returns fallback violet; no type badges shown
- Backend price history unavailable: chart section is hidden, no error shown to user
- GlassSurface on Safari: fallback CSS path renders a clean frosted panel — still usable
- Image load failure: `<img>` `onError` swaps to a slate placeholder div matching the aspect ratio

## Testing
```tsx
// HoloCard: given types=['Fire'], after mousemove, --glow-color should be '239 68 68'
// CardDetailModal: renders with card prop, Add to Portfolio calls addToCollection
// primaryGlow: returns fallback for empty/undefined types
```
