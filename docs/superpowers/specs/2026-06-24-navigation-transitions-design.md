# Navigation & Transitions — Design Spec
**Date:** 2026-06-24  
**Status:** Approved

## Overview

Rework the navbar to a floating, liquid-glass panel using the React Bits GlassSurface component. Add Glimm WebGL sweep transitions between routes. Add a global Cmd+K command palette using the `cmdk` package.

## Architecture

### New files
- `src/components/GlassSurface.tsx` — React Bits GlassSurface, copied verbatim from source
- `src/components/GlassSurface.css` — accompanying CSS, copied verbatim from source
- `src/components/CommandPalette.tsx` — Cmd+K overlay using `cmdk`

### Modified files
- `src/components/Navbar.tsx` — floating layout, GlassSurface wrapper, Glimm nav links
- `src/main.tsx` — add `<GlimmProvider>`
- `src/App.tsx` — add `<CommandPalette>`, global `⌘K`/`Ctrl+K` keydown listener

### New dependencies
- `glimm` — WebGL sweep page transitions
- `cmdk` — headless command palette primitives

## Navbar

### Layout
- `position: fixed; top: 0; left: 0; right: 0; z-index: 50`
- Outer wrapper: `pt-3 px-4` so the glass panel floats with space above/beside it
- GlassSurface wraps the inner nav bar: `width="100%" borderRadius={16} blur={14} brightness={18} opacity={0.92}`
- Inner content: `h-14 flex items-center gap-3 px-4`
- `main` in App.tsx gets `pt-20` to clear the fixed nav

### Links
Replace `<Link>` with a `<NavLink>` component local to Navbar.tsx:

```tsx
function NavLink({ to, children }: { to: string; children: ReactNode }) {
  const { sweep } = useGlimm()
  const navigate = useNavigate()
  const location = useLocation()
  const active = location.pathname.startsWith(to)
  return (
    <button
      onClick={() => sweep(() => navigate(to))}
      className={active ? 'active-styles' : 'inactive-styles'}
    >
      {children}
    </button>
  )
}
```

Active/inactive styles unchanged from current Tailwind classes. No `<a>` tag — the sweep handles navigation.

### Right-side elements
NotificationBell, user email, logout/signin button — no structural changes.

## Glimm

### Provider placement (`main.tsx`)
```tsx
<BrowserRouter>
  <GlimmProvider palette="prism" sweepMs={900} outroMs={600}>
    <App />
  </GlimmProvider>
</BrowserRouter>
```

`palette="prism"` — multicolour, suits a TCG game aesthetic. `sweepMs` shortened slightly from default 1100ms for snappier feel.

### Route transitions
All nav links go through `sweep(() => navigate(to))`. Direct URL changes (back button, deep links) degrade gracefully — Glimm does nothing, React Router handles normally. `prefers-reduced-motion` is respected by Glimm natively.

## Command Palette

### Trigger
- Global `keydown` listener in App.tsx: `⌘K` (Mac) / `Ctrl+K` (Win/Linux)
- `open` state in App.tsx, passed to `<CommandPalette open={open} onClose={() => setOpen(false)} />`

### Commands (static for sub-project A, extended in sub-project C)
| Group | Command | Action |
|---|---|---|
| Navigate | Go to Cards | `sweep(() => navigate('/cards'))` |
| Navigate | Go to Portfolio | `sweep(() => navigate('/portfolio'))` |
| Navigate | Go to Marketplace | `sweep(() => navigate('/marketplace'))` |
| Search | Search cards | Close palette, focus search input on `/cards` |

### Styling
- `<Command.Dialog>` rendered as a React Portal
- Backdrop: `fixed inset-0 bg-slate-950/60 backdrop-blur-sm z-50`
- Panel: `fixed top-1/3 left-1/2 -translate-x-1/2 w-full max-w-lg`
- Dark glass panel: `bg-slate-900/95 border border-slate-700/50 rounded-2xl shadow-2xl`
- Input: borderless, `text-slate-100 placeholder-slate-500`
- Items: `flex items-center gap-3 px-4 py-2.5 text-sm text-slate-300 rounded-lg`
- Selected item: `bg-violet-500/20 text-violet-200`

## Error Handling & Edge Cases
- Glimm WebGL unavailable (old browser): degrades to instant navigation, no error
- GlassSurface on Safari/Firefox: component's built-in `--fallback` CSS path (backdrop-filter blur only, no SVG displacement) — still looks good
- cmdk: pure DOM, no special considerations

## Testing
```tsx
// Verify sweep fires on nav click (smoke test)
// render Navbar, click "Cards" link, assert navigate was called
// assert GlimmProvider sweep was called with navigate callback
```
Unit test is minimal — the visual effect can only be verified in-browser.
