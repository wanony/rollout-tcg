CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;

CREATE TABLE IF NOT EXISTS price_snapshots (
    card_id     UUID          NOT NULL,
    price       NUMERIC(12,4) NOT NULL,
    currency    VARCHAR(3)    NOT NULL DEFAULT 'USD',
    source      VARCHAR(50)   NOT NULL,
    recorded_at TIMESTAMPTZ   NOT NULL
);

SELECT create_hypertable(
    'price_snapshots',
    'recorded_at',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS ix_price_snapshots_card
    ON price_snapshots(card_id, recorded_at DESC);
