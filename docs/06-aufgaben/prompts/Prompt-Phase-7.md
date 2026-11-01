Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze **Phase 7** des Aufgaben-Dokuments um: **Parallelbetrieb &
Umschaltung** (zwei Gateways nebeneinander, Payload-Vergleich, dann die
Umschaltung auf `ComfoBox`).

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md                     ← die Projektregeln
2. .github/instructions/csharp.instructions.md         ← die C#-Regeln
3. .github/instructions/mqtt.instructions.md           ← die MQTT-Regeln (Topic-Vertrag!)
4. .github/instructions/bacnet.instructions.md        ← der BACnet-Stack
5. .github/instructions/docker.instructions.md        ← die Docker-Regeln
6. .github/instructions/docs.instructions.md          ← die Doku-Regeln
7. docs/06-aufgaben/README.md                          ← wie eine Session abläuft
8. docs/06-aufgaben/status.md                          ← Stand nach Phase 8a–8d
9. docs/06-aufgaben/phase-7.md                         ← die Aufgaben 7.1–7.5
10. docs/06-aufgaben/protokoll.md                      ← die Fallstricke aus Phase 8a–8d
11. docs/04-migration/mqtt-kompatibilitaet.md          ← der bindende Topic-Vertrag
12. docs/02-modernisierung/uebersicht-und-zielbild.md  ← das Zielbild (§7)
13. docs/04-migration/checkliste.md                    ← die Abnahme pro Phase
14. tools/generate-topics/                             ← der Generator für `docs/topics.json`

Ziel der Phase 7: Der alte und der neue Gateway laufen **parallel** (mit
unterschiedlichem `BaseTopic`), die Payloads werden **maschinell verglichen**,
und danach wird **auf `ComfoBox` umgeschaltet** – ohne dass die Heizung
dazwischen ausfällt.

Stand: **Phase 8a–8d ist abgeschlossen** (alle 14 PT ✅). **Aufgabe 7.0** (der
Vergleich ohne Gerät) ist am 2026-11-01 fertig: der Gateway hat einen
Simulationsbetrieb, das Vergleichswerkzeug `tools/compare-payloads/` steht, und
die Probe mit zwei Gateways am selben Broker ist grün. Aus Phase 6 ist
**6.4** (Test auf der echten DS923+) blockiert – die Hardware steht hier nicht
zur Verfügung. Offen sind **7.1–7.5** (der echte Parallelbetrieb).

Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- ✅ `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- ✅ `dotnet test src/ComfoBox.sln -c Release` → **327 Tests grün**
  (46 BACnet + 30 Domain + 10 Contract + 241 Gateway). Das ist die Messlatte:
  sie müssen **ohne** Änderung grün bleiben.
- ✅ `pytest custom_components/comfobox/tests -v` → **35 Tests grün**
  (Config Flow 3, API-Client 10, Coordinator 8, Entity-Mapping 12, Schreibpfade 2).
  `ruff check custom_components/comfobox` → grün.
- ✅ `python -m pytest tools/compare-payloads/tests -q` → **12 Tests grün**
  (Vergleichsregeln: Zahl mit Toleranz, Enum exakt, Verfügbarkeit, Katalog,
  Basis-Topic-Normalisierung).
- ✅ Die Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  ist **eingefroren** (SHA256
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B`,
  5.657 Bytes). Nicht verändern, nicht neu erzeugen.
- ✅ `docs/topics.json` ist **unverändert** (SHA256
  `67D7485BF961B2B6D11BA5E788932056F073B564F52922D2DE8B34DFC10C1463`) und wird
  vom Contract-Test Feld für Feld gegen den Domain-Katalog geprüft.
- ✅ Das Container-Image ist gebaut und verifiziert (261 MB, ein Image für
  Gateway + UI, tini als PID 1, USER comfobox, Healthcheck, VOLUME
  /var/lib/comfobox). `deploy/compose/compose.yaml` ist die **manuelle**
  Compose-Datei; `deploy/synology-spk/src/target/compose.template.yaml` ist die
  **gerenderte** Vorlage für das SPK.
- ✅ Der **Schreibzähler** (24 h) ist persistent und heilig. Kein Schreibpfad
  darf ihn umgehen. Der Parallelbetrieb darf **nicht** schreiben.
- ✅ `tools/generate-topics/generate-topics.mjs` reproduziert `docs/topics.json`
  **byteidentisch** (127 Items) – das ist die Grundlage für den Payload-Vergleich
  (Aufgabe 7.2).
- ✅ Das HA-Ökosystem ist fertig: HACS-Integration (35 Tests), Add-on mit
  Options-Mapping (`AddOnOptionsProvider`), Release-Workflow
  `.github/workflows/ha-release.yml`.
