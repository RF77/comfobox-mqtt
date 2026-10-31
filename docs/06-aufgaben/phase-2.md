# Phase 2 – Domain & Gateway-Kern (6 PT)

**Ziel:** Der Gateway liest alle 127 Items und schreibt in den Cache – **noch ohne MQTT**.
**Braucht:** Phase 1
**Spec:** [datenmodell-und-api §1–2](../02-modernisierung/datenmodell-und-api.md) ·
[uebersicht-und-zielbild](../02-modernisierung/uebersicht-und-zielbild.md)

> **Warum zuerst die Domäne?** Weil sie **framework-frei** sein muss. Alles, was
> hier hineingehört, ist später ohne Container testbar.

---

## 2.1 – Item-Katalog erstellen · 1.5 PT

| | |
|---|---|
| **Quelle** | `src_old/ComfoBoxLib/Items/**` (127 Items) |
| **Ziel** | `src/ComfoBox.Domain/Items/` |
| **Verifikation** | Test prüft **127** Items mit allen Metadaten |

**Pro Item gehören hinein:**

| Feld | Quelle |
|---|---|
| `Id` | Name der Property |
| `Group` | Klasse (`Zone`, `States`, `Warmwater`, `Config`, `Tests`) |
| `DisplayName` | `Description` |
| `BacnetObjectId` | aus `AnalogValue(185, …)` / `EnumValue(…)` |
| `ValueType` | `float` / `enum` / `bool` |
| `Unit` | Einheit (`"°C"`, `"h"`, `"%"`, `null`) |
| `Min` / `Max` | Grenzen (dürfen `null` sein) |
| `IsReadOnly` | Schreibbarkeit |
| `EnumValues` | Enum-Namen |
| `RefreshPriority` | `Once` / `Primary` / `Secondary` |

> **Befund A15:** Im Altbestand werden die Prioritäts-Attribute **gelesen und
> verworfen** – alle Items bekommen `RefreshPriority.None`. Hier müssen sie
> **wirklich** ausgewertet werden.

> **Befund A5:** Die Einheit `°C` steht im Altbestand als `"\u00b0C"` – in der
> Domäne wird sie korrekt als `"°C"` geführt.

**Akzeptanzkriterien:**
- [ ] **Genau 127** Items
- [ ] Jedes Item hat BACnet-Objekt-ID, Typ, Einheit, Min/Max, Schreibbarkeit
- [ ] Enum-Items haben ihre **exakten** Enum-Namen
- [ ] Die Prioritäten werden **wirklich** ausgewertet (Test!)
- [ ] Ein Test vergleicht den Katalog gegen `docs/topics.json`

---

## 2.2 – Werttypen neu · 1.0 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Domain/Values/` |

| Typ | Zweck |
|---|---|
| `AnalogValue` | Zahl mit Einheit, Min/Max |
| `EnumValue` | Auswahl mit Namensliste |

> **`DateValue` und `CommandValue` sind tot** (Befund A20) und werden **nicht**
> wiederbelebt. Der Wert von `InitialOperationDate` bleibt schlicht leer.

> **InvariantCulture** (Befund A5): Jede Formatierung und jede Umwandlung in
> der Domäne nutzt `CultureInfo.InvariantCulture` – **ohne Ausnahme**.

**Akzeptanzkriterien:**
- [ ] `DateValue`/`CommandValue` existieren **nicht**
- [ ] Ein Test beweist: `21.5` wird auf `de-DE` **richtig** geparst
- [ ] Ein Test beweist: `23,5` wird **nicht** als `235` interpretiert

---

## 2.3 – Gateway-Host, DI, Options · 1.0 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Gateway/Program.cs`, `Configuration/` |

````csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<GatewayOptions>()
    .Bind(builder.Configuration.GetSection("Gateway"))
    .ValidateDataAnnotations()
    .ValidateOnStart();          // ← Start schlägt bei ungültiger Config fehl

