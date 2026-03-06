-- 02_kpis.sql
-- KPI tables for the dashboard.

-- Rolling volatility (annualized)
CREATE OR REPLACE VIEW dji_rolling_vol AS
SELECT
  date,
  close,
  daily_return,
  log_return,
  drawdown,
  STDDEV_SAMP(log_return) OVER (ORDER BY date ROWS BETWEEN 19 PRECEDING AND CURRENT ROW) * SQRT(252) AS vol_20d_ann,
  STDDEV_SAMP(log_return) OVER (ORDER BY date ROWS BETWEEN 59 PRECEDING AND CURRENT ROW) * SQRT(252) AS vol_60d_ann
FROM dji_enriched;

-- Monthly returns
CREATE OR REPLACE VIEW dji_monthly_returns AS
SELECT
  DATE_TRUNC('month', date) AS month,
  (EXP(SUM(log_return)) - 1) AS month_return
FROM dji_enriched
WHERE log_return IS NOT NULL
GROUP BY 1
ORDER BY 1;

-- Yearly returns
CREATE OR REPLACE VIEW dji_yearly_returns AS
SELECT
  DATE_TRUNC('year', date) AS year,
  (EXP(SUM(log_return)) - 1) AS year_return
FROM dji_enriched
WHERE log_return IS NOT NULL
GROUP BY 1
ORDER BY 1;

-- Summary KPIs (single row)
CREATE OR REPLACE VIEW dji_kpi_summary AS
WITH base AS (
  SELECT
    AVG(daily_return) AS avg_daily_return,
    STDDEV_SAMP(daily_return) AS daily_return_std,
    AVG(log_return) AS avg_log_return,
    STDDEV_SAMP(log_return) AS log_return_std,
    MIN(drawdown) AS max_drawdown
  FROM dji_enriched
  WHERE daily_return IS NOT NULL
),
last_day AS (
  SELECT *
  FROM dji_rolling_vol
  QUALIFY ROW_NUMBER() OVER (ORDER BY date DESC) = 1
),
ytd AS (
  SELECT
    (EXP(SUM(log_return)) - 1) AS ytd_return
  FROM dji_enriched
  WHERE date >= DATE_TRUNC('year', (SELECT date FROM last_day))
    AND log_return IS NOT NULL
),
one_year AS (
  SELECT
    (EXP(SUM(log_return)) - 1) AS one_year_return
  FROM dji_enriched
  WHERE date >= (SELECT date FROM last_day) - INTERVAL 1 YEAR
    AND log_return IS NOT NULL
)
SELECT
  (SELECT date FROM last_day) AS as_of_date,
  (SELECT close FROM last_day) AS last_close,
  (SELECT daily_return FROM last_day) AS last_daily_return,
  (SELECT vol_20d_ann FROM last_day) AS vol_20d_ann,
  (SELECT vol_60d_ann FROM last_day) AS vol_60d_ann,
  ytd.ytd_return,
  one_year.one_year_return,
  base.avg_daily_return,
  base.daily_return_std,
  (base.avg_daily_return / NULLIF(base.daily_return_std, 0)) * SQRT(252) AS sharpe_simple_ann,
  base.max_drawdown
FROM base, ytd, one_year;
