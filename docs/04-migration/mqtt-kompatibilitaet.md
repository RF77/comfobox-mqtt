# MQTT-Kompatibilität

> Anforderung 4: *„MQTT Adressen sollen gleich bleiben."*

Dieses Dokument ist die **verbindliche Zusicherung**. Jede Abweichung ist ein Fehler.

## 1. Der Vertrag

### 1.1 Unveränderlich

| Element | Wert |
|---|---|
| `BaseTopic` (Default) | `ComfoBox` |
| Item-Topic | `ComfoBox/{Group}/{Name}` |
| Schreib-Topic | `ComfoBox/{Group}/{Name}/Set` |
| Enum-Zahl-Topic | `ComfoBox/{Group}/{Name}/AsNumber` |
| Enum-Zahl-Schreib-Topic | `ComfoBox/{Group}/{Name}/AsNumber/Set` |
| Sonder-Topic | `ComfoBox/Special/NumberOfWritesPer24h` |
| Verbindungs-Topic | `ComfoBox/Connected` (Payload `1`/`0`) |
| Versions-Topic | `ComfoBox/Version` (Payload `Ver <x.y.z>`) |
| Enum-**Namen** | unverändert (z. B. `HeatingWithHeatPump`) |
| Payload-Typen | Zahl (Punkt), Enum-Name, `1`/`0`, Text |

### 1.2 Neu hinzugefügt (additiv, ohne Konflikt)

| Topic | Zweck |
|---|---|
| `ComfoBox/Status/Health` | Availability (`online`/`degraded`/`offline`) |
| `ComfoBox/Status/Version` | Version ohne `Ver `-Präfix |
| `ComfoBox/Status/Uptime` | Laufzeit in Sekunden |
| `ComfoBox/Status/StartedAt` | Startzeitpunkt (ISO 8601) |
| `ComfoBox/Status/Writes` | Schreibzähler 24 h |
| `ComfoBox/Status/WritesMax` | Limit |
| `ComfoBox/Status/Bacnet` | JSON-Status BACnet |
| `ComfoBox/Status/Mqtt` | JSON-Status MQTT |
| `ComfoBox/Status/LastError` | letzter Fehler (Text) |
| `ComfoBox/Status/Errors` | Fehlerhistorie (JSON) |
| `ComfoBox/Status/Restart` | Signal nach Neustart |
| `ComfoBox/Command/Restart` | **Subscribe** – Neustart auslösen |

> **Regel:** Neue Topics beginnen **immer** mit `ComfoBox/Status/` bzw. `ComfoBox/Command/`.
> Es gibt keine Kollision mit bestehenden Item-Topics, da es kein Item mit der Gruppe
> `Status` oder `Command` gibt.

### 1.3 Nicht mehr vorhanden

| Topic | Grund |
|---|---|
| `ComfoBox/Virtual/*` | war im Ist-Zustand **nie aktiv** (Code auskommentiert) → keine Kompatibilitätsverletzung |

> **Prüfung:** Der Contract-Test schlägt fehl, falls ein `Virtual`-Topic im neuen Katalog
> auftaucht – es gab sie nie.

## 2. Topic-Ableitung im Detail

### 2.1 Gruppen

Aus `docs/topics.json` (127 Einträge) ergeben sich folgende Topic-Präfixe:

````
ComfoBox/Config/
ComfoBox/States/
ComfoBox/Tests/
ComfoBox/Time/
ComfoBox/Warmwater/
ComfoBox/Zone/
````

> **`Time` bleibt erhalten**, auch wenn die Klasse im Ist-Zustand leer war – ein Topic-Präfix
> `ComfoBox/Time` existiert in `docs/topics.json` nicht, aber `ComfoBox/Time/…` wird in der
> Doku referenziert. Der Contract-Test entscheidet anhand der Fixture.

### 2.2 Item-Namen

Die Item-Namen enthalten **Großbuchstaben** (`SetPointNormal`, `HeatingCoolingMode`) – genau wie
im Ist-Zustand. **Keine** Umstellung auf snake_case oder kebab-case!

> **Typische Fehlerquelle:** Die interne `Id` (`zone.setpoint_normal`) ist für REST/UI gedacht.
> Der **Topic-Pfad** muss weiterhin `Zone/SetPointNormal` sein. Der Contract-Test vergleicht
> die Topic-Pfade, nicht die IDs.

### 2.3 Schreibbarkeit

Die Regel aus dem Ist-Zustand:

| Item-Typ | `/Set`-Topic? |
|---|---|
| `IsReadOnly == true` | **nein** |
| `IsReadOnly == false` (d. h. `ExpertMode == true`) | ja |
| `RequiresExpertMode && ExpertMode == false` | **nein** |
| `RequiresExpertMode && ExpertMode == true` | ja |

> **Kompatibilitäts-Falle:** Im Ist-Zustand hängt die Existenz des `/Set`-Topics vom
> **Startzeitpunkt** ab (ExpertMode aus der Config). Ein Topic, das im Ist-Zustand **nicht**
> existierte, kann nach der Migration mit aktiviertem ExpertMode **neu** erscheinen. Das ist
> unkritisch (mehr Funktionalität), aber im Contract-Test als „optional" zu behandeln.

## 3. Payload-Interpretation

Hier liegt die eigentliche Gefahr. Die **Interpretation** wird korrigiert, die **Formate** nicht.

### 3.1 Zahlen

| Aspekt | Ist-Zustand | Soll | Bewertung |
|---|---|---|---|
| Publish `23.5` (de-DE) | `23,5` | `23.5` | **Fix** – HA konnte `23,5` nicht parsen |
| Parse `23.5` (de-DE) | Fehler → verworfen | `23.5` | **Fix** – Schreibvorgänge waren kaputt |
| Parse `23,5` (de-DE) | → `235` (falsch!) | Fehler → abgelehnt | **Fix** – verhindert falsche Sollwerte |
| Parse `23,5` (en-US) | `23.5` | `23,5` → abgelehnt | **Verhaltensänderung** |

