-- ════════════════════════════════════════════════════════════════
-- CivicOps Command — PostgreSQL Initialization Script
-- Runs once on first container start (before EF migrations).
-- Sets up extensions, TimescaleDB hypertables, and performance tuning.
-- ════════════════════════════════════════════════════════════════

-- ── Extensions ──────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";          -- fuzzy text search
CREATE EXTENSION IF NOT EXISTS "timescaledb" CASCADE;  -- GPS time-series

-- pgvector for AI embeddings (hotspot similarity). Optional.
CREATE EXTENSION IF NOT EXISTS "vector";

-- ── Performance tuning ──────────────────────────────────────────
ALTER SYSTEM SET shared_buffers = '256MB';
ALTER SYSTEM SET effective_cache_size = '1GB';
ALTER SYSTEM SET work_mem = '16MB';
ALTER SYSTEM SET maintenance_work_mem = '128MB';
ALTER SYSTEM SET random_page_cost = 1.1;          -- SSD-optimised
ALTER SYSTEM SET max_connections = 100;

-- ── NOTE ────────────────────────────────────────────────────────
-- The tables themselves are created by EF Core migrations on startup.
-- After migrations run, the following hypertable conversion should be
-- applied. This is handled by a post-migration hook, or run manually:
--
--   SELECT create_hypertable('vehicle_gps_events', 'recorded_at',
--       chunk_time_interval => INTERVAL '1 day',
--       if_not_exists => TRUE,
--       migrate_data => TRUE);
--
--   -- Retention policy: keep raw GPS for 90 days (Professional tier)
--   SELECT add_retention_policy('vehicle_gps_events',
--       INTERVAL '90 days', if_not_exists => TRUE);
--
--   -- Continuous aggregate for hourly fleet stats
--   CREATE MATERIALIZED VIEW IF NOT EXISTS gps_hourly_stats
--   WITH (timescaledb.continuous) AS
--   SELECT
--       time_bucket('1 hour', recorded_at) AS bucket,
--       vehicle_id,
--       tenant_id,
--       AVG(speed_kmh) AS avg_speed,
--       MAX(speed_kmh) AS max_speed,
--       COUNT(*) AS ping_count
--   FROM vehicle_gps_events
--   GROUP BY bucket, vehicle_id, tenant_id;

-- Confirm setup
DO $$
BEGIN
    RAISE NOTICE 'CivicOps database initialized with TimescaleDB, pg_trgm, pgvector.';
END $$;
