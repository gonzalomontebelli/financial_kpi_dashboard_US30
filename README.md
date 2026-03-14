# QUANTUM_US30 — Systematic Trading Research & Performance Analysis
### US30 (Dow Jones Industrial Average) · M5 · 2017–2026

---

> **Historical Backtest Summary** · $4,000 → $285,080 · 9.17 Years · 3,406 Trades · CAGR 59.24% · 9 Profitable Years

---

## Overview

QUANTUM_US30 is a dual-strategy systematic intraday trading framework applied to the US30 index (Dow Jones Industrial Average) on the 5-minute timeframe.

The system combines a London session breakout structure with a New York session range-reversal framework. Execution is governed by time-window constraints, trend filters, dynamic risk allocation, and event-based trading restrictions.

The strategy was backtested in cTrader from **January 3, 2017 to March 5, 2026**, covering multiple market regimes including:

- the COVID volatility shock (2020)  
- the global tightening cycle (2022)  
- sustained high-volatility environments through 2025  

Across the tested sample, the system produced positive annual results in each calendar year.

This repository documents the research workflow, performance evaluation, risk metrics, and stress testing infrastructure rather than presenting a system for commercialization or deployment.

---

## Repository Structure


```
QUANTUM_US30/
├── README.md                          ← This file
│
├── reports/
│   ├── 01_performance_report.md       ← Full backtest performance metrics
│   ├── 02_risk_analysis.md            ← Drawdown, Sharpe, Sortino, Calmar
│   ├── 03_montecarlo_simulation.md    ← 5,000-path Monte Carlo (4 scenarios)
│   ├── 04_execution_cost_analysis.md. ← Spread, Slippage & Execution Frictions
|   ├── 05_detailed_metrics.md         ← Index strategy behavior under time-based constraints
│
├── data/
│   ├── US30_dashboard.xlsx            ← Full information dash board (to download)
│   ├── equity_curve.csv               ← Trade-by-trade equity curve 
│   ├── annual_returns.csv             ← Year-by-year performance
│   ├── monthly_returns.csv            ← Month-by-month P&L breakdown
│   ├── strategy_breakdown.csv         ← Per-strategy statistics
│   ├── dji_data.cvs                   ← Historical price from Yahoo Finance: ^DJI open, close, high, low, volume
│   ├── NEWS.cvs                       ← Variable used to filter news days, sourced from Investing.com’s economic calendar
│   ├── log-toDownload.cvs             ← Backtest activity log (journal-style) recording the bot’s execution flow, including spread and slippage metrics for each trade
|
|
├── analysis/
│   ├── dashboard.html                 ← Interactive analytics dashboard (to download)
│   └── Report-ctrader.html            ← Original report from C-trade (to download)
│
├── Code/
│   ├── Python                         ← Build and analyis report
│   ├── SQL                            ← Clean - Analysis
|   ├── C#                             ← Showcase ( security and defense)
```


---

## Key Performance Summary

| Metric | Value |
|--------|-------|
| **Period** | Jan 2017 – Mar 2026 (9.17 years) |
| **Initial Capital** | $4,000 |
| **Final Equity** | $285,080 |
| **Net Profit** | $281,080 (+7,027%) |
| **CAGR** | 59.24% |
| **Total Trades** | 3,406 |
| **Win Rate** | 42.92% |
| **Profit Factor** | 1.591 |
| **Avg Trade** | +$82.53 |
| **Avg Win** | +$517.55 |
| **Avg Loss** | -$244.64 |
| **Win/Loss Ratio** | 2.12× |
| **Max Drawdown** | -19.40% (-$26,521) |
| **Sharpe Ratio** | 1.77 |
| **Sortino Ratio** | 3.14 |
| **Calmar Ratio** | 3.05 |
| **Profitable Years** | 10 / 10 |

These figures reflect historical backtest performance under defined assumptions and should not be interpreted as forward-looking expectations.

---

## Strategy Architecture

```text
┌─────────────────────────────────────────────────────────┐
│                   QUANTUM_US30 ENGINE                   │
├──────────────────────┬──────────────────────────────────┤
│  Strategy 1          │  Strategy 2                      │
│  LONDON_1B1S         │  RRL (Range Reversal London)     │
│                      │                                  │
│  Session: NY Open    │  Session: NY Afternoon           │
│  Window: 14:30–18:00 │  Window: 17:30–21:00 Madrid      │
│  SL: 61.5 pips       │  SL: 61.5 pips                   │
│  TP: 122.5 pips      │  TP: 122.5 pips                  │
│  Max 1 trade/day     │  Max 2 trades/day (1B + 1S)      │
└──────────────────────┴──────────────────────────────────┘
          │                         │
          └─────────────────────────┘
                       ▼

         ┌──────────────────────┐
         │   SHARED FILTERS     │
         │  • EMA-50 trend      │
         │  • Max spread 3.5pip │
         │  • NewsGuard Lite    │
         │  • No Wednesday      │
         │  • Force close 16:55 │
         └──────────────────────┘
```

---

## Annual Returns — Consistent Positive Years

| Year | Trades | Net P&L | Win Rate |
|------|--------|---------|----------|
| 2017 | 358 | +$769 | 50.6% |
| 2018 | 398 | +$179 | 41.0% |
| 2019 | 370 | +$1,160 | 44.1% |
| 2020 | 340 | +$494 | 40.3% |
| 2021 | 362 | +$2,406 | 46.4% |
| 2022 | 374 | +$8,612 | 40.4% |
| 2023 | 397 | +$25,351 | 41.1% |
| 2024 | 361 | +$48,582 | 41.3% |
| 2025 | 374 | +$154,488 | 41.4% |
| 2026* | 72 | +$39,039 | 44.4% |

\*Jan–Mar only.

Performance acceleration in later years reflects percent-risk compounding rather than structural changes in expectancy.

---

## Monte Carlo (Bootstrap Resampling · 5,000 Paths)

| Scenario | Median Final | P(Profit) | Median Max DD |
|----------|--------------|-----------|---------------|
| A — Base (Historical) | $284,984 | 100% | -70.8% |
| B — High Spread (+$50/trade) | $210,263 | 100% | -89.2% |
| C — Enlarged SL (-$30/loss) | $226,515 | 100% | -88.7% |
| D — Combined Stress | $69,630 | 90.1% | -174.3% |

Monte Carlo simulations represent alternative trade sequences and stress assumptions rather than expected outcomes.

---

## Interactive Dashboard

Open `analysis/dashboard.html` in any browser for full interactive analytics:

- Equity curve
- Drawdown curve
- Monthly heatmap
- Annual returns
- Day-of-week breakdown
- Trade distribution
- Monte Carlo fan chart
- Stress comparison

---

## Execution Assumptions & Limitations

- Backtest results depend on historical data quality and execution assumptions
- Real-world performance may differ due to slippage, spread variation, liquidity, and latency
- Percent-risk sizing produces nonlinear growth as equity scales
- Strategy performance varies across market regimes
- NewsGuard filtering requires periodic updates for forward use

This repository is presented strictly for research and portfolio demonstration purposes and does not constitute financial advice or an offer to manage capital.
