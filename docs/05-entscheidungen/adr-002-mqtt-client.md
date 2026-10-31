# ADR-002: MQTT-Library – Wechsel von M2Mqtt zu MQTTnet 5

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | MQTT, Integration |

## Kontext

Der Ist-Zustand nutzt **M2Mqtt 4.3.0.0** (`uPLibrary.Networking.M2Mqtt`), zuletzt 2017 veröffentlicht.

Daraus entstehen im Feld beobachtete Probleme:

| Problem | Beleg |
|---|---|
| Kein Async-Client → alles eventbasiert, blockierende Callbacks | `MqttMsgPublishReceived` |
| Kein .NET-Core-Support | Paket ist auf .NET Framework 4.x beschränkt |
| **Keine TLS-Unterstützung** im genutzten Pfad | `InitMqttClient` (mit `MqttSslProtocols`) ist toter Code |
| **Keine Authentifizierung** | `client.Connect(..., null, null, ...)` |
| Handgeschriebene Reconnect-Logik: Rekursion ohne Backoff/Cancellation | Befund A9 |
| `keepAlivePeriod = 2` s → unnötig häufige Disconnects | Befund A24 |
| Keine eingebettete Broker-Fähigkeit (für Standalone) | – |
| `Connected=1` wird nach Reconnect nicht publiziert | Befund A10 |

Anforderung 1 nennt den Wechsel der MQTT-Library ausdrücklich als mögliches Ziel.

## Entscheidung

**Der MQTT-Client wird durch MQTTnet 5 (Version 5.2.0.1603) ersetzt.**

- `MQTTnet` (MIT, dotnet/MQTTnet, aktiv gepflegt) für den Client
- `MQTTnet.AspNetCore` für den optionalen eingebetteten Broker
- MQTT-Protokollversion **5** (HA empfiehlt v5, ist abwärtskompatibel zu 3.1.1)
- Reconnect über eine ereignisbasierte Schleife mit **exponentiellem Backoff** und
  `CancellationToken`
- TLS, Username/Password, Port **pro Broker** konfigurierbar
- Keepalive **30 s** (statt 2 s)
- LWT auf `ComfoBox/Status/Health` und `ComfoBox/Connected`

### Topic-Kompatibilität

Der Wechsel ändert **keine** Topic-Adresse und **kein** Payload-Format.
Die kultur-invarianten Zahl-Payloads sind eine **Korrektur** (Befund A5), keine Änderung.

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **MQTTnet 5** ✅ | Async, TLS, Auth, MQTT 5, aktiv gepflegt, Broker optional | – |
| MQTTnet 4.x | stabil, aber kein MQTT 5, API-Differenzen | kein MQTT-5-Support |
| **MQTTnet.Extensions.ManagedClient** | höhere Abstraktion | weniger Kontrolle über QoS/Retain/LWT-Details; wir brauchen exakte Kontrolle |
| Eclipse Paho (.NET) | etabliert, async | aktivitietsniedriger, Broker-Server nicht enthalten |
| **FluentMqtt** | gut | Projekt eingestellt (letzte Aktivität 2021) |
| **M2Mqtt weiterverwenden + patchen** | minimaler Aufwand | blockiert .NET 10 komplett; behebt TLS/Auth/async-Defekte nicht |
| **Eigene MQTT-Implementierung** | volle Kontrolle | viel Aufwand, Fehlerrisiko, Sicherheitsverantwortung (TLS) |

## Konsequenzen

### Positiv

- **TLS und Authentifizierung** werden überhaupt erst möglich (Befund A25)
- Sauberer Reconnect mit Backoff → behebt A9, A10, A11
- `async/await` statt Events → keine `async void`-Krisen (Befund A1)
- MQTT 5 → bessere QoS-Semantik, `Message Expiry`, Shared Subscriptions
- Optionaler eingebetteter Broker → Standalone-Betrieb ohne externen Broker
- Starke Community, aktive Entwicklung, Apache-2.0-kompatible MIT-Lizenz

### Negativ / Aufwand

- API ist **komplett anders** → `ComfoBoxMqttClient.cs` wird neu geschrieben (geschätzt 3 PT)
- QoS wird von „Subscribe 2 / Publish 0" auf „Subscribe 1 / Publish 0|1" geändert →
  **kompatibel** (QoS wird vom Empfänger bestimmt), aber Verhalten prüfen
- `willDelayInterval` wird von M2Mqtt nicht 1:1 übernommen → Health-Semantik wird bewusst
  vereinfacht (`Status/Health` + explizites `offline` beim orderly Shutdown)
- Neue Abhängigkeit (`MQTTnet`) mit eigenem Release-Rhythmus

### Neutral

- `ComfoBox/Connected` und `ComfoBox/Version` bleiben bestehen → **keine** Migrations-Aktion
  für bestehende HA-Setups nötig
- Der Discovery-Prefix bleibt konfigurierbar (Default `homeassistant`)

## Verwandte Dokumente

- [mqtt-library-migration](../02-modernisierung/mqtt-library-migration.md)
- [home-assistant](../02-modernisierung/home-assistant.md)
- [mqtt-kompatibilitaet](../04-migration/mqtt-kompatibilitaet.md)
- [ADR-001](adr-001-dotnet-version.md)
