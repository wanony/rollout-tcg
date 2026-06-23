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
