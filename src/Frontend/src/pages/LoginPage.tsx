import { useAuth } from '../auth/useAuth'

export default function LoginPage() {
  const { login } = useAuth()
  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 text-center">
      <div className="text-5xl">✦</div>
      <h1 className="text-3xl font-bold text-slate-100">Rollout TCG</h1>
      <p className="text-slate-400">Sign in to manage your collection and trade cards.</p>
      <button onClick={login}
        className="mt-2 rounded-xl bg-blue-600 px-8 py-3 font-semibold text-white transition-colors hover:bg-blue-500 active:scale-95">
        Sign In
      </button>
    </div>
  )
}
