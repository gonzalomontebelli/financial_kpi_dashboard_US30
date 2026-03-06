-- ============================================================
--  US30 BACKTESTING & DJI FINANCIAL ANALYSIS — SQL SCRIPTS
--  Dataset: Backtest_raw.csv + _dji_datos.xlsx + calendarioMacro.xlsx
--  Compatible: PostgreSQL 14+ / SQLite 3.35+ / DuckDB
-- ============================================================


-- ============================================================
-- SECTION 0 · TABLE DEFINITIONS
-- ============================================================

-- Tabla de trades del backtesting
CREATE TABLE IF NOT EXISTS trades (
    id              INTEGER PRIMARY KEY,
    strategy        TEXT,                          -- LONDON_1B1S | RRL
    entry_time      TIMESTAMP,
    close_time      TIMESTAMP,
    volume          NUMERIC(10,2),
    entry_price     NUMERIC(12,2),
    close_price     NUMERIC(12,2),
    pips            NUMERIC(10,1),
    net_pnl         NUMERIC(12,2),
    gross_pnl       NUMERIC(12,2),
    balance         NUMERIC(14,2)
);

-- Tabla de precios históricos DJI
CREATE TABLE IF NOT EXISTS dji_prices (
    trade_date  DATE PRIMARY KEY,
    open_price  NUMERIC(12,2),
    high_price  NUMERIC(12,2),
    low_price   NUMERIC(12,2),
    close_price NUMERIC(12,2),
    volume      BIGINT
);

-- Tabla de calendario macroeconómico
CREATE TABLE IF NOT EXISTS macro_events (
    event_id    SERIAL PRIMARY KEY,
    event_time  TIMESTAMP,
    event_name  TEXT,
    category    TEXT
);


-- ============================================================
-- SECTION 1 · COMPUTED COLUMNS VIEW (base enriched)
-- ============================================================

CREATE OR REPLACE VIEW v_trades_enriched AS
SELECT
    t.id,
    t.strategy,
    t.entry_time,
    t.close_time,
    DATE(t.entry_time)                                          AS trade_date,
    EXTRACT(YEAR  FROM t.entry_time)::INT                       AS trade_year,
    EXTRACT(MONTH FROM t.entry_time)::INT                       AS trade_month,
    EXTRACT(HOUR  FROM t.entry_time)::INT                       AS entry_hour,
    TO_CHAR(t.entry_time, 'Day')                                AS weekday,
    EXTRACT(DOW   FROM t.entry_time)::INT                       AS weekday_num,  -- 0=Sun,1=Mon...
    t.volume,
    t.entry_price,
    t.close_price,
    t.pips,
    t.net_pnl,
    t.gross_pnl,
    t.balance,

    -- Trade classification
    CASE WHEN t.net_pnl > 0 THEN 1 ELSE 0 END                  AS is_winner,
    CASE WHEN t.net_pnl > 0 THEN 'WIN' ELSE 'LOSS' END         AS result,

    -- Duration in minutes
    EXTRACT(EPOCH FROM (t.close_time - t.entry_time)) / 60.0   AS duration_min,

    -- Price return per trade (pips / entry_price)
    ROUND((t.close_price - t.entry_price) / t.entry_price * 100, 4) AS price_return_pct,

    -- DJI enrichment
    d.open_price                                                AS dji_open,
    d.high_price                                                AS dji_high,
    d.low_price                                                 AS dji_low,
    d.close_price                                               AS dji_close,
    ROUND((d.high_price - d.low_price) / d.close_price * 100, 4) AS dji_daily_range_pct,

    -- Daily return of DJI
    ROUND(
        (d.close_price - LAG(d.close_price) OVER (ORDER BY d.trade_date))
        / LAG(d.close_price) OVER (ORDER BY d.trade_date) * 100,
    4)                                                          AS dji_daily_return_pct,

    -- Macro events count on same day
    COALESCE(m.macro_count, 0)                                  AS macro_events_count,
    CASE WHEN COALESCE(m.macro_count, 0) > 0 THEN 1 ELSE 0 END AS is_macro_day

