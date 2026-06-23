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
