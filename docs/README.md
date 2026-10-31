# ComfoBox MQTT – Projektdokumentation

Diese Dokumentation besteht aus zwei Teilen:

1. **[Ist-Zustand](01-ist-zustand/README.md)** – was der aktuelle Code (Stand 2016, .NET Framework 4.5.2) tatsächlich macht.
2. **[Modernisierung / Spec](02-modernisierung/README.md)** – die Spezifikation für die Erneuerung: .NET, MQTT-Library, UI, Container, Home-Assistant-Integration.

Die Teile 03–05 enthalten Deployment, Migrations-Roadmap und die dokumentierten Architektur-Entscheidungen (ADR).

---

## Inhaltsverzeichnis

| Ordner | Inhalt | Zielgruppe |
|---|---|---|
| [01-ist-zustand](01-ist-zustand/README.md) | Bestandsaufnahme des bestehenden Systems | Alle, die das System nicht kennen |
| [02-modernisierung](02-modernisierung/README.md) | **Spec** der Erneuerung | Implementierung |
| [03-deployment](03-deployment/README.md) | Container, Synology DS923+, CI/CD | Betrieb & Deployment |
| [04-migration](04-migration/README.md) | Roadmap, Kompatibilitäts-Garantien, Tests, Risiken | Projektleitung |
| [05-entscheidungen](05-entscheidungen/README.md) | Architektur-Entscheidungsprotokolle (ADR) | Reviewer |

### 01 – Ist-Zustand

| Dokument | Inhalt |
|---|---|
| [Architektur](01-ist-zustand/architektur.md) | Solution, Projekte, Schichten, Datenfluss, Threading-Modell |
| [Hardware & BACnet](01-ist-zustand/hardware-bacnet.md) | RS485-Adapter, Wiring, MSTP-Protokoll, Baudrate, Timings |
| [MQTT-Schema](01-ist-zustand/mqtt-schema.md) | Topic-Baum, Payloads, QoS, Retain, Konventionen |
| [Konfiguration](01-ist-zustand/konfiguration.md) | Alle Settings, app.config, Doppelstruktur, Log4net |
| [Betrieb heute](01-ist-zustand/betrieb-heute.md) | Installation, Windows-Service, Mono, Raspberry-Deployment |
| [Befunde](01-ist-zustand/befunde.md) | Priorisierte Liste der Defekte und Risiken (Basis für die Spec) |

### 02 – Modernisierung (Spec)

| Dokument | Inhalt |
|---|---|
| [Übersicht & Zielbild](02-modernisierung/uebersicht-und-zielbild.md) | Zielarchitektur, Komponenten, Datenflüsse, Migrationsstrategie |
| [Zielversionen](02-modernisierung/zielversionen.md) | Fixierte Versionen für .NET, MQTTnet, Angular, Optimus UI, Docker |
| [.NET-Migration](02-modernisierung/dotnet-migration.md) | TFM, Settings-Ersatz, Logging, Hosting, Paket-Mapping |
| [MQTT-Library-Migration](02-modernisierung/mqtt-library-migration.md) | M2Mqtt → MQTTnet, API-Mapping, LWT, Reconnect, TLS, QoS |
| [BACnet-Portierung](02-modernisierung/bacnet-portierung.md) | Was bleibt, was fliegt raus, wie der Protokollstack gesichert wird |
| [Datenmodell & API](02-modernisierung/datenmodell-und-api.md) | Item-Katalog, REST-API, SSE, Validierung, Schreibschutz |
| [Home Assistant](02-modernisierung/home-assistant.md) | Discovery, Availability, Restart, Payload-Fixes |
| [Health & Status](02-modernisierung/health-und-status.md) | Health-Endpunkte, Status-Topics, Fehlerpropagation, Alarmierung |
| [UI mit Angular & Optimus](02-modernisierung/ui-angular-optimus.md) | Frontend-Architektur, Optimus UI, Komponenten, Build/Pipeline |
| [Versionen & Pakete](02-modernisierung/zielversionen.md) | Fixierte Versionen für .NET, MQTTnet, Angular, Optimus UI, Docker |

### 03 – Deployment