FROM trades t
LEFT JOIN dji_prices d  ON DATE(t.entry_time) = d.trade_date
LEFT JOIN (
    SELECT DATE(event_time) AS event_date, COUNT(*) AS macro_count
    FROM macro_events
    GROUP BY DATE(event_time)
) m ON DATE(t.entry_time) = m.event_date;


-- ============================================================
-- SECTION 2 · GLOBAL KPIs
-- ============================================================

-- 2.1 · Complete performance summary
SELECT
    COUNT(*)                                                    AS total_trades,
    SUM(is_winner)                                              AS winning_trades,
    COUNT(*) - SUM(is_winner)                                   AS losing_trades,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS win_rate_pct,
    ROUND(SUM(net_pnl), 2)                                      AS net_profit,
    ROUND(SUM(CASE WHEN net_pnl > 0 THEN net_pnl ELSE 0 END), 2) AS gross_profit,
    ROUND(ABS(SUM(CASE WHEN net_pnl < 0 THEN net_pnl ELSE 0 END)), 2) AS gross_loss,
    ROUND(
        SUM(CASE WHEN net_pnl > 0 THEN net_pnl ELSE 0 END)
        / NULLIF(ABS(SUM(CASE WHEN net_pnl < 0 THEN net_pnl ELSE 0 END)), 0),
    2)                                                          AS profit_factor,
    ROUND(AVG(CASE WHEN net_pnl > 0 THEN net_pnl END), 2)      AS avg_win,
    ROUND(AVG(CASE WHEN net_pnl < 0 THEN net_pnl END), 2)      AS avg_loss,
    ROUND(
        AVG(CASE WHEN net_pnl > 0 THEN net_pnl END)
        / NULLIF(ABS(AVG(CASE WHEN net_pnl < 0 THEN net_pnl END)), 0),
    2)                                                          AS risk_reward_ratio,
    MAX(net_pnl)                                                AS best_trade,
    MIN(net_pnl)                                                AS worst_trade,
    ROUND(AVG(duration_min), 2)                                 AS avg_duration_min,
    MIN(entry_time)                                             AS first_trade,
    MAX(close_time)                                             AS last_trade
FROM v_trades_enriched;


-- ============================================================
-- SECTION 3 · CLOSING PRICE MOVEMENTS & RETURNS
-- ============================================================

-- 3.1 · Daily DJI closing price with returns and rolling metrics
SELECT
    trade_date,
    close_price,
    LAG(close_price) OVER (ORDER BY trade_date)                 AS prev_close,
    ROUND(
        (close_price - LAG(close_price) OVER (ORDER BY trade_date))
        / LAG(close_price) OVER (ORDER BY trade_date) * 100,
    4)                                                          AS daily_return_pct,
    ROUND(
        AVG((close_price - LAG(close_price) OVER (ORDER BY trade_date))
            / LAG(close_price) OVER (ORDER BY trade_date) * 100)
        OVER (ORDER BY trade_date ROWS BETWEEN 19 PRECEDING AND CURRENT ROW),
    4)                                                          AS avg_return_20d,
    high_price - low_price                                      AS daily_range,
    ROUND((high_price - low_price) / close_price * 100, 4)     AS daily_range_pct,
    volume
FROM dji_prices
WHERE trade_date >= '2017-01-01'
ORDER BY trade_date;


-- 3.2 · Classify daily moves by magnitude
SELECT
    move_category,
    COUNT(*)                                                    AS trading_days,
    ROUND(AVG(daily_return_pct), 4)                             AS avg_return_pct,
    ROUND(SUM(daily_return_pct), 4)                             AS cumulative_return_pct
FROM (
    SELECT
        trade_date,
        ROUND(
            (close_price - LAG(close_price) OVER (ORDER BY trade_date))
            / LAG(close_price) OVER (ORDER BY trade_date) * 100,
        4) AS daily_return_pct,
        CASE
            WHEN ABS((close_price - LAG(close_price) OVER (ORDER BY trade_date))
                     / LAG(close_price) OVER (ORDER BY trade_date) * 100) < 0.5 THEN '< 0.5% move'
            WHEN ABS((close_price - LAG(close_price) OVER (ORDER BY trade_date))
                     / LAG(close_price) OVER (ORDER BY trade_date) * 100) < 1.0 THEN '0.5–1.0% move'
            WHEN ABS((close_price - LAG(close_price) OVER (ORDER BY trade_date))
                     / LAG(close_price) OVER (ORDER BY trade_date) * 100) < 2.0 THEN '1.0–2.0% move'
            ELSE '>2.0% move (high impact)'
        END AS move_category
    FROM dji_prices
    WHERE trade_date >= '2017-01-01'
) sub
WHERE daily_return_pct IS NOT NULL
GROUP BY move_category
ORDER BY MIN(ABS(daily_return_pct));


