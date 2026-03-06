-- 01_enrich_prices.sql
-- Works on DuckDB (and is mostly Postgres-compatible).
-- Assumes a table called dji_prices exists.

CREATE OR REPLACE VIEW dji_enriched AS
SELECT
  Date::DATE AS date,
  Open::DOUBLE AS open,
  High::DOUBLE AS high,
  Low::DOUBLE AS low,
  Close::DOUBLE AS close,
  Volume::BIGINT AS volume,
  (Close / LAG(Close) OVER (ORDER BY Date) - 1) AS daily_return,
  LN(Close / LAG(Close) OVER (ORDER BY Date)) AS log_return,
  MAX(Close) OVER (ORDER BY Date ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_max_close,
  (Close / MAX(Close) OVER (ORDER BY Date ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) - 1) AS drawdown
FROM dji_prices
ORDER BY Date;
