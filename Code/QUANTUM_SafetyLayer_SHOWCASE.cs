// ============================================================
// QUANTUM_US30 — SAFETY LAYER SHOWCASE
// ============================================================
// This file is a PUBLIC DEMO of the risk & protection framework
// used in the QUANTUM_US30 trading bot (cTrader / cAlgo).
//
// ⚠️  WHAT THIS FILE IS:
//     A clean, documented reference of every safety mechanism.
//     All entry/exit logic, calendar dates, risk percentages,
//     and strategy-specific parameters have been REMOVED.
//
// ⚠️  WHAT THIS FILE IS NOT:
//     A working bot. It will compile but will never open a trade.
//     No proprietary edge is exposed here.
//
// Sections:
//   1. Parameters — safety knobs exposed in the cTrader UI
//   2. OnStart()  — initialization sequence
//   3. Protection checks called every tick / bar / timer cycle
//       3a. Max Drawdown Guard
//       3b. Daily Loss Guard
//       3c. Weekly Loss Guard
//       3d. Monthly Loss Guard
//       3e. Dynamic DD Risk Scaling
//       3f. Nominal Risk Cap
//       3g. Combined Open-Risk Cap
//   4. Forced Daily Close (16:55 NY)
//   5. NewsGuard Lite — macro event blackout windows
//   6. Blocked-Days calendar
//   7. Helper utilities (timezone, volume validation, logging)
// ============================================================

