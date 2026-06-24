import { Component, ReactNode } from 'react'
import Dither from './Dither'

class DitherErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }
  static getDerivedStateFromError() { return { failed: true } }
  render() {
    if (this.state.failed) return null
    return this.props.children
  }
}

export default function DitherBackground() {
  return (
    <DitherErrorBoundary>
      <Dither
        waveColor={[0.3, 0.1, 0.5]}
        waveSpeed={0.04}
        waveFrequency={3}
        waveAmplitude={0.3}
        colorNum={4}
        pixelSize={2}
        enableMouseInteraction={true}
      />
    </DitherErrorBoundary>
  )
}
