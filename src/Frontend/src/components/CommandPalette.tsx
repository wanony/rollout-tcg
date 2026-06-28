import { createContext, Dispatch, SetStateAction, useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { Command } from 'cmdk'
import { useNavigate } from 'react-router-dom'

export interface PaletteCommand {
  id: string
  label: string
  group: string
  action: () => void
}

// Pages set contextual commands via this context
export const PageCommandsContext = createContext<Dispatch<SetStateAction<PaletteCommand[]>>>(() => {})

interface CommandPaletteProps {
  open: boolean
  onClose: () => void
  pageCommands?: PaletteCommand[]
}

export default function CommandPalette({ open, onClose, pageCommands = [] }: CommandPaletteProps) {
  const navigate = useNavigate()
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (open) setTimeout(() => inputRef.current?.focus(), 0)
  }, [open])

  useEffect(() => {
    if (!open) return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.stopImmediatePropagation()
        onClose()
      }
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [open, onClose])

  if (!open) return null

  const navCommands: PaletteCommand[] = [
    { id: 'nav-cards',       label: 'Go to Cards',       group: 'Navigate', action: () => { navigate('/cards');       onClose() } },
    { id: 'nav-portfolio',   label: 'Go to Portfolio',   group: 'Navigate', action: () => { navigate('/portfolio');   onClose() } },
    { id: 'nav-marketplace', label: 'Go to Marketplace', group: 'Navigate', action: () => { navigate('/marketplace'); onClose() } },
  ]

  const allGroups = [
    { heading: 'Navigate', commands: navCommands },
    ...(pageCommands.length > 0
      ? [{ heading: 'Actions', commands: pageCommands }]
      : []),
  ]

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-start justify-center pt-[20vh]">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-slate-950/60 backdrop-blur-sm"
        onClick={onClose}
      />
      {/* Panel */}
      <Command
        className="relative w-full max-w-lg rounded-2xl border border-slate-700/60 bg-slate-950/95 shadow-2xl overflow-hidden"
        loop
      >
          <div className="border-b border-slate-700/50 px-4 flex items-center justify-between">
            <Command.Input
              ref={inputRef}
              placeholder="Type a command or search…"
              className="w-full bg-transparent py-3.5 text-sm text-slate-100 placeholder-slate-500 outline-none"
            />
            <span className="ml-3 shrink-0 rounded border border-slate-700 px-1.5 py-0.5 text-xs text-slate-500 font-mono">⌘K</span>
          </div>
          <Command.List className="max-h-72 overflow-y-auto py-2">
            <Command.Empty className="py-8 text-center text-sm text-slate-500">
              No results.
            </Command.Empty>
            {allGroups.map(({ heading, commands }) => (
              <Command.Group
                key={heading}
                heading={heading}
                className="px-2 [&_[cmdk-group-heading]]:px-2 [&_[cmdk-group-heading]]:py-1.5 [&_[cmdk-group-heading]]:text-xs [&_[cmdk-group-heading]]:font-medium [&_[cmdk-group-heading]]:text-slate-500"
              >
                {commands.map(cmd => (
                  <Command.Item
                    key={cmd.id}
                    value={cmd.label}
                    onSelect={cmd.action}
                    className="flex cursor-pointer items-center gap-3 rounded-lg px-4 py-2.5 text-sm text-slate-300 aria-selected:bg-blue-500/20 aria-selected:text-blue-200"
                  >
                    {cmd.label}
                  </Command.Item>
                ))}
              </Command.Group>
            ))}
          </Command.List>
      </Command>
    </div>,
    document.body,
  )
}
