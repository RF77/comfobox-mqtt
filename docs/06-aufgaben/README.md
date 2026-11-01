# 06 – Aufgaben (Implementierung)

> Dieses Verzeichnis ist die **Arbeitsanleitung** für die Umsetzung. Jede Aufgabe ist
> so formuliert, dass sie in **einer** KI-Session (oder an einem Tag) erledigt werden kann.

## 1. Zweck

Die [Roadmap](../04-migration/roadmap.md) beschreibt die **Phasen**. Dieses
Verzeichnis beschreibt die **Aufgaben** – klein genug für eine Session, groß genug
für einen Review.

| Frage | Antwort |
|---|---|
| Was ist als Nächstes dran? | [status.md](status.md) |
| Was genau muss ich tun? | die Phasen-Datei, z. B. [phase-1.md](phase-1.md) |
| Was habe ich heute gemacht? | [protokoll.md](protokoll.md) |
| Was sind die Regeln? | [copilot-instructions.md](../../.github/copilot-instructions.md) |

## 2. Dateien

| Datei | Inhalt |
|---|---|
| [status.md](status.md) | **Fortschrittstabelle** – eine Zeile pro Aufgabe |
| [protokoll.md](protokoll.md) | **Session-Protokoll** – was wurde wann gemacht |
| [prompts/](prompts/) | **Kopierfertige Session-Prompts** je Phase |
| [phase-0.md](phase-0.md) | Repo-Hygiene & CI |
| [phase-1.md](phase-1.md) | BACnet-Portierung & Golden-Tests |
| [phase-2.md](phase-2.md) | Domain & Gateway-Kern |
| [phase-3.md](phase-3.md) | MQTT-Migration & HA-Discovery |
| [phase-4.md](phase-4.md) | REST/SSE-API |
| [phase-5.md](phase-5.md) | Angular/Optimus UI |
| [phase-6.md](phase-6.md) | Container & Synology |
| [phase-8.md](phase-8.md) | Synology-Paket & Getting Started |
| [phase-8ad.md](phase-8ad.md) | HA-Add-on & HACS-Integration |
| [phase-7.md](phase-7.md) | Parallelbetrieb & Umschaltung |
| [prompts/](prompts/) | **Kopierfertige Session-Prompts** je Phase |

## 3. Wie eine Session läuft

### 3.1 Vorher

| Schritt | Kommando / Aktion |
|---|---|
| 1 | `git pull` – auf dem aktuellen Stand sein |
| 2 | [status.md](status.md) lesen: Was ist als Nächstes dran? |
| 3 | Phasen-Datei lesen: **eine** Aufgabe auswählen |
| 4 | Abhängigkeiten prüfen: Sind alle Voraussetzungen `✅`? |
| 5 | Die verlinkten **Spec-Dokumente** lesen – nicht nur die Aufgabenbeschreibung |

### 3.2 Während

| Regel | Grund |
|---|---|
| **Nur die gewählte Aufgabe** umsetzen | sonst wird der Review unprüfbar |
| Der Spec ist **verbindlich** | die Aufgabe ist die Kurzfassung, nicht die Quelle |
| Bestehende Tests **nicht** löschen, um grün zu werden | die Tests sind der Vertrag |
| `src_old/` **nicht** anfassen | es ist Archiv |
| Bei Widerspruch Spec ↔ Aufgabe | **Aufgabe anpassen**, Spec nicht – und im Protokoll vermerken |

### 3.3 Nachher

| Schritt | Aktion |
|---|---|
| 1 | Verifikation: alle Kommandos aus der Aufgabe müssen grün sein |
| 2 | [status.md](status.md): Status der Aufgabe auf `✅` setzen |
| 3 | [protokoll.md](protokoll.md): Eintrag mit Datum, Aufgabe, Ergebnis, offenen Punkten |
| 4 | Commit: `feat(scope): …` / `fix(scope): …` (Conventional Commits) |
| 5 | Bei neuen Entscheidungen: ADR anlegen, `docs/README.md` + `docs/05-entscheidungen/README.md` aktualisieren |
| 6 | **Den Prompt für die nächste Session ausgeben** (siehe §3.4) |

### 3.4 Der nächste Prompt

> **Regel: Am Ende **jeder** Session wird der Prompt für die nächste Session
> ausgegeben.** Er ist kopierfertig und **vollständig** – die nächste Session
> braucht keinen Kontext aus dem Chat.

| Punkt | Inhalt |
|---|---|
| **Form** | ein Textblock in Fenced-Code, zum direkten Kopieren |
| **Inhalt** | Ziel, die zu lesenden Dateien (in Reihenfolge), die Aufgaben, die verifizierten Fallstricke, die Regeln |
| **Fallstricke** | alles, was in dieser Session **überraschend** war, kommt in den nächsten Prompt |
| **Quelle** | [prompts/](prompts/) – dort liegt je Phase eine fertige Vorlage |

