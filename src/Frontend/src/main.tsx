import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { GlimmProvider } from 'glimm/react'
import { AuthProvider } from './auth/AuthProvider'
import App from './App'
import './index.css'

const queryClient = new QueryClient()

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <GlimmProvider palette="prism" sweepMs={900} outroMs={600}>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <App />
          </AuthProvider>
        </QueryClientProvider>
      </GlimmProvider>
    </BrowserRouter>
  </React.StrictMode>,
)
