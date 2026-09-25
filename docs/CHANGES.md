# Summary of the rework

**Starting point.** The original project was a WPF form (.NET Framework 4.8) with input fields for temperatures,
wall and window materials. It contained neither calculations nor a database. The original state is preserved on the
branch `backup/original-project`.

**Current state.**

* **Architecture:** .NET 8 with three layers — `Core` (calculations), `Data` (SQLite), `App` (WPF/MVVM) — plus tests
  and CI (GitHub Actions on Linux and Windows).
* **Standards-based calculations:** U-values per EN ISO 6946, slab on ground per EN ISO 13370, design heat load per
  EN 12831, annual heating demand with a bin method and the EN ISO 13790 gain utilisation factor, domestic hot water.
* **Heat pumps:** seasonal simulation with the hplib performance map model, heating curve, part load and cycling per
  EN 14825, back-up heater below the operation limit. The model reproduces the certified SCOP of all 9,075 units with
  a median error of about 4 % (automated test).
* **Database:** SQLite with bilingual reference data (materials, windows, climate, tariffs, heat generators, minimum
  thermal resistances), user projects and settings, and a catalogue of **9,075 real certified heat pumps from
  174 manufacturers** (Heat Pump KEYMARK via hplib, MIT licence). No product data are invented. The catalogue can be
  rebuilt reproducibly from the raw data (`tools/build_catalog.py`, `tools/verify_catalog.py`).
* **User interface:** new design with a sidebar, live U-values, validation, key figures, charts, system comparison
  (cost and CO₂), heat pump selection, searchable catalogue, editable tariffs, project management, HTML report export
  and an English/Ukrainian language switch.

**Open points.** The climate data of the regional centres are reference values per DSTU-N B V.1.1-27:2010 and must be
checked against the standard (it was not accessible while the data were prepared). Tariffs are indicative. Investment
costs of the systems are not included.
