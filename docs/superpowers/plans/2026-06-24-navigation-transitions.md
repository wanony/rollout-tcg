# Navigation & Transitions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the sticky navbar with a floating GlassSurface panel, add Glimm WebGL sweep transitions between routes, and add a global Cmd+K command palette.

**Architecture:** GlassSurface (React Bits, copied from source) wraps the navbar content; it sits `position: fixed` with top/side margin so it floats. Glimm wraps the BrowserRouter in `main.tsx`; nav link clicks call `sweep(() => navigate(to))` from `useGlimm()`. The command palette uses `cmdk` rendered into a React Portal; open state lives in `App.tsx` and a `PageCommandsContext` allows any page to register contextual commands.

**Tech Stack:** React 18, TypeScript, Tailwind CSS, `glimm` (WebGL sweep), `cmdk` (command palette), Vitest (unit tests)

## Global Constraints

- All frontend code lives in `src/Frontend/src/`
- Tailwind dark-first; no light-mode-specific additions
- GlassSurface and BorderGlow are copy-in components (no npm package); copy verbatim from React Bits source
- Run all commands from `src/Frontend/`
- `glimm` React adapter: `import { GlimmProvider, useGlimm } from 'glimm/react'`
- `cmdk` import: `import { Command } from 'cmdk'`

---

### Task 1: Install dependencies and set up Vitest

**Files:**
- Modify: `src/Frontend/package.json`
- Modify: `src/Frontend/vite.config.ts`
- Create: `src/Frontend/src/test-setup.ts`

**Interfaces:**
- Produces: `npm test` command runs Vitest; `glimm` and `cmdk` importable in source

- [ ] **Step 1: Install runtime dependencies**

```bash
cd src/Frontend && npm install glimm cmdk
```

Expected: packages added to `node_modules`; no peer dependency errors.

- [ ] **Step 2: Install Vitest dev dependencies**

```bash
cd src/Frontend && npm install -D vitest @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom
```

- [ ] **Step 3: Add test script and vitest config to vite.config.ts**

Current `vite.config.ts`:
```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
})
```

Replace with:
```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
  },
})
```

- [ ] **Step 4: Add test setup file**

Create `src/Frontend/src/test-setup.ts`:
```ts
import '@testing-library/jest-dom'
```

- [ ] **Step 5: Add test script to package.json**

In the `"scripts"` section of `src/Frontend/package.json`, add:
```json
"test": "vitest run"
```

- [ ] **Step 6: Verify vitest works**

```bash
cd src/Frontend && npm test
```

Expected: `No test files found, exiting with code 0` (or similar — no failures).

- [ ] **Step 7: Commit**

```bash
cd src/Frontend && git add package.json package-lock.json vite.config.ts src/test-setup.ts && git commit -m "chore: add glimm, cmdk, vitest to frontend"
```

---

### Task 2: Add GlassSurface component

**Files:**
- Create: `src/Frontend/src/components/GlassSurface.tsx`
- Create: `src/Frontend/src/components/GlassSurface.css`

**Interfaces:**
- Produces: `export default GlassSurface` — used by Navbar (Task 4) and CardDetailModal (Plan B)
- Produces props: `{ children, width?, height?, borderRadius?, blur?, brightness?, opacity?, distortionScale?, redOffset?, greenOffset?, blueOffset?, backgroundOpacity?, saturation?, xChannel?, yChannel?, mixBlendMode?, className?, style? }`

- [ ] **Step 1: Create GlassSurface.css**

Create `src/Frontend/src/components/GlassSurface.css` with the full content:

