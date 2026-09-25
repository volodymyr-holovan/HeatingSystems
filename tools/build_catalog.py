#!/usr/bin/env python3
"""Build the HeatingSystems SQLite catalogue.

Creates ``database/heating_catalog.db`` from
  * ``database/schema.sql``            – table definitions,
  * ``database/seed_reference.sql``    – materials, windows, climate, tariffs, technologies, requirements,
  * ``database/raw/hplib_database.csv`` – Heat Pump KEYMARK product population prepared by the
    hplib project (FZJ-IEK3-VSA/hplib, MIT licence).

Import rules (all rows that are dropped are counted and stored in the ``meta`` table):
  * "Generic" archetype rows are skipped – only real certified products are imported;
  * exact duplicates (same manufacturer, model, reference power, SCOP and refrigerant) are merged;
  * rows whose regression model yields a non-physical COP (≤ 1 or > 10 at A7/W35 or A2/W55)
    or non-positive reference powers are rejected.

Usage:  python tools/build_catalog.py [--output PATH]
Only the Python standard library is required.
"""
from __future__ import annotations

import argparse
import csv
import datetime as dt
import hashlib
import math
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DB_DIR = ROOT / "database"
RAW_CSV = DB_DIR / "raw" / "hplib_database.csv"
HPLIB_COMMIT = "125c2ef6c3fc38043368daac1352557fd2a90106"
CATALOG_VERSION = "2026.09.1"

# hplib group id -> (source_type, control); 1 air, 2 brine, 3 water; 1 regulated, 2 on/off
GROUPS = {1: (1, 1), 2: (2, 1), 3: (3, 1), 4: (1, 2), 5: (2, 2), 6: (3, 2)}
# Source inlet temperature per source type for model evaluation (EN 14825 reference points)
SOURCE_T = {2: 0.0, 3: 10.0}


def num(value: str | None) -> float | None:
    if value is None:
        return None
    value = value.strip().replace(",", ".")
    if not value or value.lower() == "nan":
        return None
    try:
        result = float(value)
    except ValueError:
        return None
    return result if math.isfinite(result) else None


def refrigerant_mass(value: str) -> float | None:
    # e.g. "0,450 kg"
    return num(value.lower().replace("kg", ""))


def iso_date(value: str) -> str | None:
    value = value.strip()
    if not value:
        return None
    try:
        return dt.datetime.strptime(value, "%d.%m.%Y").date().isoformat()
    except ValueError:
        return None


def positive(value: float | None) -> float | None:
    return value if value is not None and value > 0 else None


def model_cop(row: dict, source_type: int, t_amb: float, t_out: float) -> float:
    t_in = SOURCE_T.get(source_type, t_amb)
    return row["cop_p1"] * t_in + row["cop_p2"] * t_out + row["cop_p3"] + row["cop_p4"] * t_amb


def read_products(stats: dict) -> list[dict]:
    products: list[dict] = []
    seen: set[tuple] = set()
    with RAW_CSV.open(encoding="utf-8", newline="") as fh:
        for raw in csv.DictReader(fh):
            stats["rows"] += 1
            manufacturer = " ".join(raw["Manufacturer"].split())
            if manufacturer == "Generic":
                stats["generic"] += 1
                continue
            group = int(float(raw["Group"]))
            source_type, control = GROUPS[group]
            row = {
                "manufacturer": manufacturer,
                "series": " ".join(raw["Model"].split()),
                "model": " ".join(raw["Titel"].split()),
                "source_type": source_type,
                "control": control,
                "hplib_group": group,
                "certification_date": iso_date(raw["Date"]),
                "refrigerant": raw["Refrigerant"].strip().upper(),
                "refrigerant_mass_kg": refrigerant_mass(raw["Mass of Refrigerant [kg]"]),
                "rated_power_low": num(raw["Rated Power low T [kW]"]),
                "rated_power_medium": num(raw["Rated Power medium T [kW]"]),
                "scop": num(raw["SCOP"]),
                "eta_s_low": num(raw["eta low T [%]"]),
                "eta_s_medium": num(raw["eta medium T [%]"]),
                "sound_power_indoor": positive(num(raw["SPL indoor high Power [dBA]"])) or positive(num(raw["SPL indoor low Power [dBA]"])),
                "sound_power_outdoor": positive(num(raw["SPL outdoor high Power [dBA]"])) or positive(num(raw["SPL outdoor low Power [dBA]"])),
                "max_flow_temperature": num(raw["Max. water heating temperature [°C]"]),
                "bivalent_temperature": num(raw["Bivalence temperature [°C]"]),
                "operation_limit_temperature": num(raw["Tolerance temperature [°C]"]),
                "heating_rod_power": num(raw["Power heating rod low T [kW]"]),
                "p_th_ref": num(raw["P_th_h_ref [W]"]),
                "p_el_ref": num(raw["P_el_h_ref [W]"]),
                "cop_ref": num(raw["COP_ref"]),
                "cop_p1": num(raw["p1_COP [-]"]), "cop_p2": num(raw["p2_COP [-]"]),
                "cop_p3": num(raw["p3_COP [-]"]), "cop_p4": num(raw["p4_COP [-]"]),
                "pel_p1": num(raw["p1_P_el_h [1/°C]"]), "pel_p2": num(raw["p2_P_el_h [1/°C]"]),
                "pel_p3": num(raw["p3_P_el_h [-]"]), "pel_p4": num(raw["p4_P_el_h [1/°C]"]),
            }
            required = ("rated_power_low", "rated_power_medium", "scop", "eta_s_low", "eta_s_medium",
                        "p_th_ref", "p_el_ref", "cop_ref", "cop_p1", "cop_p2", "cop_p3", "cop_p4",
                        "pel_p1", "pel_p2", "pel_p3", "pel_p4")
            if any(row[k] is None for k in required) or not row["refrigerant"]:
                stats["incomplete"] += 1
                continue
            if row["max_flow_temperature"] is None:
                row["max_flow_temperature"] = 55.0
                stats["default_max_flow"] += 1
            if row["p_th_ref"] <= 0 or row["p_el_ref"] <= 0:
                stats["invalid_model"] += 1
                continue
            row["cop_a7w35"] = model_cop(row, source_type, 7.0, 35.0)
            row["cop_a2w55"] = model_cop(row, source_type, 2.0, 55.0)
            if not (1.0 < row["cop_a7w35"] <= 10.0 and 1.0 < row["cop_a2w55"] <= 10.0):
                stats["invalid_model"] += 1
                continue
            key = (manufacturer.lower(), row["model"].lower(), row["p_th_ref"], row["scop"], row["refrigerant"])
            if key in seen:
                stats["duplicates"] += 1
                continue
            seen.add(key)
            products.append(row)
    products.sort(key=lambda r: (r["manufacturer"].lower(), r["model"].lower(), r["p_th_ref"]))
    return products