**Vorlage:** [prompts/Prompt-Phase-0.md](prompts/Prompt-Phase-0.md) ·
[prompts/Prompt-Phase-1.md](prompts/Prompt-Phase-1.md) ·
[prompts/Prompt-Phase-2.md](prompts/Prompt-Phase-2.md) ·
[prompts/Prompt-Phase-3.md](prompts/Prompt-Phase-3.md) ·
[prompts/Prompt-Phase-4.md](prompts/Prompt-Phase-4.md) ·
[prompts/Prompt-Phase-5.md](prompts/Prompt-Phase-5.md) ·
[prompts/Prompt-Phase-8.md](prompts/Prompt-Phase-8.md) ·
[prompts/Prompt-Phase-8ad.md](prompts/Prompt-Phase-8ad.md)

> **Warum?** Die Phasen sind aufeinander aufgebaut. Wer die Fallstricke nicht
> weitergibt, verschwendet in der nächsten Session Zeit mit demselben Fehler –
> oder überschreitet die Phase, weil der Zielbild unklar ist.

## 4. Status-Logik

| Status | Bedeutung | Bedingung |
|---|---|---|
| `⬜` | offen | – |
| `🟡` | in Arbeit | angefangen, nicht fertig |
| `✅` | fertig | alle Akzeptanzkriterien erfüllt **und** verifiziert |
| `⛔` | blockiert | Grund in [protokoll.md](protokoll.md) |

> **Eine Aufgabe gilt erst als `✅`, wenn die Verifikation gelaufen ist.**
> „Ich habe es geschrieben" ist kein Fertig-Kriterium.

## 5. Phasen und ihre Abhängigkeiten

```
Phase 0 ──▶ Phase 1 ──▶ Phase 2 ──▶ Phase 3 ──▶ Phase 4 ──▶ Phase 5 ──▶ Phase 6 ──▶ Phase 8 ──▶ Phase 8a–d ──▶ Phase 7
  2 PT        8 PT        6 PT        6 PT        4 PT        8 PT        4 PT        13 PT        14 PT          3 PT
```

| Phase | Braucht | PT |
|---|---|---|
| 0 | – | 2 |
| 1 | 0 | 8 |
| 2 | 1 | 6 |
| 3 | 2 | 6 |
| 4 | 2, 3 | 4 |
| 5 | 4 | 8 |
| 6 | 3, 4, 5 | 4 |
| 8 | 5, 6 | 13 |
| 8a–d | 4, 6, 8 | 14 |
| 7 | 6 | 3 |

> **Summe: 63 PT** ≈ 16 Arbeitstage.

## 6. Vorbedingungen, die global gelten

| Bedingung | Status |
|---|---|
| .NET SDK 10.0.400 | ✅ installiert |
| Node.js 22 | ✅ installiert |
| Git-Bash (`C:\Program Files\Git\bin\bash.exe`) | ✅ vorhanden |
| Docker-Daemon | ✅ **läuft** (Docker Desktop 29.9.2, Linux-Container) |
| Python 3.12 | ✅ installiert (`winget --scope user`, ohne Admin) |

> **Hinweis für die Umgebung:** Python 3.12 ist seit 2026-10-31 installiert, die
> HA-Integrationstests laufen grün. Der **Docker-Daemon läuft seit 2026-11-01** –
> die Aufgaben 6.1–6.3, 6.5 und 6.6 sind verifiziert. Offen bleibt **6.4**
> (Test auf der echten DS923+ – das Gerät steht hier nicht zur Verfügung).

## 7. Globale Regeln ( Kurzform)

Die vollständigen Regeln stehen in
[`.github/copilot-instructions.md`](../../.github/copilot-instructions.md). Die wichtigsten:

| # | Regel |
|---|---|
| 1 | `src_old/` ist **Archiv** – nicht bauen, nicht pflegen, nur lesen |
| 2 | MQTT-Topics und Payloads **bleiben identisch** |
| 3 | Zahlen **immer** mit `InvariantCulture` |
| 4 | Der **Schreibzähler** darf von keinem Schreibpfad umgangen werden |
| 5 | Ein ungültiger Wert wird **abgelehnt** – nie ein Default, nie `0` |
| 6 | Der BACnet-Codec wird **1:1 portiert**, nicht verbessert |
| 7 | `async`/`await`, kein `.Result`, kein `async void` |
| 8 | `ILogger`, kein `System.Diagnostics.Trace` |
| 9 | Fehler landen in **drei** Kanälen: Log, `ComfoBox/Status/Errors`, UI |
| 10 | Jede Verhaltensänderung → Doku aktualisieren + `CHANGELOG.md` |

## 9. Verwandte Dokumente

- [Roadmap](../04-migration/roadmap.md) – Phasen, Aufwände, Meilensteine
- [Checkliste](../04-migration/checkliste.md) – Abnahme pro Phase
- [Teststrategie](../04-migration/teststrategie.md)
- [MQTT-Kompatibilität](../04-migration/mqtt-kompatibilitaet.md) – der bindende Vertrag
- [src/README.md](../../src/README.md) – Zielstruktur der Neuzuimplementierung