```css
.glass-surface {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  transition: opacity 0.26s ease-out;
}

.glass-surface__filter {
  width: 100%;
  height: 100%;
  pointer-events: none;
  position: absolute;
  inset: 0;
  opacity: 0;
  z-index: -1;
}

.glass-surface__content {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0.5rem;
  border-radius: inherit;
  position: relative;
  z-index: 1;
}

.glass-surface--svg {
  background: light-dark(hsl(0 0% 100% / var(--glass-frost, 0)), hsl(0 0% 0% / var(--glass-frost, 0)));
  backdrop-filter: var(--filter-id, url(#glass-filter)) saturate(var(--glass-saturation, 1));
  box-shadow:
    0 0 2px 1px light-dark(color-mix(in oklch, black, transparent 85%), color-mix(in oklch, white, transparent 65%))
      inset,
    0 0 10px 4px light-dark(color-mix(in oklch, black, transparent 90%), color-mix(in oklch, white, transparent 85%))
      inset,
    0px 4px 16px rgba(17, 17, 26, 0.05),
    0px 8px 24px rgba(17, 17, 26, 0.05),
    0px 16px 56px rgba(17, 17, 26, 0.05),
    0px 4px 16px rgba(17, 17, 26, 0.05) inset,
    0px 8px 24px rgba(17, 17, 26, 0.05) inset,
    0px 16px 56px rgba(17, 17, 26, 0.05) inset;
}

.glass-surface--fallback {
  background: rgba(255, 255, 255, 0.25);
  backdrop-filter: blur(12px) saturate(1.8) brightness(1.1);
  -webkit-backdrop-filter: blur(12px) saturate(1.8) brightness(1.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  box-shadow:
    0 8px 32px 0 rgba(31, 38, 135, 0.2),
    0 2px 16px 0 rgba(31, 38, 135, 0.1),
    inset 0 1px 0 0 rgba(255, 255, 255, 0.4),
    inset 0 -1px 0 0 rgba(255, 255, 255, 0.2);
}

@media (prefers-color-scheme: dark) {
  .glass-surface--fallback {
    background: rgba(255, 255, 255, 0.1);
    backdrop-filter: blur(12px) saturate(1.8) brightness(1.2);
    -webkit-backdrop-filter: blur(12px) saturate(1.8) brightness(1.2);
    border: 1px solid rgba(255, 255, 255, 0.2);
    box-shadow:
      inset 0 1px 0 0 rgba(255, 255, 255, 0.2),
      inset 0 -1px 0 0 rgba(255, 255, 255, 0.1);
  }
}

@supports not (backdrop-filter: blur(10px)) {
  .glass-surface--fallback {
    background: rgba(255, 255, 255, 0.4);
    box-shadow:
      inset 0 1px 0 0 rgba(255, 255, 255, 0.5),
      inset 0 -1px 0 0 rgba(255, 255, 255, 0.3);
  }

  .glass-surface--fallback::before {
    content: '';
    position: absolute;
    inset: 0;
    background: rgba(255, 255, 255, 0.15);
    border-radius: inherit;
    z-index: -1;
  }
}

@supports not (backdrop-filter: blur(10px)) {
  @media (prefers-color-scheme: dark) {
    .glass-surface--fallback {
      background: rgba(0, 0, 0, 0.4);
    }

    .glass-surface--fallback::before {
      background: rgba(255, 255, 255, 0.05);
    }
  }
}

.glass-surface:focus-visible {
  outline: 2px solid light-dark(#007aff, #0a84ff);
  outline-offset: 2px;
}
```

- [ ] **Step 2: Create GlassSurface.tsx**

Create `src/Frontend/src/components/GlassSurface.tsx`:

