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
