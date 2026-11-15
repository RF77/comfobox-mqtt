# Technikerhandbuch ECR450 – Auswertung im Repo

Dieses Verzeichnis hält die **Auswertung** des ELESTA-Technikerhandbuchs fest, gegen das die
Implementierung des Gateways abgeglichen wurde. Es enthält bewusst **nicht** das Handbuch
selbst (siehe [§2](#2-warum-der-volltext-nicht-im-repo-liegt)).

## 1. Quelle

| | |
|---|---|
| Dokument | `164829_TH_ECR450_V4.0.pdf` |
| Titel | ECONESTA ECR450 – Technikerhandbuch, Konfiguration der Parameter |
| Hersteller | ELESTA energy control AG, Bad Ragaz (CH) |
| Stand | `164829_V4.0/12.11` – passend zu Regler-Software-Version **4** |
| Umfang | 119 Seiten |
| Bezug | über den Wärmepumpen-Hersteller (Zehnder / ComfoBox) |

Das Handbuch beschreibt **alle** Parameter des Reglers: Menüpfad, Display-Bezeichnung,
Einstellbereich, die möglichen Werte und die **Datenpunkt-Nummer (DP)**. Die DP-Nummer ist
der Schlüssel: genau diese Nummer adressiert das Gateway als *BACnet-Object-Identifier*.

```
Display-Bezeichnung   Bereich/Werte   DP  Einheit
Raumsollwert normal   5.0..30.0       62  °C
                                     └─ BacnetObjectId im Item-Katalog
```

## 2. Warum der Volltext nicht im Repo liegt

Das Handbuch trägt im Impressum den Hinweis, dass es **ausschliesslich für den internen
Gebrauch des Herstellers** bestimmt ist und dass Weiterverwendung, Übersetzungen und
Änderungen nur nach Rücksprache mit ELESTA erlaubt sind. Dieses Repository ist öffentlich
(`github.com/RF77/comfobox-mqtt`). Eine vollständige Übernahme der 119 Seiten in `docs/`
wäre daher eine unzulässige Weiterverwendung.

**Was hier stattdessen steht, ist die verwertbare Information:**
Datenpunkt-Nummern, Display-Bezeichnungen, Einstellbereiche, Wertebelegungen der Enums,
Menüzugehörigkeit und Seitenverweise. Das sind Fakten über die Geräteschnittstelle – sie
sind die Grundlage jeder Implementierung und keine Übernahme des Fliesstexts.

### Volltext lokal erzeugen (nicht einchecken)

`tools/extract-handbook.py` erzeugt aus der PDF eine Textdatei für die lokale Arbeit.
Zielverzeichnis `docs/07-technikerhandbuch/_volltext/` ist in `.gitignore` eingetragen.

```bash
python -m pip install pypdf
python tools/extract-handbook.py "H:/Dokumente/SmartHome/ComfoBox/164829_TH_ECR450_V4.0.pdf"
```

Ergebnis: `_volltext/ecr450.txt` – eine Seite pro Block mit `===== SEITE n =====`.
Die Spalten der Tabellen werden dabei **nicht** erhalten (PDF-Text-Extraktion), die
Zuordnung *Bezeichnung → DP* ist deshalb in [datenpunkt-register.md](datenpunkt-register.md)
von Hand kuratiert.

## 3. Inhalt dieses Verzeichnisses

| Dokument | Zweck |
|---|---|
| [datenpunkt-register.md](datenpunkt-register.md) | **Alle** Parameter des Reglers als Register: DP, Bezeichnung, Bereich/Werte, Einheit, Kapitel, Seite |
| [review-ist-implementierung.md](review-ist-implementierung.md) | Abgleich des Item-Katalogs gegen das Handbuch – Fehler, Abweichungen, offene Fragen |
| [verbesserungen-und-neue-topics.md](verbesserungen-und-neue-topics.md) | Vorschläge für zusätzliche (überwiegend lesende) Topics |
| [../topics-v2.md](../topics-v2.md) | Der **neue Topic-Vertrag** (V2) – bestehende Topics unverändert plus Ergänzungen |

## 4. Was das Handbuch **nicht** hergibt

Damit niemand vergeblich sucht:

| Thema | Befund |
|---|---|
| **Fehlerspeicher** | §4.9 beschreibt 10 Speicherplätze mit Datum/Uhrzeit, nennt aber **keine** DP-Nummer. Der Fehlerspeicher ist damit über dieses Handbuch **nicht** als Datenpunkt belegt. Nur die Fehler-*Codes* sind tabelliert (Wert 0…965). |
| **Anlagenspezifische Bedeutung von E- und R-Eingängen** | Die Nummern E1…E28, D1…D28, R1…R27, Y1/Y2 sind vorhanden; **welche** Funktion ein Eingang hat, ist eine Konfigurationsfrage (§6.2.4/6.2.5). Die Topics `States/Inputs/E6…` beschreiben daher eine *Annahme* über die Verdrahtung, keine Geräte-Eigenschaft. |
| **Zonen 2…7** | Alle Zonen-Parameter sind vorhanden, aber die DP-Nummern werden im Handbuch **nur für den ersten Kreis** angegeben (explizit so im Vorwort, Seite 6). Weitere Kreise zeigt nur das Gerät selbst über die „Info"-Taste. |
| **Remote-IO ECM320A / ECM310A** | Ebenfalls nur für den ersten Kanal dokumentiert. |
| **Wertebereich von „Zustand Zone" (DP 59)** | Die Legende fehlt im Handbuch (nur „Aktueller Regelungs-Zustand der Zone"). |

## 5. Beziehungen zum restlichen Projekt

| Dokument | Beziehung |
|---|---|
| [../topics.json](../topics.json) | **eingefroren** – Test-Fixture des Contract-Tests, aus dem Legacy-Katalog generiert |
| [../topics.md](../topics.md) | **eingefroren** – dieselben 127 Items als Markdown; vom Legacy-Programm geschrieben, **nicht** vom Generator |
| [../../src_old/docs/topics.md](../../src_old/docs/topics.md) | Archivkopie dieses Stands (siehe [src_old/docs/README.md](../../src_old/docs/README.md)) |
| [../topics-v2.md](../topics-v2.md) | der neue, handgepflegte Vertrag (dieses Handbuch als Quelle) |
| [../../src/ComfoBox.Domain/Items/Catalog.cs](../../src/ComfoBox.Domain/Items/Catalog.cs) | generierter Item-Katalog; `BacnetObjectId` = DP-Nummer aus diesem Handbuch |
| [../04-migration/mqtt-kompatibilitaet.md](../04-migration/mqtt-kompatibilitaet.md) | die Zusage, dass V2 **additiv** bleibt |

## 6. Pflege

- Änderungen an einer **bestehenden** Topic-Adresse sind nur über ein neues ADR zulässig
  (siehe [../05-entscheidungen/](../05-entscheidungen/README.md)).
- Neue Topics werden zuerst in [../topics-v2.md](../topics-v2.md) beschrieben, dann im
  Katalog (`src_old/` → Generator → `Catalog.cs`) umgesetzt.
- Wird das Handbuch in einer neuen Version geliefert: neue Spalte „Seite" prüfen, DP-Nummern
  stichprobenweise gegen [datenpunkt-register.md](datenpunkt-register.md) verifizieren.