```tsx
import { useEffect, useState, useRef, useId, CSSProperties, ReactNode } from 'react'
import './GlassSurface.css'

interface GlassSurfaceProps {
  children?: ReactNode
  width?: number | string
  height?: number | string
  borderRadius?: number
  borderWidth?: number
  brightness?: number
  opacity?: number
  blur?: number
  displace?: number
  backgroundOpacity?: number
  saturation?: number
  distortionScale?: number
  redOffset?: number
  greenOffset?: number
  blueOffset?: number
  xChannel?: string
  yChannel?: string
  mixBlendMode?: string
  className?: string
  style?: CSSProperties
}

export default function GlassSurface({
  children,
  width = 200,
  height = 80,
  borderRadius = 20,
  borderWidth = 0.07,
  brightness = 50,
  opacity = 0.93,
  blur = 11,
  displace = 0,
  backgroundOpacity = 0,
  saturation = 1,
  distortionScale = -180,
  redOffset = 0,
  greenOffset = 10,
  blueOffset = 20,
  xChannel = 'R',
  yChannel = 'G',
  mixBlendMode = 'difference',
  className = '',
  style = {},
}: GlassSurfaceProps) {
  const uniqueId = useId().replace(/:/g, '-')
  const filterId = `glass-filter-${uniqueId}`
  const redGradId = `red-grad-${uniqueId}`
  const blueGradId = `blue-grad-${uniqueId}`

  const [svgSupported, setSvgSupported] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const feImageRef = useRef<SVGFEImageElement>(null)
  const redChannelRef = useRef<SVGFEDisplacementMapElement>(null)
  const greenChannelRef = useRef<SVGFEDisplacementMapElement>(null)
  const blueChannelRef = useRef<SVGFEDisplacementMapElement>(null)
  const gaussianBlurRef = useRef<SVGFEGaussianBlurElement>(null)

  const generateDisplacementMap = () => {
    const rect = containerRef.current?.getBoundingClientRect()
    const actualWidth = rect?.width || 400
    const actualHeight = rect?.height || 200
    const edgeSize = Math.min(actualWidth, actualHeight) * (borderWidth * 0.5)
    const svgContent = `
      <svg viewBox="0 0 ${actualWidth} ${actualHeight}" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <linearGradient id="${redGradId}" x1="100%" y1="0%" x2="0%" y2="0%">
            <stop offset="0%" stop-color="#0000"/>
            <stop offset="100%" stop-color="red"/>
          </linearGradient>
          <linearGradient id="${blueGradId}" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stop-color="#0000"/>
            <stop offset="100%" stop-color="blue"/>
          </linearGradient>
        </defs>
        <rect x="0" y="0" width="${actualWidth}" height="${actualHeight}" fill="black"></rect>
        <rect x="0" y="0" width="${actualWidth}" height="${actualHeight}" rx="${borderRadius}" fill="url(#${redGradId})" />
        <rect x="0" y="0" width="${actualWidth}" height="${actualHeight}" rx="${borderRadius}" fill="url(#${blueGradId})" style="mix-blend-mode: ${mixBlendMode}" />
        <rect x="${edgeSize}" y="${edgeSize}" width="${actualWidth - edgeSize * 2}" height="${actualHeight - edgeSize * 2}" rx="${borderRadius}" fill="hsl(0 0% ${brightness}% / ${opacity})" style="filter:blur(${blur}px)" />
      </svg>
    `
    return `data:image/svg+xml,${encodeURIComponent(svgContent)}`
  }

  const updateDisplacementMap = () => {
    feImageRef.current?.setAttribute('href', generateDisplacementMap())
  }

  useEffect(() => {
    updateDisplacementMap()
    ;[
      { ref: redChannelRef, offset: redOffset },
      { ref: greenChannelRef, offset: greenOffset },
      { ref: blueChannelRef, offset: blueOffset },
    ].forEach(({ ref, offset }) => {
      if (ref.current) {
        ref.current.setAttribute('scale', (distortionScale + offset).toString())
        ref.current.setAttribute('xChannelSelector', xChannel)
        ref.current.setAttribute('yChannelSelector', yChannel)
      }
    })
    gaussianBlurRef.current?.setAttribute('stdDeviation', displace.toString())
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [width, height, borderRadius, borderWidth, brightness, opacity, blur, displace,
      distortionScale, redOffset, greenOffset, blueOffset, xChannel, yChannel, mixBlendMode])

  useEffect(() => {
    if (!containerRef.current) return
    const obs = new ResizeObserver(() => setTimeout(updateDisplacementMap, 0))
    obs.observe(containerRef.current)
    return () => obs.disconnect()
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    setTimeout(updateDisplacementMap, 0)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [width, height])

  useEffect(() => {
    const isWebkit = /Safari/.test(navigator.userAgent) && !/Chrome/.test(navigator.userAgent)
    const isFirefox = /Firefox/.test(navigator.userAgent)
    if (isWebkit || isFirefox) { setSvgSupported(false); return }
    const div = document.createElement('div')
    div.style.backdropFilter = `url(#${filterId})`
    setSvgSupported(div.style.backdropFilter !== '')
  }, [filterId])

  const containerStyle: CSSProperties = {
    ...style,
    width: typeof width === 'number' ? `${width}px` : width,
    height: typeof height === 'number' ? `${height}px` : height,
    borderRadius: `${borderRadius}px`,
    '--glass-frost': backgroundOpacity,
    '--glass-saturation': saturation,
    '--filter-id': `url(#${filterId})`,
  } as CSSProperties

  return (
    <div
      ref={containerRef}
      className={`glass-surface ${svgSupported ? 'glass-surface--svg' : 'glass-surface--fallback'} ${className}`}
      style={containerStyle}
    >
      <svg className="glass-surface__filter" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <filter id={filterId} colorInterpolationFilters="sRGB" x="0%" y="0%" width="100%" height="100%">
            <feImage ref={feImageRef} x="0" y="0" width="100%" height="100%" preserveAspectRatio="none" result="map" />
            <feDisplacementMap ref={redChannelRef} in="SourceGraphic" in2="map" result="dispRed" />
            <feColorMatrix in="dispRed" type="matrix"
              values="1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0" result="red" />
            <feDisplacementMap ref={greenChannelRef} in="SourceGraphic" in2="map" result="dispGreen" />
            <feColorMatrix in="dispGreen" type="matrix"
              values="0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0" result="green" />
            <feDisplacementMap ref={blueChannelRef} in="SourceGraphic" in2="map" result="dispBlue" />
            <feColorMatrix in="dispBlue" type="matrix"
              values="0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0" result="blue" />
            <feBlend in="red" in2="green" mode="screen" result="rg" />
            <feBlend in="rg" in2="blue" mode="screen" result="output" />
            <feGaussianBlur ref={gaussianBlurRef} in="output" stdDeviation="0.7" />
          </filter>
        </defs>
      </svg>
      <div className="glass-surface__content">{children}</div>
    </div>
  )
}
```

- [ ] **Step 3: Commit**

```bash
cd src/Frontend && git add src/components/GlassSurface.tsx src/components/GlassSurface.css && git commit -m "feat: add GlassSurface component (React Bits)"
```

---

### Task 3: Wrap app with GlimmProvider

**Files:**
- Modify: `src/Frontend/src/main.tsx`

**Interfaces:**
- Consumes: `import { GlimmProvider } from 'glimm/react'`
- Produces: `useGlimm()` available to any component inside the tree

- [ ] **Step 1: Update main.tsx**

Replace the entire content of `src/Frontend/src/main.tsx`:

```tsx
import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { GlimmProvider } from 'glimm/react'
import { AuthProvider } from './auth/AuthProvider'
import App from './App'
import './index.css'

