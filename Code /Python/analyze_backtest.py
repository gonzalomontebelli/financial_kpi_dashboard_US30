import csv
import math
import statistics
import datetime
from collections import defaultdict

path = "2026-03-05 20-41-51 History - TEST (US30, m5, True, True, 0, 1, 61.5, 122.5, 9, 15, 7,).csv"


def to_float(s: str) -> float:
    s = (s or "").replace('"', '').replace('\xa0', '').replace(' ', '').replace(',', '.')
    return float(s)


def parse_dt(s: str) -> datetime.datetime:
    s = s.replace('"', '').strip()
    for fmt in ("%d/%m/%Y %H:%M:%S.%f", "%d/%m/%Y %H:%M:%S"):
        try:
            return datetime.datetime.strptime(s, fmt)
        except ValueError:
            continue
    raise ValueError(s)


rows = []
with open(path, newline="", encoding="utf-8") as f:
    reader = csv.DictReader(f)
    for row in reader:
        rows.append(
            {
                "pnl": to_float(row["$ neto"]),
                "close": parse_dt(row["Hora de cierre (UTC+1)"]),
                "label": row["Etiqueta"].replace('"', ''),
                "balance": to_float(row["Saldo $"]),
            }
        )

n = len(rows)
pnls = [r["pnl"] for r in rows]
wins = [x for x in pnls if x > 0]
losses = [x for x in pnls if x < 0]

win_rate = len(wins) / n * 100 if n else 0.0
profit_factor = (sum(wins) / (-sum(losses))) if losses else float("inf")
expectancy = statistics.mean(pnls) if n else 0.0
stdev = statistics.stdev(pnls) if n > 1 else 0.0
stderr = stdev / math.sqrt(n) if n > 1 else 0.0
t_stat = expectancy / stderr if stderr > 0 else 0.0
phi = 0.5 * (1 + math.erf(abs(t_stat) / math.sqrt(2)))
p_value = 2 * (1 - phi)
ci_low = expectancy - 1.96 * stderr
ci_high = expectancy + 1.96 * stderr

start_balance = rows[0]["balance"] - rows[0]["pnl"] if n else 0.0
equity = start_balance
peak = equity
max_dd = 0.0
for r in rows:
    equity += r["pnl"]
    peak = max(peak, equity)
    max_dd = max(max_dd, peak - equity)

by_label = defaultdict(list)
by_week = defaultdict(float)
by_month = defaultdict(float)
for r in rows:
    by_label[r["label"]].append(r["pnl"])
    year, week, _ = r["close"].isocalendar()
    by_week[f"{year}-W{week:02d}"] += r["pnl"]
    by_month[f"{r['close'].year}-{r['close'].month:02d}"] += r["pnl"]

print("SUMMARY")
print(f"trades={n}")
print(f"win_rate_pct={win_rate:.4f}")
print(f"profit_factor={profit_factor:.6f}")
print(f"expectancy_per_trade={expectancy:.6f}")
print(f"t_stat_normal_approx={t_stat:.6f}")
print(f"p_value_two_tailed_normal_approx={p_value:.12g}")
print(f"ci95_expectancy=[{ci_low:.6f}, {ci_high:.6f}]")
print(f"start_balance_est={start_balance:.2f}")
print(f"end_balance={rows[-1]['balance']:.2f}")
print(f"max_dd_abs={max_dd:.2f}")
print(f"weeks={len(by_week)}")
print(f"months={len(by_month)}")
print(f"worst_week={min(by_week.values()):.2f}")
print(f"worst_month={min(by_month.values()):.2f}")

print("LABEL_BREAKDOWN")
for label, values in sorted(by_label.items()):
    label_wins = [x for x in values if x > 0]
    label_losses = [x for x in values if x < 0]
    label_pf = (sum(label_wins) / (-sum(label_losses))) if label_losses else float("inf")
    print(
        f"{label}: trades={len(values)}, win_rate={len(label_wins)/len(values)*100:.4f}, pf={label_pf:.6f}, expectancy={statistics.mean(values):.6f}"
    )

print("WORST_5_WEEKS")
for key, value in sorted(by_week.items(), key=lambda kv: kv[1])[:5]:
    print(f"{key}: {value:.2f}")

print("WORST_5_MONTHS")
for key, value in sorted(by_month.items(), key=lambda kv: kv[1])[:5]:
    print(f"{key}: {value:.2f}")