-- 3.3 · Cumulative compounded return on DJI (2017–2026)
WITH returns AS (
    SELECT
        trade_date,
        1 + (close_price - LAG(close_price) OVER (ORDER BY trade_date))
              / LAG(close_price) OVER (ORDER BY trade_date) AS daily_factor
    FROM dji_prices
    WHERE trade_date >= '2017-01-01'
),
compounded AS (
    SELECT
        trade_date,
        EXP(SUM(LN(NULLIF(daily_factor, 0)))
            OVER (ORDER BY trade_date)) AS cumulative_growth
    FROM returns
    WHERE daily_factor IS NOT NULL
)
SELECT
    trade_date,
    ROUND((cumulative_growth - 1) * 100, 4) AS cumulative_return_pct,
    ROUND(cumulative_growth, 6)              AS growth_factor
FROM compounded
ORDER BY trade_date;


-- ============================================================
-- SECTION 4 · VOLATILITY ANALYSIS
-- ============================================================

-- 4.1 · Rolling 20-day annualized volatility (standard method)
WITH daily_returns AS (
    SELECT
        trade_date,
        LN(close_price / LAG(close_price) OVER (ORDER BY trade_date)) AS log_return
    FROM dji_prices
    WHERE trade_date >= '2017-01-01'
)
SELECT
    trade_date,
    log_return,
    ROUND(
        STDDEV(log_return) OVER (
            ORDER BY trade_date
            ROWS BETWEEN 19 PRECEDING AND CURRENT ROW
        ) * SQRT(252) * 100,
    4) AS volatility_20d_annualized_pct,
    ROUND(
        STDDEV(log_return) OVER (
            ORDER BY trade_date
            ROWS BETWEEN 59 PRECEDING AND CURRENT ROW
        ) * SQRT(252) * 100,
    4) AS volatility_60d_annualized_pct
FROM daily_returns
WHERE log_return IS NOT NULL
ORDER BY trade_date;


-- 4.2 · Annual average volatility (risk regimes)
WITH daily_returns AS (
    SELECT
        EXTRACT(YEAR FROM trade_date)::INT AS yr,
        LN(close_price / LAG(close_price) OVER (ORDER BY trade_date)) AS log_return
    FROM dji_prices
    WHERE trade_date >= '2017-01-01'
)
SELECT
    yr                                                          AS year,
    ROUND(STDDEV(log_return) * SQRT(252) * 100, 2)             AS annual_volatility_pct,
    ROUND(AVG(ABS(log_return)) * 100, 4)                        AS avg_abs_daily_return_pct,
    ROUND(MAX(ABS(log_return)) * 100, 4)                        AS max_abs_daily_return_pct,
    CASE
        WHEN STDDEV(log_return) * SQRT(252) * 100 < 12 THEN 'Low Volatility'
        WHEN STDDEV(log_return) * SQRT(252) * 100 < 20 THEN 'Medium Volatility'
        ELSE 'High Volatility'
    END                                                         AS volatility_regime
FROM daily_returns
WHERE log_return IS NOT NULL
GROUP BY yr
ORDER BY yr;