const queryClient = new QueryClient()

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <GlimmProvider palette="prism" sweepMs={900} outroMs={600}>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <App />
          </AuthProvider>
        </QueryClientProvider>
      </GlimmProvider>
    </BrowserRouter>
  </React.StrictMode>,
)
```

- [ ] **Step 2: Verify dev server starts without errors**

```bash
cd src/Frontend && npm run dev
```

Expected: Vite starts, no TypeScript errors. Open `http://localhost:5173` — app loads normally (Glimm has no visible effect yet since no links call sweep).

- [ ] **Step 3: Commit**

```bash
cd src/Frontend && git add src/main.tsx && git commit -m "feat: wrap app with GlimmProvider (prism palette)"
```

---

### Task 4: Rework Navbar to floating GlassSurface

**Files:**
- Modify: `src/Frontend/src/components/Navbar.tsx`
- Modify: `src/Frontend/src/App.tsx`

**Interfaces:**
- Consumes: `GlassSurface` from `./GlassSurface`
- Consumes: `useGlimm` from `glimm/react`
- Consumes: `useNavigate`, `useLocation` from `react-router-dom`
- Produces: floating navbar visible on all pages; nav clicks trigger Glimm sweep

- [ ] **Step 1: Replace Navbar.tsx**

Replace the entire content of `src/Frontend/src/components/Navbar.tsx`:

