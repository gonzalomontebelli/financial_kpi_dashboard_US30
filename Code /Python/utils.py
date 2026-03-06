import pandas as pd
from pathlib import Path

def load_prices_dataframe(csv_path: Path | None = None, xlsx_path: Path | None = None) -> pd.DataFrame:
    if xlsx_path is not None and xlsx_path.exists():
        # Expect a sheet with OHLCV columns. If multiple sheets, use the first.
        df = pd.read_excel(xlsx_path)
    elif csv_path is not None and csv_path.exists():
        df = pd.read_csv(csv_path)
    else:
        raise FileNotFoundError("No dataset found. Put data in data/dji_prices.csv or data/dji_prices.xlsx")

    # Normalize column names
    df.columns = [c.strip().title() for c in df.columns]
    if "Date" not in df.columns:
        raise ValueError("Expected a 'Date' column.")
    df["Date"] = pd.to_datetime(df["Date"]).dt.date
    df = df.sort_values("Date").reset_index(drop=True)
    return df
