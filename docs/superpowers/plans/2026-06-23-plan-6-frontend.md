# Frontend Web UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a React + Vite SPA that lets authenticated users browse cards, manage their portfolio, buy and sell on the marketplace, and receive real-time notifications. Wire YARP into the ApiGateway to proxy all frontend API calls to backend services. Add remaining services (card-catalog, pricing, api-gateway, frontend) to docker-compose so the full stack runs with one command.

**Architecture:**
- `src/Frontend/` — Vite 5 + React 18 + TypeScript project. Dev server on port 3000 (matches IdentityServer `spa` client config). All API calls go to `/api/*` which Vite proxies to the ApiGateway in dev.
- ApiGateway gains YARP reverse proxy: `/api/cards/*` → card-catalog (5005), `/api/portfolio/*` → portfolio (5002), `/api/listings/*` → marketplace (5003), `/api/notifications/*` → notification (5004), `/api/hubs/*` → notification (5004, WebSocket), `/api/prices/*` → pricing (5006).
- Auth via `oidc-client-ts` PKCE flow against the existing Duende IdentityServer at port 5001. The `spa` client is already registered in `src/IdentityService/Config.cs` with redirect to `http://localhost:3000/callback`.
- SignalR bell: `useSignalR` hook connects to `/api/hubs/notifications?userId=<sub>` and appends live notifications.
- Docker: `src/Frontend/Dockerfile` builds with Node, serves with nginx. Nginx also handles SPA fallback routing.

**Tech Stack:**
- Frontend: React 18, TypeScript, Vite 5, React Router v6, TanStack Query v5, oidc-client-ts 3.x, @microsoft/signalr 8.x, Tailwind CSS 3.x, axios
- Gateway: Microsoft.AspNetCore.ReverseProxy (YARP) 2.1.0

## Global Constraints

- Frontend dev port: **3000** (must match IdentityServer `spa` client `RedirectUris` and `AllowedCorsOrigins` in `src/IdentityService/Config.cs` — do not change the port)
- Vite config: `server.port = 3000`; proxy `/api` → `http://localhost:5000` (ApiGateway)
- All backend API calls use path prefix `/api/` — never call service ports directly from the frontend
- YARP routes strip `/api` prefix via `PathPattern` transform then forward the remaining path
- Route mapping (frontend path → service → service path):
  - `/api/cards/{**rest}` → card-catalog:5005 → `/cards/{**rest}`
  - `/api/portfolio/{**rest}` → portfolio:5002 → `/portfolio/{**rest}`
  - `/api/listings/{**rest}` → marketplace:5003 → `/listings/{**rest}`
  - `/api/notifications/{**rest}` → notification:5004 → `/notifications/{**rest}`
  - `/api/hubs/{**rest}` → notification:5004 → `/hubs/{**rest}` (SignalR WebSocket)
  - `/api/prices/{**rest}` → pricing:5006 → `/prices/{**rest}`
- YARP package version: `Microsoft.AspNetCore.ReverseProxy` `2.1.0` — verify on NuGet if restore fails; pick latest 2.x
- `TreatWarningsAsErrors=true` applies to .NET projects only — no `Directory.Build.props` effect on the Vite project
- Docker ports:
  - api-gateway: 5000
  - card-catalog: 5005
  - pricing: 5006
  - frontend (nginx): 80
- `src/IdentityService/Config.cs` must add `http://localhost/callback` and `http://localhost` to `RedirectUris`, `PostLogoutRedirectUris`, and `AllowedCorsOrigins` to support the dockerised nginx-served frontend
- Node version in Dockerfile: `node:20-alpine`
- nginx: `nginx:alpine`

---

## File Map

**New files:**
```
src/Frontend/package.json
src/Frontend/tsconfig.json
src/Frontend/tsconfig.node.json
src/Frontend/vite.config.ts
src/Frontend/index.html
src/Frontend/tailwind.config.js
src/Frontend/postcss.config.js
src/Frontend/Dockerfile
src/Frontend/nginx.conf
src/Frontend/src/main.tsx
src/Frontend/src/App.tsx
src/Frontend/src/types/api.ts
src/Frontend/src/auth/authConfig.ts
src/Frontend/src/auth/AuthProvider.tsx
src/Frontend/src/auth/useAuth.ts
src/Frontend/src/api/client.ts
src/Frontend/src/api/cards.ts
src/Frontend/src/api/portfolio.ts
src/Frontend/src/api/marketplace.ts
src/Frontend/src/api/notifications.ts
src/Frontend/src/components/Navbar.tsx
src/Frontend/src/components/NotificationBell.tsx
src/Frontend/src/hooks/useSignalR.ts
src/Frontend/src/pages/LoginPage.tsx
src/Frontend/src/pages/CallbackPage.tsx
src/Frontend/src/pages/CardsPage.tsx
src/Frontend/src/pages/PortfolioPage.tsx
src/Frontend/src/pages/MarketplacePage.tsx
src/Frontend/src/pages/NewListingPage.tsx
src/Frontend/src/pages/NotificationsPage.tsx
```

**Modified files:**
```
Directory.Packages.props                       — add YARP package version
src/ApiGateway/TCGTrading.ApiGateway.csproj    — add YARP package ref
src/ApiGateway/Program.cs                      — add YARP + CORS
src/ApiGateway/appsettings.json                — add ReverseProxy routes + Cors section
src/ApiGateway/appsettings.Development.json    — dev cluster addresses (localhost ports)
src/IdentityService/Config.cs                  — add docker redirect URIs
docker-compose.yml                             — add api-gateway, card-catalog, pricing, frontend services
```

---

### Task 1: YARP in ApiGateway

