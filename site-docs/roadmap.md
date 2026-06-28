# Roadmap

## Done

- Card catalog powered by TCGdex — free API, no key required, 589+ cards with images
- Real price data in card details — Cardmarket EUR (30-day trend, 7-day avg, today) and TCGplayer USD
- Filter by type, rarity, set, or artist name
- Holographic card tilt + foil effect
- Dither background that reacts to type filter (Fire = red tones, Water = blue, etc.)
- Portfolio with clickable entries that open full card detail
- Real-time notifications via SignalR
- Marketplace listings and offers
- OAuth2 login with PKCE
- Full observability stack (Grafana, Tempo, Prometheus, Loki)
- Docker Compose — one command to run everything locally

---

## Up next

### Offer flow in the UI
The backend already supports making and accepting offers. The frontend just needs the UI wired up — right now you can only accept an offer via the API directly.

### Demo data
Add a startup seed that creates `demo@rollout.dev` with a pre-populated portfolio and some active marketplace listings, so there's something to look at without having to add cards manually.

### JWT validation at the gateway
Right now the API Gateway forwards tokens through and each service validates them independently. Moving validation to the gateway means one place to enforce it.

---

## Ideas / longer term

### Wishlist + auto-matching
Users keep a wishlist of cards they want. When a matching listing appears on the marketplace, they get a notification. Could also do group-level matching — "someone in your group has a card on your wishlist".

### Pack opening
Virtual pack purchases using platform credits. Randomised card pulls weighted by rarity. Animated card reveal that adds them straight to your portfolio.

### Discord bot
Link a Discord server to a Rollout group. Bot imports server members automatically. Slash commands for price checks, wishlist lookup, portfolio summary. Notifications sent to Discord when a match or sale happens.

### Browser extension
Chrome/Firefox extension that overlays Rollout data on eBay, TCGPlayer, and Cardmarket listing pages — shows if you already own the card, what you paid, and compares the listing price against the Rollout pricing data.

### Kubernetes
Helm charts for all services so the whole thing can run on a real cluster with proper resource limits, rolling updates, and secret management.

### Price prediction
Use the TimescaleDB price history to train a simple model that predicts next-day card prices. Would expose a `GET /prices/{cardId}/forecast` endpoint.