- ✅ **Der Vergleich ohne Gerät steht** (ADR-013, Aufgabe 7.0):
  - `Bacnet:Simulation=true` → der Gateway antwortet selbst (deterministische
    Werte aus dem Item-Katalog, optional über `Bacnet:SimulationValuesPath`).
    `/api/status` meldet `"simulated": true`.
  - `tools/compare-payloads/compare_payloads.py` → live (`--old mqtt://…`)
    oder gegen eine Aufzeichnung (`--old-file`), mit `record` zum Aufzeichnen.
    Exit-Code ≠ 0 bei Abweichungen.
  - Die Probe (zwei Gateways, mosquitto im Docker, Poll-Intervall 5 s) ist
    grün: 125 Topics verglichen, keine Abweichung. Die Gegenprobe
    (manipulierte Aufzeichnung) meldet 2 Abweichungen.
  - **Offen in 7.2:** der Lauf gegen das **echte** alte Gateway. Dafür braucht
    es eine Aufzeichnung (`record`) am System mit dem CH340-Adapter; der
    Vergleich läuft danach offline.
- ⚠️ **Die HA-Integration spricht mit dem Gateway, nicht mit dem Bus.**
  Es gibt **nie** zwei gleichzeitige Master auf dem MS/TP-Bus.
- ⚠️ **Der Topic-Vertrag ist unverhandelbar:** Topic-Adressen und Payload-Formate
  bleiben gültig. Enum-Namen nicht umbenennen.
- ⚠️ **Der Container darf KEINEN Docker-Socket bekommen.**
- ⚠️ **`postuninst` darf das `var`-Verzeichnis nicht lösern** – sonst ist der
  Schreibzähler weg.
- ⚠️ **Lifecycle-Exit-Codes:** 0 ok · 1 Fehler · 2 Params · 3 nicht gestartet ·
  4 Abhängigkeit · **150 „soll gestoppt"**.
- ⚠️ **Die `config.json` hat Vorrang vor Env-Variablen**
  (`GatewayConfiguration.AddGatewaySources`). Passwörter dürfen **nie** in
  `docker inspect` landen. Die `options.json` des HA-Add-ons hat wiederum
  Vorrang vor beiden.
- ⚠️ **`src_old/` ist Archiv**: lesen ist erlaubt und erwünscht, ändern nie.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root.
- `create_file` **überschreibt keine vorhandene Datei** – bei "File already
  exists" die Datei erst löschen (`Remove-Item`) oder mit
  `replace_string_in_file` bearbeiten.
- `TreatWarningsAsErrors=true` – jeder Analyzer-Fehler stoppt den Build.

Fallstricke aus Phase 8a–8d (bitte beachten – alle wurden teuer):
- ⚠️ **Zwei Gateways am selben Broker brauchen verschiedene
  `Gateway:DeviceId` *und* verschiedene `Mqtt:DiscoveryIdentifier`.** Sonst
  verdrängen sie sich am Broker (*Session taken over* → Endlos
  Verbinden/Trennen, und der LWT meldet das andere Gateway als `offline`) und
  überschreiben sich ihre Home-Assistant-Entitäten (gleiche `unique_id`s).
- ⚠️ **Der Legacy nutzt die BACnet-Objekt-Id 864 zweimal**
  (`KühlraumsollAbstStandby` 0–10 K und `RestartDelay` 20–60 min). Am echten
  Gerät tragen beide Topics denselben Rohwert – der Vergleich in 7.2 muss
  damit rechnen.
- ⚠️ **Sekundäre Items werden nur jeden `SecondaryPollFactor`-Zyklus gelesen**
  (Standard 5). Zwei Gateways lesen sie nicht im Gleichtakt → eine
  Zeitstempel-Differenz bis zu einem ganzen Zyklus ist **normal**. Die
  Verfügbarkeitsregel rechnet mit `poll-interval × secondary-factor`.
- ⚠️ **`docs/topics.json` hat ein UTF-8-BOM** → in Python `utf-8-sig` zum Lesen
  verwenden.
- ⚠️ **Ein MQTT-URI mit `#` nicht mit `urlparse` zerlegen** (das `#` beginnt
  ein Fragment). Der Topic-Filter beginnt **ohne** Schrägstrich
  (`ComfoBox/#`).
- ⚠️ **Die Item-Ids des Gateways enthalten einen PUNKT**
  (`zone.set_point_normal`), die HA-`entity_id` einen **Unterstrich**. Eine
  Umrechnung per String-Operation ist deshalb **immer falsch** – die Id kommt
  aus dem Entity-Registry (`unique_id`).
