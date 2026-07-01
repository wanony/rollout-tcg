import { useNavigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import NotificationBell from './NotificationBell'

function GithubIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden>
      <path d="M12 0C5.37 0 0 5.37 0 12c0 5.3 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577v-2.165c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.09-.745.083-.729.083-.729 1.205.084 1.84 1.237 1.84 1.237 1.07 1.834 2.807 1.304 3.492.997.108-.775.418-1.305.762-1.605-2.665-.3-5.467-1.332-5.467-5.93 0-1.31.468-2.38 1.236-3.22-.124-.303-.535-1.523.117-3.176 0 0 1.008-.322 3.3 1.23A11.5 11.5 0 0 1 12 6.8c1.02.005 2.047.138 3.006.404 2.29-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.873.12 3.176.77.84 1.235 1.91 1.235 3.22 0 4.61-2.807 5.625-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576C20.566 21.797 24 17.3 24 12c0-6.63-5.37-12-12-12z"/>
    </svg>
  )
}

function Logo() {
  const navigate = useNavigate()
  return (
    <button
      onClick={() => navigate('/cards')}
      className="mr-2 flex items-center gap-2 sm:mr-4 text-left"
    >
      <img src="/favicon.ico" alt="" className="h-6 w-6" aria-hidden />
      <span className="hidden font-bold tracking-tight text-slate-100 sm:block">Rollout TCG</span>
    </button>
  )
}

function NavLink({ to, label }: { to: string; label: string }) {
  const navigate = useNavigate()
  const location = useLocation()
  const active = location.pathname.startsWith(to)
  return (
    <button
      onClick={() => navigate(to)}
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
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  return (
    <div className="fixed left-0 right-0 top-0 z-50 px-4 pt-3">
      <div className="rounded-2xl border border-white/[0.06] bg-slate-900/85 backdrop-blur-xl shadow-xl">
        <div className="flex h-14 w-full items-center gap-1 px-4 sm:gap-3">
          <Logo />

          <NavLink to="/cards" label="Cards" />
          <NavLink to="/portfolio" label="Portfolio" />
          <NavLink to="/marketplace" label="Market" />

          <div className="ml-auto flex items-center gap-2">
            <a
              href="https://github.com/wanony/rollout-tcg"
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center justify-center rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-white/10 hover:text-slate-100"
              aria-label="View source on GitHub"
            >
              <GithubIcon />
            </a>
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
                  Sign out
                </button>
              </>
            ) : (
              <button
                onClick={() => navigate('/login')}
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
