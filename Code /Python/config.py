from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]

# Prefer full dataset if provided; fall back to sample.
DATA_CSV = REPO_ROOT / "data" / "dji_prices.csv"
DATA_XLSX = REPO_ROOT / "data" / "dji_prices.xlsx"
SAMPLE_CSV = REPO_ROOT / "data" / "sample" / "dji_sample.csv"

OUTPUT_DIR = REPO_ROOT / "outputs"
SQL_DIR = REPO_ROOT / "sql"
EXCEL_DIR = REPO_ROOT / "excel"