-- 4.3 · Backtest performance segmented by DJI volatility quartile
WITH vol_calc AS (
    SELECT
        trade_date,
        NTILE(4) OVER (ORDER BY
            STDDEV(LN(close_price / LAG(close_price) OVER (ORDER BY trade_date)))
            OVER (ORDER BY trade_date ROWS BETWEEN 19 PRECEDING AND CURRENT ROW)
        ) AS vol_quartile
    FROM dji_prices
    WHERE trade_date >= '2017-01-01'
),
trades_with_vol AS (
    SELECT t.*, v.vol_quartile
    FROM v_trades_enriched t
    LEFT JOIN vol_calc v ON t.trade_date = v.trade_date
)
SELECT
    CASE vol_quartile
        WHEN 1 THEN 'Q1 — Low Volatility'
        WHEN 2 THEN 'Q2 — Moderate'
        WHEN 3 THEN 'Q3 — Elevated'
        WHEN 4 THEN 'Q4 — High Volatility'
        ELSE 'Unknown'
    END                                                         AS volatility_regime,
    COUNT(*)                                                    AS total_trades,
    SUM(is_winner)                                              AS wins,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS win_rate_pct,
    ROUND(SUM(net_pnl), 2)                                      AS net_profit,
    ROUND(AVG(net_pnl), 2)                                      AS avg_pnl_per_trade,
    ROUND(AVG(duration_min), 1)                                 AS avg_duration_min
FROM trades_with_vol
GROUP BY vol_quartile
ORDER BY vol_quartile;


-- ============================================================
-- SECTION 5 · MACRO EVENT CORRELATION
-- ============================================================

-- 5.1 · Strategy performance: news days vs quiet days
SELECT
    CASE is_macro_day
        WHEN 1 THEN 'Macro Event Day'
        ELSE 'No Macro Event'
    END                                                         AS market_condition,
    COUNT(*)                                                    AS total_trades,
    SUM(is_winner)                                              AS wins,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS win_rate_pct,
    ROUND(SUM(net_pnl), 2)                                      AS net_profit,
    ROUND(AVG(net_pnl), 2)                                      AS avg_pnl_per_trade,
    ROUND(AVG(duration_min), 1)                                 AS avg_duration_min,
    ROUND(AVG(dji_daily_range_pct), 4)                          AS avg_market_range_pct
FROM v_trades_enriched
GROUP BY is_macro_day
ORDER BY is_macro_day DESC;


-- 5.2 · Performance grouped by macro event category
SELECT
    COALESCE(me.category, 'No Event')                          AS event_category,
    COUNT(DISTINCT t.id)                                        AS total_trades,
    SUM(is_winner)                                              AS wins,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(DISTINCT t.id) * 100, 2) AS win_rate_pct,
    ROUND(SUM(t.net_pnl), 2)                                    AS net_profit,
    ROUND(AVG(t.net_pnl), 2)                                    AS avg_pnl_per_trade
FROM v_trades_enriched t
LEFT JOIN (
    SELECT DATE(event_time) AS event_date, category
    FROM macro_events
) me ON t.trade_date = me.event_date
GROUP BY COALESCE(me.category, 'No Event')
ORDER BY net_profit DESC;


-- 5.3 · DJI daily range on macro vs non-macro days
SELECT
    CASE WHEN m.event_date IS NOT NULL THEN 'Macro Day' ELSE 'No Event' END AS condition,
    COUNT(d.trade_date)                                         AS trading_days,
    ROUND(AVG(d.high_price - d.low_price), 2)                  AS avg_daily_range,
    ROUND(AVG((d.high_price - d.low_price) / d.close_price * 100), 4) AS avg_range_pct,
    ROUND(STDDEV((d.high_price - d.low_price) / d.close_price * 100), 4) AS stddev_range_pct
FROM dji_prices d
LEFT JOIN (
    SELECT DISTINCT DATE(event_time) AS event_date FROM macro_events
) m ON d.trade_date = m.event_date
WHERE d.trade_date >= '2017-01-01'
GROUP BY CASE WHEN m.event_date IS NOT NULL THEN 'Macro Day' ELSE 'No Event' END;


-- ============================================================
-- SECTION 6 · ANNUAL PERFORMANCE
-- ============================================================

