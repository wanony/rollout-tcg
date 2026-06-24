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

function RequireAuth({ children }: { children: ReactNode }) {
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
