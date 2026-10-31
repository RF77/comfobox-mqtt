# 05 – Architektur-Entscheidungen (ADR)

Ein **Architecture Decision Record** (ADR) dokumentiert eine Entscheidung, ihre Begründung und
ihre Konsequenzen. Ein ADR ist **unveränderlich** – eine geänderte Entscheidung wird durch ein
**neues** ADR ersetzt, das das alte ersetzt (Status `Superseded`).

## 1. Konventionen

| Feld | Bedeutung |
|---|---|
| **Status** | `Proposed` → `Accepted` → (`Deprecated` \| `Superseded`) |
| **Datum** | Datum der Entscheidung |
| **Kontext** | Die Ausgangslage und die Kräfte, die wirken |
| **Entscheidung** | Was wird entschieden |
| **Alternativen** | Welche Optionen erwogen und verworfen wurden |
| **Konsequenzen** | Was das nach sich zieht (positiv **und** negativ) |

## 2. Übersicht

| ADR | Titel | Status |
|---|---|---|
| [ADR-001](adr-001-dotnet-version.md) | Ziel-Framework .NET 10 (LTS) | Accepted |
| [ADR-002](adr-002-mqtt-client.md) | MQTT-Library: Wechsel von M2Mqtt zu MQTTnet 5 | Accepted |
| [ADR-003](adr-003-frontend.md) | Frontend: Angular 22 + Optimus UI | Accepted |
| [ADR-004](adr-004-containerisierung.md) | Betrieb als Container-Image auf Synology DS923+ | Accepted |
| [ADR-005](adr-005-api-stil.md) | Schnittstelle Gateway ↔ UI: REST + SSE | Accepted |
| [ADR-006](adr-006-item-katalog.md) | Item-Katalog als deklarative Domäne | Accepted |

## 3. Offene Entscheidungen (noch kein ADR)

Diese Fragen sind noch offen und werden nach Rückmeldung zu ADRs
(siehe [offene Fragen](../02-modernisierung/uebersicht-und-zielbild.md#9-offene-fragen-entscheidung-durch-den-auftraggeber)):

| # | Frage | Mögliche ADR |
|---|---|---|
| Q1 | Eingebetteter Broker im Gateway? | ADR-007 (optional) |
| Q2 | Authentifizierung der Web-UI | ADR-008 |
| Q3 | Ein oder mehrere ComfoBox-Geräte? | ADR-009 (falls >1) |
| Q4 | `ExpertMode` zur Laufzeit umschaltbar? | Teil von ADR-005 |
| Q6 | Discovery-Prefix konfigurierbar? | Teil von ADR-002 |
