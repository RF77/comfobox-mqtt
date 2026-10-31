# Abnahme-Checklisten

Checklisten pro Phase. Jede Phase gilt erst als abgeschlossen, wenn **alle** Punkte erfüllt sind.

---

## Phase 0 – Repo-Hygiene & CI

- [ ] `global.json` vorhanden, SDK-Version gepinnt
- [ ] `Directory.Packages.props` mit allen Paketversionen
- [ ] `Directory.Build.props` mit `Nullable=enable` und `TreatWarningsAsErrors=true`
- [ ] `dotnet build` läuft ohne Visual Studio
- [ ] `dotnet test` läuft (auch bei 0 Tests)
- [ ] `.gitignore` enthält `.env`, `appsettings.override.json`, `*.user`
- [ ] CI-Workflow `build-test.yml` läuft grün bei einem Test-PR
- [ ] `docs/topics.json` und `docs/Mono/MoMA Scan Results.html` liegen unverändert im Repo
- [ ] `CHANGELOG.md` angelegt
- [ ] README verweist auf `docs/`

---

## Phase 1 – BACnet-Portierung

- [ ] Golden-Frames als JSON-Fixture abgelegt
- [ ] Golden-Tests existieren und waren **vor** der Portierung grün (gegen net452)
- [ ] `ComfoBox.Bacnet` baut ohne Windows-spezifische Abhängigkeiten
- [ ] Golden-Tests grün **ohne** Fixture-Änderung
- [ ] Pipe-, UDP- und PTP-Transport entfernt
- [ ] Kein `System.Diagnostics.Trace` mehr im Projekt
- [ ] Kein `lock (this)`, kein unsicheres `m_invoke_id++`
- [ ] `ThreadPriority.Highest` entfernt
- [ ] Port-Open-Fehler → sauberer Fehler, **kein** Spin-Loop
- [ ] Reconnect mit Backoff getestet (Port schließen/öffnen)
- [ ] MIT-Lizenz und Original-Urheber dokumentiert
- [ ] `dotnet test` vollständig grün

---

## Phase 2 – Domain & Gateway-Kern

- [ ] `ComfoBox.Domain` ohne externe Abhängigkeiten
- [ ] Item-Katalog enthält **exakt 127** Items
- [ ] Alle Item-Metadaten (Min/Max/Unit/IsReadOnly/Beschreibung) übernommen
- [ ] `DateValue` und `CommandValue` entfernt (keine toten Zweige)
- [ ] Priorisierung (`Once`/`Primary`/`Secondary`) **wirksam** (nicht toter Code)
- [ ] `ValueCache` vorhanden, `IsStale` funktioniert
- [ ] **Persistenter Schreibzähler** aktiv und getestet (`Count_Persists_AcrossRestart`)
- [ ] Konfigurationsvalidierung: Start schlägt bei fehlendem Port/Broker fehl
- [ ] `dotnet test` grün

---

## Phase 3 – MQTT & Home-Assistant-Discovery

- [ ] MQTTnet 5 im Einsatz, M2Mqtt vollständig entfernt
- [ ] TLS, Username/Password und Port **pro Broker** konfigurierbar
- [ ] Keepalive ≥ 15 s (nicht 2 s)
- [ ] Reconnect mit Backoff **und** CancellationToken
- [ ] **Nach Reconnect wird `Connected=1` publiziert** (Regressionstest A10)
- [ ] LWT auf `Status/Health` und `Connected` registriert
- [ ] Sauberer Shutdown: `offline` publishen, dann trennen (kein LWT-Feuer)
- [ ] Alle Zahlen-Payloads **invariant** (`InvariantCulture`) – Regressionstest
- [ ] Ungültige Enum-Payload schreibt **keinen** 0-Wert (Regressionstest A6)
- [ ] `ComfoBox/Connected` und `ComfoBox/Version` unverändert vorhanden
- [ ] `ComfoBox/Special/NumberOfWritesPer24h` unverändert vorhanden
- [ ] Home-Assistant-Discovery für alle 127 Items
- [ ] Discovery-Payloads sind valides JSON und erfüllen das HA-Schema
- [ ] **Contract-Test grün** (alle Topics aus `docs/topics.json`)
- [ ] Kein `Virtual`-Topic im Katalog

