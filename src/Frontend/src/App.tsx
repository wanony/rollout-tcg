import { Routes, Route, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useEffect, ReactNode, useState, useMemo, useCallback } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
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
import ScrollToTopFAB from './components/ScrollToTopFAB'
import { DitherColorContext } from './components/DitherColorContext'

function RequireAuth({ children }: { children: ReactNode }) {
  const { user, isLoading } = useAuth()
  const navigate = useNavigate()
  useEffect(() => { if (!isLoading && !user) navigate('/login', { replace: true }) }, [user, isLoading, navigate])
  if (isLoading) return (
    <div className="flex h-40 items-center justify-center text-slate-400">Loading…</div>
  )
  if (!user) return null
  return <>{children}</>
}

// Route → dither color (RGB 0-1). Dark, saturated hues that read clearly on slate-950.
const ROUTE_COLORS: Record<string, [number, number, number]> = {
  '/cards':         [0.03, 0.10, 0.30],
  '/portfolio':     [0.03, 0.22, 0.10],
  '/marketplace':   [0.28, 0.14, 0.02],
  '/notifications': [0.04, 0.17, 0.22],
}
const DEFAULT_COLOR: [number, number, number] = [0.03, 0.06, 0.18]

export default function App() {
  const { user } = useAuth()
  const location = useLocation()
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [pageCommands, setPageCommands] = useState<PaletteCommand[]>([])
  const [ditherOverride, setDitherOverride] = useState<[number, number, number] | null>(null)

  const routeColor = useMemo(() => {
    const match = Object.keys(ROUTE_COLORS).find(r => location.pathname.startsWith(r))
    return match ? ROUTE_COLORS[match] : DEFAULT_COLOR
  }, [location.pathname])

  const ditherColor = ditherOverride ?? routeColor
  const setOverride = useCallback((c: [number, number, number] | null) => setDitherOverride(c), [])

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
        <DitherBackground waveColor={ditherColor} />
      </div>

      <div className="relative z-10 min-h-screen">
        <Navbar />
        <DitherColorContext.Provider value={setOverride}>
        <PageCommandsContext.Provider value={setPageCommands}>
          <main className="mx-auto w-full max-w-6xl px-3 pt-20 pb-4 sm:px-6 sm:pb-6">
            <AnimatePresence mode="wait" initial={false}>
              <motion.div
                key={location.pathname}
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.18, ease: 'easeInOut' }}
              >
                <Routes location={location}>
                  <Route path="/" element={<Navigate to="/cards" replace />} />
                  <Route path="/callback" element={<CallbackPage />} />
                  <Route path="/login" element={<LoginPage />} />
                  <Route path="/cards" element={<CardsPage />} />
                  <Route path="/portfolio" element={<RequireAuth><PortfolioPage /></RequireAuth>} />
                  <Route path="/marketplace" element={<RequireAuth><MarketplacePage /></RequireAuth>} />
                  <Route path="/marketplace/new" element={<RequireAuth><NewListingPage /></RequireAuth>} />
                  <Route path="/notifications" element={<RequireAuth><NotificationsPage /></RequireAuth>} />
                </Routes>
              </motion.div>
            </AnimatePresence>
          </main>
        </PageCommandsContext.Provider>
        </DitherColorContext.Provider>
      </div>

      <ScrollToTopFAB />

      <CommandPalette
        open={paletteOpen}
        onClose={() => setPaletteOpen(false)}
        pageCommands={pageCommands}
      />
    </div>
  )
}