Add YARP reverse proxy to ApiGateway so all frontend `/api/*` traffic is forwarded to the correct backend service.

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/ApiGateway/TCGTrading.ApiGateway.csproj`
- Modify: `src/ApiGateway/appsettings.json`
- Create: `src/ApiGateway/appsettings.Development.json`
- Modify: `src/ApiGateway/Program.cs`
- Modify: `src/IdentityService/Config.cs`

**Interfaces:**
- Produces: `/api/cards/*`, `/api/portfolio/*`, `/api/listings/*`, `/api/notifications/*`, `/api/hubs/*`, `/api/prices/*` all forwarded to respective services

- [ ] **Step 1: Add YARP to Directory.Packages.props**

Open `Directory.Packages.props`. Add inside the existing `<ItemGroup>` before the closing tag:

```xml
    <!-- API Gateway -->
    <PackageVersion Include="Microsoft.AspNetCore.ReverseProxy" Version="2.1.0" />
```

- [ ] **Step 2: Add YARP package ref to ApiGateway csproj**

Open `src/ApiGateway/TCGTrading.ApiGateway.csproj`. Add inside `<ItemGroup>`:

```xml
    <PackageReference Include="Microsoft.AspNetCore.ReverseProxy" />
```

- [ ] **Step 3: Add YARP routes and CORS to appsettings.json**

Replace `src/ApiGateway/appsettings.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Telemetry": {
    "OtlpEndpoint": "http://otel-collector:4317",
    "LokiEndpoint": "http://loki:3100"
  },
  "RateLimit": {
    "AnonymousPermitLimit": 60,
    "AuthenticatedPermitLimit": 300,
    "WindowSeconds": 60
  },
  "Redis": {
    "ConnectionString": "redis:6379"
  },
  "Cors": {
    "AllowedOrigin": "http://localhost:3000"
  },
  "ReverseProxy": {
    "Routes": {
      "cards-route": {
        "ClusterId": "card-catalog",
        "Match": { "Path": "/api/cards/{**rest}" },
        "Transforms": [{ "PathPattern": "/cards/{**rest}" }]
      },
      "portfolio-route": {
        "ClusterId": "portfolio",
        "Match": { "Path": "/api/portfolio/{**rest}" },
        "Transforms": [{ "PathPattern": "/portfolio/{**rest}" }]
      },
      "listings-route": {
        "ClusterId": "marketplace",
        "Match": { "Path": "/api/listings/{**rest}" },
        "Transforms": [{ "PathPattern": "/listings/{**rest}" }]
      },
      "notifications-route": {
        "ClusterId": "notification",
        "Match": { "Path": "/api/notifications/{**rest}" },
        "Transforms": [{ "PathPattern": "/notifications/{**rest}" }]
      },
      "hubs-route": {
        "ClusterId": "notification",
        "Match": { "Path": "/api/hubs/{**rest}" },
        "Transforms": [{ "PathPattern": "/hubs/{**rest}" }]
      },
      "prices-route": {
        "ClusterId": "pricing",
        "Match": { "Path": "/api/prices/{**rest}" },
        "Transforms": [{ "PathPattern": "/prices/{**rest}" }]
      }
    },
    "Clusters": {
      "card-catalog": {
        "Destinations": {
          "primary": { "Address": "http://card-catalog:5005" }
        }
      },
      "portfolio": {
        "Destinations": {
          "primary": { "Address": "http://portfolio:5002" }
        }
      },
      "marketplace": {
        "Destinations": {
          "primary": { "Address": "http://marketplace:5003" }
        }
      },
      "notification": {
        "Destinations": {
          "primary": { "Address": "http://notification:5004" }
        }
      },
      "pricing": {
        "Destinations": {
          "primary": { "Address": "http://pricing:5006" }
        }
      }
    }
  }
}
```

- [ ] **Step 4: Create appsettings.Development.json for ApiGateway**

Create `src/ApiGateway/appsettings.Development.json`:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Telemetry": {
    "OtlpEndpoint": "http://localhost:4317",
    "LokiEndpoint": "http://localhost:3100"
  },
  "ReverseProxy": {
    "Clusters": {
      "card-catalog": {
        "Destinations": { "primary": { "Address": "http://localhost:5005" } }
      },
      "portfolio": {
        "Destinations": { "primary": { "Address": "http://localhost:5002" } }
      },
      "marketplace": {
        "Destinations": { "primary": { "Address": "http://localhost:5003" } }
      },
      "notification": {
        "Destinations": { "primary": { "Address": "http://localhost:5004" } }
      },
      "pricing": {
        "Destinations": { "primary": { "Address": "http://localhost:5006" } }
      }
    }
  }
}
```

- [ ] **Step 5: Update ApiGateway Program.cs — add YARP + CORS**

Replace the entire contents of `src/ApiGateway/Program.cs`:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TCGTrading.ApiGateway.Infrastructure.RateLimiting;
using TCGTrading.SharedKernel.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetry("api-gateway");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.AddRateLimiting();

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors();
app.UseRateLimiter();

app.MapPrometheusScrapingEndpoint();
app.MapGet("/health", () => Results.Ok());
app.MapReverseProxy();

app.Run();

public partial class Program { }
```

- [ ] **Step 6: Add docker redirect URIs to IdentityServer Config.cs**

Open `src/IdentityService/Config.cs`. The `spa` client block currently has:
```csharp
RedirectUris = { "http://localhost:3000/callback" },
PostLogoutRedirectUris = { "http://localhost:3000" },
AllowedCorsOrigins = { "http://localhost:3000" },
```

Replace those three lines with:
```csharp
RedirectUris = { "http://localhost:3000/callback", "http://localhost/callback" },
PostLogoutRedirectUris = { "http://localhost:3000", "http://localhost" },
AllowedCorsOrigins = { "http://localhost:3000", "http://localhost" },
```

- [ ] **Step 7: Build ApiGateway**

```bash
dotnet build src/ApiGateway/TCGTrading.ApiGateway.csproj
```

Expected: Build succeeded, 0 warnings.

- [ ] **Step 8: Add remaining services + frontend to docker-compose.yml**

Open `docker-compose.yml`. Add after the `notification` service block, before the `otel-collector` block:

```yaml
  card-catalog:
    build:
      context: .
      dockerfile: src/CardCatalog/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:5005
      ConnectionStrings__CardDb: "Host=postgres;Port=5432;Database=db_cards;Username=tcg;Password=dev"
      MeiliSearch__Host: "http://meilisearch:7700"
      MeiliSearch__ApiKey: masterKey
    ports:
      - "5005:5005"
    depends_on:
      postgres:
        condition: service_healthy
      meilisearch:
        condition: service_healthy

  pricing:
    build:
      context: .
      dockerfile: src/Pricing/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:5006
      ConnectionStrings__PricingDb: "Host=timescaledb;Port=5432;Database=db_pricing;Username=tcg;Password=dev"
    ports:
      - "5006:5006"
    depends_on:
      timescaledb:
        condition: service_healthy

  api-gateway:
    build:
      context: .
      dockerfile: src/ApiGateway/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:5000
      Redis__ConnectionString: "redis:6379"
      Cors__AllowedOrigin: "http://localhost:3000"
    ports:
      - "5000:5000"
    depends_on:
      redis:
        condition: service_healthy
      portfolio:
        condition: service_started
      marketplace:
        condition: service_started
      notification:
        condition: service_started
      card-catalog:
        condition: service_started
      pricing:
        condition: service_started

  frontend:
    build:
      context: src/Frontend
      dockerfile: Dockerfile
    ports:
      - "80:80"
    depends_on:
      - api-gateway
      - identity-service
```

Also add to the `volumes:` section at the bottom of docker-compose.yml (if not already present — these are already there):

No new volumes needed.

- [ ] **Step 9: Build and smoke test YARP routing**

Start just the gateway and portfolio (simplest smoke test):

```bash
docker compose up postgres portfolio -d
dotnet run --project src/ApiGateway/TCGTrading.ApiGateway.csproj &
sleep 3
curl http://localhost:5000/api/portfolio/cards?userId=11111111-1111-1111-1111-111111111111
```

Expected: HTTP 200 with `[]` (empty portfolio, proxied through YARP to portfolio service).

- [ ] **Step 10: Commit**

```bash
git add Directory.Packages.props src/ApiGateway/ src/IdentityService/Config.cs docker-compose.yml
git commit -m "feat(gateway): add YARP reverse proxy and docker-compose service entries"
```

---

### Task 2: Vite project scaffold and auth setup

**Files:**
- Create: `src/Frontend/package.json`
- Create: `src/Frontend/tsconfig.json`
- Create: `src/Frontend/tsconfig.node.json`
- Create: `src/Frontend/vite.config.ts`
- Create: `src/Frontend/index.html`
- Create: `src/Frontend/tailwind.config.js`
- Create: `src/Frontend/postcss.config.js`
- Create: `src/Frontend/src/main.tsx`
- Create: `src/Frontend/src/App.tsx`
- Create: `src/Frontend/src/types/api.ts`
- Create: `src/Frontend/src/auth/authConfig.ts`
- Create: `src/Frontend/src/auth/AuthProvider.tsx`
- Create: `src/Frontend/src/auth/useAuth.ts`

**Interfaces:**
- Produces: `npm run dev` starts Vite on port 3000 with `/api` proxied to gateway; `useAuth()` returns `{ user, login, logout, isLoading }`; `authConfig` has oidc settings

- [ ] **Step 1: Create package.json**

Create `src/Frontend/package.json`:

```json
{
  "name": "tcg-trading-frontend",
  "private": true,
  "version": "1.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "react": "^18.3.1",
    "react-dom": "^18.3.1",
    "react-router-dom": "^6.26.2",
    "@tanstack/react-query": "^5.56.2",
    "oidc-client-ts": "^3.1.0",
    "@microsoft/signalr": "^8.0.7",
    "axios": "^1.7.7"
  },
  "devDependencies": {
    "@types/react": "^18.3.5",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.1",
    "typescript": "^5.5.3",
    "vite": "^5.4.3",
    "tailwindcss": "^3.4.10",
    "autoprefixer": "^10.4.20",
    "postcss": "^8.4.45"
  }
}
```

- [ ] **Step 2: Create tsconfig.json**

Create `src/Frontend/tsconfig.json`:

```json
{
  "files": [],
  "references": [
    { "path": "./tsconfig.node.json" },
    { "path": "./tsconfig.app.json" }
  ]
}
```

Create `src/Frontend/tsconfig.node.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2023"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowSyntheticDefaultImports": true,
    "strict": true,
    "noEmit": true,
    "skipLibCheck": true
  },
  "include": ["vite.config.ts"]
}
```

Create `src/Frontend/tsconfig.app.json`:

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "isolatedModules": true,
    "moduleDetection": "force",
    "jsx": "react-jsx",
    "strict": true,
    "noEmit": true,
    "skipLibCheck": true
  },
  "include": ["src"]
}
```

- [ ] **Step 3: Create vite.config.ts**

Create `src/Frontend/vite.config.ts`:

```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
```

- [ ] **Step 4: Create tailwind config**

Create `src/Frontend/tailwind.config.js`:

```js
/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: { extend: {} },
  plugins: [],
}
```

Create `src/Frontend/postcss.config.js`:

```js
export default {
  plugins: {
    tailwindcss: {},
    autoprefixer: {},
  },
}
```

- [ ] **Step 5: Create index.html**

Create `src/Frontend/index.html`:

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Rollout TCG</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 6: Create API types**

Create `src/Frontend/src/types/api.ts`:

```ts
export interface CollectionItem {
  id: string
  userId: string
  cardId: string
  cardName: string
  quantity: number
  condition: string
  acquisitionPriceUsd: number
  createdAt: string
}

export interface PortfolioSummary {
  totalItems: number
  totalCards: number
  totalAcquisitionCostUsd: number
}

export interface Listing {
  id: string
  sellerId: string
  cardId: string
  cardName: string
  condition: string
  askingPriceUsd: number
  status: 'Active' | 'Sold' | 'Cancelled'
  buyerId?: string
  purchasedAt?: string
  createdAt: string
}

export interface CardSearchResult {
  id: string
  name: string
  set: string
  rarity: string
  type: string
  text: string
}

export interface CardSearchResponse {
  items: CardSearchResult[]
  total: number
  page: number
  pageSize: number
}

export interface Notification {
  id: string
  userId: string
  title: string
  message: string
  type: string
  referenceId?: string
  isRead: boolean
  createdAt: string
}
```

- [ ] **Step 7: Create auth config**

Create `src/Frontend/src/auth/authConfig.ts`:

```ts
export const IDENTITY_URL = 'http://localhost:5001'

export const oidcSettings = {
  authority: IDENTITY_URL,
  client_id: 'spa',
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: window.location.origin,
  scope: 'openid profile email tcg.full',
  response_type: 'code',
  userStore: undefined as undefined, // populated by AuthProvider
}
```

- [ ] **Step 8: Create AuthProvider**

Create `src/Frontend/src/auth/AuthProvider.tsx`:

```tsx
import { createContext, useContext, useEffect, useState, ReactNode } from 'react'
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts'
import { oidcSettings } from './authConfig'

interface AuthContextValue {
  user: User | null
  isLoading: boolean
  login: () => void
  logout: () => void
  userManager: UserManager
}

const AuthContext = createContext<AuthContextValue | null>(null)

const manager = new UserManager({
  ...oidcSettings,
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    manager.getUser().then(u => {
      setUser(u)
      setIsLoading(false)
    })

    const onUserLoaded = (u: User) => setUser(u)
    const onUserUnloaded = () => setUser(null)
    manager.events.addUserLoaded(onUserLoaded)
    manager.events.addUserUnloaded(onUserUnloaded)

    return () => {
      manager.events.removeUserLoaded(onUserLoaded)
      manager.events.removeUserUnloaded(onUserUnloaded)
    }
  }, [])

  return (
    <AuthContext.Provider value={{
      user,
      isLoading,
      login: () => manager.signinRedirect(),
      logout: () => manager.signoutRedirect(),
      userManager: manager,
    }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuthContext() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuthContext must be inside AuthProvider')
  return ctx
}
```

- [ ] **Step 9: Create useAuth hook**

Create `src/Frontend/src/auth/useAuth.ts`:

```ts
export { useAuthContext as useAuth } from './AuthProvider'
```

- [ ] **Step 10: Install dependencies**

```bash
cd src/Frontend && npm install
```

Expected: `node_modules/` created, no errors.

- [ ] **Step 11: Create main.tsx**

Create `src/Frontend/src/main.tsx`:

```tsx
import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider } from './auth/AuthProvider'
import App from './App'
import './index.css'

const queryClient = new QueryClient()

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <App />
        </AuthProvider>
      </QueryClientProvider>
    </BrowserRouter>
  </React.StrictMode>,
)
```

Create `src/Frontend/src/index.css`:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

- [ ] **Step 12: Create placeholder App.tsx**

Create `src/Frontend/src/App.tsx`:

```tsx
export default function App() {
  return <div className="p-8 text-gray-800">Loading...</div>
}
```

- [ ] **Step 13: Verify dev server starts**

```bash
cd src/Frontend && npm run dev
```

Expected: output includes `Local: http://localhost:3000/`. Open browser — see "Loading...". No console errors.

- [ ] **Step 14: Commit**

```bash
cd ../..
git add src/Frontend/
git commit -m "feat(frontend): scaffold Vite project with auth setup"
```

---

### Task 3: API client layer

**Files:**
- Create: `src/Frontend/src/api/client.ts`
- Create: `src/Frontend/src/api/cards.ts`
- Create: `src/Frontend/src/api/portfolio.ts`
- Create: `src/Frontend/src/api/marketplace.ts`
- Create: `src/Frontend/src/api/notifications.ts`

**Interfaces:**
- Produces: typed async functions for every backend endpoint consumed by the UI

- [ ] **Step 1: Create axios client with auth header**

Create `src/Frontend/src/api/client.ts`:

```ts
import axios from 'axios'

// All requests go through /api — Vite dev proxy forwards to ApiGateway
export const api = axios.create({ baseURL: '/api' })

// Set Authorization header from oidc-client-ts stored user
export function setAuthToken(token: string | null) {
  if (token) {
    api.defaults.headers.common['Authorization'] = `Bearer ${token}`
  } else {
    delete api.defaults.headers.common['Authorization']
  }
}
```

- [ ] **Step 2: Create card catalog API**

Create `src/Frontend/src/api/cards.ts`:

```ts
import { CardSearchResponse } from '../types/api'
import { api } from './client'

export async function searchCards(
  q?: string,
  set?: string,
  rarity?: string,
  page = 1,
  pageSize = 20,
): Promise<CardSearchResponse> {
  const params = new URLSearchParams()
  if (q) params.set('q', q)
  if (set) params.set('set', set)
  if (rarity) params.set('rarity', rarity)
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))
  const { data } = await api.get<CardSearchResponse>(`/cards/search?${params}`)
  return data
}
```

- [ ] **Step 3: Create portfolio API**

Create `src/Frontend/src/api/portfolio.ts`:

```ts
import { CollectionItem, PortfolioSummary } from '../types/api'
import { api } from './client'

export async function getPortfolioCards(userId: string): Promise<CollectionItem[]> {
  const { data } = await api.get<CollectionItem[]>(`/portfolio/cards?userId=${userId}`)
  return data
}

export async function getPortfolioSummary(userId: string): Promise<PortfolioSummary> {
  const { data } = await api.get<PortfolioSummary>(`/portfolio/summary?userId=${userId}`)
  return data
}

export async function addCardToPortfolio(payload: {
  userId: string
  cardId: string
  cardName: string
  quantity: number
  condition: string
  acquisitionPriceUsd: number
}): Promise<CollectionItem> {
  const { data } = await api.post<CollectionItem>('/portfolio/cards', payload)
  return data
}

export async function removeCardFromPortfolio(id: string): Promise<void> {
  await api.delete(`/portfolio/cards/${id}`)
}
```

- [ ] **Step 4: Create marketplace API**

Create `src/Frontend/src/api/marketplace.ts`:

```ts
import { Listing } from '../types/api'
import { api } from './client'

export async function getListings(): Promise<Listing[]> {
  const { data } = await api.get<Listing[]>('/listings')
  return data
}

export async function createListing(payload: {
  sellerId: string
  cardId: string
  cardName: string
  condition: string
  askingPriceUsd: number
}): Promise<Listing> {
  const { data } = await api.post<Listing>('/listings', payload)
  return data
}

export async function purchaseListing(id: string, buyerId: string): Promise<Listing> {
  const { data } = await api.post<Listing>(`/listings/${id}/purchase`, { buyerId })
  return data
}

export async function cancelListing(id: string): Promise<void> {
  await api.delete(`/listings/${id}`)
}
```

- [ ] **Step 5: Create notifications API**

Create `src/Frontend/src/api/notifications.ts`:

```ts
import { Notification } from '../types/api'
import { api } from './client'

export async function getNotifications(userId: string): Promise<Notification[]> {
  const { data } = await api.get<Notification[]>(`/notifications?userId=${userId}`)
  return data
}

export async function markNotificationRead(id: string): Promise<Notification> {
  const { data } = await api.post<Notification>(`/notifications/${id}/read`)
  return data
}
```

- [ ] **Step 6: Commit**

```bash
git add src/Frontend/src/api/ src/Frontend/src/types/
git commit -m "feat(frontend): add typed API client layer"
```

---

### Task 4: Navbar, login, and callback pages

**Files:**
- Create: `src/Frontend/src/components/Navbar.tsx`
- Create: `src/Frontend/src/pages/LoginPage.tsx`
- Create: `src/Frontend/src/pages/CallbackPage.tsx`
- Update: `src/Frontend/src/App.tsx`

**Interfaces:**
- Produces: React Router routes; login redirects to IdentityServer; callback processes OIDC response; navbar shows username + logout

- [ ] **Step 1: Create Navbar**

Create `src/Frontend/src/components/Navbar.tsx`:

```tsx
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import NotificationBell from './NotificationBell'

export default function Navbar() {
  const { user, login, logout } = useAuth()

  return (
    <nav className="bg-gray-900 text-white px-6 py-3 flex items-center gap-6">
      <span className="font-bold text-lg">Rollout TCG</span>
      <Link to="/cards" className="hover:text-gray-300">Cards</Link>
      <Link to="/portfolio" className="hover:text-gray-300">Portfolio</Link>
      <Link to="/marketplace" className="hover:text-gray-300">Marketplace</Link>
      <Link to="/notifications" className="hover:text-gray-300">Notifications</Link>
      <div className="ml-auto flex items-center gap-4">
        {user ? (
          <>
            <NotificationBell userId={user.profile.sub} />
            <span className="text-sm text-gray-400">{user.profile.email}</span>
            <button onClick={logout} className="text-sm hover:text-gray-300">Logout</button>
          </>
        ) : (
          <button onClick={login} className="text-sm hover:text-gray-300">Login</button>
        )}
      </div>
    </nav>
  )
}
```

- [ ] **Step 2: Create LoginPage**

Create `src/Frontend/src/pages/LoginPage.tsx`:

```tsx
import { useAuth } from '../auth/useAuth'

export default function LoginPage() {
  const { login } = useAuth()
  return (
    <div className="flex flex-col items-center justify-center min-h-[60vh] gap-4">
      <h1 className="text-3xl font-bold">Rollout TCG</h1>
      <p className="text-gray-600">Sign in to manage your collection and trade cards.</p>
      <button
        onClick={login}
        className="px-6 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
      >
        Sign In
      </button>
    </div>
  )
}
```

- [ ] **Step 3: Create CallbackPage**

Create `src/Frontend/src/pages/CallbackPage.tsx`:

```tsx
import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'

export default function CallbackPage() {
  const { userManager } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    userManager.signinRedirectCallback()
      .then(() => navigate('/cards'))
      .catch(() => navigate('/'))
  }, [userManager, navigate])

  return <div className="p-8">Completing sign in...</div>
}
```

- [ ] **Step 4: Create placeholder NotificationBell (will be filled in Task 7)**

Create `src/Frontend/src/components/NotificationBell.tsx`:

```tsx
export default function NotificationBell({ userId: _userId }: { userId: string }) {
  return <span className="text-sm">🔔</span>
}
```

- [ ] **Step 5: Update App.tsx with routing**

Replace `src/Frontend/src/App.tsx`:

```tsx
import { Routes, Route, Navigate } from 'react-router-dom'
import { useEffect } from 'react'
import { useAuth } from './auth/useAuth'
import { setAuthToken } from './api/client'
import Navbar from './components/Navbar'
import LoginPage from './pages/LoginPage'
import CallbackPage from './pages/CallbackPage'
import CardsPage from './pages/CardsPage'
import PortfolioPage from './pages/PortfolioPage'
import MarketplacePage from './pages/MarketplacePage'
import NewListingPage from './pages/NewListingPage'
import NotificationsPage from './pages/NotificationsPage'

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, isLoading, login } = useAuth()
  useEffect(() => { if (!isLoading && !user) login() }, [user, isLoading, login])
  if (isLoading) return <div className="p-8">Loading...</div>
  if (!user) return null
  return <>{children}</>
}

export default function App() {
  const { user } = useAuth()

  useEffect(() => {
    setAuthToken(user?.access_token ?? null)
  }, [user])

  return (
    <div className="min-h-screen bg-gray-50">
      <Navbar />
      <main className="max-w-6xl mx-auto px-4 py-6">
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
    </div>
  )
}
```

Create placeholder pages (filled in Tasks 5 and 6):

Create `src/Frontend/src/pages/CardsPage.tsx`:
```tsx
export default function CardsPage() { return <div>Cards</div> }
```

Create `src/Frontend/src/pages/PortfolioPage.tsx`:
```tsx
export default function PortfolioPage() { return <div>Portfolio</div> }
```

Create `src/Frontend/src/pages/MarketplacePage.tsx`:
```tsx
export default function MarketplacePage() { return <div>Marketplace</div> }
```

Create `src/Frontend/src/pages/NewListingPage.tsx`:
```tsx
export default function NewListingPage() { return <div>New Listing</div> }
```

Create `src/Frontend/src/pages/NotificationsPage.tsx`:
```tsx
export default function NotificationsPage() { return <div>Notifications</div> }
```

- [ ] **Step 6: Verify the app starts and routes work**

```bash
cd src/Frontend && npm run dev
```

Open `http://localhost:3000`. Clicking "Sign In" should redirect to `http://localhost:5001` (IdentityServer). After logging in it should redirect to `/callback` then `/cards`.

IdentityServer must be running for auth to work:
```bash
docker compose up postgres identity-service -d
```

- [ ] **Step 7: Commit**

```bash
cd ../..
git add src/Frontend/src/
git commit -m "feat(frontend): add routing, auth flow, and navbar"
```

---

### Task 5: Cards and Portfolio pages

**Files:**
- Replace: `src/Frontend/src/pages/CardsPage.tsx`
- Replace: `src/Frontend/src/pages/PortfolioPage.tsx`

**Interfaces:**
- Produces: Cards page with fuzzy search + results grid; Portfolio page with collection list + summary + add card form + remove button

- [ ] **Step 1: Build CardsPage**

Replace `src/Frontend/src/pages/CardsPage.tsx`:

```tsx
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { searchCards } from '../api/cards'

export default function CardsPage() {
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['cards', search],
    queryFn: () => searchCards(search || undefined),
  })

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Card Catalog</h1>
      <div className="flex gap-2 mb-6">
        <input
          value={q}
          onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && setSearch(q)}
          placeholder="Search cards..."
          className="border rounded px-3 py-2 w-64"
        />
        <button
          onClick={() => setSearch(q)}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
        >
          Search
        </button>
      </div>

      {isLoading && <p>Loading...</p>}
      {data && (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {data.items.map(card => (
            <div key={card.id} className="border rounded p-4 bg-white shadow-sm">
              <div className="font-semibold">{card.name}</div>
              <div className="text-sm text-gray-600">{card.set}</div>
              <div className="text-sm text-gray-500">{card.rarity} · {card.type}</div>
              <p className="text-sm mt-2">{card.text}</p>
            </div>
          ))}
        </div>
      )}
      {data?.items.length === 0 && <p className="text-gray-500">No cards found.</p>}
    </div>
  )
}
```

- [ ] **Step 2: Build PortfolioPage**

Replace `src/Frontend/src/pages/PortfolioPage.tsx`:

```tsx
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import {
  getPortfolioCards, getPortfolioSummary,
  addCardToPortfolio, removeCardFromPortfolio
} from '../api/portfolio'

export default function PortfolioPage() {
  const { user } = useAuth()
  const userId = user!.profile.sub
  const qc = useQueryClient()

  const { data: cards } = useQuery({
    queryKey: ['portfolio', userId],
    queryFn: () => getPortfolioCards(userId),
  })

  const { data: summary } = useQuery({
    queryKey: ['portfolio-summary', userId],
    queryFn: () => getPortfolioSummary(userId),
  })

  const addMutation = useMutation({
    mutationFn: addCardToPortfolio,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['portfolio', userId] })
      qc.invalidateQueries({ queryKey: ['portfolio-summary', userId] })
    },
  })

  const removeMutation = useMutation({
    mutationFn: removeCardFromPortfolio,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['portfolio', userId] })
      qc.invalidateQueries({ queryKey: ['portfolio-summary', userId] })
    },
  })

  const [form, setForm] = useState({
    cardId: '', cardName: '', quantity: '1',
    condition: 'NearMint', acquisitionPriceUsd: ''
  })

  const handleAdd = (e: React.FormEvent) => {
    e.preventDefault()
    addMutation.mutate({
      userId,
      cardId: form.cardId || crypto.randomUUID(),
      cardName: form.cardName,
      quantity: Number(form.quantity),
      condition: form.condition,
      acquisitionPriceUsd: Number(form.acquisitionPriceUsd),
    })
    setForm({ cardId: '', cardName: '', quantity: '1', condition: 'NearMint', acquisitionPriceUsd: '' })
  }

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">My Portfolio</h1>

      {summary && (
        <div className="bg-white border rounded p-4 mb-6 flex gap-8">
          <div><div className="text-sm text-gray-500">Items</div><div className="text-xl font-bold">{summary.totalItems}</div></div>
          <div><div className="text-sm text-gray-500">Total cards</div><div className="text-xl font-bold">{summary.totalCards}</div></div>
          <div><div className="text-sm text-gray-500">Acquisition cost</div><div className="text-xl font-bold">${summary.totalAcquisitionCostUsd.toFixed(2)}</div></div>
        </div>
      )}

      <form onSubmit={handleAdd} className="bg-white border rounded p-4 mb-6 flex flex-wrap gap-2">
        <input required placeholder="Card name" value={form.cardName}
          onChange={e => setForm(f => ({ ...f, cardName: e.target.value }))}
          className="border rounded px-2 py-1" />
        <input required type="number" min="1" placeholder="Qty" value={form.quantity}
          onChange={e => setForm(f => ({ ...f, quantity: e.target.value }))}
          className="border rounded px-2 py-1 w-20" />
        <input required type="number" step="0.01" placeholder="Price paid" value={form.acquisitionPriceUsd}
          onChange={e => setForm(f => ({ ...f, acquisitionPriceUsd: e.target.value }))}
          className="border rounded px-2 py-1 w-28" />
        <select value={form.condition}
          onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}
          className="border rounded px-2 py-1">
          {['Mint', 'NearMint', 'LightlyPlayed', 'Played', 'HeavilyPlayed'].map(c =>
            <option key={c}>{c}</option>)}
        </select>
        <button type="submit"
          className="px-4 py-1 bg-green-600 text-white rounded hover:bg-green-700">
          Add Card
        </button>
      </form>

      <div className="space-y-2">
        {cards?.map(item => (
          <div key={item.id} className="bg-white border rounded px-4 py-3 flex items-center gap-4">
            <div className="flex-1">
              <div className="font-medium">{item.cardName}</div>
              <div className="text-sm text-gray-500">Qty: {item.quantity} · {item.condition} · ${item.acquisitionPriceUsd}/ea</div>
            </div>
            <button
              onClick={() => removeMutation.mutate(item.id)}
              className="text-sm text-red-600 hover:text-red-800"
            >Remove</button>
          </div>
        ))}
        {cards?.length === 0 && <p className="text-gray-500">No cards yet. Add one above.</p>}
      </div>
    </div>
  )
}
```

- [ ] **Step 3: Verify in browser**

With ApiGateway, portfolio service, and card-catalog running, navigate to `/cards` and `/portfolio`. Cards should load from MeiliSearch. Portfolio add/remove should persist.

- [ ] **Step 4: Commit**

```bash
git add src/Frontend/src/pages/CardsPage.tsx src/Frontend/src/pages/PortfolioPage.tsx
git commit -m "feat(frontend): add cards search and portfolio pages"
```

---

### Task 6: Marketplace pages

**Files:**
- Replace: `src/Frontend/src/pages/MarketplacePage.tsx`
- Replace: `src/Frontend/src/pages/NewListingPage.tsx`

**Interfaces:**
- Produces: Marketplace browse page with buy button; New Listing form page

- [ ] **Step 1: Build MarketplacePage**

Replace `src/Frontend/src/pages/MarketplacePage.tsx`:

```tsx
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { getListings, purchaseListing } from '../api/marketplace'

export default function MarketplacePage() {
  const { user } = useAuth()
  const qc = useQueryClient()

  const { data: listings, isLoading } = useQuery({
    queryKey: ['listings'],
    queryFn: getListings,
  })

  const purchase = useMutation({
    mutationFn: ({ id, buyerId }: { id: string; buyerId: string }) =>
      purchaseListing(id, buyerId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['listings'] }),
  })

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-bold">Marketplace</h1>
        {user && (
          <Link to="/marketplace/new"
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">
            + List a Card
          </Link>
        )}
      </div>

      {isLoading && <p>Loading...</p>}

      <div className="space-y-3">
        {listings?.map(listing => (
          <div key={listing.id} className="bg-white border rounded px-4 py-3 flex items-center gap-4">
            <div className="flex-1">
              <div className="font-medium">{listing.cardName}</div>
              <div className="text-sm text-gray-500">{listing.condition} · Seller: {listing.sellerId.slice(0, 8)}…</div>
            </div>
            <div className="font-bold text-green-700">${listing.askingPriceUsd.toFixed(2)}</div>
            {user && user.profile.sub !== listing.sellerId && (
              <button
                onClick={() => purchase.mutate({ id: listing.id, buyerId: user.profile.sub })}
                disabled={purchase.isPending}
                className="px-3 py-1 bg-blue-600 text-white text-sm rounded hover:bg-blue-700 disabled:opacity-50"
              >
                Buy
              </button>
            )}
          </div>
        ))}
        {listings?.length === 0 && <p className="text-gray-500">No active listings.</p>}
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Build NewListingPage**

Replace `src/Frontend/src/pages/NewListingPage.tsx`:

```tsx
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { createListing } from '../api/marketplace'

export default function NewListingPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({
    cardName: '', cardId: '', condition: 'NearMint', askingPriceUsd: ''
  })

  const create = useMutation({
    mutationFn: createListing,
    onSuccess: () => navigate('/marketplace'),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    create.mutate({
      sellerId: user!.profile.sub,
      cardId: form.cardId || crypto.randomUUID(),
      cardName: form.cardName,
      condition: form.condition,
      askingPriceUsd: Number(form.askingPriceUsd),
    })
  }

  return (
    <div className="max-w-md">
      <h1 className="text-2xl font-bold mb-4">List a Card for Sale</h1>
      <form onSubmit={handleSubmit} className="bg-white border rounded p-6 space-y-4">
        <div>
          <label className="block text-sm font-medium mb-1">Card name</label>
          <input required value={form.cardName}
            onChange={e => setForm(f => ({ ...f, cardName: e.target.value }))}
            className="border rounded px-3 py-2 w-full" />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Condition</label>
          <select value={form.condition}
            onChange={e => setForm(f => ({ ...f, condition: e.target.value }))}
            className="border rounded px-3 py-2 w-full">
            {['Mint', 'NearMint', 'LightlyPlayed', 'Played', 'HeavilyPlayed'].map(c =>
              <option key={c}>{c}</option>)}
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Asking price (USD)</label>
          <input required type="number" step="0.01" min="0.01" value={form.askingPriceUsd}
            onChange={e => setForm(f => ({ ...f, askingPriceUsd: e.target.value }))}
            className="border rounded px-3 py-2 w-full" />
        </div>
        <button type="submit" disabled={create.isPending}
          className="w-full py-2 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50">
          {create.isPending ? 'Creating...' : 'Create Listing'}
        </button>
      </form>
    </div>
  )
}
```

- [ ] **Step 3: Verify in browser**

Navigate to `/marketplace`. Listings from the Marketplace service should appear. "List a Card" button should open the form and submit successfully.

- [ ] **Step 4: Commit**

```bash
git add src/Frontend/src/pages/MarketplacePage.tsx src/Frontend/src/pages/NewListingPage.tsx
git commit -m "feat(frontend): add marketplace browse and new listing pages"
```

---

### Task 7: SignalR notification bell and notifications page

**Files:**
- Create: `src/Frontend/src/hooks/useSignalR.ts`
- Replace: `src/Frontend/src/components/NotificationBell.tsx`
- Replace: `src/Frontend/src/pages/NotificationsPage.tsx`

**Interfaces:**
- Produces: `useSignalR(userId)` returns `{ liveNotifications }` — array of `Notification` items pushed from hub; `NotificationBell` shows unread count badge; `NotificationsPage` shows all notifications with mark-read

- [ ] **Step 1: Create useSignalR hook**

Create `src/Frontend/src/hooks/useSignalR.ts`:

```ts
import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { Notification } from '../types/api'

export function useSignalR(userId: string | undefined) {
  const [liveNotifications, setLiveNotifications] = useState<Notification[]>([])

  useEffect(() => {
    if (!userId) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/api/hubs/notifications?userId=${userId}`)
      .withAutomaticReconnect()
      .build()

    connection.on('notification', (notification: Notification) => {
      setLiveNotifications(prev => [notification, ...prev])
    })

    connection.start().catch(err => console.warn('SignalR connect error:', err))

    return () => { connection.stop() }
  }, [userId])

  return { liveNotifications }
}
```

- [ ] **Step 2: Replace NotificationBell with live count**

Replace `src/Frontend/src/components/NotificationBell.tsx`:

```tsx
import { Link } from 'react-router-dom'
import { useSignalR } from '../hooks/useSignalR'

export default function NotificationBell({ userId }: { userId: string }) {
  const { liveNotifications } = useSignalR(userId)
  const unread = liveNotifications.filter(n => !n.isRead).length

  return (
    <Link to="/notifications" className="relative">
      <span>🔔</span>
      {unread > 0 && (
        <span className="absolute -top-1 -right-1 bg-red-500 text-white text-xs rounded-full w-4 h-4 flex items-center justify-center">
          {unread}
        </span>
      )}
    </Link>
  )
}
```

- [ ] **Step 3: Build NotificationsPage**

Replace `src/Frontend/src/pages/NotificationsPage.tsx`:

```tsx
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { getNotifications, markNotificationRead } from '../api/notifications'

export default function NotificationsPage() {
  const { user } = useAuth()
  const userId = user!.profile.sub
  const qc = useQueryClient()

  const { data: notifications } = useQuery({
    queryKey: ['notifications', userId],
    queryFn: () => getNotifications(userId),
  })

  const markRead = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications', userId] }),
  })

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Notifications</h1>
      <div className="space-y-2">
        {notifications?.map(n => (
          <div key={n.id}
            className={`bg-white border rounded px-4 py-3 flex items-start gap-3 ${!n.isRead ? 'border-blue-400' : ''}`}>
            <div className="flex-1">
              <div className={`font-medium ${!n.isRead ? 'text-blue-800' : ''}`}>{n.title}</div>
              <div className="text-sm text-gray-600">{n.message}</div>
              <div className="text-xs text-gray-400 mt-1">{new Date(n.createdAt).toLocaleString()}</div>
            </div>
            {!n.isRead && (
              <button
                onClick={() => markRead.mutate(n.id)}
                className="text-xs text-blue-600 hover:text-blue-800 whitespace-nowrap"
              >
                Mark read
              </button>
            )}
          </div>
        ))}
        {notifications?.length === 0 && <p className="text-gray-500">No notifications yet.</p>}
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Commit**

```bash
git add src/Frontend/src/hooks/ src/Frontend/src/components/ src/Frontend/src/pages/NotificationsPage.tsx
git commit -m "feat(frontend): add SignalR notification bell and notifications page"
```

---

### Task 8: Docker build and full-stack verification

**Files:**
- Create: `src/Frontend/Dockerfile`
- Create: `src/Frontend/nginx.conf`

**Interfaces:**
- Produces: `docker compose up` starts the full stack; navigate to `http://localhost` and use all features

- [ ] **Step 1: Create nginx.conf**

Create `src/Frontend/nginx.conf`:

```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    # SPA fallback — all unmatched paths serve index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    gzip on;
    gzip_types text/plain text/css application/javascript application/json;
}
```

- [ ] **Step 2: Create Frontend Dockerfile**

Create `src/Frontend/Dockerfile`:

```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
```

- [ ] **Step 3: Verify the frontend Docker build**

```bash
docker build -t tcg-frontend src/Frontend/
```

Expected: Successfully built (takes ~60 seconds first run). No errors.

- [ ] **Step 4: Full-stack docker compose up**

```bash
docker compose up -d
```

Wait ~45 seconds for all services to start. Check health:

```bash
docker compose ps
```

Expected: All services showing `Up` or `healthy`. If any service shows `Exit`, check logs:

```bash
docker compose logs <service-name>
```

- [ ] **Step 5: End-to-end smoke test**

Open `http://localhost` in the browser.

Test the golden path:

1. Click **Sign In** → redirected to IdentityServer → register or log in
2. Navigate to **Cards** → search results appear from MeiliSearch
3. Navigate to **Portfolio** → add a card → card appears in list, summary updates
4. Navigate to **Marketplace** → click **+ List a Card** → fill form → listing appears on marketplace page
5. Open a second browser window, log in as a different user → buy the listing
6. Both windows should receive a notification (bell badge increments)
7. Navigate to **Notifications** → notification appears, "Mark read" works

- [ ] **Step 6: Commit**

```bash
git add src/Frontend/Dockerfile src/Frontend/nginx.conf
git commit -m "feat(frontend): add Docker build for nginx-served SPA"
```
