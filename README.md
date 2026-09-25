# Heating Systems — design and selection

A Windows desktop application (WPF, .NET 8) for engineering calculations of a building's design heat load and
annual energy demand, and for choosing a heating system. It compares boilers, district heating and heat pumps by
running cost and CO₂ emissions and selects specific heat pump models from a catalogue of
**9,075 certified models by 174 manufacturers**.

The user interface is bilingual: **English** (default) and **Ukrainian**. The language can be switched at any time
in the sidebar; the choice is remembered.

> The original version of the project (before the rework) is kept on the branch `backup/original-project`.

## Features

| Page | Purpose |
|---|---|
| **Building** | Climate, geometry and construction input. U-values are updated live, invalid input is highlighted. |
| **Results** | Design heat load, heat loss breakdown, annual demand, demand by outdoor temperature, check against DBN V.2.6-31. |
| **Comparison** | Annual cost (UAH), fuel quantity and CO₂ for 8 types of heat generators and the best heat pumps. |
| **Heat pump selection** | Seasonal simulation of every suitable catalogue model for the building; constraints for heat source, refrigerant, noise and oversizing. |
| **Catalogue** | Search and filtering of all models; operating points and the model-reproduced SCOP for each. |
| **Reference data** | Tariffs (editable), climate, materials, windows, heat generators, data sources. |
| **Projects** | Saving and opening calculations (SQLite). |

The calculation report can be exported to HTML (Ctrl+P) and printed or saved as PDF from the browser.

## Methods

| Quantity | Formula / standard |
|---|---|
| Thermal transmittance | U = 1 / (R_si + Σ d/λ + R_se), EN ISO 6946 (R_se = R_si towards unheated spaces) |
| Slab on ground | EN ISO 13370: B′ = A/(0.5P), d_t = w + λ_g(R_si + R_f + R_se); periodic coefficient H_pe = 0.37·P·λ·ln(δ/d_t + 1) |
| Design heat load | EN 12831: Φ = Σ A·(U + ΔU_TB)·b·(θi − θe) + f_g1·f_g2·A·U_g·(θi − θe) + 0.34·V̇·(θi − θe) |
| Infiltration / ventilation | V̇_inf = V·n50·e; natural V̇ = max(V̇_inf, n·V); mechanical V̇ = V̇_inf + n·V·(1 − η_HR) |
| Annual demand | bin method: heating season hours distributed over outdoor temperature (normal distribution, mean = season mean, θe ≈ 3.3σ quantile); internal gains with the EN ISO 13790 utilisation factor η = (1 − γ^a)/(1 − γ^(a+1)) in every bin |
| Heating curve | θ_flow = θi + (θ_flow,d − θi)·x^(1/n), x = (θi − θ)/(θi − θe), n = 1.3 (radiators) / 1.1 (underfloor) |
| Heat pump | hplib model: COP = p1·T_in + p2·T_out + p3 + p4·T_amb; inverter units: P_el,max = P_el,ref, P_el,min = 25 %; cycling per EN 14825: COP·CR/(0.9·CR + 0.1); electric back-up below TOL and when output is insufficient |
| Domestic hot water | Q = N·V·365·1.163·10⁻³·(θ_hot − θ_cold)·(1 + f_losses) |
| Boilers | seasonal efficiency on net calorific value; condensing boilers gain efficiency at lower flow temperatures |

**Heat pump model validation.** An automated test reproduces the certified SCOP of all 9,075 catalogue models with
the EN 14825 bin method (average climate, 35 °C) from the regression coefficients. Median error ≈ 4 %,
below 8 % for 90 % of the models.

## Data

* **Heat pump catalogue** — [Heat Pump KEYMARK](https://keymark.eu) certification data as processed by
  [hplib](https://github.com/FZJ-IEK3-VSA/hplib) (Forschungszentrum Jülich, MIT licence, commit `125c2ef`).
  Raw file: `database/raw/hplib_database.csv`. During import 14 generic records, 856 exact duplicates and
  8 records with a non-physical COP model were dropped. Every catalogue entry is a real certified product.
* **Climate** — reference values for the regional centres of Ukraine per DSTU-N B V.1.1-27:2010. The standard could
  not be retrieved while the data were prepared, so the values **must be verified** against its tables before being
  used in design documentation. All values can be changed in the database.
* **Tariffs and CO₂ factors** — indicative (2024–2025), editable on the "Reference data" page.
* **Materials and windows** — DSTU B V.2.6-189:2013, EN ISO 10456, EN ISO 10077-1 (typical values).
* **Envelope requirements** — DBN V.2.6-31:2016, table 3.

Reference data are stored in both languages (`name_en` / `name_uk` columns).

## Database

The SQLite file `database/heating_catalog.db` ships with the application. On first start it is copied to
`%LOCALAPPDATA%\HeatingSystems\heating.db`, where projects, edited tariffs and settings (UI language) are stored.
When a newer catalogue is shipped, the application migrates the user's projects, tariffs and settings automatically
and keeps the previous file as `.bak`.

```
database/
  schema.sql            schema (reference data, catalogue, projects, settings, view v_heat_pumps, indexes)
  seed_reference.sql    bilingual reference data with a source for every value
  raw/                  hplib source data + licence
  heating_catalog.db    built database (generated)
tools/
  build_catalog.py      builds the database (Python standard library only)
  verify_catalog.py     checks that the committed database is reproducible from the raw data
```

Rebuild the database: `python tools/build_catalog.py`

## Localisation

UI strings live in `src/HeatingSystems.Core/Localization/strings-en.json` and `strings-uk.json`.
XAML uses the `{l:Tr key}` markup extension, code uses `Localizer.T("key")` / `Localizer.F("key", args)`.
Tests make sure both tables contain the same keys and placeholders and that every key used in the source exists.

## Solution structure

```
src/HeatingSystems.Core    calculation engine, models, localisation, report (net8.0, no dependencies)
src/HeatingSystems.Data    SQLite data access (Microsoft.Data.Sqlite)
src/HeatingSystems.App     WPF user interface, MVVM (CommunityToolkit.Mvvm)
tests/HeatingSystems.Tests       unit tests for the engine, database, localisation and model validation (cross-platform)
tests/HeatingSystems.App.Tests   UI smoke test: opens the window, visits every page in both languages, fails on binding errors
```

## Build and run

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
(or Visual Studio 2022 17.8+).

```
dotnet build HeatingSystems.sln -c Release
dotnet test  HeatingSystems.sln -c Release
dotnet run   --project src/HeatingSystems.App
```

The engine and database tests also build and run on Linux/macOS: `dotnet test tests/HeatingSystems.Tests`.

### Running without installing .NET

1. Open the repository's **Actions** tab → the latest successful **CI** run → **Artifacts**.
2. Download `HeatingSystems-win-x64` (requires signing in to GitHub) and extract it to any folder.
3. Run `HeatingSystems.exe`. If Windows SmartScreen shows a warning, choose "More info" → "Run anyway"
   (the application is not code-signed).

The build is self-contained (.NET 8 included) and runs on Windows 10/11 x64. Keep `heating_catalog.db` next to the
`.exe`.

Keyboard shortcuts: **F5** — calculate, **Ctrl+S** — save, **Ctrl+N** — new project, **Ctrl+P** — report.

## Limitations

* The building is treated as a single thermal zone; solar gains are not included, so the demand is conservative.
* The distribution of outdoor temperature hours is approximated from climate parameters; hourly climate data
  (e.g. TMY files) would be more accurate.
* The heat pump ranking uses running costs only: equipment, installation and borehole costs are not in the catalogue.
