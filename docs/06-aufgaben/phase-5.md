# Phase 5 – Angular/Optimus UI (8 PT)

**Ziel:** Eine Oberfläche, mit der man die Anlage bedienen kann – **und** ein
Getting-Started-Assistent, der die Einrichtung übernimmt.
**Braucht:** Phase 4
**Spec:** [ui-angular-optimus](../02-modernisierung/ui-angular-optimus.md) ·
[benutzerdokumentation](../02-modernisierung/benutzerdokumentation.md)

> **Sprache:** UI-Texte **deutsch**, Code-Bezeichner **englisch**.
> **Stil:** Optimus UI (`@openng/optimus-ui`), Preset **Aura**, Signals statt RxJS.

---

## 5.1 – Angular-Projekt & Optimus UI · 0.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/` |

````bash
cd src
npx @angular/cli@22 new ComfoBox.Web --style=scss --routing --skip-git
cd ComfoBox.Web
ng add @openng/optimus-ui
````

| Punkt | Wert |
|---|---|
| Preset | **Aura** |
| Config | `provideOptimus({ theme: { preset: Aura } })` |
| Change Detection | `provideZonelessChangeDetection()` |
| State | Signal-Stores, **kein** NgRx |
| API-Basis | `/api` (relativ → derselbe Host) |

**Akzeptanzkriterien:**
- [ ] `provideOptimus({ theme: { preset: Aura } })` gesetzt
- [ ] Zoneless aktiv, **kein** Zone.js-Overhead
- [ ] `npm run lint` grün
- [ ] Produktions-Build erfolgreich

---

## 5.2 – API-Client, Stores, SSE · 1.0 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/src/app/core/` |

| Komponente | Aufgabe |
|---|---|
| `api-client.ts` | `fetch`-Wrapper, Fehlerbehandlung, Typisierung |
| `items.service.ts` | Item-CRUD |
| `health.service.ts` | Health/Status |
| `config.service.ts` | Konfiguration |
| `setup.service.ts` | Setup-Endpunkte |
| `events.service.ts` | SSE mit Reconnect + Backoff |
| `items.store.ts` | SignalStore mit dem Item-Zustand |
| `health.store.ts` | Health-Zustand |
| `setup.store.ts` | Assistent-Zustand |

**Akzeptanzkriterien:**
- [ ] `signal`/`computed` statt RxJS-Subscriptions im Template
- [ ] SSE reconnectet mit Backoff
- [ ] API-Fehler werden **nicht** verschluckt (Toast + Fehlerbanner)
- [ ] Ein einziger `api-client` für alle Endpunkte

---

## 5.3 – Dashboard · 1.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/src/app/features/dashboard/` |

**Inhalt:** Health-Banner, Verbindungskacheln (BACnet, Broker), Version, Uptime,
Schreibzähler, häufig genutzte Items als Kacheln.

**Akzeptanzkriterien:**
- [ ] Health-Banner: grün / gelb / rot
- [ ] Fehlerbanner bei `degraded` und `offline` **sichtbar und aussagekräftig**
- [ ] Bei `offline` kommt der Fehler aus dem **letzten** SSE-Event bzw. `localStorage`
- [ ] Schnellaktionen: Neustart, BACnet-Reconnect, MQTT-Reconnect, Konfiguration öffnen
- [ ] Neustart mit Bestätigungsdialog

---

## 5.4 – Item-Browser & Schreiben · 2.0 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/src/app/features/items/` |

| Anforderung | Umsetzung |
|---|---|
| alle 127 Items | gruppiert, Volltextsuche |
| Filter | „nur schreibbar“, „nur Expertenmodus“, „veraltet“ |
| Zahl schreiben | `p-inputnumber` mit Min/Max aus dem Katalog |
| Auswahl schreiben | `p-select` mit den **exakten** Enum-Namen |
| Fehler | Toast **und** Serverwert wiederherstellen |

> **Wichtig:** Bei Fehlern wird der **Serverwert** wiederhergestellt – die UI zeigt
> nie einen optimistischen Wert, den das Gerät nicht angenommen hat.

**Akzeptanzkriterien:**
- [ ] Alle 127 Items sichtbar, gruppiert, durchsuchbar
- [ ] Min/Max werden **aus dem Katalog** übernommen (nicht hartkodiert)
- [ ] Enum-Namen **unverändert**
- [ ] Schreibfehler → Toast + Rücksetzen auf den Serverwert
- [ ] „Veraltet“-Werte sind als solche erkennbar

---

## 5.5 – Konfigurationsseite · 1.0 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/src/app/features/config/` |

| Bereich | Felder |
|---|---|
| Allgemein | `BaseTopic`, `PollIntervalSeconds`, `UseMqttRetain` |
| Schreibschutz | `MaxNumberOfWritesPer24h`, `ExpertMode` |
| MQTT | Broker-Liste (nur Konfigurationsdatei → Neustart nötig) |
| BACnet | Port, Baudrate, Geräte-ID (nur Konfigurationsdatei → Neustart nötig) |
| Diagnose | Neustart, Reconnects, Log-Level |
| **API-Token** | Token erzeugen (PBKDF2-Hash, nur Admin) |

**Akzeptanzkriterien:**
- [ ] `ExpertMode` umschaltbar, **mit** Bestätigungsdialog
- [ ] Felder, die einen Neustart erfordern, sind als solche markiert
- [ ] API-Token kann erzeugt und widerrufen werden
- [ ] Passwörter werden **nie** im Klartext angezeigt (nur einmal beim Erzeugen)

