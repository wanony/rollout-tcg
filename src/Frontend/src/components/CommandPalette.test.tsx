import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'

// Mock glimm so tests don't need WebGL
vi.mock('glimm/react', () => ({
  GlimmProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useGlimm: () => ({ sweep: (fn: () => void) => fn() }),
}))

import CommandPalette from './CommandPalette'

function Wrapper({ children }: { children: React.ReactNode }) {
  return <MemoryRouter>{children}</MemoryRouter>
}

describe('CommandPalette', () => {
  it('renders nothing when closed', () => {
    render(<CommandPalette open={false} onClose={vi.fn()} />, { wrapper: Wrapper })
    expect(screen.queryByPlaceholderText('Type a command or search…')).toBeNull()
  })

  it('renders input when open', () => {
    render(<CommandPalette open={true} onClose={vi.fn()} />, { wrapper: Wrapper })
    expect(screen.getByPlaceholderText('Type a command or search…')).toBeInTheDocument()
  })

  it('calls action when a page command item is selected', async () => {
    const action = vi.fn()
    const commands = [{ id: 'test', label: 'Test Action', group: 'Actions', action }]
    render(
      <CommandPalette open={true} onClose={vi.fn()} pageCommands={commands} />,
      { wrapper: Wrapper },
    )
    await userEvent.click(screen.getByText('Test Action'))
    expect(action).toHaveBeenCalled()
  })
})
