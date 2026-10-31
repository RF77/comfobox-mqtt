# 01 – Ist-Zustand (Legacy-Bestand)

> ⚠️ **Dieser Abschnitt dokumentiert den Altbestand unter [`src_old/`](../../src_old).**
>
> Der Code wurde am 2026-10-31 mit `git mv src src_old` **verschoben** – die
> Dateihistorie bleibt vollständig erhalten. `src_old/` wird **nicht** mehr gebaut und
> **nicht** mehr gepflegt; er dient ausschließlich als **Referenz** für die
> Neuzuimplementierung in [`src/`](../../src):
>
> | Wofür | Quelle in `src_old/` |
> |---|---|
> | BACnet-Protokollverhalten | `Bacnet/` |
> | Item-Metadaten (127 Items, BACnet-IDs, Min/Max, Einheiten) | `ComfoBoxLib/Items/**` |
> | Fehlerbilder und Verhaltensreferenzen | `ComfoBoxMqtt/`, `ComfoBoxLib/` |
>
> **Zeilenangaben in diesem Abschnitt beziehen sich auf `src_old/`.**
> → [src/README.md](../../src/README.md) · [Roadmap](../04-migration/roadmap.md)

Diese Sektion beschreibt das System **wie es heute im Repo liegt**. Sie ist die Grundlage für die
Modernisierungs-Spec in [../02-modernisierung](../02-modernisierung/README.md).

Keine Bewertung, keine Änderungsvorschläge – nur Fakt, damit die Befunde in
[befunde.md](befunde.md) nachvollziehbar sind.

---

## Kurzfassung

ComfoBox MQTT ist ein **RS485/BACnet-MSTP-Gateway** für die Zehnder-ComfoBox Series 5 mit
ELESTA-Controller. Ein .NET-Framework-4.5.2-Prozess liest 127 BACnet-Objekte per Polling und
veröffentlicht sie als MQTT-Topics. Der Nutzer steuert die Heizung über Home Assistant, openHAB
oder Grafana/InfluxDB.

```
                    ┌───────────────────────────────┐
  USB-RS485-Adapter │  ComfoBoxMqttConsole (.NET 4.5)│
  /dev/ttyUSB0  ◄───┤  ComfoBoxLib → BACnet MSTP      │
                    │  ComfoBoxMqtt  → MQTT (M2Mqtt) │
                    └───────────────┬───────────────┘
                                    │ MQTT 1883
                    ┌───────────────▼───────────────┐
                    │ Home Assistant / openHAB      │
                    │ Mosquitto Broker              │
                    └───────────────────────────────┘
```

**Alter:** Code von 2015–2016, ein Commit, keine CI, keine Tests, kein Docker.
**Laufzeit:** Mono auf Raspberry Pi (Linux) oder Windows-Service (Windows).

---

## Dokumente

| Dokument | Inhalt |
|---|---|
| [architektur.md](architektur.md) | Solution, Projekte, Schichten, Datenfluss, Threading, Abhängigkeiten |
| [hardware-bacnet.md](hardware-bacnet.md) | Verdrahtung, Adapter, MSTP-Protokoll, Timings, Baudraten |
| [mqtt-schema.md](mqtt-schema.md) | Topic-Baum, Payloads, QoS, Retain, Kompatibilitätsvertrag |
| [konfiguration.md](konfiguration.md) | Alle Settings, app.config, Log4net, Doppelstruktur |
| [betrieb-heute.md](betrieb-heute.md) | Installation, Service, Mono, Deployment per Copy-Skript |
| [befunde.md](befunde.md) | Priorisierte Defekte und Risiken |

---

## Projektübersicht

| Projekt | Pfad | Typ | TFM | Zweck |
|---|---|---|---|---|
| `BacnetLib` | `src_old/Bacnet` | Library | net452 | Fremdimport BACnet-Protokollstack (UDP + MSTP), ~12.800 Z. |
| `ComfoBoxLib` | `src_old/ComfoBoxLib` | Library | net452 | BACnet-Objekt-Katalog (127 Items) + Client-Wrapper |
| `ComfoBoxMqtt` | `src_old/ComfoBoxMqtt` | Library | net452 | MQTT-Fassade, Item-Factory, Polling |
| `ComfoBoxMqttConsole` | `src_old/ComfoBoxMqttConsole` | Exe | net452 | Konsolen-App – **Haupt-Deployment auf Linux/Mono** |
| `ComfoboxService` | `src_old/ComfoboxService` | WinExe | net452 | Windows-Service-Wrapper |
| `DemoClient` | `src_old/DemoClient` | WinExe (WPF) | net452 | Standalone-GUI direkt gegen BACnet |
| `ServiceInstaller` | `src_old/ServiceInstaller` | WiX-Paketet | – | MSI-Installer für den Windows-Service |
| `XmlConfigMerge` | `src_old/XmlConfigMerge` | Binär-Artefakt | – | `XmlConfigMergeConsole.exe` – **nur Binärdatei, kein Quellcode** |

Solution: `src/Comfobox.sln` (VS 2015, Format 12.00).

### Abhängigkeiten (alle via `packages.config`, kein `PackageReference`)

| Paket | Version | Projekte |
|---|---|---|
| log4net | 2.0.5 | alle |
| M2Mqtt | 4.3.0.0 | ComfoBoxMqtt, Console, Service |
| Fasterflect | 2.1.3 | ComfoBoxMqtt, DemoClient |
| Newtonsoft.Json | 8.0.3 | ComfoBoxMqtt |
| Nito.AsyncEx | 3.0.1 | Console, Service |

### Zentrale Artefakte (dürfen bei der Migration nicht verloren gehen)

| Artefakt | Inhalt | Verwendung in der Migration |
|---|---|---|
| [`docs/topics.json`](../topics.json) | 127 Items mit Subscribe-/Publish-Topic, Typ, Min/Max, Unit, Readonly | **Fixture für Contract-Tests** |
| [`docs/topics.md`](../topics.md) | dieselben 127 Items als Markdown | menschlich lesbare Referenz |
| [`docs/Mono/MoMA Scan Results.html`](../Mono/MoMA%20Scan%20Results.html) | BACnet-Monitor-Scan des ELESTA | **Fixture für BACnet-Regressionstests** |
| `src_old/ComfoBoxLib/Items/**` | 127 Item-Definitionen mit BACnet-Objekt-IDs | Kern des Datenmodells |
