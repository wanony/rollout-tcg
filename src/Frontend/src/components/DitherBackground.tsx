import { Component, ReactNode } from 'react'
import Dither, { DitherProps } from './Dither'

class DitherErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }
  static getDerivedStateFromError() { return { failed: true } }
  render() {
    if (this.state.failed) return null
    return this.props.children
  }
}

export default function DitherBackground({ waveColor }: Pick<DitherProps, 'waveColor'>) {
  return (
    <DitherErrorBoundary>
      <Dither
        waveColor={waveColor}
        waveSpeed={0.04}
        waveFrequency={3}
        waveAmplitude={0.25}
        colorNum={8}
        pixelSize={2}
        enableMouseInteraction={true}
      />
    </DitherErrorBoundary>
  )
}
