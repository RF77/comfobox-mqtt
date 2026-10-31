# Übersicht & Zielbild

## 1. Ausgangslage in einem Satz

Der Gateway funktioniert, ist aber als .NET-Framework-4.5.2-Prozess mit Mono-Einsatz, ohne Tests,
ohne CI, ohne Container und ohne Health-Konzept ein technisches Fossil – mit drei echten
Produktionsfehlern (kulturabhängige Zahlen, Enum-Schreibfehler, 100-%-CPU-Spin-Loop).

## 2. Zielbild

```
┌───────────────────────────────────────────────────────────────────────────┐
│                    Docker-Container "comfobox-gateway"                    │
│                                                                           │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  ComfoBox.Gateway  (ASP.NET Core, .NET 10)                         │  │
│  │                                                                     │  │
│  │  ┌───────────────┐   ┌──────────────┐   ┌────────────────────────┐  │  │
│  │  │ BacnetClient  │──►│ ItemCatalog  │──►│ MqttPublisher          │  │  │
│  │  │ (MSTP)        │   │ (127 Items)  │   │ (MQTTnet 5)            │  │  │
│  │  └───────┬───────┘   └──────┬───────┘   │  ├─ Items              │  │  │
│  │          │                  │           │  ├─ HomeAssistantDisc. │  │  │
│  │          │                  │           │  └─ Health/Status      │  │  │
│  │          │                  ▼           └───────────┬────────────┘  │  │
│  │          │           ┌──────────────┐              │               │  │
│  │          │           │ ValueCache   │              │               │  │
│  │          │           │ (letzte      │──────────────┘               │  │
│  │          │           │  Werte)      │                              │  │
│  │          │           └──────┬───────┘                              │  │
│  │          │                  │                                      │  │
│  │          │                  ▼                                      │  │
│  │          │           ┌──────────────────────┐                      │  │
│  │          └──────────►│ REST/SSE API (:8080) │◄──── Angular UI ───┘  │  │
│  │                      └──────────────────────┘      (nginx)         │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  ComfoBox.Web — Angular 22 + Optimus UI (statisch, nginx)          │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                           │
│   /dev/ttyUSB0  ◄─── USB-RS485-Adapter (CH340) ───► ComfoBox ELESTA     │
└───────────────────────────────────────────────────────────────────────────┘
                                     │
                                     │ MQTT 1883 / TLS
                                     ▼
                        Mosquitto (HA-Add-on oder Container auf DS923+)
                                     │
                                     ▼
                              Home Assistant
```

## 3. Warum ein monolithischer .NET-Prozess (statt Microservices)

Die Aufgabe ist ein **einzelnes Gerät** mit einer **engen Kopplung** (RS485-Bus, ein Wert = ein Topic).
Eine Aufteilung in Services würde den Serial-Port-Handling, den Schreibschutz und die
Zustandskonsistenz nur erschweren.

Stattdessen:

- **Ein Prozess, modularer Aufbau.** Klare Schichtentrennung, aber ein gemeinsamer Host.
- **Ein Container-Image** enthält Gateway + UI → einfaches Deployment auf der DS923+.
- **Optional eingebetteter Broker** (MQTTnet-Server) für den Standalone-Betrieb ohne externen
  Broker – per Config abschaltbar, Standard ist der externe Broker.

## 4. Komponenten und ihre Verantwortung

| Komponente | Verantwortung | Nicht verantwortlich für |
|---|---|---|
| `ComfoBox.Bacnet` | MSTP-Protokoll, SerialPort, Framing, Timeouts | MQTT, HTTP, Item-Semantik |
| `ComfoBox.Domain` | Item-Katalog, Typen, Validierung, Metadaten | I/O, Transport |
| `ComfoBox.Gateway/Bacnet` | Polling, Schreibzugriff, Reconnect, Retry | Protokoll-Details |
| `ComfoBox.Gateway/Mqtt` | Publish/Subscribe, Discovery, LWT, Reconnect | BACnet-Details |
| `ComfoBox.Gateway/Health` | Health-Checks, Fehler-Sammlung, Status-Publishing | Fachlogik |
| `ComfoBox.Gateway/Api` | REST, SSE, Konfigurations-Endpunkte | Transport-Details |
| `ComfoBox.Web` | UI, Zustand, Bedienung | Protokoll-/MQTT-Details |

### Abhängigkeitsrichtung (keine Zyklen)

```
ComfoBox.Web  ──HTTP/SSE──►  ComfoBox.Gateway
                                   │
                                   ├──► ComfoBox.Domain ◄── ComfoBox.Bacnet
                                   └──► MQTTnet / ASP.NET Core
```

## 5. Datenflüsse

### 5.1 Lesen (Poll)

```
PollScheduler (priorisiert)
  → IComfoBoxDevice.ReadAsync(item)      [SemaphoreSlim(1) serialisiert den Bus]
      → BacnetClient.ReadPropertyAsync
          → BacnetMstpTransport (State-Machine)
              → SerialPort
  → ValueCache.Update(item, value)
  → MqttPublisher.PublishAsync(topic, payload, retain: true)
  → (optional) HomeAssistantDiscovery (nur bei Änderung/Neuheit)
  → SSE → Angular UI
```

**Priorisierung:** Die vorhandenen Attribute `[Primary]`, `[Secondary]`, `[Once]` werden **jetzt
wirklich implementiert** – das war im Ist-Zustand toter Code (Befund A15).

| Priorität | Intervall | Umfang |
|---|---|---|
| `Once` | einmalig beim Start | Items, die sich nie ändern |
| `Primary` | 60 s (konfigurierbar) | `Zone`, `States`, `Warmwater` |
| `Secondary` | 300 s (konfigurierbar) | `Config`, `Tests`, `Time` |