- ⚠️ **`aria-label` auf `<p-button>` erreicht den inneren `<button>` nicht.**
  Optimus erwartet die **Eigenschaft** `ariaLabel` (Alias `ariaLabel`, nicht
  `aria-label`). Bei dynamischen Werten: `[ariaLabel]="…"` (eckige Klammern).
- ⚠️ **Ein ungültiges `Authorization` ergibt jetzt IMMER `401`** – auch auf den
  offenen Endpunkten (ADR-012). Ohne Header bleibt der LAN-Modus unverändert.
- ⚠️ **`IConfigurationProvider` ist keine `IConfigurationSource`** – der
  Provider braucht eine Quelle, und `ConfigurationManager.Add(source)` ist
  nicht öffentlich (Cast auf `IConfigurationBuilder`).
- ⚠️ **Der HA-Supervisor zieht das Image mit dem Versions-Tag** – das
  Add-on-Manifest braucht `image: ghcr.io/rf77/comfobox-gateway:<version>`.
- ⚠️ **`dotnet ComfoBox.Gateway.dll` braucht das Arbeitsverzeichnis des
  Ausgabeordners** – sonst fehlen `appsettings.json` und `wwwroot`, und
  `ValidateOnStart` bricht ab. Mit `Start-Process -WorkingDirectory` zuverlässig.
- ⚠️ **`page.reload({ waitUntil: 'networkidle' })` läuft in einen Timeout** –
  der SSE-Stream hält die Verbindung offen. `waitUntil: 'domcontentloaded'`.
- ⚠️ **Der VS-Code-Browser-Tab ist nicht sichtbar** → Klicks erzeugen **kein**
  DOM-Ereignis. Für UI-Tests dort `element.click()` per `page.evaluate` nutzen.
- ⚠️ **`ruff format` ist im Repo nicht einheitlich** (7 Altdateien weichen ab).
  `ruff check` ist der Lint – der ist grün.
- ⚠️ **Tests, die die Konfiguration aufbauen, sind von der Umgebung
  abhängig:** `AddGatewaySources` liest `COMFOBOX_*` aus der **Prozess**
  -Umgebung. Ein manuell gestarteter Gateway setzt die Variablen in der
  Shell → sie lecken in den nächsten `dotnet test`. Tests müssen die
  Variablen **explizit setzen** und danach zurücksetzen.

Regeln für diese Session:
- Nur die Phase 7 umsetzen, nichts darüber hinaus.
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Die 327 .NET-Tests, die 12 Werkzeug-Tests und die 35 HA-Tests müssen grün
  bleiben.
- **Der Topic-Vertrag ist unverhandelbar:** Topic-Adressen und Payload-Formate
  bleiben gültig. Enum-Namen nicht umbenennen.
- **Der Schreibzähler (24 h) ist heilig.** Kein Schreibpfad darf ihn umgehen.
- **Im Parallelbetrieb wird nicht geschrieben** – nur gelesen und verglichen.
- **Zwei Master auf einem MS/TP-Bus sind nicht zulässig.** Für den
  Parallelbetrieb braucht es **zwei** Adapter (oder ein Verschieben des alten
  Dienstes auf den zweiten Port).
- UI-Texte deutsch, Code-Bezeichner englisch.
- Nach jeder Aufgabe: Status in docs/06-aufgaben/status.md setzen
  (⬜ offen / 🟡 in Arbeit / ✅ fertig und verifiziert / ⛔ blockiert).
- Am Ende: Eintrag in docs/06-aufgaben/protokoll.md (Datum, Aufgabe, Ergebnis,
  Verifikation, offene Punkte, Überraschungen).
- Wenn eine neue Entscheidung nötig ist: ADR anlegen und docs/README.md sowie
  docs/05-entscheidungen/README.md aktualisieren.
- Keine Secrets, keine Passwörter in Dateien oder Logs.
- Sprache: Code-Bezeichner englisch, Kommentare und Doku deutsch.

Am Ende der Session:
1. Führe alle Verifikationsbefehle aus phase-7.md aus und zeige das Ergebnis.
   Der wichtigste ist: `dotnet test src/ComfoBox.sln -c Release` ist grün, die
   Golden-Fixture ist byte-identisch (Prüfsumme vergleichen!),
   `pytest custom_components/comfobox/tests -v` und
   `ruff check custom_components/comfobox` sind grün,
   `python -m pytest tools/compare-payloads/tests -q` ist grün und
   `node tools/build-docs.mjs` bricht nicht ab.
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 6.4 –
   Test auf der echten DS923+, oder die Nacharbeiten zu Phase 7), in derselben
   Form wie dieser Prompt – inklusive der Fallstricke, die in dieser Session
   aufgetreten sind.
