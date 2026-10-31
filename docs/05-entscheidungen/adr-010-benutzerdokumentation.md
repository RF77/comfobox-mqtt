# ADR-010: Benutzerdokumentation – eine Quelle, drei Auslieferungskanäle

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Dokumentation, Frontend, Deployment |

## Kontext

Im Repo liegt eine umfangreiche Doku (57 Markdown-Dateien in `docs/`), aber:

1. Sie richtet sich an **Entwickler** – „BACnet-Stack portiert", „Contract-Test",
   „ADR-Status". Ein Betreiber einer Heizungsanlage kann damit nichts anfangen.
2. Sie ist **nur** als Markdown auf GitHub erreichbar. Wer den Container benutzt,
   hat **keinen** Zugriff auf das Repository.
3. Die Oberfläche hat **keine** Hilfe. Bei einem Problem („die Heizung zeigt
   `unavailable`") bleibt nur das Logfile.

Gleichzeitig wächst die Zahl der Auslieferungswege:

| Kanal | Wer liest |
|---|---|
| Web-UI (Container) | Betreiber |
| Home Assistant (HACS-Dialog, Config Flow) | HA-Nutzer |
| Synology (Paketzentrum, `helpurl`) | NAS-Nutzer |

> *„Doku für Gateway selbst wohl für den Benutzer in unserem Angular UI, also so quasi,
> was aktuell als Doku auch schon im GIT liegt, aber als HTML für den Benutzer lesbar.
> Help für HA in HACS natürlich auch und evt. auch für Synology."*

## Entscheidung

### E1 – Getrennte Benutzerdoku, gemeinsame Quelle

Es gibt **zwei** Doku-Welten im Repo:

| Welt | Ort | Zielgruppe |
|---|---|---|
| Spezifikation | `docs/01-…` bis `docs/05-…` | Entwickler, Reviewer |
| **Benutzerdokumentation** | **`docs/benutzer/`** | Betreiber |

Die Benutzerdoku ist **deutsch**, anleitungsgerecht und enthält **keine**
Implementierungsdetails. Sie verlinkt die Spezifikation, sie kopiert sie nicht.

### E2 – Drei Kanäle, ein Inhalt

| Kanal | Auslieferung | Build |
|---|---|---|
| **A – Web-UI** | statisches HTML unter `/help/**` | `tools/build-docs.mjs` zur Build-Zeit |
| **B – HACS** | `README.md` + `strings.json` + Link auf die Doku | Teil des Repos |
| **C – Synology** | `INFO` (`description_ger`, `helpurl`) + `README.md` + Link | Teil des SPKs |

> **Regel:** Jeder Text wird **einmal** geschrieben. Die drei Kanäle enthalten Links,
> keine Abschriften.

### E3 – Markdown → HTML zur Build-Zeit

Die Benutzerdoku wird **im Docker-Build** in HTML umgewandelt und landet als statische
Datei im Image.

| Option | Verworfen, weil |
|---|---|
| **Build-Zeit (gewählt)** ✅ | kein Parser im Browser, kein CDN, indexierbar, Doku-Updates sind normale Code-Updates |
| Markdown im Browser rendern | 40 KB JS extra, Markdown im Quellcode sichtbar |
| Markdown im Backend rendern | Serverlast für reine Leseinhalte |

Der Build **bricht ab**, wenn eine Seite kein vollständiges Front-Matter hat oder
auf einen toten Link zeigt.

### E4 – Front-Matter steuert Navigation und Suche

````yaml
---
title: Erste Schritte
description: Installation, Anschluss und die ersten gelesenen Werte
order: 10
icon: pi pi-rocket
tags: [installation, anleitung]
---
````

Der Index (`help/index.json`) trägt Titel, Beschreibung, Tags und Volltext → die Suche
läuft clientseitig, ohne Serveranfrage.

### E5 – Hilfe ist Teil des Produkts

| Punkt | Entscheidung |
|---|---|
| Route | `/help` und `/help/:slug` in der Angular-App |
| Zugriff | geschützt wie der Rest der App, aber **vor** der Ersteinrichtung erreichbar |
| Verlinkung | der Setup-Assistent verlinkt an 5 Stellen auf die passende Hilfeseite |
| Offline | die Inhalte liegen im Container → kein Internet nötig |
| Druck | eigenes Stylesheet für Ausdrucke |

### E6 – Sprachregeln

| Regel | Begründung |
|---|---|
| **Deutsch**, Sie-Form | Zielgruppe sind Betreiber |
| Anleitungssatz zuerst | „Klicken Sie auf **Installieren**" statt „Die Installation erfolgt über…" |
| Fehler: **Symptom → Ursache → Lösung** | der Nutzer kommt mit dem Symptom |
| Kein Jargon ohne Erklärung | „MS/TP-Token" wird erklärt |
| Keine Implementierungsdetails | kein `SemaphoreSlim`, keine Paketversionen |

### E7 – Synology-Integration bleibt hinten

Die direkte Synology-Integration (DSM-Geräte-UI statt Web-UI) ist **nicht** Teil dieses
Umfangs.

| Begründung |
|---|
| Doppelte Implementierung derselben Oberfläche in einer zweiten Technologie |
| Das Web-UI ist bereits über das Paketzentrum erreichbar (`adminport` + `adminurl`) |
| Der Auftraggeber hat die Priorität ausdrücklich zurückgestellt |
| Die Benutzerdoku für Kanal C deckt den DSM-Nutzer ab, ohne eine zweite Oberfläche |

> **Wiedereinstieg**, falls es doch gebraucht wird: ein DSM-Paket mit einer
> `DSM`-UI-Seite, die nur die Konfiguration anzeigt und den Rest an das Web-UI
> verlinkt. **Kein** vollständiges UI.

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **Build-Zeit-HTML + drei Kanäle** ✅ | eine Quelle, offline, indexierbar, updatebar | – |
| Doku nur im Web-UI | einfach | HA- und NAS-Nutzer hätten nichts; die Kanäle sind billig dazuzufügen |
| Doku nur auf GitHub | kein Aufwand | wer den Container nutzt, kommt nicht ans Repo |
| Live aus dem Netz laden | immer aktuell | kein Internet = keine Hilfe; außerdem ein Externer Abhängigkeit für reine Leseinhalte |
| Wiki auf GitHub | getrennte Pflege | Drift zwischen Wiki und Repo ist die Regel, nicht die Ausnahme |
| Ein einziges langes Dokument | keine Navigation | 10 Themen in einem Scroll → unbrauchbar |
| Synology-Integration jetzt | konsistente Oberfläche | doppelte Technologie, zurückgestellt |

## Konsequenzen

### Positiv

- Der Nutzer hat **im Betrieb** Hilfe – ohne Repo, ohne Internet
- **Ein** Text, drei Orte → keine Drift zwischen UI-Hilfe, HACS-Dialog und DSM-Paket
- Die Spezifikation bleibt sauber getrennt → Entwickler werden nicht mit
  Anleitungstexten belästigt, Betreiber nicht mit `MQTTnet 5.2.0.1603`
- Der Build **erzwingt** Doku-Qualität (Front-Matter, keine toten Links)
- Die Doku versioniert mit dem Code → alte Releases haben ihre passende Doku

### Negativ / Aufwand

- **≈7 PT** zusätzlich (Struktur + 10 Seiten ≈ 4 PT, Build-Skript ≈ 1 PT,
  UI-Hilfebereich ≈ 1,5 PT, Kanal-Anbindung ≈ 0,5 PT)
- Ein **zweites** Doku-Format (Front-Matter) → Pflege
- Doppelte Pflege: eine Verhaltensänderung muss **sowohl** die Spezifikation **als auch**
  die Benutzerdoku betreffen → Review-Regel nötig
- Der Build bricht bei fehlerhaftem Markdown ab → das ist beabsichtigt, aber es ist eine
  **zusätzliche Fehlerquelle** im Release
- `PACKAGE_ICON.PNG` fehlt noch → das Paketzentrum zeigt ohne Icon ein Standardbild

### Neutral

- Die Benutzerdoku ist **nicht** Teil der MQTT-Kompatibilitätszusage → Änderungen daran
  sind unkritisch
- Die Doku braucht **kein** Backend → keine zusätzliche Angriffsfläche
- Der Suchindex ist statisch → keine Volltextsuche über mehrere Seiten hinweg

## Verwandte Dokumente

- [benutzerdokumentation.md](../02-modernisierung/benutzerdokumentation.md) – die vollständige Spezifikation
- [ui-angular-optimus.md](../02-modernisierung/ui-angular-optimus.md) § 13 – der Hilfe-Bereich
- [installationsstufen.md](../03-deployment/installationsstufen.md)
- [synology-paketcenter.md](../03-deployment/synology-paketcenter.md)
- [ADR-007](adr-007-ein-container-image.md) · [ADR-009](adr-009-ha-integration-ohne-mqtt.md)
