# Frontend Overview

The frontend is a React 18 + TypeScript SPA built with Vite. In development it runs on port 3000 with Vite's dev server proxying API calls. In production it is compiled to a static bundle served by nginx inside Docker.

## Pages

| Route | Component | Auth required |
|---|---|---|
| `/` | Redirects to `/cards` | No |
| `/cards` | `CardsPage` | No |
| `/portfolio` | `PortfolioPage` | Yes |
| `/marketplace` | `MarketplacePage` | No |
| `/marketplace/new` | `NewListingPage` | Yes |
| `/notifications` | `NotificationsPage` | Yes |
| `/callback` | `CallbackPage` | No (OIDC return) |
| `/login` | `LoginPage` | No |

Protected routes are wrapped in `<RequireAuth>` which redirects unauthenticated users to the Identity Service login page automatically.

## Key components

| Component | Purpose |
|---|---|
| `Navbar` | Floating GlassSurface panel — links, user menu, notification bell badge |
| `DitherBackground` | Full-screen animated dither pattern (WebGL canvas) |
| `GlassSurface` | Frosted glass panel with backdrop blur — used by Navbar and modals |
| `BorderGlow` | Animated border glow wrapper — used on CTA buttons |
| `HoloCard` | Holographic card renderer with type-driven glow and tilt effect |
| `CardDetailModal` | React Portal with GlassSurface panel, price sparkline, and add-to-portfolio CTA |
| `CommandPalette` | Cmd+K global command palette — global + page-contextual commands |
| `AutocompleteDropdown` | Debounced search input with keyboard-navigable suggestion list |
| `FilterChips` | Type / rarity / set filter chips with active state |
| `SortDropdown` | Sort order selector (name, price asc/desc, rarity) |
| `NotificationBell` | Real-time notification count badge, powered by `useSignalR` |

## Tech stack

| Library | Version | Use |
|---|---|---|
| React | 18 | UI framework |
| TypeScript | 5 | Type safety |
| Vite | 5 | Build tool and dev server |
| React Router | v6 | Client-side routing |
| TanStack Query | v5 | Server state, caching, infinite scroll |
| oidc-client-ts | 3.x | OIDC / PKCE auth flow |
| @microsoft/signalr | 8.x | Real-time notification hub |
| Tailwind CSS | 3.x | Utility-first styling, dark slate theme |
| framer-motion | Latest | Page transitions and list animations |
| cmdk | Latest | Command palette |
| Axios | Latest | HTTP client with injected Bearer token |

## Project layout

```
src/Frontend/src/
  api/
    client.ts         Axios instance; setAuthToken() called after OIDC login
    cards.ts          TanStack Query hooks for card search (useInfiniteQuery)
    portfolio.ts      Portfolio CRUD hooks
    marketplace.ts    Listing create / buy / list hooks
    notifications.ts  Notification fetch + mark-read hooks
    pokemontcg.ts     PokémonTCG API types (PokemonCard, CardFilters, etc.)
  auth/
    authConfig.ts     oidc-client-ts UserManager configuration
    AuthProvider.tsx  Context provider wrapping UserManager
    useAuth.ts        Hook: { user, login, logout, isLoading }
  components/         Shared UI components (see table above)
  hooks/
    useSignalR.ts     Opens and manages a SignalR connection; returns notification list
  lib/
    typeColors.ts     Maps Pokémon card type names to Tailwind glow colours
  pages/              Page-level components
  types/
    api.ts            Shared API response types
```

## API proxy

In development, all `/api/*` requests are proxied by Vite to the API Gateway:

```typescript
// vite.config.ts
server: {
  proxy: {
    '/api': { target: 'http://localhost:5010', changeOrigin: true }
  }
}
```

In production (Docker), nginx serves the static bundle and forwards `/api/*` to the API Gateway container.

## Cmd+K command palette

Press `Cmd+K` (Mac) or `Ctrl+K` (Windows/Linux) to open the global command palette. It combines:

- **Global commands** — defined in `CommandPalette.tsx` (navigate to pages, toggle theme, etc.)
- **Page commands** — registered by the active page via `PageCommandsContext`; for example, CardsPage registers "Search cards" and "Clear filters"

Pages register commands using the context:

```typescript
const setPageCommands = useContext(PageCommandsContext)
useEffect(() => {
  setPageCommands([
    { id: 'clear-filters', label: 'Clear all filters', action: () => setFilters({}) },
  ])
  return () => setPageCommands([])
}, [])
```
