# compare-payloads – MQTT-Payloads vergleichen

Werkzeug für **Aufgabe 7.2** (Phase 7): es zeigt, dass die alte und die neue
Implementierung **dieselben** Werte publizieren. Das ist die Voraussetzung
für die Umschaltung auf `ComfoBox`.

## Die Regeln

| Vergleich | Regel |
|---|---|
| Zahlen | Betrag + Toleranz (0.01) |
| Enums | **exakter** Name – keine Toleranz |
| Verfügbarkeit | Zeitstempel-Differenz < 2 Zyklen |

Verglichen werden **nur** die Topics aus `docs/topics.json` (127 Items). Damit
kann der Vergleich keine Topics erfinden (Status, Discovery, `/Set`) und keine
auslassen. Die beiden Gateways dürfen einen **verschiedenen `BaseTopic`**
haben: das erste Segment wird entfernt, bevor verglichen wird.

## Aufruf

```bash
# Live gegen Live (zwei Gateways, unterschiedliches BaseTopic)
python tools/compare-payloads/compare_payloads.py \
    --old mqtt://broker:1883/ComfoBox/# \
    --new mqtt://broker:1883/ComfoBox2/# \
    --duration 300

# Gegen eine Aufzeichnung des alten Gateways (Golden Master)
python tools/compare-payloads/compare_payloads.py \
    --old-file old-snapshot.json \
    --new mqtt://broker:1883/ComfoBox2/# \
    --duration 300

# Aufzeichnung anlegen (am realen System, mit dem alten Gateway)
python tools/compare-payloads/compare_payloads.py record \
    --source mqtt://broker:1883/ComfoBox/# \
    --out old-snapshot.json --duration 600
```

Der **Exit-Code ist ungleich 0**, wenn es Abweichungen gab – damit lässt sich
der Vergleich in ein Skript oder eine CI-Pipeline einhängen.

### Optionen

| Option | Bedeutung |
|---|---|
| `--duration` | Dauer der Aufzeichnung in Sekunden (Standard 300) |
| `--tolerance` | Toleranz für Zahlenwerte (Standard 0.01) |
| `--poll-interval` | Dauer eines Poll-Zyklus (Standard 60 s) |
| `--secondary-factor` | Wie viele Zyklen ein sekundäres Item wartet (Standard 5) |
| `--catalog` | Pfad zu `docs/topics.json` |
| `--json` | Schreibt den Report als JSON in eine Datei |
| `--username` / `--password` | Zugangsdaten für den Broker |

## Voraussetzungen

```bash
pip install -r tools/compare-payloads/requirements.txt
```

Nur der Live-Modus braucht `paho-mqtt`; der Vergleich selbst ist reine
Standardbibliothek.

## Tests

```bash
python -m pytest tools/compare-payloads/tests -q
```

Die Tests brauchen **keinen** Broker – der Vergleich ist gegen Aufzeichnungen
(`Snapshot`) testbar.

## Grenzen

- Der Vergleich sagt nichts über die **BACnet-Schicht** aus. Er vergleicht die
  MQTT-Payloads. Die BACnet-Schicht ist durch die Golden-Tests in
  `ComfoBox.Bacnet.Tests` abgedeckt.
- Der Vergleich braucht auf beiden Seiten **denselben Gerätezustand**. Im
  Simulationsbetrieb (`Bacnet:Simulation=true`) liefern beide Instanzen
  dieselben deterministischen Werte; am realen System muss die zweite Instanz
  am selben Gerät hängen.
