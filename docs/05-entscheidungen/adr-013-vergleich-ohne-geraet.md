# ADR-013 – Vergleich ohne Gerät (Simulator + Golden Master)

> **Status:** angenommen · **Datum:** 2026-11-01 · **Bereich:** Phase 7 (Parallelbetrieb)

## Kontext

Phase 7 vergleicht die MQTT-Payloads des alten und des neuen Gateways. Der
Vergleich setzt zwei Dinge voraus:

1. **zwei Gateway-Instanzen** mit unterschiedlichem `BaseTopic`, und
2. **Werte von der ComfoBox** – beide Instanzen müssen dasselbe Gerät sehen,
   sonst vergleicht man Äpfel mit Birnen.

Auf dem Entwicklungsrechner (Notebook) ist **kein CH340-Adapter** angeschlossen:
der serielle Port `COM_x` gibt es dort nicht, und ein BACnet-MS/TP-Slave (die
ComfoBox) antwortet nicht.

Die Frage war: *Wie lässt sich Phase 7 ohne Gerät prüfen?*

## Belege aus der Umgebung

| | Befund |
|---|---|
| .NET Framework | **4.8.1** installiert (`NDP\v4\Full` → Release 533509) → der Legacy-Gateway (net452) wäre lauffähig |
| MSBuild | VS 18 Enterprise vorhanden → der Legacy wäre baubar |
| Mono | **nicht** installiert |
| Docker | 29.9.2 → ein Broker (mosquitto) ist lokal startbar |
| NuGet-Cache | `M2Mqtt 4.3.0` und `log4net 2.0.5` **nicht** im Cache (Restore nötig) |
| Codec | `EncodeReadPropertyAcknowledge` existiert bereits (`src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs`) |
| Naht | `ISerialPort` / `IBacnetClient` sind bewusst Testnähte |

## Optionen

| | Option | Bewertung |
|---|---|---|
| **A** | **com0com**: zwei virtuelle COM-Paare, der Simulator in der Mitte, der Legacy-Gateway nativ | Vollständiger Parallelbetrieb mit dem Original. **Braucht:** Treiberinstallation mit Admin-Rechten (UAC), NuGet-Restore des Legacy, zwei Ports. |
| **B** | **Golden Master**: die Payload des alten Gateways einmal aufzeichnen und offline vergleichen | Kein Treiber, kein Legacy-Build, **reproduzierbar** (Fixture im Repo → CI-tauglich). Braucht: eine Aufzeichnung am realen System (dort, wo der CH340 hängt). |
| **C** | **Alles in Docker** mit socat-PTYs | Treiberfrei, aber der Legacy bräuchte ein selbst gebautes Mono-Image. Hoher Aufwand. |
| **D** | **Simulation im Gateway** (`COMFOBOX_Bacnet__Simulation=true`) | Kein Treiber, kein Gerät, kein Broker nötig. Prüft alle Schichten **oberhalb** des BACnet-Busses. |

## Entscheidung

**B + D.** Der Gateway bekommt einen optionalen Simulationsbetrieb (D), das
Vergleichswerkzeug bekommt einen Aufzeichnungs-Modus (B). Damit ist der
Parallelvergleich **vollständig ohne Gerät** durchführbar:

- zwei Gateway-Instanzen mit `BaseTopic=ComfoBox` und `BaseTopic=ComfoBox2`,
- beide im Simulationsbetrieb mit **denselben deterministischen Werten**,
- ein Broker (mosquitto) und der Vergleich über `tools/compare-payloads/`.

Der **Golden-Master** (B) ist der Weg für den echten Parallelvergleich: die
Payload des alten Gateways werden einmal am realen System aufgezeichnet; der
Vergleich läuft danach offline gegen diese Aufzeichnung.

### Details

**Simulationsbetrieb.** `Bacnet:Simulation=true` ersetzt den BACnet-Client
durch `SimulatedBacnetClient`. Er beantwortet `ReadProperty` mit Werten aus dem
Item-Katalog:

- die Werte sind **deterministisch** (FNV-1a über die BACnet-Objekt-Id, nicht
  `string.GetHashCode()` – der ist pro Prozess zufällig),
