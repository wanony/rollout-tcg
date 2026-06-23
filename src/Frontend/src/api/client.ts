import axios from 'axios'

// All requests go through /api — Vite dev proxy forwards to ApiGateway
export const api = axios.create({ baseURL: '/api' })

// Set Authorization header from oidc-client-ts stored user
export function setAuthToken(token: string | null) {
  if (token) {
    api.defaults.headers.common['Authorization'] = `Bearer ${token}`
  } else {
    delete api.defaults.headers.common['Authorization']
  }
}
