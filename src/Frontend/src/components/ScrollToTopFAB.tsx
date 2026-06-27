import { useEffect, useState } from 'react'
import { ArrowUp } from 'lucide-react'

export default function ScrollToTopFAB() {
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    const onScroll = () => setVisible(window.scrollY > 400)
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  if (!visible) return null

  return (
    <button
      onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
      className="fixed bottom-6 right-6 z-50 flex h-11 w-11 items-center justify-center rounded-full border border-white/10 bg-slate-800/90 text-slate-300 shadow-lg backdrop-blur-sm transition-all hover:bg-slate-700 hover:text-white active:scale-95"
      aria-label="Scroll to top"
    >
      <ArrowUp size={18} />
    </button>
  )
}
