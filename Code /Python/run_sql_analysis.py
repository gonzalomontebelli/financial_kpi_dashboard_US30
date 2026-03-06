import duckdb
import pandas as pd

from config import DATA_CSV, DATA_XLSX, SAMPLE_CSV, OUTPUT_DIR, SQL_DIR
from utils import load_prices_dataframe

def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # 1) Load dataset (full if available, otherwise sample)
    if DATA_XLSX.exists() or DATA_CSV.exists():
        df = load_prices_dataframe(csv_path=DATA_CSV, xlsx_path=DATA_XLSX)
        dataset_used = "full"
    else:
        df = load_prices_dataframe(csv_path=SAMPLE_CSV, xlsx_path=None)
        dataset_used = "sample"

    con = duckdb.connect(database=":memory:")

    # 2) Create table + insert data
    con.execute((SQL_DIR / "00_create_table.sql").read_text())
    con.register("tmp_df", df)
    con.execute("INSERT INTO dji_prices SELECT * FROM tmp_df")

    # 3) Enrich + KPIs via SQL
    con.execute((SQL_DIR / "01_enrich_prices.sql").read_text())
    con.execute((SQL_DIR / "02_kpis.sql").read_text())

    # 4) Export results
    kpis = con.execute("SELECT * FROM dji_kpi_summary").df()
    monthly = con.execute("SELECT * FROM dji_monthly_returns").df()
    yearly = con.execute("SELECT * FROM dji_yearly_returns").df()
    rolling = con.execute("SELECT * FROM dji_rolling_vol").df()

    kpis.to_csv(OUTPUT_DIR / "kpis_summary.csv", index=False)
    monthly.to_csv(OUTPUT_DIR / "monthly_returns.csv", index=False)
    yearly.to_csv(OUTPUT_DIR / "yearly_returns.csv", index=False)
    rolling.to_csv(OUTPUT_DIR / "rolling_volatility.csv", index=False)

    print(f"OK. Dataset used: {dataset_used}. Outputs written to: {OUTPUT_DIR}")

if __name__ == "__main__":
    main()
