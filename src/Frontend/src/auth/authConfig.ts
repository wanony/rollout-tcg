export const IDENTITY_URL = window.location.origin

// Matches DemoSeedData.Email in SharedKernel — the fixed demo account seeded by IdentityService.
export const DEMO_EMAIL = 'demo@rollout.dev'

export const oidcSettings = {
  authority: IDENTITY_URL,
  client_id: 'spa',
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: window.location.origin,
  scope: 'openid profile email tcg.full',
  response_type: 'code',
  userStore: undefined as undefined, // populated by AuthProvider
}
