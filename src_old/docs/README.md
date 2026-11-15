# `src_old/docs` – Archiv der Legacy-Dokumente

Dieses Verzeichnis gehört zum **Archiv** `src_old/`. Es wird **nicht** gebaut, **nicht**
gepflegt und nicht ausgeliefert. Es hält nur fest, was der Legacy-Code erzeugt hat.

## Inhalt

| Datei | Herkunft |
|---|---|
| [topics.md](topics.md) | Erzeugt vom Legacy-Programm über `WriteTopicsFilePath` ([`src_old/ComfoBoxMqtt/App.config`](../ComfoBoxMqtt/App.config)) |

Der Legacy-Eintrag lautet `..\..\..\..\docs\topics.md`. Vom Build-Verzeichnis
(`src_old/ComfoBoxMqtt/bin/Debug`) aus aufgelöst ergibt das die **Repo-Wurzel**, also
`docs/topics.md`. Das Programm schrieb beide Zieldateien dorthin:

```
WriteTopicsToFile       → ..\..\..\..\docs\topics.md
WriteTopicsJsonFilePath → ..\..\..\..\docs\topics.json
```

## Warum hier eine Kopie liegt

`docs/topics.md` und `docs/topics.json` sind **eingefroren** – sie sind der V1-Vertrag und
die Test-Fixture des Contract-Tests (`src/ComfoBox.Contract.Tests`). Sie werden **nicht**
mehr geändert, auch nicht, wenn sich der Item-Katalog korrigiert.

Damit der historische Stand trotzdem auffindbar bleibt und niemand versehentlich die
eingefrorene Datei „aktualisiert", liegt hier eine unveränderte Kopie mit Archiv-Hinweis.

## Wird `topics.md` heute noch erzeugt?

**Nein.** Der neue Generator
([`tools/generate-topics/generate-topics.mjs`](../../tools/generate-topics/generate-topics.mjs))
erzeugt ausschliesslich:

| Ausgabe | Aufruf |
|---|---|
| `src/ComfoBox.Domain/Items/Catalog.cs` + `ItemEnums.cs` | `--catalog <verzeichnis>` |
| `docs/topics.json` (Test-Fixture) | `--out docs/topics.json` |

Ein Markdown-Export existiert **nicht** (Stand 2026-11-15). Der aktuelle, erweiterte Vertrag
steht in [`docs/topics-v2.md`](../../docs/topics-v2.md).

> Soll `topics.md` wieder reproduzierbar werden, ist ein `--emit-markdown`-Modus im
> Generator die saubere Lösung (≈ 0,5 PT). Als Option notiert in
> [Phase 9, Aufgabe 9.14](../../docs/06-aufgaben/phase-9-datenpunkt-korrekturen.md#914--topicsmd-bleibt-eingefroren-archivkopie-in-src_old).