> **Kompatibilität:** Der Ist-Zustand pollt alles alle 60 s. Die Priorisierung reduziert die
> Buslast. Für HA ist das unkritisch, weil alle Werte **retained** publiziert werden.

### 5.2 Schreiben

```
MQTT {topic}/Set  ──┐
                     ├──► MqttSubscriber ──► WriteCommand
REST PUT /api/items/{id} ─┘                        │
                                                  ▼
                              Validierung (Typ, Min/Max, ReadOnly, ExpertMode)
                                                  │
                              Schreibschutz (persistenter 24-h-Zähler)
                                                  │
                                                  ▼
                              BacnetClient.WritePropertyAsync
                                                  │
                                                  ▼
                              MqttPublisher.PublishAsync(topic, newValue, retain: true)
```

**Wichtig:** Ein Schreibbefehl, der die Validierung nicht besteht, wird **abgelehnt und
publiziert** (Fehler auf `Status/Errors` + HTTP 4xx). Es wird **nie** ein Default-Wert
geschrieben (behebt Befund A6).

### 5.3 Health / Fehler

```
Jede Schicht ──► IHealthReporter.Report(HealthEvent)
                      │
                      ├──► StatusTopicPublisher  → ComfoBox/Status/Errors (JSON, retained)
                      ├──► HealthStateAggregator → ComfoBox/Status/Health (online|degraded|offline)
                      ├──► /api/health, /api/status  (REST)
                      └──► SSE Event "health" → UI-Banner
```

## 6. Prozess-Lebenszyklus

```
Start
 ├─ 1. Konfiguration laden & validieren   → Fehler ⇒ Startabbruch (exit 1)
 ├─ 2. Persistenter Schreibzähler laden
 ├─ 3. Item-Katalog aufbauen
 ├─ 4. BACnet verbinden (WhoIs → IAm)      → Fehler ⇒ degraded, Retry im Hintergrund
 ├─ 5. MQTT verbinden + LWT registrieren + Discovery senden
 ├─ 6. ValueCache mit Retained-Werten initialisieren
 ├─ 7. Poll-Loop starten
 ├─ 8. HTTP-Server starten
 └─ 9. Health = online

Stop (SIGTERM / docker stop / Restart-Befehl)
 ├─ 1. MQTT: "offline" publishen, dann trennen (LWT wird nicht ausgelöst)
 ├─ 2. Poll-Loop stoppen (CancellationToken, sauber awaited)
 ├─ 3. BACnet schließen (SerialPort freigeben)
 ├─ 4. HTTP-Server stoppen
 └─ 5. Schreibzähler persistieren
```

> Der heutige „Reconnect beim Stoppen"-Fehler (Befund A11) wird durch einen
> `CancellationToken` im gesamten Lebenszyklus strukturell ausgeschlossen.

## 7. Migrationsstrategie (Big-Bang mit Parallelbetrieb)

| Schritt | Vorgehen |
|---|---|
| 1. **Dual-Publish-Modus** | Das neue Gateway publiziert zunächst **zusätzlich** auf den alten Topics. Der alte Dienst läuft weiter. |
| 2. **Vergleich** | Ein Test-Tool vergleicht die Payload-Ströme beider Gateways (`old/new` → Diff-Report). |
| 3. **Umschaltung** | HA-Automationen werden von `ComfoBox/…` auf die neuen Topics umgestellt (die alten bleiben als Alias erhalten). |
| 4. **Abschaltung** | Alter Dienst wird gestoppt, `ComfoBox/…` bleibt vom neuen Gateway bedient. |

> Variante: **Topic-Alias** – das neue Gateway publiziert auf `ComfoBox2/…` und **parallel** auf
> `ComfoBox/…`. Dadurch kann der Umschalter in HA rein konfigurativ erfolgen.
> Entscheidung: **Variante 2 (Alias)** – siehe [ADR-002](../05-entscheidungen/adr-002-mqtt-client.md).

## 8. Was ausdrücklich **nicht** geändert wird

| Bereich | Begründung |
|---|---|
| MQTT-Topic-Adressen | Anforderung 4 |
| Payload-Formate der Item-Topics | Anforderung 4 + HA-Kompatibilität |
| Enum-Wertnamen | Teil des Topic-Vertrags |
| BACnet-Objekt-IDs (127 Items) | Hardware-seitig fix, keine Wahl |
| `BaseTopic`-Default `ComfoBox` | Bestandsinstallationen |
| Topic-Struktur `/Set`, `/AsNumber`, `/AsNumber/Set` | Bestandsautomationen |

## 9. Offene Fragen (Entscheidung durch den Auftraggeber)

| # | Frage | Vorschlag | Auswirkung bei anderer Entscheidung |
|---|---|---|---|
| Q1 | Soll das Gateway einen **eigenen Broker** mitbringen? | optional, Standard aus | Aufwand +0,5 Tag, vereinfacht Standalone-Betrieb |
| Q2 | Soll die UI **Authentifizierung** haben? | Ja, einfaches Token/Reverse-Proxy | Aufwand +2 Tage; ohne: UI nur im LAN |
| Q3 | Wie viele ComfoBox-Geräte parallel? | 1 | Bei >1: Multi-Instanz-Design nötig (größerer Umbau) |
| Q4 | Soll `ExpertMode` per UI/HA umschaltbar sein? | Ja, mit Bestätigung | Ohne: bleibt statisch in der Config |
| Q5 | Sollen die Werte weiter **retained** publiziert werden? | Ja | Bei Nein: HA-Entities verlieren nach Broker-Neustart den State |
| Q6 | Ziel-URL für HA-Discovery: `homeassistant` (Default)? | ja, konfigurierbar | – |