```tsx
import { ReactNode } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { useGlimm } from 'glimm/react'
import { useAuth } from '../auth/useAuth'
import NotificationBell from './NotificationBell'
import GlassSurface from './GlassSurface'

function NavLink({ to, icon, label }: { to: string; icon: string; label: string }) {
  const { sweep } = useGlimm()
  const navigate = useNavigate()
  const location = useLocation()
  const active = location.pathname.startsWith(to)
  return (
    <button
      onClick={() => sweep(() => navigate(to))}
      className={`flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-sm font-medium transition-colors sm:px-3
        ${active
          ? 'bg-violet-500/20 text-violet-300'
          : 'text-slate-400 hover:bg-white/10 hover:text-slate-100'}`}
    >
      <span>{icon}</span>
      <span className="hidden sm:block">{label}</span>
    </button>
  )
}

export default function Navbar() {
  const { user, login, logout } = useAuth()

  return (
    <div className="fixed left-0 right-0 top-0 z-50 px-4 pt-3">
      <GlassSurface
        width="100%"
        height={56}
        borderRadius={16}
        blur={14}
        brightness={18}
        opacity={0.92}
        distortionScale={-160}
      >
        <div className="flex h-14 w-full items-center gap-1 px-4 sm:gap-3">
          {/* Logo */}
          <button
            onClick={() => {
              const { sweep } = (window as any).__glimm_noop ?? { sweep: (fn: () => void) => fn() }
              sweep(() => {})
            }}
            className="mr-2 flex items-center gap-2 sm:mr-4"
            style={{ background: 'none', border: 'none', cursor: 'pointer' }}
          >
            <span className="text-xl">✦</span>
            <span className="hidden font-bold tracking-tight text-slate-100 sm:block">Rollout TCG</span>
          </button>

          <NavLink to="/cards" icon="🃏" label="Cards" />
          <NavLink to="/portfolio" icon="📦" label="Portfolio" />
          <NavLink to="/marketplace" icon="🏪" label="Market" />

          <div className="ml-auto flex items-center gap-2">
            {user ? (
              <>
                <NotificationBell userId={user.profile.sub} />
                <span className="hidden max-w-[120px] truncate text-xs text-slate-400 sm:block">
                  {user.profile.email}
                </span>
                <button
                  onClick={logout}
                  className="rounded-lg px-2.5 py-1.5 text-xs font-medium text-slate-400 transition-colors hover:bg-white/10 hover:text-slate-100"
                >
                  Out
                </button>
              </>
            ) : (
              <button
                onClick={login}
                className="rounded-lg bg-violet-600 px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-violet-500"
              >
                Sign in
              </button>
            )}
          </div>
        </div>
      </GlassSurface>
    </div>
  )
}
```

Note: The logo button uses a fallback sweep noop — update in Step 2 below.

- [ ] **Step 2: Fix logo button to use useGlimm properly**

The logo should navigate home without needing a noop trick. Extract it as its own component inside Navbar.tsx. Replace the logo `<button>` block:

```tsx
function Logo() {
  const { sweep } = useGlimm()
  const navigate = useNavigate()
  return (
    <button
      onClick={() => sweep(() => navigate('/cards'))}
      className="mr-2 flex items-center gap-2 sm:mr-4 text-left"
    >
      <span className="text-xl">✦</span>
      <span className="hidden font-bold tracking-tight text-slate-100 sm:block">Rollout TCG</span>
    </button>
  )
}
```

And replace the logo JSX in `Navbar` with `<Logo />`.

- [ ] **Step 3: Update App.tsx — add pt-20 to main**

In `src/Frontend/src/App.tsx`, find the `<main>` element:

```tsx
<main className="mx-auto w-full max-w-6xl flex-1 px-3 py-4 sm:px-6 sm:py-6">
```

Replace with:

```tsx
<main className="mx-auto w-full max-w-6xl flex-1 px-3 pt-20 pb-4 sm:px-6 sm:pb-6">
```

- [ ] **Step 4: Remove the old z-index flex layout wrapper in App.tsx**

The old App.tsx had:
```tsx
<div className="relative z-10 flex min-h-screen flex-col">
  <Navbar />
  <main ...>
```

Since Navbar is now `position: fixed`, it no longer needs to be in the flex flow. Update the content layer div to remove `flex-col` and Navbar:

```tsx
<div className="relative z-10 min-h-screen">
  <Navbar />
  <main className="mx-auto w-full max-w-6xl px-3 pt-20 pb-4 sm:px-6 sm:pb-6">
    <Routes>
      ...
    </Routes>
  </main>
</div>
```

- [ ] **Step 5: Verify in browser**

```bash
cd src/Frontend && npm run dev
```

Open `http://localhost:5173`. Expected:
- Navbar floats above content with glass effect and top margin
- Clicking Cards / Portfolio / Marketplace triggers the prism colour sweep transition
- Content is not obscured by navbar (pt-20 clears it)
- On Safari/Firefox: fallback frosted-glass rendering (still looks clean)

- [ ] **Step 6: Commit**

```bash
cd src/Frontend && git add src/components/Navbar.tsx src/App.tsx && git commit -m "feat: floating GlassSurface navbar with Glimm sweep transitions"
```

---

### Task 5: Build CommandPalette and wire into App

**Files:**
- Create: `src/Frontend/src/components/CommandPalette.tsx`
- Modify: `src/Frontend/src/App.tsx`