> **Warum ist die letzte Zeile akzeptabel?** `23,5` ist in MQTT **kein** gültiger Zahlenwert.
> HA selbst lehnt solche Payloads ebenfalls ab. Wer `23,5` sendet, hat einen Fehler in der
> Automation. Der neue Gateway lehnt ihn **laut** ab, statt still 235 zu schreiben.
> → Meldung in `Status/Errors` + UI.

**Empfehlung für bestehende Automationen:** Der Migrations-Guide enthält einen
`jq`-/Template-Snippet, der `,` → `.` ersetzt.

### 3.2 Enums

| Eingabe | Topic | Ist-Zustand | Soll |
|---|---|---|---|
| `Normal` | `/Set` | ✅ geschrieben | ✅ geschrieben |
| `normal` (klein) | `/Set` | ❌ verworfen | ✅ geschrieben (case-insensitive) |
| `6` | `/Set` | ✅ geschrieben (Enum.Parse des Namens „6" → Fehler → **0 geschrieben!**) | ✅ geschrieben |
| `6` | `/AsNumber/Set` | ✅ geschrieben | ✅ geschrieben |
| `abc` | `/Set` | ⚠️ **0 geschrieben!** | ❌ abgelehnt + Fehlermeldung |
| `abc` | `/AsNumber/Set` | ⚠️ **0 geschrieben!** | ❌ abgelehnt + Fehlermeldung |

> Die Zeile „`6` auf `/Set`" verhält sich im Ist-Zustand **gefährlich** (er schreibt 0 statt 6).
> Die neue Implementierung schreibt korrekt 6.

### 3.3 Wertebereich

| Regel | Ist | Soll |
|---|---|---|
| außerhalb `Min`/`Max` | verworfen (geloggt) | verworfen + **Fehlerkanal** |
| `null` | verworfen | verworfen + Fehlerkanal |
| Typ passt nicht (Text auf Analog) | verworfen | verworfen + Fehlerkanal |

## 4. Contract-Test

Der Test ist die technische Durchsetzung des Vertrags.

````csharp
// src/ComfoBox.Contract.Tests/TopicCompatibilityTests.cs
public sealed class TopicCompatibilityTests
{
    private static readonly JsonDocument Legacy = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "topics.json")));

    [Fact]
    public void EveryLegacyItem_IsPresentInTheCatalog()
    {
        var catalog = Catalog.All.Select(i => i.StateTopic).ToHashSet(StringComparer.Ordinal);

        var missing = Legacy.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("SubscribeTopic").GetString()!)
            .Where(t => !catalog.Contains(t))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryLegacyItem_HasTheSameSetTopic()
    {
        // gleiche Prüfung für PublishTopic (/Set)
    }

    [Fact]
    public void NoVirtualTopics_Exist()
    {
        Assert.DoesNotContain(Catalog.All, i => i.Group == "Virtual");
    }

    [Theory]
    [InlineData("zone.setpoint_normal", "ComfoBox/Zone/SetPointNormal")]
    [InlineData("zone.heatingcoolingmode", "ComfoBox/Zone/HeatingCoolingMode")]
    public void TopicPath_IsDerivedFromLegacy(string id, string expected)
    {
        Assert.Equal(expected, Catalog.Find(id)!.StateTopic);
    }

    [Fact]
    public void BaseTopic_IsComfobox()
    {
        Assert.Equal("ComfoBox", Catalog.BaseTopic);
    }
}
````

### Zusätzliche Round-Trip-Tests

````csharp
[Theory]
[InlineData("21.5")]
[InlineData("0")]
[InlineData("-12.25")]
public void AnalogPayload_RoundTrips_Invariant(string payload)
{
    var value = MqttPayload.ParseFloat(payload);
    Assert.True(value.HasValue);
    Assert.Equal(payload, MqttPayload.Format(value!.Value));
}

[Fact]
public void AnalogPayload_Parses_RegardlessOfCurrentCulture()
{
    var previous = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        Assert.True(MqttPayload.TryParse("23.5", out var v));
        Assert.Equal(23.5f, v);
    }
    finally { CultureInfo.CurrentCulture = previous; }
}
````

## 5. Migrations-Checkliste für bestehende HA-Setups

| # | Aufgabe | Warum |
|---|---|---|
| 1 | Bestehende HA-MQTT-Entitäten **nicht** löschen | sie funktionieren weiter |
| 2 | Neue Discovery-Entitäten erzeugen → Dubletten in HA | `unique_id` prüfen |
| 3 | Alte manuelle Entitäten mit gleichem `entity_id` löschen | Kosmetik, keine Funktion |
| 4 | Automationen auf `,`-Payloads prüfen | Payload-Interpretation (siehe 3.1) |
| 5 | `ComfoBox/Connected` als `availability_topic` bleibt gültig | aber `Status/Health` ist besser |
| 6 | Schreib-Automationen auf `/Set` bleiben unverändert | Topologie identisch |
| 7 | `ComfoBox/Virtual/*` in Automationen entfernen (falls vorhanden) | existierte nie |

## 6. Zusicherung

> **Die Topics und Payload-Formate der bestehenden Installation bleiben gültig.**
> Änderungen sind ausschließlich **additiv** (neue `Status/`- und `Command/`-Topics) oder
> **korrigierend** (kultur-invariante Zahlen, kein „0"-Fallback bei ungültigen Enums).
> Der Contract-Test stellt dies automatisch sicher und schlägt bei jeder Regression fehl.