builder.Services.AddSingleton<IItemCatalog, ItemCatalog>();
builder.Services.AddSingleton<IValueCache, ValueCache>();
builder.Services.AddHostedService<BacnetPollingService>();

var app = builder.Build();
app.MapGet("/api/health", …);
app.Run();
````

**Options-Validierung:**

| Option | Regel |
|---|---|
| `Bacnet:Port` | Pflicht, nicht leer |
| `Bacnet:Baudrate` | erlaubt: 9600, 19200, 38400, 57600, 76800, 115200 |
| `Gateway:BaseTopic` | Default `ComfoBox`, keine Leerzeichen |
| `Mqtt:Brokers` | mindestens ein Broker mit Host + Port |

**Akzeptanzkriterien:**
- [ ] Konfigurationsvalidierung: Start schlägt bei fehlendem Port/Broker fehl
- [ ] `Nullable` aktiv, keine Warnungen
- [ ] `ILogger` überall, kein `Trace`

---

## 2.4 – `BacnetDevice` · 1.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Gateway/Bacnet/BacnetDevice.cs` |

| Thema | Anforderung |
|---|---|
| Serialisierung | **ein** `SemaphoreSlim` für alle BACnet-Zugriffe |
| Priorisierung | `Once` → einmal beim Start, `Primary` → häufig, `Secondary` → selten |
| Reihenfolge | Primäre Items zuerst – der Nutzer sieht schneller Werte |
| Fehler | ein Lesefehler darf den Rest nicht abbrechen |
| Timeout | je Item, damit ein hängender Wert nicht alles blockiert |

**Akzeptanzkriterien:**
- [ ] Kein Item blockiert ein anderes
- [ ] Prioritäten sind im Test **beobachtbar** (Reihenfolge der Lesezugriffe)
- [ ] Ein Fehler wird geloggt **und** in die Fehler-Senke geschrieben

---

## 2.5 – `ValueCache` · 0.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Gateway/ValueCache.cs` |

| Aspekt | Wert |
|---|---|
| Speicherung | letzter Wert **+** Zeitstempel pro Item |
| `IsStale` | älter als 3 Poll-Zyklen |
| Threadsicherheit | `ConcurrentDictionary` |
| Benachrichtigung | SSE-Abonnenten werden bei Änderung benachrichtigt |

**Akzeptanzkriterien:**
- [ ] `IsStale` funktioniert (Test mit simulierter Zeit)
- [ ] Paralleles Lesen/Schreiben ohne `InvalidOperationException`

---

## 2.6 – Persistenter Schreibzähler · 0.5 PT

| | |
|---|---|
| **Befund** | **A3** – der Zähler ist im Altbestand **nicht** persistent |
| **Ziel** | `src/ComfoBox.Gateway/Protection/WriteCounter.cs` |

> **Der Schreibzähler ist heilig.** Er ist der EEPROM-Schutz der ComfoBox.

| Anforderung | Umsetzung |
|---|---|
| persistent | JSON-Datei im Datenverzeichnis |
| **kein** Pfad darf ihn umgehen | Zähler wird **im Writer** erhöht, nicht im Aufrufer |
| Neustart | Zähler bleibt |
| Zeitfenster | 24 h gleitend |
| Limit | Default 200 |

**Akzeptanzkriterien:**
- [ ] Test `Count_Persists_AcrossRestart` ✅
- [ ] Der Zähler wird **nur** bei **erfolgreichen** Schreibvorgängen erhöht (A22)
- [ ] Bei Überschreitung wird der Schreibvorgang **abgelehnt**
- [ ] Die Datei liegt im Datenverzeichnis und wird atomar geschrieben

---

## Abnahme der Phase 2

- [ ] Der Gateway liest alle 127 Items und schreibt in den Cache
- [ ] `ComfoBox.Domain` hat **keine** externen Abhängigkeiten
- [ ] Der Schreibzähler ist persistent und getestet
- [ ] **Noch kein MQTT** – die Werte sind nur intern sichtbar

→ Weiter mit [Phase 3](phase-3.md)
