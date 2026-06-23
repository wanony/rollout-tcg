import { useAuth } from '../auth/useAuth'

export default function LoginPage() {
  const { login } = useAuth()
  return (
    <div className="flex flex-col items-center justify-center min-h-[60vh] gap-4">
      <h1 className="text-3xl font-bold">Rollout TCG</h1>
      <p className="text-gray-600">Sign in to manage your collection and trade cards.</p>
      <button
        onClick={login}
        className="px-6 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
      >
        Sign In
      </button>
    </div>
  )
}
