# ComfoBox (Home-Assistant-Integration)

Home-Assistant-Integration für das **ComfoBox-Gateway** (Zehnder ComfoBox Series 5 /
ELESTA) – **ohne MQTT-Broker**.

Die Integration spricht mit dem Gateway über REST/SSE und legt ein Gerät
**„ComfoBox ELESTA"** mit allen 127 Items als Entitäten an.

## Installation

```
HACS → Integration → ComfoBox → Hinzufügen
```

1. Adresse des Gateways eintragen (Standard: `http://localhost:8099`)
2. API-Token eintragen – erzeugt in der Web-UI des Gateways unter
   **Konfiguration → API-Token**
3. **Hinzufügen** → das Gerät erscheint unter *Geräte & Dienste*

> **Voraussetzung:** Das Gateway muss laufen (als Home-Assistant-Add-on, Synology-Paket
> oder Docker-Container).

## Was bekommt der Nutzer?

| Plattform | Inhalt |
|---|---|
| `sensor` | Messgrößen, Zähler, Reglerangaben und lesende Auswahlwerte (als Text) |
| `number` | schreibbare Sollwerte mit **exaktem** Min/Max aus dem Katalog |
| `select` | schreibbare Enum- und Kodewerte (Beschriftung aus dem Katalog, geschrieben wird die Zahl) |
| `switch` | schreibbare 0/1-Werte |
| `button` | Auslöser wie „Warmwasser erhitzen“ |
| `binary_sensor` | lesende 0/1-Zustände (Ausgänge, Freigaben), BACnet- und Broker-Verbindung |

> **Genau eine Entität je Wert.** Ein 0/1-Wert ist ein `switch` **oder** ein
> `binary_sensor` – nie beides. Nur Messgrößen und Zähler erzeugen eine
> Langzeitstatistik; Testausgänge und expertenpflichtige Einstellungen sind
> standardmäßig ausgeschaltet.

## Wichtige Eigenschaften

| Eigenschaft | Wert |
|---|---|
| MQTT-Broker | **nicht** erforderlich |
| Bacnet-Zugriff | nur über das Gateway (es ist der einzige Master am Bus) |
| Schreibschutz | liegt im Gateway (24-h-Limit) – die Integration umgeht ihn nicht |
| Aktualisierung | **Push** über `/api/events` (SSE) – kein Poll-Takt für die Werte |
| Statusabfrage | 60 s (einstellbar 15 … 3600 s, Optionen der Integration) |
| Bei Gateway-Ausfall | Entitäten werden `unavailable` – **kein** Flackern |

## Services

| Service | Zweck |
|---|---|
| `comfobox.set_value` | Wert schreiben |
| `comfobox.restart` | Gateway neu starten |
| `comfobox.read_all` | alle Werte sofort neu lesen |

## Quellen

- [Spezifikation](../../docs/02-modernisierung/ha-integration.md)
- [ADR-009](../../docs/05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md)
- [Installationsstufen](../../docs/03-deployment/installationsstufen.md)
