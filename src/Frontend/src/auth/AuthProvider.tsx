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
