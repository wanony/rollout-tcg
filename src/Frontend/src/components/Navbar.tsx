import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import NotificationBell from './NotificationBell'

export default function Navbar() {
  const { user, login, logout } = useAuth()
  const location = useLocation()

  const navLinks = [
    { to: '/cards', label: 'Cards', icon: '🃏' },
    { to: '/portfolio', label: 'Portfolio', icon: '📦' },
    { to: '/marketplace', label: 'Market', icon: '🏪' },
  ]

  return (
    <nav className="sticky top-0 z-50 border-b border-slate-800/60 bg-slate-950/80 backdrop-blur-md">
      <div className="mx-auto flex h-14 max-w-6xl items-center gap-1 px-3 sm:gap-3 sm:px-6">
        {/* Logo */}
        <Link to="/cards" className="mr-2 flex items-center gap-2 sm:mr-4">
          <span className="text-xl">✦</span>
          <span className="hidden font-bold tracking-tight text-slate-100 sm:block">Rollout TCG</span>
        </Link>

        {/* Nav links */}
        {navLinks.map(link => (
          <Link
            key={link.to}
            to={link.to}
            className={`flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-sm font-medium transition-colors sm:px-3
              ${location.pathname.startsWith(link.to)
                ? 'bg-violet-500/20 text-violet-300'
                : 'text-slate-400 hover:bg-slate-800/60 hover:text-slate-100'}`}
          >
            <span>{link.icon}</span>
            <span className="hidden sm:block">{link.label}</span>
          </Link>
        ))}

        {/* Right side */}
        <div className="ml-auto flex items-center gap-2">
          {user ? (
            <>
              <NotificationBell userId={user.profile.sub} />
              <span className="hidden max-w-[120px] truncate text-xs text-slate-400 sm:block">
                {user.profile.email}
              </span>
              <button
                onClick={logout}
                className="rounded-lg px-2.5 py-1.5 text-xs font-medium text-slate-400 transition-colors hover:bg-slate-800/60 hover:text-slate-100"
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
    </nav>
  )
}
