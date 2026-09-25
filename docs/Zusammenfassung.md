# Zusammenfassung der Überarbeitung

**Ausgangslage.** Das ursprüngliche Projekt war ein WPF-Formular (.NET Framework 4.8) mit Eingabefeldern für
Temperaturen, Wand- und Fenstermaterialien. Es enthielt weder Berechnungen noch eine Datenbank. Der
ursprüngliche Stand ist im Branch `backup/original-project` gesichert.

**Neuer Stand.**

* **Architektur:** .NET 8, drei Schichten — `Core` (Berechnung), `Data` (SQLite), `App` (WPF/MVVM) — plus Tests
  und CI (GitHub Actions, Linux und Windows).
* **Berechnung nach Normen:** U-Werte nach EN ISO 6946, erdberührte Bodenplatte nach EN ISO 13370,
  Heizlast nach EN 12831, Jahresheizwärmebedarf mit dem Bin-Verfahren und dem Ausnutzungsgrad der
  Wärmegewinne nach EN ISO 13790, Trinkwarmwasser.
* **Wärmepumpen:** Jahressimulation mit dem hplib-Kennfeldmodell, Heizkurve, Teillast und Takten nach
  EN 14825, Heizstab unterhalb der Einsatzgrenze. Das Modell reproduziert den zertifizierten SCOP aller
  9 075 Geräte mit einem medianen Fehler von etwa 4 % (automatisierter Test).
* **Datenbank:** SQLite mit Nachschlagedaten (Materialien, Fenster, Klima, Tarife, Wärmeerzeuger,
  Mindestwärmedurchlasswiderstände) und einem Katalog von **9 075 realen, zertifizierten Wärmepumpen von
  174 Herstellern** (Heat Pump KEYMARK über hplib, MIT-Lizenz). Keine Produktdaten sind erfunden. Der
  Katalog lässt sich reproduzierbar aus den Rohdaten erzeugen (`tools/build_catalog.py`,
  `tools/verify_catalog.py`).
* **Oberfläche:** neues Design mit Seitenleiste, Live-U-Werten, Validierung, Kennzahlen, Diagrammen,
  Systemvergleich (Kosten und CO₂), Wärmepumpenauswahl, durchsuchbarem Katalog, editierbaren Tarifen,
  Projektverwaltung und HTML-Berichtsexport.

**Offene Punkte.** Die Klimadaten der Gebietshauptstädte sind Richtwerte nach ДСТУ-Н Б В.1.1-27:2010 und
müssen mit der Norm abgeglichen werden, denn das Dokument war während der Arbeit nicht abrufbar. Tarife sind
Richtwerte. Investitionskosten der Anlagen sind nicht enthalten.
