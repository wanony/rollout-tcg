import { Routes, Route, Navigate } from 'react-router-dom'
import { useEffect, ReactNode } from 'react'
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

  useEffect(() => {
    setAuthToken(user?.access_token ?? null)
  }, [user])

  return (
    <div className="relative min-h-screen bg-slate-950">
      {/* Dither background — fixed, behind everything */}
      <div className="fixed inset-0 z-0">
        <DitherBackground />
      </div>

      {/* Content layer */}
      <div className="relative z-10 flex min-h-screen flex-col">
        <Navbar />
        <main className="mx-auto w-full max-w-6xl flex-1 px-3 py-4 sm:px-6 sm:py-6">
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
    </div>
  )
}
