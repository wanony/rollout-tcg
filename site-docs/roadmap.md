# Roadmap

Rollout TCG v1 is feature-complete for portfolio tracking, marketplace, real-time notifications, full-text search, price history, and observability. The items below represent planned additions.

## In progress / near-term

### Graph library migration
Replace `lightweight-charts` (TradingView — watermark on free tier) with [Recharts](https://recharts.org/), a pure React, MIT-licensed charting library.

### Pricing data alignment
The Pricing service currently seeds synthetic price history using internal card GUIDs. Planned: align seed data with real pokemontcg.io card IDs and use TCGPlayer market prices as the base value, generating realistic history from those anchors.

### Demo user pre-seed
A hosted service that creates `demo@rollout.dev` at startup with a pre-populated portfolio (real pokemontcg.io card IDs) and active marketplace listings, so the site is never empty for a first-time visitor.

---

## Hirability — production engineering features

Each item below demonstrates a distinct production engineering skill.

### Kubernetes (Helm)
Helm charts for all 7 services + infrastructure components. Demonstrates production-grade deployment: resource limits, health check probes, rolling updates, secret management.

### ML.NET price prediction
An ML.NET regression model trained on the TimescaleDB price history to predict a card's next-day price. Exposes a `GET /prices/{cardId}/forecast` endpoint. Demonstrates ML integration in a .NET service.

### GraphQL endpoint
A GraphQL API alongside the existing REST endpoints, using Hot Chocolate. Exposes cards, portfolio, and marketplace data with proper type safety and N+1 protection. Demonstrates API breadth.

### Unleash feature flags (self-hosted)
Self-hosted Unleash server for feature gating. Demonstrates production deployment patterns — gradual rollout, A/B testing, kill switches — without a SaaS dependency.

### CQRS read models
Separate read-optimised projections for portfolio summaries and marketplace stats. Demonstrates CQRS pattern and event-sourced read models.

### Infrastructure as Code (Pulumi / Terraform)
Cloud provisioning for the full stack on a cloud provider. Demonstrates IaC skills alongside the existing Helm charts.

---

## Social layer

### Groups
User-created groups (friend groups, server circles) with privacy controls (private, invite-only, public). Group members can view each other's collections.

### Discord integration
- Link a Discord server to a Rollout group
- Bot auto-imports Discord server members
- Slash commands: price check, wishlist lookup, portfolio summary
- Notifications delivered to Discord when a match is found

### Direct messaging
DMs between users and group chat for trade negotiation.

---

## Wishlist & auto-matching

Users maintain wishlists of cards they want. When a matching listing appears, the system notifies the wishing user. Matching runs event-driven: `ListingCreatedEvent` triggers a wishlist check.

Extensions:
- Group-level matching: "someone in your group has a card you want"
- Discord notification when a match is found

---

## Browser extension

Chrome + Firefox WebExtensions (single codebase) that overlays Rollout data on TCG marketplace sites (eBay, TCGPlayer, Cardmarket):
- "You own 3 of these — current value $X" shown inline on listing pages
- One-click add to wishlist or portfolio
- Price comparison against Rollout's Pricing service
- OAuth2 token stored in `chrome.storage.local`

---

## Gamification

### Pack opening (v2)
Virtual pack purchases using platform credits. Randomised card pulls with rarity weighting per set. Animated reveal UX. Cards automatically added to portfolio on open.

---

## Technical debt

- [ ] Outbox pattern for Marketplace events (current: direct publish, no at-least-once guarantee)
- [ ] JWT validation at the API Gateway level (current: gateway forwards tokens, validation happens per-service)
- [ ] Persisted signing credential for IdentityServer (current: developer signing key, regenerates on volume wipe)