---

## 5.6 – Diagnoseseite · 0.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/src/app/features/diagnostics/` |

| Ansicht | Inhalt |
|---|---|
| Health-Checks | Tabelle aller Checks mit Status, Letzter Erfolg, Meldung |
| Fehlerhistorie | letzte 50 Fehler, filterbar |
| Schreibvorgänge | Zähler, Limit, Zeitfenster |
| Verbindungen | Broker-Status, Reconnect-Zähler |
| Rohdaten | Live-Ansicht der Topics |

**Akzeptanzkriterien:**
- [ ] Alle fünf Ansichten vorhanden
- [ ] Die Fehlerhistorie ist nach Schweregrad filterbar

---

## 5.7 – Responsive, A11y, i18n · 0.5 PT

| Aspekt | Maßnahme |
|---|---|
| 360 px – 1920 px | responsive Layout |
| Tastatur | Optimus UI ist a11y-konform |
| Screenreader | `aria-label` auf allen Icon-Buttons |
| Sprache | **Deutsch** |

**Akzeptanzkriterien:**
- [ ] Responsive von 360 px bis 1920 px
- [ ] Lighthouse Accessibility ≥ 90
- [ ] Alle Texte deutsch, alle Bezeichner englisch

---

## 5.8 – SPA-Auslieferung & Docker-Stage · 0.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Gateway/wwwroot/` + Dockerfile-Stage |

````csharp
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");   // NACH den API-Routen!
````

**Akzeptanzkriterien:**
- [ ] Die SPA wird aus dem .NET-Host ausgeliefert – **kein** nginx
- [ ] `MapFallbackToFile` steht **nach** den API-Routen
- [ ] `AddResponseCompression()` mit Brotli/Gzip
- [ ] Die Docker-Stage baut die SPA und kopiert sie nach `wwwroot/`

---

## 5.9 – Getting-Started-Assistent · 2.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/src/app/features/setup/` |
| **Spec** | [ui-angular-optimus §3](../02-modernisierung/ui-angular-optimus.md#3-getting-started-setup-assistent) |

**Die sechs Schritte:** `welcome` → `serial` → `bacnet` → `mqtt` → `security` → `done`

**Akzeptanzkriterien:**
- [ ] Der Assistent startet automatisch, wenn `setupCompleted=false`
- [ ] Zustand bleibt beim Zurück-Navigieren erhalten
- [ ] Portliste zeigt Beschreibung **und** stabilen Pfad, aktualisiert sich bei Hotplug
- [ ] BACnet- und MQTT-Test mit **kontextbezogenen** Fehlermeldungen
- [ ] Passwortregeln (≥ 12 Zeichen, 3 von 4 Klassen), keine Speicherung des Klartexts im State
- [ ] Nach `POST /api/setup/apply` wird der Neustart **beobachtet** und der Erfolg gemeldet
- [ ] Nach der Ersteinrichtung passwortgeschützt
- [ ] **Wiederholbar** (Port/Broker später wechselbar)

---

## 5.10 – Login & Cookie-Auth · 1.0 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/src/app/features/login/`, `core/guards/auth.guard.ts` |

| Aspekt | Wert |
|---|---|
| Verfahren | Cookie-Sitzung (kein JWT) |
| Speicherung | Passwort nur als PBKDF2-Hash |
| Sitzung | serverseitig, widerrufbar |
| Guard | `authGuard` leitet auf `/login` um |

**Akzeptanzkriterien:**
- [ ] Login mit Benutzername/Passwort
- [ ] `setupGuard` leitet bei `setupCompleted=false` auf `/setup` um
- [ ] Abmelden beendet die Sitzung serverseitig
- [ ] Kein Passwort im LocalStorage/SessionStorage

---

## 5.11 – Hilfe-Bereich · 1.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Web/src/app/features/help/` |
| **Spec** | [benutzerdokumentation §3](../02-modernisierung/benutzerdokumentation.md#3-kanal-a--dokumentation-in-der-web-ui) |

| Aspekt | Wert |
|---|---|
| Route | `/help` und `/help/:slug` |
| Inhalt | statisches HTML aus `public/help/` (Build aus `docs/benutzer/`) |
| Suche | Volltext über `help/index.json` |
| Druck | eigenes Stylesheet |
| Verlinkung | der Assistent verlinkt an 5 Stellen auf passende Hilfeseiten |

**Akzeptanzkriterien:**
- [ ] `npm run docs:build` erzeugt `public/help/**` (HTML + `index.json` + `style.css`)
- [ ] `/help/:slug` rendert die Seite mit Vor/Zurück-Navigation
- [ ] Volltextsuche über den Index
- [ ] Die Seiten funktionieren **ohne** Internetverbindung
- [ ] Der Build bricht bei fehlendem Front-Matter **und** bei toten Links ab

---

## Abnahme der Phase 5

- [ ] Dashboard, Item-Browser, Konfiguration, Diagnose, Login, Assistent, Hilfe vorhanden
- [ ] Fehlerbanner sichtbar und verständlich
- [ ] Schreibfehler → Serverwert wiederhergestellt
- [ ] Live-Werte via SSE (kein Polling im Browser)
- [ ] `npm run lint` grün, Produktions-Build erfolgreich
- [ ] Meilenstein **M3** erreicht: „UI bedient die Anlage“

→ Weiter mit [Phase 6](phase-6.md)
