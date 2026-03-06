-- 00_create_table.sql
-- DuckDB table for DJIA prices.

CREATE TABLE IF NOT EXISTS dji_prices (
  Date DATE,
  Open DOUBLE,
  High DOUBLE,
  Low DOUBLE,
  Close DOUBLE,
  Volume BIGINT
);