-- 6.1 · Year-by-year backtesting results
SELECT
    trade_year                                                  AS year,
    COUNT(*)                                                    AS total_trades,
    SUM(is_winner)                                              AS wins,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS win_rate_pct,
    ROUND(SUM(net_pnl), 2)                                      AS net_profit,
    ROUND(AVG(CASE WHEN net_pnl > 0 THEN net_pnl END), 2)      AS avg_win,
    ROUND(AVG(CASE WHEN net_pnl < 0 THEN net_pnl END), 2)      AS avg_loss,
    ROUND(
        AVG(CASE WHEN net_pnl > 0 THEN net_pnl END)
        / NULLIF(ABS(AVG(CASE WHEN net_pnl < 0 THEN net_pnl END)), 0),
    2)                                                          AS risk_reward,
    MAX(net_pnl)                                                AS best_trade,
    MIN(net_pnl)                                                AS worst_trade,
    ROUND(AVG(duration_min), 1)                                 AS avg_duration_min,
    MAX(balance)                                                AS year_end_balance
FROM v_trades_enriched
GROUP BY trade_year
ORDER BY trade_year;


-- 6.2 · Compound growth — balance evolution
SELECT
    trade_year,
    trade_month,
    TO_CHAR(DATE_TRUNC('month', entry_time), 'YYYY-MM')         AS month_label,
    ROUND(MAX(balance), 2)                                      AS month_end_balance,
    ROUND(SUM(net_pnl), 2)                                      AS monthly_net_profit,
    COUNT(*)                                                    AS monthly_trades,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS monthly_win_rate_pct
FROM v_trades_enriched
GROUP BY trade_year, trade_month, DATE_TRUNC('month', entry_time)
ORDER BY trade_year, trade_month;


-- ============================================================
-- SECTION 7 · STRATEGY COMPARISON
-- ============================================================

-- 7.1 · Full breakdown by strategy
SELECT
    strategy,
    COUNT(*)                                                    AS total_trades,
    SUM(is_winner)                                              AS wins,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS win_rate_pct,
    ROUND(SUM(net_pnl), 2)                                      AS net_profit,
    ROUND(AVG(net_pnl), 2)                                      AS avg_pnl_per_trade,
    ROUND(AVG(CASE WHEN net_pnl > 0 THEN net_pnl END), 2)      AS avg_win,
    ROUND(AVG(CASE WHEN net_pnl < 0 THEN net_pnl END), 2)      AS avg_loss,
    ROUND(
        SUM(CASE WHEN net_pnl > 0 THEN net_pnl ELSE 0 END)
        / NULLIF(ABS(SUM(CASE WHEN net_pnl < 0 THEN net_pnl ELSE 0 END)), 0),
    2)                                                          AS profit_factor,
    ROUND(AVG(volume), 2)                                       AS avg_volume,
    ROUND(AVG(duration_min), 1)                                 AS avg_duration_min,
    MAX(net_pnl)                                                AS best_trade,
    MIN(net_pnl)                                                AS worst_trade
FROM v_trades_enriched
GROUP BY strategy
ORDER BY net_profit DESC;


-- 7.2 · Strategy performance by year
SELECT
    strategy,
    trade_year                                                  AS year,
    COUNT(*)                                                    AS trades,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS win_rate_pct,
    ROUND(SUM(net_pnl), 2)                                      AS net_profit
FROM v_trades_enriched
GROUP BY strategy, trade_year
ORDER BY strategy, trade_year;


-- ============================================================
-- SECTION 8 · TIME-OF-DAY & WEEKDAY ANALYSIS
-- ============================================================

-- 8.1 · Performance by entry hour
SELECT
    entry_hour                                                  AS hour_utc1,
    COUNT(*)                                                    AS trades,
    SUM(is_winner)                                              AS wins,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS win_rate_pct,
    ROUND(SUM(net_pnl), 2)                                      AS net_profit,
    ROUND(AVG(net_pnl), 2)                                      AS avg_pnl
FROM v_trades_enriched
GROUP BY entry_hour
ORDER BY entry_hour;


-- 8.2 · Performance by weekday
SELECT
    CASE weekday_num
        WHEN 1 THEN 'Monday'
        WHEN 2 THEN 'Tuesday'
        WHEN 3 THEN 'Wednesday'
        WHEN 4 THEN 'Thursday'
        WHEN 5 THEN 'Friday'
    END                                                         AS weekday,
    weekday_num,
    COUNT(*)                                                    AS trades,
    SUM(is_winner)                                              AS wins,
    ROUND(SUM(is_winner)::NUMERIC / COUNT(*) * 100, 2)         AS win_rate_pct,
    ROUND(SUM(net_pnl), 2)                                      AS net_profit,
    ROUND(AVG(net_pnl), 2)                                      AS avg_pnl,
    ROUND(AVG(duration_min), 1)                                 AS avg_duration_min