- eine Wert-Datei (`Bacnet:SimulationValuesPath`) kann einzelne Werte
  überschreiben – damit lässt sich eine Aufzeichnung des alten Gateways in die
  Simulation einspeisen,
- der Wert hängt an der **Objekt-Id**, nicht am Topic: im Legacy zeigen zwei
  Topics auf dasselbe Objekt (Objekt 864: `KühlraumsollAbstStandby` und
  `RestartDelay`) – am echten Gerät lesen beide denselben Rohwert.

> Der BACnet-**Transport** (MSTP-Frame, CRC, Token-Passing) wird im
> Simulationsbetrieb **nicht** durchlaufen. Diese Schicht ist durch die
> Golden-Tests in `ComfoBox.Bacnet.Tests` abgedeckt. Der Simulationsbetrieb
> prüft die Schichten *oberhalb* des Busses: Item → Wert → MQTT → Discovery.

**Sichtbarkeit.** `/api/status` meldet `"simulated": true`, das MQTT-Status-Topic
`simulated: true`. Ein Gateway im Simulationsbetrieb ist damit nie mit einem
Produktivbetrieb zu verwechseln.

**Zwei Fehler, die die Probe gefunden hat** (beide in der Parallelbetrieb-Phase
relevant, beide mit Regressionstest abgesichert):

1. **Client-ID-Kollision.** Beide Instanzen meldeten sich mit
   `comfobox-gateway-{Hostname}` an → der Broker trennte die ältere Sitzung
   (*Session taken over*) und deren Last-Will meldete das andere Gateway als
   `offline`. Die wirksame Client-ID ist jetzt `{ClientId}-{Gateway:DeviceId}`.
2. **Discovery-Kollision.** Die Kennung der HA-Discovery war die Konstante
   `comfobox` → beide Instanzen publizierten auf **denselben** Discovery-Topics
   und hätten sich die Entitäten gegenseitig überschrieben (Aufgabe 7.3).
   Die Kennung ist jetzt konfigurierbar: `Mqtt:DiscoveryIdentifier`
   (Standard unverändert `comfobox`).

## Folgen

**Positiv**

- Der Parallelvergleich ist ohne Gerät **reproduzierbar** prüfbar – auch in CI.
- Die automatisierten Tests decken jetzt auch die Schichten oberhalb des Busses
  ab (vorher war das nur mit echter Hardware möglich).
- Ein Fehler wie die Client-ID-Kollision fällt **vor** dem Umschaltpunkt auf.

**Negativ**

- Der BACnet-Transport ist im Simulationsbetrieb nicht abgedeckt. Das ist eine
  bewusste Lücke – sie ist durch die Golden-Tests abgedeckt, muss aber im
  Aufgabentext stehen.
- Der Simulationsbetrieb ist ein zusätzlicher Schalter im Produktivcode. Er ist
  standardmäßig **aus**, muss explizit gesetzt werden und wird im Status
  ausgewiesen.

**Neutral**

- Für den echten Vergleich am realen System bleibt ein CH340-Adapter nötig.
  Das ist unvermeidbar: die Werte des Geräts kann nur das Gerät liefern.

## Alternativen, die verworfen wurden

- **com0com (A)** bleibt die Option, wenn der **Legacy-Gateway selbst** laufen
  soll. Sie braucht aber Admin-Rechte (Treiber) und einen NuGet-Restore des
  Legacy – beides ist in einer unbeaufsichtigten Sitzung nicht möglich.
- **Docker + Mono (C)** ist der treiberfreie Weg für den Legacy, aber der
  Aufwand (eigenes Mono-Image, Legacy-Build im Container) ist gegenüber dem
  Erkenntnisgewinn zu groß.

## Verwandte

- [ADR-012 – API-Zugriffskontrolle](adr-012-api-zugriffskontrolle.md)
- [phase-7.md – Parallelbetrieb & Umschaltung](../06-aufgaben/phase-7.md)
- [tools/compare-payloads](../../tools/compare-payloads/README.md)
