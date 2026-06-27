import { useNavigate, useLocation } from 'react-router-dom'
import { useGlimm } from 'glimm/react'
import { useAuth } from '../auth/useAuth'
import NotificationBell from './NotificationBell'

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

function NavLink({ to, label }: { to: string; label: string }) {
  const { sweep } = useGlimm()
  const navigate = useNavigate()
  const location = useLocation()
  const active = location.pathname.startsWith(to)
  return (
    <button
      onClick={() => sweep(() => navigate(to))}
      className={`rounded-lg px-2.5 py-1.5 text-sm font-medium transition-colors sm:px-3
        ${active
          ? 'bg-blue-500/20 text-blue-300'
          : 'text-slate-400 hover:bg-white/10 hover:text-slate-100'}`}
    >
      {label}
    </button>
  )
}

export default function Navbar() {
  const { user, login, logout } = useAuth()

  return (
    <div className="fixed left-0 right-0 top-0 z-50 px-4 pt-3">
      <div className="rounded-2xl border border-white/[0.06] bg-slate-900/85 backdrop-blur-xl shadow-xl">
        <div className="flex h-14 w-full items-center gap-1 px-4 sm:gap-3">
          <Logo />

          <NavLink to="/cards" label="Cards" />
          <NavLink to="/portfolio" label="Portfolio" />
          <NavLink to="/marketplace" label="Market" />

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
                className="rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-blue-500"
              >
                Sign in
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
