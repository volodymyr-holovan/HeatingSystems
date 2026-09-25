#!/usr/bin/env python3
"""Checks that database/heating_catalog.db is reproducible from the raw data.

Rebuilds the catalogue into a temporary file and compares the content of all data tables
(timestamps in ``meta`` are ignored). Exit code 1 when the committed database is stale.
"""
from __future__ import annotations

import hashlib
import sqlite3
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TABLES = ["materials", "window_types", "climate_locations", "energy_carriers", "heating_technologies",
          "envelope_requirements", "manufacturers", "heat_pumps"]


def digest(path: Path) -> dict[str, str]:
    con = sqlite3.connect(path)
    try:
        result = {}
        for table in TABLES:
            h = hashlib.sha256()
            for row in con.execute(f"SELECT * FROM {table} ORDER BY 1"):
                h.update(repr(row).encode("utf-8"))
            result[table] = h.hexdigest()
        rows = con.execute("SELECT key, value FROM meta WHERE key <> 'built_at' ORDER BY key").fetchall()
        result["meta"] = hashlib.sha256(repr(rows).encode("utf-8")).hexdigest()
        return result
    finally:
        con.close()


def main() -> int:
    committed = ROOT / "database" / "heating_catalog.db"
    with tempfile.TemporaryDirectory() as tmp:
        fresh = Path(tmp) / "fresh.db"
        subprocess.run([sys.executable, str(ROOT / "tools" / "build_catalog.py"), "--output", str(fresh)], check=True)
        a, b = digest(committed), digest(fresh)
    stale = [t for t in a if a[t] != b[t]]
    if stale:
        print("database/heating_catalog.db is out of date for tables:", ", ".join(stale))
        print("Run: python tools/build_catalog.py")
        return 1
    print("heating_catalog.db is reproducible from the raw data.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
