<!--
Pflicht-Checkliste für alle PRs.
Bezug: docs/04-migration/checkliste.md
-->

## Was wird geändert?

<!-- Kurzbeschreibung, 1–3 Sätze. -->

## Art der Änderung

- [ ] Bugfix (nenne die Befund-ID, z. B. `A5`, `A8`, `A10`)
- [ ] Feature / neue Funktion
- [ ] Refactoring (kein Verhaltenswechsel)
- [ ] Dokumentation
- [ ] Build / CI

## Betroffene Bereich

- [ ] BACnet / RS485 (`src/ComfoBox.Bacnet`)
- [ ] Domäne / Item-Katalog (`src/ComfoBox.Domain`)
- [ ] Gateway / MQTT / API (`src/ComfoBox.Gateway`)
- [ ] Web-UI (`src/ComfoBox.Web`)
- [ ] Container / Deployment
- [ ] Doku / ADR

## MQTT-Kompatibilität (Anforderung 4)

- [ ] Keine bestehende Topic-Adresse geändert
- [ ] Kein bestehendes Payload-Format geändert
- [ ] Enum-Namen unverändert
- [ ] Neue Topics nur unter `ComfoBox/Status/` bzw. `ComfoBox/Command/`
- [ ] Contract-Test (`ComfoBox.Contract.Tests`) ist grün
- [ ] Änderung ist **additiv** → `docs/04-migration/mqtt-kompatibilitaet.md` ergänzt

## Qualität

- [ ] `dotnet build src/ComfoBox.sln -c Release` grün (keine Warnungen)
- [ ] `dotnet test src/ComfoBox.sln -c Release` grün
- [ ] Neue Logik hat **Tests** (inkl. Negativfälle)
- [ ] Zahlen werden kultur-invariant verarbeitet (`InvariantCulture`)
- [ ] Kein `async void`, kein `lock (this)`, kein statischer veränderlicher Zustand
- [ ] Fehler werden **nicht** verschluckt (Log + `Status/Errors` + UI)
- [ ] Golden-Fixtures (`docs/topics.json`, `bacnet-frames.json`) **nicht** geändert
      – falls doch: im PR **begründet** und im ADR dokumentiert
- [ ] `npm run lint` grün (falls Frontend betroffen)

## Dokumentation

- [ ] Verhaltensänderung in `docs/` beschrieben
- [ ] Neues ADR, falls eine Architekturentscheidung getroffen wurde
- [ ] `CHANGELOG.md` aktualisiert
- [ ] Relative Links, keine absoluten Pfade

## Checkliste

- [ ] Die [Checkliste zur Phase](../docs/04-migration/checkliste.md) ist (soweit zutreffend) erfüllt
- [ ] Screenshots bei UI-Änderungen
- [ ] Issue verlinkt (`Closes #123`)

## Risiken / Hinweise für den Reviewer

<!-- Was sollte der Reviewer besonders prüfen? -->
