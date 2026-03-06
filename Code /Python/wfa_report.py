import csv
import math
import statistics
import datetime as dt
from dataclasses import dataclass
from typing import List, Dict, Tuple

CSV_PATH = "2026-03-05 20-41-51 History - TEST (US30, m5, True, True, 0, 1, 61.5, 122.5, 9, 15, 7,).csv"
IS_MONTHS = 24
OOS_MONTHS = 12
STEP_MONTHS = 12


def to_float(s: str) -> float:
    s = (s or "").replace('"', '').replace('\xa0', '').replace(' ', '').replace(',', '.')
    return float(s)


def parse_dt(s: str) -> dt.datetime:
    s = s.replace('"', '').strip()
    for fmt in ("%d/%m/%Y %H:%M:%S.%f", "%d/%m/%Y %H:%M:%S"):
        try:
            return dt.datetime.strptime(s, fmt)
        except ValueError:
            continue
    raise ValueError(s)


def month_start(d: dt.datetime) -> dt.datetime:
    return dt.datetime(d.year, d.month, 1)


def add_months(d: dt.datetime, n: int) -> dt.datetime:
    y = d.year + (d.month - 1 + n) // 12
    m = (d.month - 1 + n) % 12 + 1
    return dt.datetime(y, m, 1)


@dataclass
class Trade:
    close: dt.datetime
    pnl: float
    label: str


def load_trades(path: str) -> List[Trade]:
    rows: List[Trade] = []
    with open(path, newline="", encoding="utf-8") as f:
        r = csv.DictReader(f)
        for row in r:
            rows.append(
                Trade(
                    close=parse_dt(row["Hora de cierre (UTC+1)"]),
                    pnl=to_float(row["$ neto"]),
                    label=row["Etiqueta"].replace('"', ''),
                )
            )
    rows.sort(key=lambda x: x.close)
    return rows


def metrics(pnls: List[float]) -> Dict[str, float]:
    if not pnls:
        return {
            "trades": 0,
            "net": 0.0,
            "win_rate": 0.0,
            "pf": 0.0,
            "expectancy": 0.0,
            "t_stat": 0.0,
            "p_value": 1.0,
            "ci_low": 0.0,
            "ci_high": 0.0,
        }

    n = len(pnls)
    wins = [x for x in pnls if x > 0]
    losses = [x for x in pnls if x < 0]
    net = sum(pnls)
    win_rate = len(wins) / n * 100.0
    pf = (sum(wins) / (-sum(losses))) if losses else float("inf")
    expectancy = statistics.mean(pnls)

    if n > 1:
        sd = statistics.stdev(pnls)
        se = sd / math.sqrt(n) if sd > 0 else 0.0
    else:
        se = 0.0

    if se > 0:
        t_stat = expectancy / se
        phi = 0.5 * (1 + math.erf(abs(t_stat) / math.sqrt(2)))
        p_value = 2 * (1 - phi)
        ci_low = expectancy - 1.96 * se
        ci_high = expectancy + 1.96 * se
    else:
        t_stat = 0.0
        p_value = 1.0
        ci_low = expectancy
        ci_high = expectancy

    return {
        "trades": n,
        "net": net,
        "win_rate": win_rate,
        "pf": pf,
        "expectancy": expectancy,
        "t_stat": t_stat,
        "p_value": p_value,
        "ci_low": ci_low,
        "ci_high": ci_high,
    }


def select_range(trades: List[Trade], start: dt.datetime, end: dt.datetime) -> List[Trade]:
    return [t for t in trades if start <= t.close < end]


def run_wfa(trades: List[Trade]) -> Tuple[List[Dict[str, object]], List[Trade]]:
    if not trades:
        return [], []

    first = month_start(trades[0].close)
    last = month_start(trades[-1].close)

    folds: List[Dict[str, object]] = []
    all_oos: List[Trade] = []

    cursor = first
    fold_id = 1
    while True:
        is_start = cursor
        is_end = add_months(is_start, IS_MONTHS)
        oos_end = add_months(is_end, OOS_MONTHS)

        if is_end > last or is_start >= last:
            break

        is_trades = select_range(trades, is_start, is_end)
        oos_trades = select_range(trades, is_end, oos_end)

        if not oos_trades:
            break

        fold = {
            "fold": fold_id,
            "is_start": is_start.date().isoformat(),
            "is_end": (is_end - dt.timedelta(days=1)).date().isoformat(),
            "oos_start": is_end.date().isoformat(),
            "oos_end": (oos_end - dt.timedelta(days=1)).date().isoformat(),
            "is": metrics([t.pnl for t in is_trades]),
            "oos": metrics([t.pnl for t in oos_trades]),
        }
        folds.append(fold)
        all_oos.extend(oos_trades)

        fold_id += 1
        cursor = add_months(cursor, STEP_MONTHS)

    return folds, all_oos


def print_metrics(title: str, m: Dict[str, float]) -> None:
    print(title)
    print(f"  trades={m['trades']}")
    print(f"  net={m['net']:.2f}")
    print(f"  win_rate={m['win_rate']:.3f}%")
    print(f"  pf={m['pf']:.6f}")
    print(f"  expectancy={m['expectancy']:.6f}")
    print(f"  t_stat~={m['t_stat']:.6f}")
    print(f"  p_value~={m['p_value']:.12g}")
    print(f"  ci95=[{m['ci_low']:.6f}, {m['ci_high']:.6f}]")


def main() -> None:
    trades = load_trades(CSV_PATH)
    print(f"Loaded trades: {len(trades)}")
    print(f"Period: {trades[0].close.date()} -> {trades[-1].close.date()}")

    folds, all_oos = run_wfa(trades)

    print("\nWFA_FOLDS")
    for f in folds:
        o = f["oos"]
        print(
            f"fold={f['fold']} "
            f"IS[{f['is_start']}..{f['is_end']}] "
            f"OOS[{f['oos_start']}..{f['oos_end']}] "
            f"oos_trades={o['trades']} oos_pf={o['pf']:.5f} oos_net={o['net']:.2f} oos_wr={o['win_rate']:.2f}%"
        )

    agg_oos = metrics([t.pnl for t in all_oos])
    print()
    print_metrics("AGGREGATED_OOS", agg_oos)

    last_close = trades[-1].close
    reserve_start = add_months(month_start(last_close), -11)
    reserved = [t for t in trades if t.close >= reserve_start]
    reserve_m = metrics([t.pnl for t in reserved])
    print()
    print(f"RESERVED_LAST_12M start={reserve_start.date().isoformat()}")
    print_metrics("RESERVED_LAST_12M_METRICS", reserve_m)


if __name__ == "__main__":
    main()