FROM v_trades_enriched
WHERE weekday_num BETWEEN 1 AND 5
GROUP BY weekday_num, weekday
ORDER BY weekday_num;


-- ============================================================
-- SECTION 9 · DRAWDOWN ANALYSIS
-- ============================================================

-- 9.1 · Maximum drawdown calculation (running peak to trough)
WITH balance_series AS (
    SELECT
        id,
        entry_time,
        balance,
        MAX(balance) OVER (ORDER BY entry_time
                           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_peak
    FROM v_trades_enriched
)
SELECT
    entry_time,
    balance,
    running_peak,
    ROUND(balance - running_peak, 2)                            AS drawdown_abs,
    ROUND((balance - running_peak) / NULLIF(running_peak, 0) * 100, 4) AS drawdown_pct
FROM balance_series
ORDER BY drawdown_pct  -- most negative first
LIMIT 20;


-- 9.2 · Drawdown summary stats
WITH balance_series AS (
    SELECT
        balance,
        MAX(balance) OVER (ORDER BY entry_time
                           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_peak
    FROM v_trades_enriched
),
dd AS (
    SELECT (balance - running_peak) / NULLIF(running_peak, 0) * 100 AS dd_pct
    FROM balance_series
)
SELECT
    ROUND(MIN(dd_pct), 4)                                       AS max_drawdown_pct,
    ROUND(AVG(dd_pct), 4)                                       AS avg_drawdown_pct,
    COUNT(CASE WHEN dd_pct < -5  THEN 1 END)                    AS periods_below_minus_5pct,
    COUNT(CASE WHEN dd_pct < -10 THEN 1 END)                    AS periods_below_minus_10pct,
    COUNT(CASE WHEN dd_pct < -15 THEN 1 END)                    AS periods_below_minus_15pct
FROM dd;


-- ============================================================
-- SECTION 10 · CONSECUTIVE TRADE STREAKS
-- ============================================================

-- 10.1 · Identify streak groups (consecutive wins/losses)
WITH streak_groups AS (
    SELECT
        id,
        entry_time,
        net_pnl,
        is_winner,
        is_winner - LAG(is_winner, 1, is_winner) OVER (ORDER BY entry_time) AS changed,
        SUM(
            CASE WHEN is_winner != LAG(is_winner, 1, is_winner) OVER (ORDER BY entry_time)
                 THEN 1 ELSE 0 END
        ) OVER (ORDER BY entry_time)                            AS streak_id
    FROM v_trades_enriched
),
streak_stats AS (
    SELECT
        streak_id,
        is_winner,
        COUNT(*)                                                AS streak_length,
        ROUND(SUM(net_pnl), 2)                                  AS streak_pnl,
        MIN(entry_time)                                         AS streak_start,
        MAX(entry_time)                                         AS streak_end
    FROM streak_groups
    GROUP BY streak_id, is_winner
)
SELECT
    CASE is_winner WHEN 1 THEN 'Winning' ELSE 'Losing' END     AS streak_type,
    MAX(streak_length)                                          AS max_consecutive,
    ROUND(AVG(streak_length), 2)                                AS avg_streak_length,
    MIN(streak_pnl)                                             AS worst_streak_pnl,
    MAX(streak_pnl)                                             AS best_streak_pnl
FROM streak_stats
GROUP BY is_winner;


-- ============================================================
-- SECTION 11 · CORRELATION: DJI RETURNS vs STRATEGY P&L
-- ============================================================

-- 11.1 · Pearson correlation: daily DJI return vs daily backtest P&L
WITH daily_agg AS (
    SELECT
        trade_date,
        SUM(net_pnl)                                            AS daily_pnl,
        AVG(dji_daily_return_pct)                               AS dji_return
    FROM v_trades_enriched
    WHERE dji_daily_return_pct IS NOT NULL
    GROUP BY trade_date
)
SELECT
    COUNT(*)                                                    AS trading_days,
    ROUND(CORR(dji_return, daily_pnl)::NUMERIC, 4)              AS pearson_correlation,
    CASE
        WHEN ABS(CORR(dji_return, daily_pnl)) < 0.2 THEN 'Very Weak / Independent'
        WHEN ABS(CORR(dji_return, daily_pnl)) < 0.4 THEN 'Weak'
        WHEN ABS(CORR(dji_return, daily_pnl)) < 0.6 THEN 'Moderate'
        WHEN ABS(CORR(dji_return, daily_pnl)) < 0.8 THEN 'Strong'
        ELSE 'Very Strong'
    END                                                         AS correlation_strength
FROM daily_agg;


-- 11.2 · Performance on positive vs negative DJI days
WITH dji_returns AS (
    SELECT
        trade_date,
        ROUND(
            (close_price - LAG(close_price) OVER (ORDER BY trade_date))
            / LAG(close_price) OVER (ORDER BY trade_date) * 100,
        4) AS dji_daily_return
    FROM dji_prices
    WHERE trade_date >= '2017-01-01'
)
SELECT
    CASE
        WHEN d.dji_daily_return > 1    THEN 'DJI Up >1%'
        WHEN d.dji_daily_return > 0    THEN 'DJI Up 0–1%'
        WHEN d.dji_daily_return > -1   THEN 'DJI Down 0–1%'
        ELSE                                'DJI Down >1%'
    END                                                         AS dji_move_bucket,
    COUNT(t.id)                                                 AS trades,
    ROUND(SUM(t.is_winner)::NUMERIC / COUNT(t.id) * 100, 2)    AS win_rate_pct,
    ROUND(SUM(t.net_pnl), 2)                                    AS net_profit,
    ROUND(AVG(t.net_pnl), 2)                                    AS avg_pnl
FROM v_trades_enriched t
JOIN dji_returns d ON t.trade_date = d.trade_date
WHERE d.dji_daily_return IS NOT NULL
GROUP BY dji_move_bucket
ORDER BY MIN(d.dji_daily_return) DESC;


-- ============================================================
-- SECTION 12 · TOP & BOTTOM TRADES
-- ============================================================

-- 12.1 · Top 10 winning trades
SELECT
    id, strategy, entry_time, close_time,
    volume, entry_price, close_price, pips, net_pnl,
    ROUND(duration_min, 1)  AS duration_min,
    macro_events_count, dji_daily_range_pct
FROM v_trades_enriched
ORDER BY net_pnl DESC
LIMIT 10;


-- 12.2 · Top 10 losing trades
SELECT
    id, strategy, entry_time, close_time,
    volume, entry_price, close_price, pips, net_pnl,
    ROUND(duration_min, 1)  AS duration_min,
    macro_events_count, dji_daily_range_pct
FROM v_trades_enriched
ORDER BY net_pnl ASC
LIMIT 10;


-- ============================================================
-- SECTION 13 · DUCKDB QUICK LOAD (if using DuckDB)
-- ============================================================
/*
-- For DuckDB: Load directly from files without ETL
INSTALL excel; LOAD excel;

CREATE OR REPLACE TABLE trades AS
SELECT
    id,
    strategy             AS strategy,
    CAST("entry_time" AS TIMESTAMP) AS entry_time,
    CAST("close_time" AS TIMESTAMP) AS close_time,
    volume, entry_price, close_price, pips, net_pnl, gross_pnl, balance
FROM read_csv('/path/to/Backtest_raw.csv', header=true, auto_detect=true);

CREATE OR REPLACE TABLE dji_prices AS
SELECT
    CAST(Date AS DATE)   AS trade_date,
    Open AS open_price, High AS high_price,
    Low  AS low_price,  Close AS close_price,
    Volume AS volume
FROM read_xlsx('/path/to/_dji_datos.xlsx');

CREATE OR REPLACE TABLE macro_events AS
SELECT
    CAST(Event_Madrid AS TIMESTAMP) AS event_time,
    Name AS event_name,
    Category AS category
FROM read_xlsx('/path/to/calendarioMacro_2017-2026.xlsx');

-- Then run all queries above normally.
*/