---

## Phase 4 – REST/SSE-API

- [ ] Alle Endpunkte aus [Datenmodell & API](../02-modernisierung/datenmodell-und-api.md#31-endpunkte) vorhanden
- [ ] OpenAPI/Swagger erreichbar unter `/swagger`
- [ ] Validierung mit korrekten Statuscodes (400/403/409/422/502)
- [ ] Schreibfehler landen in `Status/Errors`
- [ ] SSE liefert `snapshot` + `value`-Events
- [ ] `nginx`-Proxying ohne SSE-Buffering getestet
- [ ] `/api/service/restart` startet den Container neu (auf dem NAS getestet)
- [ ] API-Tests grün

---

## Phase 5 – Angular/Optimus UI

- [ ] `ng add @openng/optimus-ui` → `provideOptimus({ theme: { preset: Aura } })`
- [ ] Dashboard zeigt Health, Verbindungen, Version, Uptime, Schreibzähler
- [ ] **Fehlerbanner** bei `degraded` und `offline` sichtbar und verständlich
- [ ] Item-Browser: alle 127 Items, gruppiert, durchsuchbar
- [ ] Analogwerte schreibbar mit Min/Max-Validierung
- [ ] Enumwerte schreibbar über Select
- [ ] Schreibfehler → Toast + Serverwert wird wiederhergestellt
- [ ] Konfigurationsseite: `ExpertMode`, Poll-Intervall, Schreiblimit änderbar
- [ ] `ExpertMode`-Umschaltung mit Bestätigungsdialog
- [ ] Neustart-Button mit Bestätigung
- [ ] Live-Werte via SSE (kein Polling im Browser)
- [ ] Responsive 360 px – 1920 px
- [ ] `npm run lint` grün, Produktions-Build erfolgreich
- [ ] UI-Texte deutsch

---

## Phase 6 – Container & Synology

- [ ] `docker build` für Gateway und Web erfolgreich
- [ ] Image-Größe im erwarteten Rahmen (≈ 230 MB / 55 MB)
- [ ] `docker compose up -d` auf der **realen DS923+** erfolgreich
- [ ] **`/dev/ttyUSB0` im Container sichtbar und lesbar**
- [ ] `IAm` wird empfangen (Status/Bacnet = connected)
- [ ] Werte werden publiziert (in HA sichtbar)
- [ ] Schreibvorgang funktioniert (Sollwert setzen, Wert prüfen)
- [ ] Container überlebt einen NAS-Neustart (`restart: unless-stopped`)
- [ ] Container-Healthcheck meldet `healthy`
- [ ] Logs landen in `/var/log/comfobox` und auf stdout
- [ ] Backup & Restore getestet
- [ ] Trivy-Scan ohne kritische Findings
- [ ] CI baut und publiziert beide Images

---

## Phase 7 – Parallelbetrieb & Umschaltung

- [ ] Neues Gateway läuft parallel mit `BaseTopic=ComfoBox2`
- [ ] Vergleichstool zeigt keine relevanten Abweichungen
- [ ] HA-Discovery mit `ComfoBox2` geprüft
- [ ] `BaseTopic` auf `ComfoBox` umgestellt
- [ ] Discovery-Entitäten aktualisieren sich (gleiche `unique_id`)
- [ ] Alte manuelle HA-Entitäten aufgeräumt
- [ ] Bestehende Automationen geprüft (Payload-Format, Enum-Namen)
- [ ] Alter Dienst gestoppt, Rollback getestet
- [ ] README aktualisiert (neuer Betrieb)
- [ ] `docs/`-Index aktualisiert

---

## Gesamtabnahme

| Kriterium | Erfüllt |
|---|---|
| Alle sechs Anforderungen umgesetzt | ☐ |
| Contract-Test grün | ☐ |
| Keine offenen P0-Befunde aus [Ist-Befunde](../01-ist-zustand/befunde.md) | ☐ |
| Betrieb auf der DS923+ > 7 Tage stabil (ohne Neustart) | ☐ |
| Backup & Restore dokumentiert **und** getestet | ☐ |
| Rollback-Pfad dokumentiert und geübt | ☐ |
| Dokumentation aktuell | ☐ |
