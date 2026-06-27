import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'

// Proxy identity server paths through Vite so auth works over HTTPS on any origin
// (Tailscale, localhost, etc.) without mixed-content blocks.
function idProxy() {
  return {
    target: 'http://localhost:5001',
    changeOrigin: true,
    configure: (proxy: import('vite').HttpProxy.Server) => {
      proxy.on('proxyReq', (proxyReq, req) => {
        proxyReq.setHeader('x-forwarded-proto', 'https')
        proxyReq.setHeader('x-forwarded-host', req.headers.host ?? 'localhost:3000')
      })
    },
  }
}

export default defineConfig({
  plugins: [react(), basicSsl()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5010',
        changeOrigin: true,
        ws: true,
      },
      '/.well-known': idProxy(),
      '/connect': idProxy(),
      '/Account': idProxy(),
      '/grants': idProxy(),
    },
  },
})
