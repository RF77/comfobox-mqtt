# Prompt – Phase 9, Hardware-Abnahme

> Kopierfertiger Prompt für die Session **nach** der Umsetzung von Phase 9.
> Phase 9 selbst ist am 2026-11-15 umgesetzt (siehe
> [protokoll.md](../protokoll.md), Abschnitt *2026-11-15 (2)*). Offen sind die
> Abnahmekriterien, die ein angeschlossenes Gerät brauchen.

---

```text
Phase 9 ist umgesetzt. Prüfe jetzt die Punkte, die nur am Gerät gehen, und ziehe die
Doku nach.

Lies in dieser Reihenfolge:
1. docs/06-aufgaben/phase-9-datenpunkt-korrekturen.md          (die Abnahmekriterien)
2. docs/06-aufgaben/protokoll.md, Abschnitt "2026-11-15 (2)"   (was umgesetzt ist)
3. docs/07-technikerhandbuch/review-ist-implementierung.md     (R1-R12 mit Status)
4. .github/copilot-instructions.md                            (Projektregeln)

Aufgaben:
- POST /api/diagnostics/bacnet/observe starten und die Objekte 2779, 2781, 2782, 2783,
  2785, 2788, 2789, 2790 (R6), 3432 (R1) und 846 (R3) lesen. Was antwortet, bleibt; was
  "unknown" liefert, wird im Review als Rückbau-Kandidat vermerkt - der Rückbau ist eine
  Zeile in src_old/ComfoBoxLib/Items/States.cs bzw. Config.cs.
- Entladeschutz (DP 157) = 3 schreiben (R2) und am Gerät prüfen, dass der Entladeschutz
  einschaltet. Wert 1 muss abgelehnt werden (AllowedValues = {0, 3}).
- PrimaryPump/StoppedMode (DP 3096) = 11 (PWM) schreiben (R8) und prüfen.
- 3452 gegen 3432 vergleichen: 3452 ist laut Handbuch "KM2-Ausgang bei Error" (0/1).
- Warmwasser: prüfen, ob der Regler 58 °C als Obergrenze der Sollwerte bestätigt (R10)
  und ob SetPointLegio danach noch sinnvoll nutzbar ist.
- Ergebnisse in docs/07-technikerhandbuch/review-ist-implementierung.md (Statuszeile je
  Befund) und in docs/06-aufgaben/protokoll.md eintragen; status.md nachziehen.

Wichtig (in dieser Session verifiziert):
- src_old/**/Items/*.cs sind Windows-1252. Mit [Text.Encoding]::GetEncoding(1252) lesen
  UND schreiben, danach git diff --stat prüfen (keine Encoding-Welle) und
  Select-String 'Ã|Â' laufen lassen.
- Nach jeder Katalogänderung: node tools/generate-topics/generate-topics.mjs --catalog
  src/ComfoBox.Domain/Items (VERZEICHNIS, keine Datei), danach --out docs/topics.json,
  --catalog-check und --check.
- docs/topics.json nie von Hand ändern; --out entfernt das UTF-8-BOM (EF BB BF) - danach
  wieder voranstellen. docs/topics.md wird NIE angefasst (eingefroren).
- Alte/neue Fixture nicht per PowerShell-Pipe vergleichen (git show | ConvertFrom-Json
  zerstört die Umlaute). Stattdessen: cmd /c "git show HEAD:docs/topics.json > datei"
  und den Vergleich in Node machen.
- Nach Doku-Änderungen: node tools/build-docs.mjs (Hilfe-Seiten sind eingecheckt) und
  pwsh -NoProfile -File tools/check-links.ps1.
- Vor Massen-Ersetzungen die Zielzeile mit Select-String AUSGEBEN - `false};` und
  `false };` sind verschiedene Muster.
- Am Ende: dotnet build (0 Warnungen), dotnet test (aktuell 396 grün), npm run lint,
  npm run build.
```

---

## Was diese Session **nicht** mehr braucht

| Punkt | Grund |
|---|---|
| R7-Migration in Home Assistant | beim Nutzer, nicht im Repo (zwei neue Entitäten, alte löschen) |
| `--emit-markdown` für den Generator | optionale eigene Session (neue Datei, **nicht** `docs/topics.md`) |
| Neue Topics (DP 3097, V2) | eigene Phase, siehe [../../topics-v2.md](../../topics-v2.md) |
