-- HeatingSystems catalogue database (SQLite 3). Reference data is bilingual (English / Ukrainian).
-- Build: python tools/build_catalog.py  →  database/heating_catalog.db
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS meta (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- ---------------------------------------------------------------- reference data
CREATE TABLE IF NOT EXISTS materials (
    id           INTEGER PRIMARY KEY,
    name_en      TEXT    NOT NULL UNIQUE,
    name_uk      TEXT    NOT NULL,
    category     INTEGER NOT NULL CHECK (category BETWEEN 1 AND 5), -- 1 masonry, 2 concrete, 3 timber, 4 insulation, 5 finish
    conductivity REAL    NOT NULL CHECK (conductivity > 0),        -- λ, W/(m·K)
    density      REAL    NOT NULL CHECK (density > 0),             -- kg/m³
    source       TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS window_types (
    id      INTEGER PRIMARY KEY,
    name_en TEXT NOT NULL UNIQUE,
    name_uk TEXT NOT NULL,
    u_value REAL NOT NULL CHECK (u_value > 0),                -- U_w, W/(m²·K)
    g_value REAL NOT NULL CHECK (g_value BETWEEN 0 AND 1),
    source  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS climate_locations (
    id                       INTEGER PRIMARY KEY,
    city_en                  TEXT    NOT NULL UNIQUE,
    city_uk                  TEXT    NOT NULL,
    region_en                TEXT    NOT NULL,
    region_uk                TEXT    NOT NULL,
    design_temperature       REAL    NOT NULL,                -- θ_e, °C (coldest five-day period)
    heating_days             INTEGER NOT NULL CHECK (heating_days BETWEEN 1 AND 366),
    heating_mean_temperature REAL    NOT NULL,                -- °C
    annual_mean_temperature  REAL    NOT NULL,                -- °C
    source                   TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS energy_carriers (
    code            TEXT PRIMARY KEY,
    name_en         TEXT NOT NULL,
    name_uk         TEXT NOT NULL,
    unit_en         TEXT NOT NULL,
    unit_uk         TEXT NOT NULL,
    energy_per_unit REAL NOT NULL CHECK (energy_per_unit > 0), -- kWh per unit
    price_per_unit  REAL NOT NULL CHECK (price_per_unit >= 0), -- UAH per unit
    co2_per_kwh     REAL NOT NULL CHECK (co2_per_kwh >= 0),    -- kg CO₂ per kWh final energy
    source          TEXT NOT NULL,
    updated_at      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS heating_technologies (
    code                  TEXT PRIMARY KEY,
    name_en               TEXT NOT NULL,
    name_uk               TEXT NOT NULL,
    carrier_code          TEXT NOT NULL REFERENCES energy_carriers (code),
    seasonal_efficiency   REAL NOT NULL CHECK (seasonal_efficiency > 0),
    low_temperature_bonus REAL NOT NULL DEFAULT 0,
    description_en        TEXT NOT NULL,
    description_uk        TEXT NOT NULL,
    source                TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS envelope_requirements (
    climate_zone   INTEGER NOT NULL,
    element        INTEGER NOT NULL,                          -- see EnvelopeElement enum
    min_resistance REAL    NOT NULL CHECK (min_resistance > 0), -- R_q,min, m²·K/W
    source         TEXT    NOT NULL,
    PRIMARY KEY (climate_zone, element)
);

-- ---------------------------------------------------------------- product catalogue
CREATE TABLE IF NOT EXISTS manufacturers (
    id   INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS heat_pumps (
    id                          INTEGER PRIMARY KEY,
    manufacturer_id             INTEGER NOT NULL REFERENCES manufacturers (id),
    series                      TEXT    NOT NULL,
    model                       TEXT    NOT NULL,
    source_type                 INTEGER NOT NULL CHECK (source_type IN (1, 2, 3)), -- 1 air, 2 brine, 3 water
    control                     INTEGER NOT NULL CHECK (control IN (1, 2)),        -- 1 inverter, 2 on/off
    hplib_group                 INTEGER NOT NULL,
    certification_date          TEXT,                                              -- ISO 8601
    refrigerant                 TEXT    NOT NULL,
    refrigerant_mass_kg         REAL,
    rated_power_low             REAL    NOT NULL,  -- kW, 35 °C
    rated_power_medium          REAL    NOT NULL,  -- kW, 55 °C
    scop                        REAL    NOT NULL,  -- EN 14825, average climate, 35 °C
    eta_s_low                   REAL    NOT NULL,  -- %
    eta_s_medium                REAL    NOT NULL,  -- %
    sound_power_indoor          REAL,              -- dB(A)
    sound_power_outdoor         REAL,              -- dB(A)
    max_flow_temperature        REAL    NOT NULL,  -- °C
    bivalent_temperature        REAL,              -- °C
    operation_limit_temperature REAL,              -- TOL, °C
    heating_rod_power           REAL,              -- kW
    p_th_ref                    REAL    NOT NULL,  -- W at A-7/W52 (B0/W52, W10/W52)
    p_el_ref                    REAL    NOT NULL,  -- W
    cop_ref                     REAL    NOT NULL,
    cop_p1 REAL NOT NULL, cop_p2 REAL NOT NULL, cop_p3 REAL NOT NULL, cop_p4 REAL NOT NULL,
    pel_p1 REAL NOT NULL, pel_p2 REAL NOT NULL, pel_p3 REAL NOT NULL, pel_p4 REAL NOT NULL,
    cop_a7w35                   REAL    NOT NULL,  -- model COP at A7/W35 (B0/W35, W10/W35)
    cop_a2w55                   REAL    NOT NULL   -- model COP at A2/W55 (B0/W55, W10/W55)
);

CREATE INDEX IF NOT EXISTS ix_heat_pumps_source_power ON heat_pumps (source_type, p_th_ref);
CREATE INDEX IF NOT EXISTS ix_heat_pumps_manufacturer ON heat_pumps (manufacturer_id);
CREATE INDEX IF NOT EXISTS ix_heat_pumps_power ON heat_pumps (rated_power_low);
CREATE INDEX IF NOT EXISTS ix_heat_pumps_scop ON heat_pumps (scop);
CREATE INDEX IF NOT EXISTS ix_heat_pumps_refrigerant ON heat_pumps (refrigerant);

CREATE VIEW IF NOT EXISTS v_heat_pumps AS
SELECT hp.*, m.name AS manufacturer
FROM heat_pumps hp
JOIN manufacturers m ON m.id = hp.manufacturer_id;

-- ---------------------------------------------------------------- user data
CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS projects (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    name             TEXT NOT NULL,
    city             TEXT NOT NULL,
    design_heat_load REAL NOT NULL,   -- W
    annual_demand    REAL NOT NULL,   -- kWh/a
    payload          TEXT NOT NULL,   -- JSON (ProjectData)
    created_at       TEXT NOT NULL,
    updated_at       TEXT NOT NULL
);
