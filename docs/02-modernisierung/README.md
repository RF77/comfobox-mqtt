# 02 – Modernisierung (Spec)

Dies ist die **Spezifikation** für die Erneuerung des ComfoBox-MQTT-Gateways.

Ausgangspunkt ist der [Ist-Zustand](../01-ist-zustand/README.md), die Begründung der
Entscheidungen steht in den [ADRs](../05-entscheidungen/README.md).

---

## Die Anforderungen als Akzeptanzkriterien

| # | Anforderung | Akzeptanzkriterium | Spec |
|---|---|---|---|
| 1 | .NET & Libraries aktualisieren, bessere MQTT-Library | Build läuft mit .NET 10 SDK; `dotnet build` ohne Visual Studio; MQTT-Client = MQTTnet 5 mit TLS/Auth/Reconnect-Backoff | [dotnet-migration](dotnet-migration.md), [mqtt-library-migration](mqtt-library-migration.md) |
| 2 | Angular/Optimus UI | Web-UI mit Konfiguration, Statusübersicht und Bedienung; zeigt Verbindungs-/Fehlerzustand an | [ui-angular-optimus](ui-angular-optimus.md) |
| 3 | Docker-Image, DS923+, Serial-Port | `docker compose up` auf der DS923+ mit `/dev/ttyUSB0` im Container; Image ≤ 300 MB; **zusätzlich** Installation per Klick als Synology-Paket | [containerisierung](../03-deployment/containerisierung.md), [synology-ds923](../03-deployment/synology-ds923.md), [synology-paketcenter](../03-deployment/synology-paketcenter.md) |
| 7 | *Installation so bequem wie möglich* | SPK per Klick im DSM-Paketcenter → Browser öffnet sich → Getting-Started-Assistent (Portwahl, BACnet-/MQTT-Test, Benutzer + Passwort) | [synology-paketcenter](../03-deployment/synology-paketcenter.md), [ui-angular-optimus §3](ui-angular-optimus.md#3-getting-started-setup-assistent) |
| 4 | MQTT-Adressen bleiben gleich | **Alle** 127 Topics + `/Set`, `/AsNumber`, `/Special`, `/Connected`, `/Version` bleiben gültig; durch Contract-Test gegen `docs/topics.json` abgesichert | [mqtt-kompatibilitaet](../04-migration/mqtt-kompatibilitaet.md) |
| 5 | Fehler/Verbindungsprobleme an HA **und** UI | Health-Topic mit LWT, Status-Topics, REST-Health-Endpoint, sichtbarer Fehlerbanner in der UI | [health-und-status](health-und-status.md), [home-assistant](home-assistant.md) |
| 6 | Neustart aus HA möglich | `ComfoBox/Command/Restart` (Subscribe) + `POST /api/service/restart` + HA-Button-Entity | [home-assistant](home-assistant.md#6-neustart-aus-home-assistant-anforderung-6) |

---

## Dokumente

| Dokument | Inhalt |
|---|---|
| [uebersicht-und-zielbild.md](uebersicht-und-zielbild.md) | Zielarchitektur, Komponenten, Datenflüsse, Migrationsstrategie, Projektstruktur |
| [zielversionen.md](zielversionen.md) | Fixierte Versionen + Begründung + Update-Politik |
| [dotnet-migration.md](dotnet-migration.md) | TFM, Paket-Mapping, Settings-Ersatz, Logging, Hosting, Struktur |
| [mqtt-library-migration.md](mqtt-library-migration.md) | M2Mqtt → MQTTnet, API-Mapping, LWT, Reconnect, TLS, QoS, Payload-Fixes |
| [bacnet-portierung.md](bacnet-portierung.md) | Portierung des Protokollstacks, Teststrategie, Regressionssicherung |
| [datenmodell-und-api.md](datenmodell-und-api.md) | Item-Katalog, Typsystem, REST-API, SSE, Validierung, Schreibschutz, Restart |
| [home-assistant.md](home-assistant.md) | Discovery, Availability, Restart, Payload-Fixes, Migrationshilfen für HA |
| [health-und-status.md](health-und-status.md) | Health-Checks, Status-Topics, Fehlerpropagation, Persistenz, Alarmierung |
| [ui-angular-optimus.md](ui-angular-optimus.md) | Frontend-Architektur, Optimus UI, Komponentenübersicht, Build, Theming |
| [ha-integration.md](ha-integration.md) | **HA-Integration ohne MQTT**: HACS, Gerät + 127 Entitäten, API-Client, Coordinator |
| [benutzerdokumentation.md](benutzerdokumentation.md) | **Benutzerdoku in drei Kanälen**: Web-UI (HTML), HACS, Synology |

---

## Zielarchitektur in einem Bild

```
   ┌──────────────── Docker: comfobox-gateway (ein Image) ──────────────────┐
   │                                                                          │
   │  ┌────────────────────────────────────────────────────────────────────┐  │
   │  │  ComfoBox.Gateway  (.NET 10, ASP.NET Core / Generic Host)         │  │
   │  │                                                                    │  │
   │  │   Bacnet.MstpClient ──► SerialPort /dev/ttyUSB0 ──► ComfoBox       │  │
   │  │        │                                                           │  │
   │  │        ▼                                                           │  │
   │  │   ItemCatalog (127 Items, deklarativ)                             │  │
   │  │        │                                                           │  │
   │  │        ├──► MqttPublisher   (MQTTnet 5) ──► Broker :1883 ──► HA   │  │
   │  │        ├──► HomeAssistantDiscovery (MQTT Discovery)               │  │
   │  │        ├──► HealthPublisher (Status-Topics + LWT)                 │  │
   │  │        └──► REST/SSE API (:8080)                                  │  │
   │  │                                                                    │  │
   │  │   ComfoBox.Web  (Angular 22 + Optimus UI) → wwwroot/, statisch    │  │
   │  │   Setup-Assistent (6 Schritte) → /api/setup/*                      │  │
   │  └────────────────────────────────────────────────────────────────────┘  │
   └──────────────────────────────────────────────────────────────────────────┘
```

> **Ein Container für Gateway und UI** → [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md).
> Kein nginx, kein Reverse-Proxy, ein Port.

---

## Projektstruktur (Ziel)

```
src/
├── ComfoBox.Bacnet/              # portierter Protokollstack (nur MSTP/Serial)
│   ├── Bacnet/
│   │   ├── BacnetCodec.cs        # Encoder/Decoder (aus BACnetBase.cs)
│   │   ├── BacnetMstpTransport.cs # Zustandsmaschine + SerialPort
│   │   └── BacnetValue.cs
│   └── Serial/SerialPortFactory.cs
│
├── ComfoBox.Domain/              # Item-Katalog + Typsystem (framework-frei)
│   ├── Items/                    # 127 Item-Definitionen (Port aus Items/**)
│   ├── Values/                   # AnalogValue, EnumValue, DateValue, CommandValue
│   └── ItemMetadata.cs
│
├── ComfoBox.Gateway/             # Host: BACnet + MQTT + API + Health
│   ├── Bacnet/                   # BacnetClient (I/O, Retries, Reconnect)
│   ├── Mqtt/                     # MqttPublisher, MqttSubscriber, Discovery
│   ├── Health/                   # HealthService, StatusPublisher
│   ├── Api/                      # REST-Endpunkte, SSE-Hub
│   ├── Configuration/            # Options + Validierung
│   └── Program.cs
│
├── ComfoBox.Web/                 # Angular 22 + Optimus UI
│   ├── src/app/core/             # ApiClient, Signal-Store, Auth, SetupStore
│   ├── src/app/features/         # setup, dashboard, items, config, diagnostics
│   └── (kein nginx – SPA wird aus wwwroot/ des .NET-Hosts ausgeliefert)
│
├── ComfoBox.Gateway.Tests/
├── ComfoBox.Bacnet.Tests/
├── ComfoBox.Domain.Tests/
└── ComfoBox.Contract.Tests/     # Topic-Kompatibilität gegen docs/topics.json
```

## Querschnitts-Prinzipien

1. **Der BACnet-Stack kennt MQTT nicht, der MQTT-Stack kennt BACnet nicht.**
   Beide kommunizieren über das `ItemCatalog` und ein schmales `IComfoBoxDevice`-Interface.
2. **Ein Item hat genau eine Wahrheit** – BACnet-Objekt-ID, Typ, Min/Max, Unit, Schreibbarkeit.
   Die MQTT-Topic, der REST-Endpunkt und die HA-Discovery werden **daraus abgeleitet**.
3. **Fehler werden nicht verschluckt.** Jede Schicht meldet Fehler in eine zentrale
   Fehler-Sinke, die nach MQTT (`Status/Errors`), nach REST (`/api/health`) und in die UI fließt.
4. **Der Prozess startet nicht „halb".** Konfiguration wird validiert; ein fehlender Port oder
   Broker ist ein **Startfehler**, kein Laufzeitproblem.
5. **Der Schreibschutz ist persistiert** und wird nicht durch einen Neustart umgangen.
6. **Keine kulturabhängige Zahlenbehandlung** – Parse und Format immer `InvariantCulture`.