**Interfaces:**
- Produces: `export default CommandPalette` — props: `{ open: boolean; onClose: () => void; pageCommands?: PaletteCommand[] }`
- Produces: `export interface PaletteCommand { id: string; label: string; group: string; action: () => void }`
- Produces: `export const PageCommandsContext` — `React.Context<Dispatch<SetStateAction<PaletteCommand[]>>>`

- [ ] **Step 1: Create CommandPalette.tsx**

Create `src/Frontend/src/components/CommandPalette.tsx`:

```tsx
import { createContext, Dispatch, SetStateAction, useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { Command } from 'cmdk'
import { useNavigate } from 'react-router-dom'
import { useGlimm } from 'glimm/react'

export interface PaletteCommand {
  id: string
  label: string
  group: string
  action: () => void
}

// Pages set contextual commands via this context
export const PageCommandsContext = createContext<Dispatch<SetStateAction<PaletteCommand[]>>>(() => {})

interface CommandPaletteProps {
  open: boolean
  onClose: () => void
  pageCommands?: PaletteCommand[]
}

export default function CommandPalette({ open, onClose, pageCommands = [] }: CommandPaletteProps) {
  const { sweep } = useGlimm()
  const navigate = useNavigate()
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (open) setTimeout(() => inputRef.current?.focus(), 0)
  }, [open])

  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape' && open) onClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [open, onClose])

  if (!open) return null

  const navCommands: PaletteCommand[] = [
    { id: 'nav-cards',       label: 'Go to Cards',       group: 'Navigate', action: () => { sweep(() => navigate('/cards'));       onClose() } },
    { id: 'nav-portfolio',   label: 'Go to Portfolio',   group: 'Navigate', action: () => { sweep(() => navigate('/portfolio'));   onClose() } },
    { id: 'nav-marketplace', label: 'Go to Marketplace', group: 'Navigate', action: () => { sweep(() => navigate('/marketplace')); onClose() } },
  ]

  const allGroups = [
    { heading: 'Navigate', commands: navCommands },
    ...(pageCommands.length > 0
      ? [{ heading: 'Actions', commands: pageCommands }]
      : []),
  ]

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-start justify-center pt-[20vh]">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-slate-950/60 backdrop-blur-sm"
        onClick={onClose}
      />
      {/* Panel */}
      <div className="relative w-full max-w-lg mx-4">
        <Command
          className="bg-slate-900/95 border border-slate-700/50 rounded-2xl shadow-2xl overflow-hidden"
          loop
        >
          <div className="border-b border-slate-700/50 px-4">
            <Command.Input
              ref={inputRef}
              placeholder="Type a command or search…"
              className="w-full bg-transparent py-3.5 text-sm text-slate-100 placeholder-slate-500 outline-none"
            />
          </div>
          <Command.List className="max-h-72 overflow-y-auto py-2">
            <Command.Empty className="py-8 text-center text-sm text-slate-500">
              No results.
            </Command.Empty>
            {allGroups.map(({ heading, commands }) => (
              <Command.Group
                key={heading}
                heading={heading}
                className="px-2 [&_[cmdk-group-heading]]:px-2 [&_[cmdk-group-heading]]:py-1.5 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:font-medium [&_[cmdk-group-heading]]:text-slate-500"
              >
                {commands.map(cmd => (
                  <Command.Item
                    key={cmd.id}
                    value={cmd.label}
                    onSelect={cmd.action}
                    className="flex cursor-pointer items-center gap-3 rounded-lg px-4 py-2.5 text-sm text-slate-300 aria-selected:bg-violet-500/20 aria-selected:text-violet-200"
                  >
                    {cmd.label}
                  </Command.Item>
                ))}
              </Command.Group>
            ))}
          </Command.List>
        </Command>
      </div>
    </div>,
    document.body,
  )
}
```

- [ ] **Step 2: Wire CommandPalette into App.tsx**

Replace the full content of `src/Frontend/src/App.tsx`:

