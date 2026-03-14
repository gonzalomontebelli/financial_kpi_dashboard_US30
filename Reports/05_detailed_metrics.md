# QUANTUM_US30 — Regime Analysis and Causal Performance Interpretation

> **Supplementary research note for the QUANTUM_US30 repository**  
> **Sample:** 3,406 trades · 2017-01-03 to 2026-03-10 · US30 (Dow Jones Industrial Average)

---

## Purpose of This Document

This document complements the main repository by interpreting **where the system’s edge appears strongest, weakest, and most regime-dependent**.

While the core README focuses on architecture, performance, and reproducibility, this note is designed to answer a different question:

**Why does the strategy perform differently across months, weekdays, and intraday execution windows?**

The objective is not to make deterministic claims about market behavior, but to build a **causal research framework** around recurring performance patterns observed in the backtest. In that sense, this file should be read as a **regime analysis layer** on top of the repository’s main performance and risk reports.

---

## Relationship to the Repository

This analysis is best understood as an extension of the research workflow already documented in the repository:

- **README.md** → system overview, strategy architecture, high-level performance
- **Reports/** → performance, risk, Monte Carlo, and execution-cost analysis
- **Data/** → source tables used for reporting and breakdowns
- **Analysis/** → interactive dashboard and original cTrader exports
- **Code/** → Python, SQL, and C# components used for processing, analysis, and system presentation

In practical terms, this file sits between the **performance report** and the **risk allocation framework**. It is not a replacement for statistical validation; it is an interpretive layer intended to guide position sizing, session weighting, and regime-aware deployment decisions.

---

## Scope and Data Context

This note consolidates two internal analytical views derived from the full QUANTUM_US30 backtest:

| Source Document | Analytical Focus |
|---|---|
| `quantum_why_seasonality.html` | Monthly and macro-seasonal regime behavior |
| `quantum_why_days_hours_1.html` | Weekday and intraday microstructure behavior |

**Backtest window:** 2017-01-03 to 2026-03-10  
**Trade count:** 3,406  
**Instrument:** US30  
**Broker environment:** Pepperstone / cTrader research context

Because the repository itself presents the strategy as a **systematic intraday research framework**, the interpretation below is framed in terms of:

1. **Expected value by regime**
2. **Execution quality by time window**
3. **Structural asymmetries between strategy legs**
4. **Implications for dynamic risk allocation**

---

## Methodological Positioning

The patterns described here should be interpreted as **conditional tendencies**, not guarantees. A profitable cluster in one month or weekday does not prove a universal market law; it indicates that the strategy appears better aligned with the dominant volatility and liquidity structure of that regime.

This document therefore uses the following working logic:

- If a period shows **positive expectancy, acceptable profit factor, and repeated yearly confirmation**, it can be treated as a candidate **favorable regime**.
- If a period shows **low win rate, flat EV, or repeated inconsistency**, it should be treated as a **fragile regime**.
- If one strategy leg repeatedly underperforms in a specific time window, that underperformance should be interpreted as a **structural mismatch between entry logic and market microstructure**.

This framing is more consistent with the repository’s research orientation than a purely narrative explanation.

---

## 1) Monthly Regime Analysis

### Core Observation

The backtest suggests that QUANTUM_US30 is **not regime-neutral**. Its expectancy varies materially by month, which implies that the same entry logic interacts very differently with seasonal volatility, institutional flows, and event concentration.

### Stronger Months

#### November

November appears to be the strongest monthly regime in the sample, with the highest average expectancy and one of the most stable positive profiles across years.

From a research perspective, this makes sense for three reasons:

1. **Post-September / October reset:** after the most unstable part of the annual cycle, markets often transition into cleaner directional behavior.
2. **Institutional repositioning:** portfolio managers begin preparing for year-end and the following allocation cycle.
3. **Improved directional continuation:** breakout systems benefit when the underlying index exhibits cleaner follow-through after session range violations.

For QUANTUM_US30, the implication is straightforward: **November behaves like a high-alignment month**, especially for the London-session component.

#### August

August is notable because it performs better than one might expect from a purely directional monthly index view.

A plausible interpretation is that the strategy benefits less from absolute monthly trend and more from **intraday movement cleanliness**. Lower participation from discretionary institutional flows may create an environment where session-defined levels are violated with fewer conflicting interventions. In other words, the strategy may be monetizing **intraday structural simplicity**, not necessarily bullish or bearish macro direction.

### Mixed but Positive Months

#### January and February

These months remain supportive overall, but their internal behavior is less uniform.

- January likely benefits from renewed risk-taking, fresh capital deployment, and clean post-holiday positioning.
- February appears especially supportive for the **London-session sell leg**, suggesting that early-year directional corrections may produce cleaner downside range breaks during that window.

However, the same conditions do not uniformly help the afternoon reversal structure, which means these months should be treated as **selectively favorable**, not universally strong.

#### June and December

These two months appear positive in average terms, but with more dispersion.

- **December** likely benefits from year-end drift, but can also suffer from holiday-thinned liquidity and execution irregularities.
- **June** may benefit from event-driven volatility and quarter-end positioning, but the edge appears less stable than in November or August.

### Weak or Fragile Months

#### September

September is the clearest weak regime in the sample.

Its profile suggests a market environment where session breakouts trigger more often without producing the continuation required for a positive breakout expectancy. This is consistent with:

- heavier portfolio rebalancing,
- more unstable directional commitment,
- greater tendency toward failed continuation after range expansion.

From a system-design standpoint, September should be treated as a **risk-reduction month**, not a normal operating month.

#### May and October

These months are best interpreted as **low-conviction regimes**.

They do not necessarily collapse across every metric, but their expectancy is too weak relative to trade count to justify standard risk treatment. In practical terms, these are months where the system may still produce trades, but the regime does not seem to reward breakout logic consistently enough.

### Monthly Conclusion

The main takeaway is not “certain months are good” in a generic sense. The real takeaway is this:

> **QUANTUM_US30 appears to have a measurable dependency on annual regime structure.**

That matters because it supports the idea of **month-sensitive risk weighting**, rather than constant exposure across the full calendar.

---

## 2) Weekday Regime Analysis

### Core Observation

The system also shows meaningful variation by weekday, which indicates that expectancy is influenced by the weekly rhythm of liquidity, information digestion, and institutional execution behavior.

### Tuesday as the Highest-Quality Day

Tuesday stands out as the strongest weekday for the primary London-session structure.

A technical interpretation is that Tuesday benefits from the following sequence:

1. Monday establishes the initial weekly directional bias.
2. Tuesday is often the first full session where that bias is expressed with conviction.
3. Breakout conditions improve because the market has already absorbed the weekend information set and early-week price discovery.

In other words, Tuesday appears to be a **confirmation day**, not merely a high-volume day. That distinction matters because breakout systems need not just movement, but movement with persistence.

### Wednesday as a Protected No-Trade Day

The repository already reflects a deliberate avoidance of Wednesday exposure. That design choice is supported by the regime logic.

Wednesday often carries elevated event uncertainty, especially when central-bank communication dominates expectations. Even in the absence of a major announcement, the market frequently behaves as if it is waiting for one. For a breakout framework, that means more range noise and lower continuation quality.

From a research perspective, Wednesday is not simply “bad”; it is **structurally less compatible** with this type of intraday entry logic.

### Monday and Friday as Secondary Regimes

#### Monday

Monday is acceptable, but with greater dispersion.

The likely reason is that Monday contains more residual uncertainty from weekend news, opening gaps, and slower institutional commitment. The system can still perform, but the variance profile is likely higher than on Tuesday or Thursday.

#### Friday

Friday is mixed but still usable.

The first part of the U.S. session can still provide clean directional movement, but later in the day the market is increasingly shaped by de-risking, position squaring, and pre-weekend flow. This creates a split personality:

- acceptable opportunity early,
- lower-quality continuation later.

### Weekday Conclusion

The evidence supports treating weekdays as **different expectancy environments**, not interchangeable trade opportunities.

This has direct implications for weighting:

- Tuesday deserves higher confidence on the London-session leg.
- Wednesday avoidance remains justified.
- Friday should likely retain conservative treatment, especially late in the day.

---

## 3) Intraday Microstructure Interpretation

### Strategic Logic of the Two Main Windows

The strategy is built around two distinct intraday opportunities:

1. a **London-session range / New York-open breakout expression**, and
2. a **later-session reversal or range-reaction structure**.

These are not equivalent engines. They monetize different market behaviors and therefore should not be evaluated as if they were interchangeable.

### Strategy 1 — London Range / New York Open Breakout

This is the cleaner of the two structures.

Its logic is supported by a well-known market feature: when New York opens, overnight positioning, pre-market orders, and opening-auction participation often create the strongest directional expansion of the day. If the market breaks a London-defined range during that period, the probability of continuation is materially higher than during lower-participation windows.

That helps explain why the early New York period appears to be the **core alpha zone** for the system.

### Strategy 2 — Later-Session Reversal / Range Reaction

The second leg is more sensitive to context.

By the time this structure becomes active, the market has already processed:

- the New York open,
- much of the morning directional move,
- and part of the day’s institutional execution.

That means the afternoon setup is operating in a more mature intraday environment, where continuation may be weaker and reversal behavior more common. This does not make the strategy invalid, but it does make it **more dependent on alignment**.

### The Structural Problem: RRL SELL

The most important asymmetry in the file is the persistent weakness of the **RRL SELL** configuration.

This should be interpreted as a structural issue, not a parameter accident.

The reason is conceptual:

- the U.S. session tends to express its strongest upside impulse earlier in the day,
- the afternoon window often reflects digestion rather than fresh expansion,
- and selling a breakout in a window that is still partially supported by residual intraday upward pressure is inherently less robust.

So the question is not “can RRL SELL ever work?” It clearly can in isolated regimes. The real question is:

> **Is the trade direction fundamentally misaligned with the session behavior it is trying to monetize?**

Based on the backtest interpretation, the answer appears to be **yes, for most of the sample**.

That makes RRL SELL a candidate for conditional deactivation, heavier filtering, or reduced risk treatment.

---

## 4) Regime-Aware Risk Implications

The strongest practical value of this document is not descriptive; it is operational.

If expectancy changes materially across months, weekdays, and strategy legs, then constant risk per trade is unlikely to be optimal.

### Suggested Interpretation Framework

#### Full-Risk Regimes

Use standard or enhanced allocation when the following conditions align:

- strong month,
- strong weekday,
- strategy leg historically aligned with that regime,
- no obvious event-driven distortion.

Examples from the current interpretation:

- November, especially on the London-session structure
- August, when movement quality remains clean
- Tuesday on the primary breakout engine

#### Reduced-Risk Regimes

Lower exposure when one or more of the following are present:

- weak month,
- fragile weekday,
- underperforming directional leg,
- event-compressed or low-conviction session behavior.

Examples:

- September
- May / October
- Tuesday on the weaker afternoon leg
- any regime where RRL SELL is active without additional confirmation

### Key Principle

The edge should not be treated as static.

A more technically consistent portfolio-level approach is:

> **hold entry logic relatively stable, but vary capital allocation according to regime quality.**

That is fully consistent with the repository’s broader emphasis on research, validation, and controlled deployment.

---

## 5) Practical Recommendations for the Repository

Based on the current structure of the project, this document is most useful if positioned as a **research interpretation note**, not as the main README and not as a marketing-style summary.

### Best Placement

The best path would be one of these:

- `Reports/05_regime_analysis.md`
- `Reports/06_causal_interpretation.md`
- `Analysis/regime_interpretation.md`

### Why This Placement Makes Sense

- It keeps the main `README.md` focused on architecture, metrics, and reproducibility.
- It preserves a professional research tone.
- It lets readers move from **performance** → **risk** → **regime interpretation** in a logical order.
- It makes the repository feel more like a quantitative research project and less like a purely descriptive backtest archive.

---

## 6) Limitations

This analysis should be read with the same caution applied to any backtest-derived interpretation.

### Main Limitations

1. **Causal explanations are inferential.** The backtest shows recurring statistical patterns, but the macro or microstructure explanations remain hypotheses unless formally tested with external data.
2. **Later-year compounding affects absolute P&L scale.** Interpretation should rely more on expectancy, win rate, and consistency than on nominal profit in the high-equity phase.
3. **Regime conclusions should be cross-checked periodically.** A pattern that held from 2017–2026 may weaken in future volatility environments.
4. **The document is not a forecasting tool.** It is a framework for interpreting historical alignment between system logic and market structure.

---

## Final Conclusion

The most important conclusion is not that the strategy works “because of seasonality” or “because of one session.”

The more robust conclusion is this:

> **QUANTUM_US30 appears to be a regime-sensitive intraday system whose edge strengthens when breakout logic is aligned with favorable calendar structure, cleaner directional participation, and the correct session-specific market behavior.**

That means the next level of refinement is unlikely to come from changing the core entry idea alone.

It is more likely to come from:

- **regime-aware position sizing,**
- **selective deactivation of structurally weak configurations,**
- **and deeper validation of session-specific expectancy.**

In repository terms, this file helps bridge the gap between **raw backtest performance** and **deployment-quality research judgment**.

---

## Suggested One-Line Description for GitHub

**Regime analysis note for QUANTUM_US30, interpreting monthly, weekly, and intraday performance asymmetries through a systematic research framework.**