def build(output: Path) -> dict:
    stats = {k: 0 for k in ("rows", "generic", "incomplete", "invalid_model", "duplicates", "default_max_flow")}
    products = read_products(stats)

    tmp = output.with_suffix(".tmp")
    tmp.unlink(missing_ok=True)
    con = sqlite3.connect(tmp)
    try:
        con.executescript((DB_DIR / "schema.sql").read_text(encoding="utf-8"))
        con.executescript((DB_DIR / "seed_reference.sql").read_text(encoding="utf-8"))

        manufacturers = sorted({p["manufacturer"] for p in products}, key=str.lower)
        con.executemany("INSERT INTO manufacturers (id, name) VALUES (?, ?)", list(enumerate(manufacturers, start=1)))
        man_id = {name: i for i, name in enumerate(manufacturers, start=1)}

        columns = ["manufacturer_id", "series", "model", "source_type", "control", "hplib_group", "certification_date",
                   "refrigerant", "refrigerant_mass_kg", "rated_power_low", "rated_power_medium", "scop", "eta_s_low",
                   "eta_s_medium", "sound_power_indoor", "sound_power_outdoor", "max_flow_temperature",
                   "bivalent_temperature", "operation_limit_temperature", "heating_rod_power", "p_th_ref", "p_el_ref",
                   "cop_ref", "cop_p1", "cop_p2", "cop_p3", "cop_p4", "pel_p1", "pel_p2", "pel_p3", "pel_p4",
                   "cop_a7w35", "cop_a2w55"]
        sql = f"INSERT INTO heat_pumps (id, {', '.join(columns)}) VALUES ({', '.join('?' * (len(columns) + 1))})"
        rows = []
        for i, p in enumerate(products, start=1):
            p = dict(p, manufacturer_id=man_id[p["manufacturer"]])
            rows.append([i] + [p[c] for c in columns])
        con.executemany(sql, rows)

        digest = hashlib.sha256(RAW_CSV.read_bytes()).hexdigest()
        meta = {
            "schema_version": "1",
            "catalog_version": CATALOG_VERSION,
            "built_at": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat(),
            "data_source": "Heat Pump KEYMARK certified products via hplib (github.com/FZJ-IEK3-VSA/hplib, MIT licence)",
            "hplib_commit": HPLIB_COMMIT,
            "hplib_csv_sha256": digest,
            "heat_pumps_imported": str(len(products)),
            "manufacturers": str(len(manufacturers)),
            **{f"import_{k}": str(v) for k, v in stats.items()},
        }
        con.executemany("INSERT INTO meta (key, value) VALUES (?, ?)", meta.items())
        con.commit()
        con.execute("PRAGMA integrity_check").fetchone()
        con.execute("VACUUM")
    finally:
        con.close()
    tmp.replace(output)
    return {"products": len(products), "manufacturers": len(set(p["manufacturer"] for p in products)), **stats}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--output", type=Path, default=DB_DIR / "heating_catalog.db")
    args = parser.parse_args()
    result = build(args.output)
    for key, value in result.items():
        print(f"{key:>18}: {value}")
    print(f"written: {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