using cAlgo.API;
using cAlgo.API.Internals;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class QUANTUM_SafetyLayer_SHOWCASE : Robot
    {
        // ============================================================
        // SECTION 1 — SAFETY PARAMETERS
        // All parameters below are exposed in the cTrader UI.
        // They control ONLY risk management and trade protection.
        // ============================================================

        // ------------------------------------------------------------
        // 1A — INITIAL BALANCE (FTMO / Funded-account reference)
        // ------------------------------------------------------------
        // Purpose : Anchors all drawdown calculations to the funded
        //           account starting capital rather than the live
        //           Account.Balance (which already reflects P&L).
        // Usage   : Set this to your challenge or funded-account size
        //           (e.g. 100 000). Leave at 0 to auto-detect.
        // ------------------------------------------------------------
        [Parameter("Initial Balance (Funded Account)", DefaultValue = 0.0, MinValue = 0)]
        public double InitialBalanceFunded { get; set; }

        // ------------------------------------------------------------
        // 1B — MAXIMUM DRAWDOWN STOP (hard kill-switch)
        // ------------------------------------------------------------
        // Purpose : If the account equity drops X % below InitialBalance
        //           the bot closes ALL positions and calls Stop().
        //           This is the last line of defence — it prevents a
        //           runaway loss from exceeding the funded-account limit.
        // Example : InitialBalance = $100 000, MaxDrawdown = 10 %
        //           → bot stops if equity falls below $90 000.
        // Note    : A softer warning fires at WarningDrawdownPercent
        //           (hardcoded to 10 %) before the hard stop at
        //           MaxDrawdownPercent (hardcoded to 35 %).
        // ------------------------------------------------------------
        // (MaxDrawdownPercent is intentionally a constant, not a param,
        //  to prevent accidental misconfiguration on a funded account.)

        // ------------------------------------------------------------
        // 1C — DAILY LOSS LIMIT
        // ------------------------------------------------------------
        // Purpose : Stops trading for the rest of the session once the
        //           account equity drops MaxDailyLossPercent % below
        //           the balance recorded at the START of that NY day.
        //           Resets automatically at the next daily boundary.
        // Example : DayStart balance = $10 200, MaxDailyLoss = 5 %
        //           → bot stops if equity falls below $9 690 intraday.
        // ------------------------------------------------------------
        // (MaxDailyLossPercent is a constant = 5.0 %)

        // ------------------------------------------------------------
        // 1D — WEEKLY LOSS LIMIT
        // ------------------------------------------------------------
        // Purpose : Blocks new trades for the remainder of the calendar
        //           week when cumulative equity loss since Monday open
        //           exceeds the threshold.
        // Reset   : Automatically on Monday morning (NY time).
        // ------------------------------------------------------------
        [Parameter("Weekly Loss Limit %", DefaultValue = 2.5, MinValue = 0.5)]
        public double WeeklyLossLimitPercent { get; set; }

        // ------------------------------------------------------------
        // 1E — MONTHLY LOSS LIMIT
        // ------------------------------------------------------------
        // Purpose : Same concept as weekly, but anchored to the first
        //           calendar day of the month. Prevents compounding
        //           losses across multiple bad weeks.
        // Reset   : Automatically on the 1st of each month (NY time).
        // ------------------------------------------------------------
        [Parameter("Monthly Loss Limit %", DefaultValue = 5.0, MinValue = 1.0)]
        public double MonthlyLossLimitPercent { get; set; }

        // ------------------------------------------------------------
        // 1F — DYNAMIC DRAWDOWN RISK SCALING
        // ------------------------------------------------------------
        // Purpose : Progressively reduces position sizing as the account
        //           draws down from its equity peak. This slows the
        //           descent and gives the account room to recover.
        //
        // Scaling table (ddFromPeak = drawdown from equity peak):
        //   dd <  5 %  →  full risk (1.0×)
        //   dd >= 5 %  →  75 % of risk
        //   dd >= 10 % →  50 % of risk   ← UseGlobalDrawdownRiskScaling
        //   dd >= 15 % →  25 % of risk      governs this tier and above
        //   dd >= 20 % →  0 % (no new trades)
        //
        // GlobalDrawdownHalfPercent defines the 50 % threshold.
        // ------------------------------------------------------------
        [Parameter("Enable Dynamic DD Risk Scaling", DefaultValue = true)]
        public bool UseGlobalDrawdownRiskScaling { get; set; }

        [Parameter("DD Half-Risk Threshold %", DefaultValue = 12.0)]
        public double GlobalDrawdownHalfPercent { get; set; }

        // ------------------------------------------------------------
        // 1G — NOMINAL RISK CAP PER TRADE ($)
        // ------------------------------------------------------------
        // Purpose : Hard ceiling on the dollar amount risked per trade,
        //           regardless of account size or calculated percentage.
        //           Prevents oversized positions when balance grows large.
        // Example : Cap = $400 → even on a $200 000 account, no single
        //           trade risks more than $400.
        // ------------------------------------------------------------
        [Parameter("Max Risk Per Trade ($)", DefaultValue = 400.0, MinValue = 50.0)]
        public double MaxNominalRiskPerTrade { get; set; }

        // ------------------------------------------------------------
        // 1H — COMBINED OPEN-RISK FACTOR
        // ------------------------------------------------------------
        // Purpose : Controls how much total nominal risk can be open
        //           simultaneously across BOTH strategies.
        //           If MaxCombinedRiskFactor = 1.5 and a new trade
        //           would bring combined exposure above 1.5× the
        //           single-trade risk budget, the new trade is rejected.
        // Example : newTrade risk = $300, already open = $350
        //           → combined = $650 > 1.5 × $300 = $450 → BLOCKED.
        // ------------------------------------------------------------
        [Parameter("Max Combined Risk Factor", DefaultValue = 1.5, MinValue = 1.0)]
        public double MaxCombinedRiskFactor { get; set; }

        // ------------------------------------------------------------
        // 1I — FORCED DAILY CLOSE (NY time)
        // ------------------------------------------------------------
        // Purpose : Every open position is closed at exactly 16:55 NY,
        //           regardless of P&L. This:
        //           • avoids holding risk through the illiquid NY close
        //           • respects funded-account "no overnight" rules
        //           • prevents gaps from hurting the account overnight
        // Implementation: OnTimer() polls every 10 s. Once nowNY >=
        //           16:55, ClosePosition() is called on every open lot.
        //           forceCloseDoneToday is set to true and latched until
        //           the next ResetDay() at the NY midnight boundary.
        // ------------------------------------------------------------
        // (ForceCloseHourNY = 16, ForceCloseMinuteNY = 55 — constants)

        // ------------------------------------------------------------
        // 1J — MAX SPREAD FILTER
        // ------------------------------------------------------------
        // Purpose : Skips trade execution if the live spread exceeds
        //           this value. Wide spreads indicate low liquidity or
        //           high volatility; entering during them inflates cost
        //           and slippage beyond the backtested assumptions.
        // ------------------------------------------------------------
        [Parameter("Max Spread (pips)", DefaultValue = 3.5)]
        public double MaxSpreadPips { get; set; }

        // ------------------------------------------------------------
        // 1K — NEWSGUARD LITE (macro event blackout windows)
        // ------------------------------------------------------------
        // Purpose : Prevents entries within a configurable window around
        //           high-impact macroeconomic events. The bot maintains
        //           a pre-loaded UTC timestamp HashSet for each event
        //           type; IsNewsBlocked() does an O(1) lookup per tick.
        //
        // Covered events:
        //   • Michigan Consumer Sentiment   (pre: 35 min, post: 30 min)
        //   • Fed / Powell Testifies        (±60 min)
        //   • Jackson Hole Symposium        (pre: 35 min, post: 5 min)
        //   • Non-Farm Payrolls (NFP)       (pre: 35 min, post: 10 min)
        //   • Consumer Price Index (CPI)    (pre: 35 min, post: 10 min)
        //
        // All windows are tunable via parameters below.
        // ------------------------------------------------------------
        public enum NewsGuardMode { Off = 0, Lite = 1 }

        [Parameter("NewsGuard Mode", DefaultValue = NewsGuardMode.Lite)]
        public NewsGuardMode NewsGuard { get; set; }

        [Parameter("Michigan Pre-event block (min)",  DefaultValue = 35)] public int MichiganPreMin        { get; set; }
        [Parameter("Michigan Post-event block (min)", DefaultValue = 30)] public int MichiganPostMin       { get; set; }
        [Parameter("Fed/Powell +/- window (min)",     DefaultValue = 60)] public int PowellTestifiesAbsMin { get; set; }
        [Parameter("Jackson Hole Pre (min)",          DefaultValue = 35)] public int JacksonPreMin         { get; set; }
        [Parameter("Jackson Hole Post (min)",         DefaultValue =  5)] public int JacksonPostMin        { get; set; }
        [Parameter("NFP Pre-event block (min)",       DefaultValue = 35)] public int NfpPreMin             { get; set; }
        [Parameter("NFP Post-event block (min)",      DefaultValue = 10)] public int NfpPostMin            { get; set; }
        [Parameter("CPI Pre-event block (min)",       DefaultValue = 35)] public int CpiPreMin             { get; set; }
        [Parameter("CPI Post-event block (min)",      DefaultValue = 10)] public int CpiPostMin            { get; set; }

        // ============================================================
        // INTERNAL CONSTANTS (not exposed — protect account limits)
        // ============================================================
        private const double MaxDrawdownPercent       = 35.0;  // hard stop
        private const double WarningDrawdownPercent   = 10.0;  // print warning
        private const double ClosePositionsDrawdownPercent = 35.0;
        private const double MaxDailyLossPercent      = 5.0;

        private const int    ForceCloseHourNY         = 16;
        private const int    ForceCloseMinuteNY       = 55;

        private const string StrategyLabel1           = "STRATEGY_A"; // replace with real label
        private const string StrategyLabel2           = "STRATEGY_B"; // replace with real label

        // ============================================================
        // PRIVATE STATE
        // ============================================================
        private TimeZoneInfo _tzMadrid, _tzNY;

        // Balance anchors
        private double _initialBalance;
        private double _dailyStartBalance;
        private double _weeklyStartBalance;
        private double _monthlyStartBalance;
        private double _equityPeak;

        // Period start dates (NY)
        private DateTime _weeklyStartNy  = DateTime.MinValue;
        private DateTime _monthlyStartNy = DateTime.MinValue;

        // Per-period loss block flags
        private bool _botStoppedByDrawdown  = false;
        private bool _botStoppedByDailyLoss = false;
        private bool _weeklyLossBlocked     = false;
        private bool _monthlyLossBlocked    = false;
        private bool _drawdownCloseTriggered = false;
        private bool _warning10PctShown     = false;
        private DateTime _lastWarningLog    = DateTime.MinValue;

        // Forced close state
        private bool     _forceCloseDoneToday  = false;
        private DateTime _currentTradingDayNy  = DateTime.MinValue;

        // NewsGuard sets — populated at OnStart() from UTC timestamp lists
        private readonly HashSet<DateTime> _michiganUtc       = new HashSet<DateTime>();
        private readonly HashSet<DateTime> _powellUtc         = new HashSet<DateTime>();
        private readonly HashSet<DateTime> _jacksonHoleUtc    = new HashSet<DateTime>();
        private readonly HashSet<DateTime> _nfpUtc            = new HashSet<DateTime>();
        private readonly HashSet<DateTime> _cpiUtc            = new HashSet<DateTime>();

        // Blocked trading days (public holidays, early closes, etc.)
        private HashSet<DateTime> _blockedDays;

        // ============================================================
        // SECTION 2 — OnStart() : INITIALIZATION
        // ============================================================
        protected override void OnStart()
        {
            // -- Resolve timezone objects once; cache for the session --
            _tzMadrid = ResolveTZ("Europe/Madrid", "Romance Standard Time");
            _tzNY     = ResolveTZ("America/New_York", "Eastern Standard Time");

            if (_tzMadrid == null) { _tzMadrid = TimeZoneInfo.Utc; Print("⚠️ Madrid TZ fallback → UTC"); }
            if (_tzNY     == null) { _tzNY     = TimeZoneInfo.Utc; Print("⚠️ NY TZ fallback → UTC");     }

            // -- Set balance anchor --
            _initialBalance = InitialBalanceFunded > 0
                ? InitialBalanceFunded
                : Account.Balance;

            _dailyStartBalance  = Account.Balance;
            _equityPeak         = Account.Equity;

            // -- Load safety data structures --
            _blockedDays = LoadBlockedDays();   // US market holiday calendar
            LoadNewsGuard();                    // parse UTC event timestamps

            // -- Seed period anchors for weekly / monthly limits --
            var nowNY = ToNY(Server.TimeInUtc);
            RefreshPeriodAnchors(nowNY);

            Print($"✅ Safety Layer initialised | InitialBalance={_initialBalance:F2} | TZ_NY={_tzNY.Id}");

            // -- Start 10-second timer for forced-close polling --
            Timer.Start(TimeSpan.FromSeconds(10));
        }

        // ============================================================
        // SECTION 3A — MAX DRAWDOWN GUARD
        // ============================================================
        // Called every tick, bar, and timer cycle.
        // Returns false → caller must abort execution immediately.
        // ============================================================
        private bool CheckMaxDrawdown()
        {
            if (_initialBalance <= 0) return true;
            if (_botStoppedByDrawdown) return false;

            double pnlPct = (Account.Equity - _initialBalance) / _initialBalance * 100.0;

            // -- Tier 1: warning at WarningDrawdownPercent --
            if (pnlPct <= -WarningDrawdownPercent)
            {
                var today = ToMadrid(Server.TimeInUtc).Date;
                if (!_warning10PctShown || today != _lastWarningLog.Date)
                {
                    _warning10PctShown = true;
                    _lastWarningLog    = today;
                    Print($"⚠️ DRAWDOWN WARNING: P&L = {pnlPct:F2}% (threshold: -{WarningDrawdownPercent}%)");
                }
            }

            // -- Tier 2: close all positions at ClosePositionsDrawdownPercent --
            if (pnlPct <= -ClosePositionsDrawdownPercent && !_drawdownCloseTriggered)
            {
                _drawdownCloseTriggered = true;
                CloseAllPositions($"Drawdown reached -{ClosePositionsDrawdownPercent}%");
            }

            // -- Tier 3: hard stop at MaxDrawdownPercent --
            if (pnlPct <= -MaxDrawdownPercent)
            {
                _botStoppedByDrawdown = true;
                CloseAllPositions($"Hard stop: drawdown -{MaxDrawdownPercent}%");
                Print($"🛑 BOT STOPPED: max drawdown limit reached ({pnlPct:F2}%)");
                Stop();
                return false;
            }

            return true;
        }

        // ============================================================
        // SECTION 3B — DAILY LOSS GUARD
        // ============================================================
        // Anchored to the balance at the start of each NY trading day.
        // Resets automatically when ResetDay() is called at midnight NY.
        // ============================================================
        private bool CheckDailyLoss()
        {
            if (_dailyStartBalance <= 0) return true;
            if (_botStoppedByDailyLoss) return false;

            double dailyLossPct = (_dailyStartBalance - Account.Equity) / _dailyStartBalance * 100.0;

            if (dailyLossPct >= MaxDailyLossPercent)
            {
                _botStoppedByDailyLoss = true;
                CloseAllPositions($"Daily loss limit reached: {dailyLossPct:F2}% >= {MaxDailyLossPercent}%");
                Print($"🛑 DAILY LOSS LIMIT: trading suspended for today.");
                Stop();
                return false;
            }

            return true;
        }

        // ============================================================
        // SECTION 3C — WEEKLY LOSS GUARD
        // ============================================================
        // Anchored to the balance at the start of the current ISO week
        // (Monday NY open). Resets on Monday automatically.
        // ============================================================
        private bool CheckWeeklyLoss(DateTime nowNY)
        {
            RefreshPeriodAnchors(nowNY);

            if (_weeklyStartBalance <= 0 || _weeklyLossBlocked) return !_weeklyLossBlocked;

            double weeklyLossPct = (_weeklyStartBalance - Account.Equity) / _weeklyStartBalance * 100.0;

            if (weeklyLossPct >= WeeklyLossLimitPercent)
            {
                _weeklyLossBlocked = true;
                CloseAllPositions($"Weekly loss limit: {weeklyLossPct:F2}% >= {WeeklyLossLimitPercent}%");
                Print($"🛑 WEEKLY LOSS LIMIT reached. No new trades until Monday.");
                return false;
            }

            return true;
        }

        // ============================================================
        // SECTION 3D — MONTHLY LOSS GUARD
        // ============================================================
        // Anchored to balance on the 1st of each calendar month (NY).
        // Resets automatically on the 1st.
        // ============================================================
        private bool CheckMonthlyLoss(DateTime nowNY)
        {
            RefreshPeriodAnchors(nowNY);

            if (_monthlyStartBalance <= 0 || _monthlyLossBlocked) return !_monthlyLossBlocked;

            double monthlyLossPct = (_monthlyStartBalance - Account.Equity) / _monthlyStartBalance * 100.0;

            if (monthlyLossPct >= MonthlyLossLimitPercent)
            {
                _monthlyLossBlocked = true;
                CloseAllPositions($"Monthly loss limit: {monthlyLossPct:F2}% >= {MonthlyLossLimitPercent}%");
                Print($"🛑 MONTHLY LOSS LIMIT reached. No new trades until next month.");
                return false;
            }

            return true;
        }

        // ============================================================
        // SECTION 3E — DYNAMIC DD RISK SCALING
        // ============================================================
        // Modifies riskPercent IN PLACE before position sizing.
        // Uses the rolling equity peak, not the initial balance.
        //
        //  ddFromPeak | multiplier applied
        //  -----------|-------------------
        //   < 5 %     |  1.00  (full risk)
        //   5–10 %    |  0.75
        //   10–15 %   |  0.50  ← GlobalDrawdownHalfPercent governs this
        //   15–20 %   |  0.25
        //   >= 20 %   |  0.00  (no new entries)
        // ============================================================
        private void ApplyDDRiskScaling(ref double riskPercent)
        {
            if (!UseGlobalDrawdownRiskScaling) return;

            // Update rolling equity peak
            if (Account.Equity > _equityPeak) _equityPeak = Account.Equity;
            if (_equityPeak <= 0) return;

            double ddFromPeak = (_equityPeak - Account.Equity) / _equityPeak * 100.0;

            if      (ddFromPeak >= 20.0) riskPercent  = 0.0;
            else if (ddFromPeak >= 15.0) riskPercent *= 0.25;
            else if (ddFromPeak >= GlobalDrawdownHalfPercent) riskPercent *= 0.50;
            else if (ddFromPeak >=  5.0) riskPercent *= 0.75;
            // else: no scaling needed
        }

        // ============================================================
        // SECTION 3F — NOMINAL RISK CAP
        // ============================================================
        // After percentage-based sizing, this clamps the dollar amount
        // to MaxNominalRiskPerTrade. Prevents oversized lots on large
        // accounts or after a period of strong equity growth.
        // ============================================================
        private double ApplyNominalRiskCap(double riskAmount)
        {
            if (riskAmount <= 0 || MaxNominalRiskPerTrade <= 0) return riskAmount;
            return Math.Min(riskAmount, MaxNominalRiskPerTrade);
        }

        // ============================================================
        // SECTION 3G — COMBINED OPEN-RISK GATE
        // ============================================================
        // Sums the nominal risk of all currently open positions.
        // Blocks a new trade if adding newRisk would exceed
        //   Max(newRisk, newRisk × MaxCombinedRiskFactor).
        //
        // This prevents both strategies from being simultaneously at
        // max risk, which could breach daily/weekly loss limits on a
        // single adverse move.
        // ============================================================
        private bool CanOpenWithCombinedRisk(double newRisk, string context)
        {
            if (newRisk <= 0) return false;

            double openRisk    = GetCurrentOpenRiskNominal();
            double allowedRisk = newRisk * Math.Max(1.0, MaxCombinedRiskFactor);

            if (openRisk + newRisk <= allowedRisk) return true;

            Print($"⛔ Combined risk gate blocked [{context}]: open={openRisk:F2} + new={newRisk:F2} > allowed={allowedRisk:F2}");
            return false;
        }

        private double GetCurrentOpenRiskNominal()
        {
            // In the real bot this reads from a per-position metrics dict.
            // Showcase: placeholder returning 0.
            return 0.0;
        }

        // ============================================================
        // SECTION 4 — FORCED DAILY CLOSE  (16:55 NY)
        // ============================================================
        // Called by OnTimer() every 10 seconds.
        // Once nowNY >= 16:55, every open position managed by this bot
        // is closed via ClosePosition(). The flag forceCloseDoneToday
        // prevents repeated close attempts once all lots are flat.
        //
        // Why 16:55 NY?
        //   • US equities close at 16:00; liquidity thins rapidly after.
        //   • The 5-minute buffer catches any TP/SL fills settling.
        //   • Most funded-account rules prohibit overnight positions.
        // ============================================================
        protected override void OnTimer()
        {
            var utcNow   = Server.TimeInUtc;
            var nowNY    = ToNY(utcNow);
            var tradingDay = nowNY.Date;

            // -- Day boundary: reset state when NY date changes --
            if (tradingDay != _currentTradingDayNy && !HasOpenPositions())
                ResetDay(tradingDay, nowNY);

            // -- Run all protection checks --
            if (!CheckMaxDrawdown())  return;
            if (!CheckDailyLoss())    return;
            if (!CheckWeeklyLoss(nowNY))  return;
            if (!CheckMonthlyLoss(nowNY)) return;

            if (_forceCloseDoneToday) return;

            var cutoff = new TimeSpan(ForceCloseHourNY, ForceCloseMinuteNY, 0);
            if (nowNY.TimeOfDay < cutoff) return;  // too early, nothing to do

            // -- Close time reached --
            var openPositions = GetBotPositions();

            if (openPositions.Count == 0)
            {
                _forceCloseDoneToday = true;
                return;
            }

            int closed = 0, failed = 0;
            foreach (var p in openPositions)
            {
                var result = ClosePosition(p);
                if (result.IsSuccessful) closed++;
                else                     failed++;
            }

            Print($"⏱️ Force-Close {nowNY:HH:mm} NY: closed={closed}, failed={failed}");

            if (!HasOpenPositions())
                _forceCloseDoneToday = true;
        }

        // ============================================================
        // SECTION 5 — NEWSGUARD LITE
        // ============================================================
        // IsNewsBlocked() is called on every tick and bar close before
        // any entry attempt. It performs a date-filtered, O(1) lookup
        // per event category.
        //
        // How the HashSet lookup works:
        //   1. At OnStart(), ParseUtcList() splits a CSV string of UTC
        //      timestamps into a HashSet<DateTime>.
        //   2. IsInWindowSameDay() iterates only events whose .Date
        //      matches today — typically 0–2 events per day.
        //   3. If utcNow falls in [event - preMin, event + postMin]
        //      the trade is blocked and the reason is logged.
        //
        // The actual UTC timestamp lists have been REMOVED from this
        // showcase. In production they contain ~200–300 dated entries
        // per event category covering 2017–2026.
        // ============================================================
        private void LoadNewsGuard()
        {
            // Production: pass the real CSV constant strings here.
            // Showcase: empty strings → NewsGuard loads but never blocks.
            ParseUtcList("", _michiganUtc);
            ParseUtcList("", _powellUtc);
            ParseUtcList("", _jacksonHoleUtc);
            ParseUtcList("", _nfpUtc);
            ParseUtcList("", _cpiUtc);

            Print($"ℹ️ NewsGuard loaded: Michigan={_michiganUtc.Count}, Fed={_powellUtc.Count}, JH={_jacksonHoleUtc.Count}, NFP={_nfpUtc.Count}, CPI={_cpiUtc.Count}");
        }

        private bool IsNewsBlocked(DateTime utcNow, out string reason)
        {
            reason = null;
            if (NewsGuard == NewsGuardMode.Off) return false;

            if (IsInWindow(utcNow, _michiganUtc,    MichiganPreMin,        MichiganPostMin,       out var ev)) { reason = $"Michigan ({ev:HH:mm} UTC)";     return true; }
            if (IsInWindow(utcNow, _powellUtc,      PowellTestifiesAbsMin, PowellTestifiesAbsMin, out ev))     { reason = $"Fed/Powell ({ev:HH:mm} UTC)";   return true; }
            if (IsInWindow(utcNow, _jacksonHoleUtc, JacksonPreMin,         JacksonPostMin,        out ev))     { reason = $"Jackson Hole ({ev:HH:mm} UTC)"; return true; }
            if (IsInWindow(utcNow, _nfpUtc,         NfpPreMin,             NfpPostMin,            out ev))     { reason = $"NFP ({ev:HH:mm} UTC)";          return true; }
            if (IsInWindow(utcNow, _cpiUtc,         CpiPreMin,             CpiPostMin,            out ev))     { reason = $"CPI ({ev:HH:mm} UTC)";          return true; }

            return false;
        }

        // Only iterates events on the same UTC date — avoids checking
        // the entire multi-year calendar on every tick.
        private static bool IsInWindow(DateTime utcNow, HashSet<DateTime> events,
                                       int preMin, int postMin, out DateTime matched)
        {
            foreach (var ev in events)
            {
                if (ev.Date != utcNow.Date) continue;
                if (utcNow >= ev.AddMinutes(-preMin) && utcNow <= ev.AddMinutes(postMin))
                {
                    matched = ev;
                    return true;
                }
            }
            matched = default;
            return false;
        }

        private static void ParseUtcList(string csv, HashSet<DateTime> target)
        {
            if (string.IsNullOrWhiteSpace(csv)) return;
            foreach (var token in csv.Split(new[] { ',', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (DateTime.TryParseExact(token.Trim(),
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
                {
                    target.Add(dt);
                }
            }
        }

        // ============================================================
        // SECTION 6 — BLOCKED-DAYS CALENDAR
        // ============================================================
        // A HashSet<DateTime> of US market holidays and early closes.
        // The bot skips ALL trading activity on these dates.
        //
        // Benefits of HashSet vs. List:
        //   • Contains() is O(1) — safe to call on every tick.
        //   • Loaded once at OnStart(); zero allocation per tick.
        //
        // The actual date list has been REMOVED from this showcase.
        // In production it covers New Year's Day, MLK Day, Presidents'
        // Day, Good Friday, Memorial Day, Independence Day (+ observed),
        // Labor Day, Thanksgiving + Black Friday, Christmas Eve / Day.
        // ============================================================
        private HashSet<DateTime> LoadBlockedDays()
        {
            // Production: insert the real comma-separated date string.
            // Showcase: returns an empty set (no days blocked).
            string dates = "";

            return new HashSet<DateTime>(
                dates.Split(',')
                     .Select(d => d.Trim())
                     .Where(d => d.Length == 10)
                     .Select(d => DateTime.ParseExact(d, "yyyy-MM-dd",
                                                      CultureInfo.InvariantCulture).Date)
            );
        }

        // ============================================================
        // SECTION 7 — UTILITY HELPERS
        // ============================================================

        // -- Timezone conversion (cached objects, no repeated lookups) --
        private DateTime ToMadrid(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _tzMadrid);
        }

        private DateTime ToNY(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _tzNY);
        }

        // Try multiple ID aliases for cross-platform compatibility
        // (Windows uses "Eastern Standard Time", Linux uses "America/New_York")
        private TimeZoneInfo ResolveTZ(params string[] ids)
        {
            foreach (var id in ids)
            {
                try { if (!string.IsNullOrEmpty(id)) return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch { }
            }
            return null;
        }

        // -- Period anchor refresh (weekly / monthly reset logic) --
        private void RefreshPeriodAnchors(DateTime nowNY)
        {
            // Week starts on Monday
            var weekStart = nowNY.Date.AddDays(-(((int)nowNY.DayOfWeek + 6) % 7));
            if (_weeklyStartNy != weekStart)
            {
                _weeklyStartNy      = weekStart;
                _weeklyStartBalance = Account.Balance;
                _weeklyLossBlocked  = false;
            }

            var monthStart = new DateTime(nowNY.Year, nowNY.Month, 1);
            if (_monthlyStartNy != monthStart)
            {
                _monthlyStartNy      = monthStart;
                _monthlyStartBalance = Account.Balance;
                _monthlyLossBlocked  = false;
            }
        }

        // -- Daily state reset --
        private void ResetDay(DateTime tradingDayNY, DateTime nowNY)
        {
            _currentTradingDayNy  = tradingDayNY;
            _dailyStartBalance    = Account.Balance;
            _botStoppedByDailyLoss = false;
            _forceCloseDoneToday  = false;
            RefreshPeriodAnchors(nowNY);
        }

        // -- Position helpers --
        private List<Position> GetBotPositions()
        {
            return Positions.FindAll(StrategyLabel1, Symbol.Name)
                            .Concat(Positions.FindAll(StrategyLabel2, Symbol.Name))
                            .ToList();
        }

        private bool HasOpenPositions() => GetBotPositions().Count > 0;

        private void CloseAllPositions(string reason)
        {
            foreach (var p in GetBotPositions()) ClosePosition(p);
            Print($"🔴 CloseAll triggered: {reason}");
        }

        // -- Volume validation guard (used before ExecuteMarketOrder) --
        private bool IsVolumeValid(double volume)
        {
            if (volume <= 0 || double.IsNaN(volume) || double.IsInfinity(volume)) return false;
            double norm = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.ToNearest);
            if (norm < Symbol.VolumeInUnitsMin || norm > Symbol.VolumeInUnitsMax)  return false;
            double tol = Math.Max(Symbol.VolumeInUnitsStep * 0.01, norm * 0.001);
            return Math.Abs(volume - norm) <= tol;
        }

        // -- Entry logic placeholder (strategies removed from showcase) --
        protected override void OnTick()
        {
            var utcNow = Server.TimeInUtc;
            var nowNY  = ToNY(utcNow);

            // Gate: run all safety checks first
            if (!CheckMaxDrawdown())     return;
            if (!CheckDailyLoss())       return;
            if (!CheckWeeklyLoss(nowNY)) return;
            if (!CheckMonthlyLoss(nowNY))return;

            if (_blockedDays.Contains(ToMadrid(utcNow).Date)) return;

            if (IsNewsBlocked(utcNow, out var newsReason))
            {
                // Log or silently skip — strategy entry would go here
                return;
            }

            // *** Strategy entry logic intentionally omitted ***
            // ApplyDDRiskScaling(ref riskPercent);
            // double riskAmount = ApplyNominalRiskCap(Account.Balance * riskPercent / 100.0);
            // if (!CanOpenWithCombinedRisk(riskAmount, "STRATEGY_A")) return;
            // ExecuteMarketOrder(...);
        }

        protected override void OnBarClosed()
        {
            var utcNow = Server.TimeInUtc;
            var nowNY  = ToNY(utcNow);

            if (!CheckMaxDrawdown())     return;
            if (!CheckDailyLoss())       return;
            if (!CheckWeeklyLoss(nowNY)) return;
            if (!CheckMonthlyLoss(nowNY))return;

            if (_blockedDays.Contains(ToMadrid(utcNow).Date)) return;
            if (IsNewsBlocked(utcNow, out _)) return;

            // *** Strategy bar-close logic intentionally omitted ***
        }

        protected override void OnStop()
        {
            // Clean up if needed
        }
    }
}
