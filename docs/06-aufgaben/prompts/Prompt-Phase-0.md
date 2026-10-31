---
title: Prompt-Phase-0
description: Kopierfertiger Session-Prompt für die Phase 0 (Repo-Hygiene & CI)
order: 0
---

# Prompt – Phase 0: Repo-Hygiene & CI

> Kopierfähiger Prompt für die **nächste** KI-Session. Er ist bewusst
> **vollständig und in sich geschlossen** – die Session braucht keinen Kontext
> aus diesem Chat.
>
> **Konvention:** Am **Ende jeder** Session wird der Prompt für die nächste
> Session ausgegeben (siehe [README §3.3](../README.md#33-nachher)).

---

## Prompt (zum Kopieren)

````text
Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze die **Phase 0** des Aufgaben-Dokuments um.

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md              ← die Projektregeln
2. docs/06-aufgaben/README.md                   ← wie eine Session abläuft
3. docs/06-aufgaben/status.md                   ← was als Nächstes dran ist
4. docs/06-aufgaben/phase-0.md                  ← die Aufgaben 0.1 bis 0.8
5. docs/02-modernisierung/zielversionen.md      ← die verbindlichen Versionen

Ziel der Phase 0: eine belastbare Basis, bevor Code bewegt wird.
Es werden noch KEINE Features gebaut – nur das Fundament.

Aufgaben (alle aus phase-0.md, mit den dort genannten Akzeptanzkriterien):
- 0.1  src/global.json                    (SDK-Pinning 10.0.400)
- 0.2  src/Directory.Packages.props       (Central Package Management)
- 0.3  src/Directory.Build.props          (Nullable, TreatWarningsAsErrors, C# 14)
- 0.4  src/ComfoBox.sln + 7 SDK-Projekte  (Domain, Bacnet, Gateway, 4 Testprojekte)
- 0.5  .gitignore                          (bereits erledigt – nur prüfen)
- 0.6  .github/workflows/build-test.yml   (CI: Build & Test)
- 0.7  Fixtures prüfen                    (docs/topics.json = 127 Items, BACnet-Fixture unverändert)
- 0.8  CHANGELOG.md anlegen

Wichtige Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- `dotnet new sln` erzeugt im .NET-10-SDK standardmäßig .slnx. Die Spec verlangt
  ComfoBox.sln → `dotnet new sln -n ComfoBox --format sln`.
- Die Templates schreiben Version="..." in jedes PackageReference. Mit
  ManagePackageVersionsCentrally=true bricht das den Restore mit NU1008.
  → In allen .csproj die Version-Attribute entfernen.
- Directory.Build.props setzt GenerateDocumentationFile=true UND
  TreatWarningsAsErrors=true. Die Template-Dateien Class1.cs / UnitTest1.cs haben
  keinen XML-Kommentar → CS1591 wird zum Fehler. → Platzhalter löschen.
- coverlet.collector kommt mit dem xunit-Template mit, steht aber nicht in der
  Paketliste der Spec → braucht eine eigene PackageVersion-Zeile.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root
  aus – dort liegen die net452-Projekte in src_old/, die nicht gebaut werden dürfen.
- src_old/ ist Archiv: nur lesen, nie ändern, nie bauen.
- docs/topics.json und docs/Mono/MoMA Scan Results.html sind Test-Fixtures und
  dürfen NICHT verändert werden.

Regeln für diese Session:
- Nur die Phase 0 umsetzen, nichts darüber hinaus.
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Nach jeder Aufgabe: Status in docs/06-aufgaben/status.md setzen
  (⬜ offen / 🟡 in Arbeit / ✅ fertig und verifiziert / ⛔ blockiert).
- Am Ende: Eintrag in docs/06-aufgaben/protokoll.md (Datum, Aufgabe, Ergebnis,
  Verifikation, offene Punkte, Überraschungen).
- Wenn eine neue Entscheidung nötig ist: ADR anlegen und docs/README.md sowie
  docs/05-entscheidungen/README.md aktualisieren.
- Keine Secrets, keine Passwörter in Dateien oder Logs.
- Sprache: Code-Bezeichner englisch, Kommentare und Doku deutsch.

Am Ende der Session:
1. Führe alle Verifikationsbefehle aus phase-0.md aus und zeige das Ergebnis.
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 1), in
   derselben Form wie dieser Prompt – inklusive der Fallstricke, die in dieser
   Session aufgetreten sind.
````

---

## Was der Prompt abdeckt

| Aufgabe | PT | Titel |
|---|---|---|
| 0.1 | 0.2 | `global.json` (SDK-Pinning) |
| 0.2 | 0.3 | `Directory.Packages.props` (Central Package Management) |
| 0.3 | 0.2 | `Directory.Build.props` (Qualitäts-Gate) |
| 0.4 | 0.3 | `ComfoBox.sln` + SDK-Projekte |
| 0.5 | 0.1 | `.gitignore` (bereits erledigt) |
| 0.6 | 0.4 | CI: Build & Test |
| 0.7 | 0.3 | Fixtures fixieren |
| 0.8 | 0.2 | `CHANGELOG.md` |
| | **2.0** | **Summe** |

---

## Vorbedingungen (geprüft am 2026-10-31)

| Bedingung | Status |
|---|---|
| .NET SDK 10.0.400 | ✅ installiert (`dotnet --version` → 10.0.400) |
| Node.js 22 | ✅ installiert (für Phase 5) |
| Python 3.12 | ✅ installiert (für die HA-Integration) |
| Git-Bash | ✅ vorhanden (Shell-Skripte) |
| Docker-Daemon | ⚠️ fehlt – **Phase 6** braucht Docker Desktop |

> **Phase 0 braucht kein Docker und kein Python.** Die Session ist vollständig
> lokal verifizierbar.

---

## Verwandte Dokumente

- [phase-0.md](../phase-0.md) – die Aufgaben im Detail
- [status.md](../status.md) – Fortschritt
- [protokoll.md](../protokoll.md) – Session-Protokoll
- [README.md](../README.md) – wie eine Session abläuft
- [Prompt-Phase-1.md](Prompt-Phase-1.md) – der Prompt für die nächste Session