| Dokument | Inhalt |
|---|---|
| [Containerisierung](03-deployment/containerisierung.md) | Dockerfile, Multi-Stage-Build, Compose, Image-Size, Secrets |
| [Synology DS923+](03-deployment/synology-ds923.md) | Container Manager, Serial-Device-Durchrechnung, udev, Betrieb |
| [Konfiguration & Secrets](03-deployment/konfiguration-und-secrets.md) | Env-Variablen, Bind-Mounts, Broker-Credentials, Backups |
| [CI/CD](03-deployment/ci-cd.md) | GitHub Actions: Build, Test, Image-Build, Release-Pipeline |

### 04 – Migration

| Dokument | Inhalt |
|---|---|
| [Roadmap](04-migration/roadmap.md) | Phasen, Aufwände, Abhängigkeiten, Meilensteine |
| [MQTT-Kompatibilität](04-migration/mqtt-kompatibilitaet.md) | Garantie: alle bestehenden Topic-Adressen bleiben gültig |
| [Teststrategie](04-migration/teststrategie.md) | Testpyramide, Regressionsschutz für Topic-Kompatibilität |
| [Risiken](04-migration/risiken.md) | Risikoregister mit Gegenmaßnahmen |
| [Checkliste](04-migration/checkliste.md) | Abnahme-Checklisten pro Phase |

### 05 – Entscheidungen

| ADR | Entscheidung |
|---|---|
| [ADR-001](05-entscheidungen/adr-001-dotnet-version.md) | Ziel-Framework .NET 10 (LTS) |
| [ADR-002](05-entscheidungen/adr-002-mqtt-client.md) | MQTT-Library: Wechsel von M2Mqtt zu MQTTnet |
| [ADR-003](05-entscheidungen/adr-003-frontend.md) | Frontend: Angular + Optimus UI |
| [ADR-004](05-entscheidungen/adr-004-containerisierung.md) | Betrieb als Container-Image (Synology DS923+) |
| [ADR-005](05-entscheidungen/adr-005-api-stil.md) | Schnittstelle zwischen Gateway und UI: REST + SSE |
| [ADR-006](05-entscheidungen/adr-006-item-katalog.md) | Item-Katalog als deklarative, framework-freie Domäne |

---

## Die sechs Anforderungen und wo sie adressiert sind

| # | Anforderung | Adressiert in |
|---|---|---|
| 1 | .NET und Libraries updaten, ggf. bessere MQTT-Library | [dotnet-migration](02-modernisierung/dotnet-migration.md), [mqtt-library-migration](02-modernisierung/mqtt-library-migration.md), [ADR-001](05-entscheidungen/adr-001-dotnet-version.md), [ADR-002](05-entscheidungen/adr-002-mqtt-client.md) |
| 2 | Angular/Optimus UI für Konfiguration und Status/Bedienung | [ui-angular-optimus](02-modernisierung/ui-angular-optimus.md), [ADR-003](05-entscheidungen/adr-003-frontend.md) |
| 3 | Docker-Image, Deployment auf DS923+, Serial-Port-Zugriff | [containerisierung](03-deployment/containerisierung.md), [synology-ds923](03-deployment/synology-ds923.md), [ADR-004](05-entscheidungen/adr-004-containerisierung.md) |
| 4 | MQTT-Adressen bleiben gleich | [mqtt-kompatibilitaet](04-migration/mqtt-kompatibilitaet.md) + Contract-Tests |
| 5 | Fehler/Verbindungsprobleme an HA und UI melden | [health-und-status](02-modernisierung/health-und-status.md), [home-assistant](02-modernisierung/home-assistant.md) |
| 6 | Neustart des Service aus HA heraus | [home-assistant](02-modernisierung/home-assistant.md#restart-aus-home-assistant), [datenmodell-und-api](02-modernisierung/datenmodell-und-api.md#restart) |

---

## Konventionen in diesem Dokument

- **Ist** beschreibt den Code, wie er heute im Repo liegt (inkl. Bugs).
- **Soll** beschreibt das Zielbild der Modernisierung.
- Pfad- und Zeilenverweise beziehen sich auf den Stand des Repos zum Zeitpunkt der Analyse.
- Versionsangaben sind in [zielversionen.md](02-modernisierung/zielversionen.md) fixiert und werden dort gepflegt.
