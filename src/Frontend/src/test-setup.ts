import '@testing-library/jest-dom'

// cmdk (and other Radix UI libs) use ResizeObserver which jsdom doesn't provide
if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
}

// cmdk calls scrollIntoView on items during keyboard/pointer interactions; jsdom doesn't implement it
if (typeof window !== 'undefined' && !window.HTMLElement.prototype.scrollIntoView) {
  window.HTMLElement.prototype.scrollIntoView = function () {}
}
