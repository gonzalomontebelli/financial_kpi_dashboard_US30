# QUANTUM_US30 — Systematic Trading Strategy
### US30 (Dow Jones Industrial Average) · M5 · 2017–2026

---

> **Backtest Results** · $4,000 → $285,080 · 9.17 Years · 3,406 Trades · CAGR 59.24% · **10/10 Profitable Years**

---

## Overview

QUANTUM_US30 is a dual-strategy systematic trading system designed for the US30 index (Dow Jones Industrial Average) on the 5-minute timeframe. It combines a London session breakout strategy with a New York session range reversal strategy, augmented by EMA trend filtering, dynamic risk sizing per calendar month and day-of-week, and a full economic event news guard (NewsGuard Lite).

The system was backtested on cTrader from **January 3, 2017 to March 5, 2026** — covering 9.17 years of live-quality tick data including the COVID crash (2020), Fed rate hike cycle (2022), and high-volatility regimes through 2025. The system produced a profit in **every single calendar year** over this period.

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
│   
│
├── data/
│   ├── US30_dashboard.xlsx            ← Full information dash board 
│   ├── equity_curve.csv               ← Trade-by-trade equity curve 
│   ├── annual_returns.csv             ← Year-by-year performance
│   ├── monthly_returns.csv            ← Month-by-month P&L breakdown
│   ├── strategy_breakdown.csv         ← Per-strategy statistics
│  
│   
│
├── analysis/
│   ├── dashboard.html                 ← Interactive analytics dashboard
│   └── Report-ctrader.html            ← Original report from C-trade 
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
| **Profitable Years** | **10 / 10 (100%)** |

---

## Strategy Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   QUANTUM_US30 ENGINE                    │
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
           └─────────┬───────────────┘
                     ▼
         ┌─────────────────────┐
         │   SHARED FILTERS     │
         │  • EMA-50 trend      │
         │  • Max spread 3.5pip │
         │  • NewsGuard Lite    │
         │  • No Wednesday      │
         │  • Force close 16:55 │
         └─────────────────────┘
```

---

## Annual Returns — 10 Consecutive Profitable Years

| Year | Trades | Net P&L | Win Rate | Notes |
|------|--------|---------|----------|-------|
| 2017 | 358 | **+$769** | 50.6% | Initial year, low sizing |
| 2018 | 398 | **+$179** | 41.0% | Tight vol compression |
| 2019 | 370 | **+$1,160** | 44.1% | Steady growth |
| 2020 | 340 | **+$494** | 40.3% | COVID protected by NewsGuard |
| 2021 | 362 | **+$2,406** | 46.4% | Post-COVID momentum |
| 2022 | 374 | **+$8,612** | 40.4% | Fed hike cycle — trending markets |
| 2023 | 397 | **+$25,351** | 41.1% | Compounding accelerates |
| 2024 | 361 | **+$48,582** | 41.3% | High-vol environment |
| 2025 | 374 | **+$154,488** | 41.4% | Peak compounding year |
| 2026* | 72 | **+$39,039** | 44.4% | Jan–Mar only |

*The dramatic increase in annual P&L from 2022 onwards reflects position sizing scaling with the growing account balance (percent-risk model). Win rate remains stable at ~41–44% throughout.*

---

## Monte Carlo (4 Scenarios · 5,000 Paths · Bootstrap Resampling)

| Scenario | Median Final | P(Profit) | Median Max DD |
|----------|-------------|-----------|---------------|
| A — Base (Historical) | $284,984 | **100%** | -70.8% |
| B — High Spread (+$50/trade) | $210,263 | **100%** | -89.2% |
| C — Enlarged SL (-$30/loss) | $226,515 | **100%** | -88.7% |
| D — Combined Stress | $69,630 | 90.1% | -174.3% |

---

## Interactive Dashboard

Open `analysis/dashboard.html` in any browser for full interactive charts:
- Equity curve
- Drawdown chart
- Monthly P&L heatmap
- Annual returns
- Day-of-week analysis
- Trade P&L distribution
- Monte Carlo fan chart
- Stress test scenario comparison

---

## Risk Warnings & Live Deployment Notes

- Results are from a backtest; live performance will differ
- No commission is modeled — live brokers charge $5–10/lot/side
- Variable position sizing creates exponential nominal exposure at larger balances
- Maximum drawdown (-19.40%) may exceed prop firm limits (typically 10–12%)
- Strategy performs best in trending/momentum regimes; may underperform in tight ranges
- NewsGuard calendar must be maintained annually for forward use

---

