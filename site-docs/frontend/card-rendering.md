# Card Rendering

Cards in the frontend have three layered visual effects: a 3D tilt on mouse hover, a rarity-driven holographic foil overlay, and a type-driven glow colour on the card border.

## HoloCard component

`HoloCard` is a wrapper component that applies all three effects to any children:

```typescript
<HoloCard rarity="Rare Holo VMAX" types={["Fire"]}>
  <img src={card.images.large} alt={card.name} />
</HoloCard>
```

### Tilt effect

`onMouseMove` calculates the cursor's position relative to the card centre and sets CSS custom properties:

```css
--rotate-x: -12deg;  /* tilt up/down */
--rotate-y:  8deg;   /* tilt left/right */
--pointer-x: 60%;   /* cursor X for shine gradient */
--pointer-y: 40%;   /* cursor Y for shine gradient */
```

The card element uses `transform: perspective(600px) rotateX(var(--rotate-x)) rotateY(var(--rotate-y))` — pure CSS 3D, no Three.js.

### Holographic tiers

Rarity drives which visual tier is applied:

| Tier | Rarities | Shine opacity | Foil opacity |
|---|---|---|---|
| `none` | Common, Uncommon | 0 | 0 |
| `holo` | Rare, Rare Holo, Illustration Rare | 0.6 | 0.35 |
| `ultra` | VMAX, VSTAR, Ultra Rare, Special Illustration Rare | 1.0 | 0.7 |

The foil overlay is a `background: conic-gradient(...)` that shifts as `--pointer-x/y` change, simulating the rainbow iridescence of real holo cards.

### Type glow

`typeColors.ts` maps Pokémon card type names to CSS colour values:

```typescript
export const TYPE_GLOW: Record<string, string> = {
  Fire:       '#f97316',
  Water:      '#38bdf8',
  Grass:      '#4ade80',
  Electric:   '#facc15',
  Psychic:    '#e879f9',
  Fighting:   '#fb923c',
  Darkness:   '#a78bfa',
  Metal:      '#94a3b8',
  Dragon:     '#6366f1',
  Colorless:  '#cbd5e1',
}

export function primaryGlow(types?: string[]): string {
  return types?.[0] ? (TYPE_GLOW[types[0]] ?? '#6366f1') : '#6366f1'
}
```

The glow colour is set as `--glow-color` on the card element. CSS rules apply it as a `box-shadow` on hover:

```css
.holo-card:hover {
  box-shadow: 0 0 24px 4px var(--glow-color, #6366f1);
}
```

For ultra-tier cards, the glow is more intense and persists slightly after mouse leave via a CSS transition.

## CardDetailModal

Clicking a card opens a full-screen React Portal (`createPortal`) with a GlassSurface panel containing:

- Large card image
- Card name, set, rarity, type chips
- **Price sparkline** — an area chart showing price history from the Pricing Service
- **Add to Portfolio** CTA wrapped in `BorderGlow`

The modal closes on backdrop click or the Escape key.

### Price sparkline

The price chart uses [Recharts](https://recharts.org/) `AreaChart` with data fetched from `GET /api/prices/{cardId}/history`:

```typescript
<AreaChart data={priceHistory} width={400} height={120}>
  <Area type="monotone" dataKey="close" stroke="#a78bfa" fill="#a78bfa33" />
  <XAxis dataKey="bucket" hide />
  <Tooltip formatter={(v) => `$${v}`} />
</AreaChart>
```

Recharts is MIT-licensed, pure React, and has no watermarks.

## DitherBackground

The full-screen background behind all pages is an animated dither pattern rendered on a `<canvas>` element. `DitherBackground.tsx` runs a `requestAnimationFrame` loop that applies a time-varying ordered dither to a procedurally generated noise field, producing the subtle animated texture visible behind the card grid.

This is a pure CPU canvas operation — no WebGL or Three.js dependency.