```tsx
import { Routes, Route, Navigate } from 'react-router-dom'
import { useEffect, ReactNode, useState } from 'react'
import { useAuth } from './auth/useAuth'
import { setAuthToken } from './api/client'
import Navbar from './components/Navbar'
import CommandPalette, { PageCommandsContext, PaletteCommand } from './components/CommandPalette'
import LoginPage from './pages/LoginPage'
import CallbackPage from './pages/CallbackPage'
import CardsPage from './pages/CardsPage'
import PortfolioPage from './pages/PortfolioPage'
import MarketplacePage from './pages/MarketplacePage'
import NewListingPage from './pages/NewListingPage'
import NotificationsPage from './pages/NotificationsPage'
import DitherBackground from './components/DitherBackground'

function RequireAuth({ children }: { children: ReactNode }) {
  const { user, isLoading, login } = useAuth()
  useEffect(() => { if (!isLoading && !user) login() }, [user, isLoading, login])
  if (isLoading) return (
    <div className="flex h-40 items-center justify-center text-slate-400">Loading…</div>
  )
  if (!user) return null
  return <>{children}</>
}

export default function App() {
  const { user } = useAuth()
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [pageCommands, setPageCommands] = useState<PaletteCommand[]>([])

  useEffect(() => {
    setAuthToken(user?.access_token ?? null)
  }, [user])

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setPaletteOpen(prev => !prev)
      }
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [])

  return (
    <div className="relative min-h-screen bg-slate-950">
      <div className="fixed inset-0 z-0">
        <DitherBackground />
      </div>

      <div className="relative z-10 min-h-screen">
        <Navbar />
        <PageCommandsContext.Provider value={setPageCommands}>
          <main className="mx-auto w-full max-w-6xl px-3 pt-20 pb-4 sm:px-6 sm:pb-6">
            <Routes>
              <Route path="/" element={<Navigate to="/cards" replace />} />
              <Route path="/callback" element={<CallbackPage />} />
              <Route path="/login" element={<LoginPage />} />
              <Route path="/cards" element={<CardsPage />} />
              <Route path="/portfolio" element={<RequireAuth><PortfolioPage /></RequireAuth>} />
              <Route path="/marketplace" element={<MarketplacePage />} />
              <Route path="/marketplace/new" element={<RequireAuth><NewListingPage /></RequireAuth>} />
              <Route path="/notifications" element={<RequireAuth><NotificationsPage /></RequireAuth>} />
            </Routes>
          </main>
        </PageCommandsContext.Provider>
      </div>

      <CommandPalette
        open={paletteOpen}
        onClose={() => setPaletteOpen(false)}
        pageCommands={pageCommands}
      />
    </div>
  )
}
```

- [ ] **Step 3: Write CommandPalette smoke test**

Create `src/Frontend/src/components/CommandPalette.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'

// Mock glimm so tests don't need WebGL
vi.mock('glimm/react', () => ({
  GlimmProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useGlimm: () => ({ sweep: (fn: () => void) => fn() }),
}))

import CommandPalette from './CommandPalette'

function Wrapper({ children }: { children: React.ReactNode }) {
  return <MemoryRouter>{children}</MemoryRouter>
}

describe('CommandPalette', () => {
  it('renders nothing when closed', () => {
    render(<CommandPalette open={false} onClose={vi.fn()} />, { wrapper: Wrapper })
    expect(screen.queryByPlaceholderText('Type a command or search…')).toBeNull()
  })

  it('renders input when open', () => {
    render(<CommandPalette open={true} onClose={vi.fn()} />, { wrapper: Wrapper })
    expect(screen.getByPlaceholderText('Type a command or search…')).toBeInTheDocument()
  })

  it('calls action when a page command item is selected', async () => {
    const action = vi.fn()
    const commands = [{ id: 'test', label: 'Test Action', group: 'Actions', action }]
    render(
      <CommandPalette open={true} onClose={vi.fn()} pageCommands={commands} />,
      { wrapper: Wrapper },
    )
    await userEvent.click(screen.getByText('Test Action'))
    expect(action).toHaveBeenCalled()
  })
})
```

- [ ] **Step 4: Run tests**

```bash
cd src/Frontend && npm test
```

Expected: `3 passed` for the CommandPalette test file.

- [ ] **Step 5: Verify in browser**

```bash
cd src/Frontend && npm run dev
```

Press `⌘K` (Mac) or `Ctrl+K` (Windows/Linux). Expected:
- Palette overlay appears with dark glass panel
- "Navigate" group shows Cards / Portfolio / Marketplace items
- Typing filters items via cmdk fuzzy search
- Clicking a navigation item triggers Glimm sweep and closes palette
- `Escape` closes palette
- Clicking backdrop closes palette

- [ ] **Step 6: Commit**

```bash
cd src/Frontend && git add src/components/CommandPalette.tsx src/components/CommandPalette.test.tsx src/App.tsx && git commit -m "feat: add Cmd+K command palette with navigation commands"
```
