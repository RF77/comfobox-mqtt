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
- [ ] **SPA wird aus dem .NET-Host ausgeliefert** (`wwwroot`, `MapFallbackToFile`) – kein nginx
- [ ] `/api/service/restart` startet den Container neu (auf dem NAS getestet)
- [ ] API-Tests grün

### 4a. Setup-API (Getting Started)

- [ ] `GET /api/setup/state` meldet `setupCompleted`
- [ ] `GET /api/setup/ports` liefert die erkannten Ports mit stabilem Pfad
- [ ] `GET /api/setup/ports/suggest` schlägt einen freien Port vor
- [ ] `POST /api/setup/bacnet/test` liefert bei Erfolg `ok=true`, bei Fehlern einen `hint`
- [ ] `POST /api/setup/mqtt/test` prüft Erreichbarkeit **und** Authentifizierung
- [ ] `PUT /api/setup/config` hasht das Passwort **serverseitig** (PBKDF2, 600 000 Iterationen)
- [ ] In `config.json` steht **kein** Klartext-Passwort; Datei-Modus 0600
- [ ] `POST /api/setup/apply` setzt `setupCompleted=true` und stößt den Neustart aus
- [ ] Nach der Ersteinrichtung sind alle `/api/setup/*`-Endpunkte authentifizierungspflichtig

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

- [ ] `docker build` für **ein** Image (Gateway + UI) erfolgreich
- [ ] Image-Größe im erwarteten Rahmen (≈ 250 MB)
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
- [ ] CI baut und publiziert das Image

---

## Phase 8 – Synology-Paket & Getting Started

### 8a. SPK

- [ ] `INFO` enthält `checksum`, `adminport`, `adminurl`, `adminprotocol`
- [ ] `install_dep_packages="ContainerManager>=1432"` → Installation startet Container Manager
- [ ] `bash -n` über **alle** Skripte in `deploy/synology-spk/` ist grün
- [ ] `preinst` hat **keine** Seiteneffekte
- [ ] `postinst` legt `var/` an, installiert udev-Regel + systemd-Unit, schreibt Default-`config.json`
- [ ] `status` liefert `0` (läuft) bzw. `150` (gestoppt) – das Paketzcenter zeigt den richtigen Status
- [ ] `postuninst` entfernt Container + Unit + udev-Regel, **lässt aber `var/` stehen**
- [ ] Kein Docker-Socket im Container (`/var/run/docker.sock` nicht gemountet)
- [ ] `config.json` hat Modus 0600
- [ ] Installation auf der **realen DS923+** ohne Parameter → Paketcenter zeigt „Installiert"
- [ ] `synopkg start/stop/status ComfoBoxGateway` funktioniert

### 8b. Supervisor & Serial-Port

- [ ] `serial-scan.sh` findet den Adapter und schreibt `ports.json`
- [ ] `supervisor.sh` rendert `compose.yaml` aus dem Template
- [ ] `devices:` enthält den **gewählten** Port (nicht alle)
- [ ] Änderung an `config.json` → Container wird ersetzt (≈ 10 s)
- [ ] Adapter wird neu angesteckt → Port wird wiedererkannt (`by-id`-Symlink stabil)
- [ ] `passAllSerialDevices` ist standardmäßig **an** (Verbindungstest vor Portwahl möglich)

### 8c. Getting-Started-Assistent

- [ ] Assistent startet automatisch beim ersten Aufruf (`setupCompleted=false`)
- [ ] Schritt 1 zeigt die Portliste mit Beschreibung und stabilem Pfad
- [ ] Schritt 1 aktualisiert sich bei Hotplug (Adapter einstecken)
- [ ] Schritt 2: BACnet-Test meldet verständliche Fehler (38400-Baud-Hinweis)
- [ ] Schritt 3: MQTT-Test meldet Authentifizierungsfehler korrekt
- [ ] Schritt 4: Passwortregeln (≥ 12 Zeichen, 3 von 4 Klassen) werden geprüft
- [ ] Schritt 5: „Übernehmen" startet den Neustart, der Erfolg wird beobachtet und gemeldet
- [ ] Assistent ist nach der Ersteinrichtung passwortgeschützt
- [ ] Assistent ist **wiederholbar** (Port/Broker später wechselbar)
- [ ] Assistent läuft auf Deutsch

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
| Installation per Klick (SPK) + Assistent auf der DS923+ verifiziert | ☐ |
| Contract-Test grün | ☐ |
| Keine offenen P0-Befunde aus [Ist-Befunde](../01-ist-zustand/befunde.md) | ☐ |
| Betrieb auf der DS923+ > 7 Tage stabil (ohne Neustart) | ☐ |
| Backup & Restore dokumentiert **und** getestet | ☐ |
| Rollback-Pfad dokumentiert und geübt | ☐ |
| Dokumentation aktuell | ☐ |
