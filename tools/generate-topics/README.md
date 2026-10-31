# generate-topics

Erzeugt die Topic-Fixture `docs/topics.json` aus dem Legacy-Item-Katalog.

## Warum?

`docs/topics.json` ist die **Test-Fixture** des Contract-Tests
(`src/ComfoBox.Contract.Tests`). Sie wurde 2017 per Reflection aus dem
Legacy-Code erzeugt und seitdem **nicht** von Hand gepflegt. Damit der
MQTT-Vertrag (`docs/04-migration/mqtt-kompatibilitaet.md`) nicht auseinanderläuft,
muss die Fixture maschinell reproduzierbar sein.

## Quellen

| Quelle | Rolle |
|---|---|
| `src_old/ComfoBoxLib/Items/{Zone,Tests,Warmwater,States,Time,Config}.cs` | Item-Katalog |
| `src_old/ComfoBoxMqtt/Groups/ItemFactory.cs` | Reihenfolge der Gruppen |

Die Altdateien sind **Windows-1252**-kodiert (kein UTF-8) – der Generator
dekodiert sie entsprechend.

## Aufruf

```bash
# Prüfen: passt die committete Fixture noch zum Katalog?
node tools/generate-topics/generate-topics.mjs --check

# Fixture neu erzeugen (nur mit Freigabe – Fixtures sind heilig!)
node tools/generate-topics/generate-topics.mjs --out docs/topics.json

# C#-Item-Katalog für ComfoBox.Domain erzeugen (Aufgabe 2.1)
node tools/generate-topics/generate-topics.mjs --catalog src/ComfoBox.Domain/Items/Catalog.g.cs
```

`--check` liefert Exit 0, wenn die Fixture deckungsgleich ist, sonst Exit 1.
Der Check läuft in CI (`build-test.yml`).

## Bekannte Abweichung

| Topic | Abweichung |
|---|---|
| `ComfoBox/Warmwater/DoHeatWaterNow` | Fixture (2017): `DateTime?` · Katalog (heute): `float?` |

Der Werttyp hat sich zwischen 2017 und heute im Legacy-Code geändert
(`CommandValue`/`DateValue` → `AnalogValue`). Die **Fixture** bleibt
unverändert – sie ist der Vertrag, nicht der Ist-Zustand. Der Topic-Pfad
(`ComfoBox/Warmwater/DoHeatWaterNow`) ist in beiden identisch.

Sobald der neue Domain-Katalog aus Phase 2 steht, entfällt die Ausnahme:
Der Generator liest dann den neuen Katalog und die Fixture wird zum
Abgleich gegen den Ist-Zustand des Geräts.

## Format der Fixture

```jsonc
{
  "SubscribeTopic": "ComfoBox/Zone/SetPointNormal",   // Topic, auf dem publiziert wird
  "PublishTopic":  "ComfoBox/Zone/SetPointNormal/Set",// Schreib-Topic (null bei Read-only)
  "Description":   "Raumsollwert normal 5.0..30.0",   // [Description]-Attribut
  "ValueType":     "float?",                          // float? | DateTime? | <Enum>?
  "Unit":          "°C",                              // null, wenn keiner
  "IsReadOnly":    false,                             // true → kein /Set-Topic
  "Min":           5.0,                               // null, wenn keiner
  "Max":           30.0                               // null, wenn keiner
}
```

Die Datei ist **minifiziert** (eine Zeile) und hat ein UTF-8-BOM – beides
entspricht dem committeten Stand und wird vom Generator so reproduziert.
