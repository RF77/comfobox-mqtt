# UI: Angular + Optimus UI

> Anforderung 2: *„Angular/Optimus UI für Konfiguration und Statusübersicht/Bedienung"*
> Anforderung 3 (Erweiterung): *„eine Art ‚Getting Started' losgeht – verfügbare Serial Ports
> sehen und auswählen, User und Passwort für die Config-Seite …"*

## 1. Technologie-Entscheidung

| Aspekt | Wert | Begründung |
|---|---|---|
| Framework | **Angular 22** | Aktive Version (Support bis 2028-06); Optimus UI verlangt Angular 21+ |
| UI-Library | **`@openng/optimus-ui` 2.x** | MIT, 80+ accessible Components, Angular-21+-kompatibel, PrimeNG-v21-API-kompatibel, Theming über Presets (Aura/Lara/Material/Nora) |
| State-Management | **Angular Signals** + Signal-Store | Kein NgRx nötig; weniger Boilerplate, ausreichend für diese Größe |
| Daten-Transport | **REST** (Lesen/Schreiben) + **SSE** (Live) | siehe [Datenmodell & API §4](datenmodell-und-api.md#4-sse-server-sent-events-für-live-werte) |
| Build | `@angular/build` (ESBuild) | Standard-Builder |
| Hosting | **statische Dateien aus dem .NET-Host** | ein Container, ein Port, kein nginx nötig (→ [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md)) |

### Warum Optimus UI

| Kriterium | Optimus UI |
|---|---|
| Angular-Version | 21+ → passt zu Angular 22 |
| Lizenz | MIT (kommerziell nutzbar) |
| Accessibility | „80+ accessible components" |
| Theming | design-agnostisch, Presets Aura/Lara/Material/Nora |
| Migration | API-kompatibel mit PrimeNG v21 → Migrationsschematic vorhanden |
| Wartung | aktiv (v2.0.2, 2026) |

> **Install:**
> ```bash
> ng add @openng/optimus-ui
> # wählt automatisch den Preset; Preset kann wechseln:
> ng add @openng/optimus-ui --theme Lara
> ```
> Ergebnis in `app.config.ts`:
> ```ts
> import { provideOptimus, Aura } from '@openng/optimus-ui';
>
> export const appConfig: ApplicationConfig = {
>   providers: [provideOptimus({ theme: { preset: Aura } })],
> };
> ```

## 2. Projektstruktur

````
src/ComfoBox.Web/
├── package.json
├── angular.json
├── Dockerfile
└── src/
    ├── index.html
    ├── main.ts
    ├── styles.scss
    ├── environments/
    │   ├── environment.ts          # apiBaseUrl: '/api' (relativ → derselbe Host)
    │   └── environment.prod.ts
    └── app/
        ├── app.config.ts
        ├── app.routes.ts
        ├── app.ts / app.html
        ├── core/
        │   ├── api/
        │   │   ├── api-client.ts          # fetch-Wrapper + Fehlerbehandlung
        │   │   ├── items.service.ts
        │   │   ├── health.service.ts
        │   │   ├── config.service.ts
        │   │   ├── setup.service.ts       # ← Getting Started
        │   │   └── events.service.ts      # SSE
        │   ├── models/                    # TS-Interfaces (ItemDto, HealthReport, …)
        │   ├── state/
        │   │   ├── items.store.ts         # SignalStore
        │   │   ├── health.store.ts
        │   │   └── setup.store.ts         # ← Assistent-Zustand
        │   └── guards/
        │       ├── auth.guard.ts          # ← Login
        │       └── setup.guard.ts         # ← Weiterleitung zum Assistenten
        └── features/
            ├── setup/                     # ← Getting-Started-Assistent
            ├── dashboard/                 # Statusübersicht
            ├── items/                     # Item-Browser + Bedienung
            ├── item-detail/               # Einzelnes Item, Verlauf, Schreiben
            ├── config/                    # Konfiguration
            ├── diagnostics/               # Fehler, Logs, Status
            ├── login/                     # ← Anmeldung
            ├── help/                      # ← Benutzerdoku (HTML aus dem Image)
            └── about/
````

## 3. Getting Started (Setup-Assistent)

> Ziel: **Ein Klick im DSM-Paketcenter → der Assistent führt durch die Einrichtung.**
> Siehe [Synology-Paketcenter](../03-deployment/synology-paketcenter.md) für den
> Installationsfluss auf der DS923+.

### 3.1 Wann erscheint der Assistent?

| Zustand | Verhalten |
|---|---|
| `config.json` fehlt oder `setupCompleted = false` | **Alle** Routen leiten auf `/setup` um |
| `setupCompleted = true`, aber Benutzer nicht angemeldet | Weiterleitung auf `/login` |
| `setupCompleted = true` und angemeldet | normale Anwendung |

```ts
// app.routes.ts
export const APP_ROUTES: Routes = [
  { path: 'setup', loadComponent: () => import('./features/setup/setup.routes').then(m => m.SETUP_ROUTES) },
  { path: 'login', loadComponent: () => import('./features/login/login').then(m => m.LoginComponent) },
  {
    path: '',
    canActivate: [setupGuard, authGuard],
    loadComponent: () => import('./features/dashboard/dashboard').then(m => m.DashboardComponent),
    children: [ /* … */ ],
  },
  { path: '**', redirectTo: '' },
];
```

> **Sicherheitsregel:** `/setup` ist nach der Ersteinrichtung **passwortgeschützt**.
> Der Assistent darf Konfiguration (inkl. Expert-Modus) verändern → das ist eine
> Administrationsfunktion.

### 3.2 Die sechs Schritte

Der Assistent ist eine **Schritt-für-Schritt-Ansicht** mit Zustand im Signal-Store –
kein linearer Formular-Wizard, sondern ein Seitenschritt mit Vor/Zurück und persistentem Zustand.

| Schritt | Route | Inhalt | API |
|---|---|---|---|
| **0 – Willkommen** | `/setup/welcome` | Erklärung, Zielbild, Voraussetzungen (ComfoBox auf 38400!) | – |
| **1 – Schnittstelle** | `/setup/serial` | Liste der Ports, Auswahl, Checkbox „alle durchreichen" | `GET /api/setup/ports` |
| **2 – BACnet** | `/setup/bacnet` | Baudrate, Geräte-ID, **Verbindungstest** | `POST /api/setup/bacnet/test` |
| **3 – MQTT** | `/setup/mqtt` | Broker, Port, User, Passwort, TLS, Discovery, optional eigener Broker | `POST /api/setup/mqtt/test` |
| **4 – Web-Zugang** | `/setup/security` | Benutzername, Passwort, Wiederholung, Stärke-Anzeige, Port-Vorschlag | `GET /api/setup/ports/suggest` |
| **5 – Fertig** | `/setup/done` | Zusammenfassung, Übernehmen, Fortschritt, HA-Hinweis | `PUT /api/setup/config`, `POST /api/setup/apply` |

### 3.3 Schritt 1 – Serielle Schnittstelle

````html
<p-card header="Serielle Schnittstelle">

  <p-message severity="info" styleClass="w-full mb-4">
    <p class="font-semibold">Hinweis</p>
    <p>
      Die ComfoBox muss auf <b>38400 Baud</b> umgestellt und <b>neu gestartet</b> sein.
      Steht sie noch auf 76800, antwortet sie nicht.
    </p>
  </p-message>

  @if (setup.ports().length === 0) {
    <p-message severity="warn" styleClass="w-full">
      Kein serieller Port gefunden. Bitte den USB-RS485-Adapter einstecken.
      <p-button label="Erneut suchen" (click)="setup.reloadPorts()" />
    </p-message>
  } @else {
    <div class="flex flex-col gap-2">
      @for (port of setup.ports(); track port.device) {
        <label class="flex items-center gap-3 p-3 border rounded cursor-pointer"
               [class.border-primary]="selected() === port.device">
          <p-radiobutton name="serial"
                         [value]="port.device"
                         [(ngModel)]="selected" />
          <div>
            <div class="font-medium">{{ port.description }}</div>
            <code class="text-sm text-muted">{{ port.device }}</code>
            @if (port.stableId) {
              <div class="text-xs text-muted">stabil: {{ port.stableId }}</div>
            }
          </div>
        </label>
      }
    </div>

    <p-toggleswitch [(ngModel)]="passAll" inputId="passAll" />
    <label for="passAll">Alle seriellen Ports durchreichen (empfohlen – ermöglicht den Verbindungstest)</label>
  }
</p-card>
````

**Besonderheiten:**

| Aspekt | Umsetzung |
|---|---|
| Live-Aktualisierung | `ports.json` wird vom Supervisor alle 3 s aktualisiert → Polling alle 5 s im Assistenten |
| Kein Port gefunden | Hinweis + „Erneut suchen"-Button; **kein** Abbruch |
| Stabiler Pfad | wird gespeichert, angezeigt wird der Device-Node |
| `passAll` | Default **an** → der Connection-Test funktioniert, bevor der Port feststeht |

### 3.4 Schritt 2 – BACnet + Verbindungstest

````html
<p-card header="BACnet-Konfiguration">

  <label>Baudrate</label>
  <p-select [options]="baudrates" [(ngModel)]="bacnet.baudrate" />

  <label>Geräte-ID des Controllers</label>
  <p-inputnumber [(ngModel)]="bacnet.masterId" [min]="0" [max]="255" />

  <p-button label="Verbindung testen"
            icon="pi pi-plug"
            [loading]="testing()"
            (click)="testBacnet()" />

  @if (bacnetResult(); as result) {
    <p-message [severity]="result.ok ? 'success' : 'error'" styleClass="w-full mt-4">
      <p>{{ result.message }}</p>
      @if (result.hint) { <p class="text-sm">{{ hintText(result.hint) }}</p> }
    </p-message>
  }
</p-card>
````

**Fehlermeldungen sind kontextbezogen** – nicht „Fehler 500", sondern eine Handlung:

| `hint` | Anzeige |
|---|---|
| `serial_port_not_found` | „Der gewählte Port ist nicht mehr vorhanden. Bitte Adapter neu einstecken oder einen anderen Port wählen." |
| `no_iam` | „Die ComfoBox antwortet nicht. Prüfen Sie: (1) steht sie auf 38400 Baud und wurde neu gestartet? (2) sind A und B richtig angeschlossen? (3) ist der Adapter an einem anderen Port?" |
| `busy` | „Der Port wird bereits von einem anderen Programm verwendet." |
| `permission_denied` | „Keine Berechtigung auf den Port. Die Gruppe *dialout* fehlt." |

### 3.5 Schritt 3 – MQTT

| Feld | Typ | Standard | Validierung |
|---|---|---|---|
| Host | Text | `homeassistant` oder `192.168.x.x` | nicht leer |
| Port | Zahl | `1883` | 1–65535 |
| Benutzer | Text | – | – |
| Passwort | Passwort | – | – |
| TLS | Schalter | aus | – |
| Discovery | Schalter | **an** | – |
| Eigenen Broker mitbringen | Schalter | aus | ⇒ startet `mosquitto` als dritten Container |

**Test:** `POST /api/setup/mqtt/test` verbindet, publiziert einen Test-Topic und
prüft die Rückmeldung. Ergebnis: „Broker *homeassistant:1883* erreichbar, Authentifizierung
erfolgreich."

### 3.6 Schritt 4 – Web-Zugang

| Feld | Validierung |
|---|---|
| Benutzername | ≥ 3 Zeichen, `[a-zA-Z0-9._-]` |
| Passwort | ≥ 12 Zeichen, mindestens 3 von 4 Klassen (Groß, Klein, Ziffer, Symbol) |
| Passwort wiederholen | identisch |
| Web-Port | Vorschlag aus `GET /api/setup/ports/suggest` (erster freier Port ab 8081) |

**Passwort-Feld:** `type="password"` mit Klartext-Schalter (`pi-eye`), nie im State
speichern – `ngModel` wird nach dem Speichern geleert.

> **Wichtig:** Das Passwort wird **einmal im Klartext** an das Gateway gesendet. Das Gateway
> hasht es (PBKDF2, 600.000 Iterationen) und speichert **nur** den Hash. Das Klartext-Passwort
> verlässt den Browser nur auf diesem einen Weg.

### 3.7 Schritt 5 – Fertig

````html
<p-card header="Fertig">

  <p-listchecklist [value]="checklist()" />

  <p-button label="Übernehmen und starten"
            icon="pi pi-check"
            [disabled]="!allValid()"
            (click)="apply()" />

  @if (applying()) {
    <p-progressbar mode="indeterminate" />
    <p>Der Gateway wird neu gestartet. Das dauert etwa 10 Sekunden …</p>
  }

  @if (applied()) {
    <p-message severity="success" styleClass="w-full mt-4">
      <p class="font-semibold">ComfoBox Gateway läuft.</p>
      <p>
        Die Werte werden jetzt nach <code>{{ baseTopic }}</code> publiziert.
        Home Assistant hat die Entitäten bereits übernommen.
      </p>
      <p-button label="Zur Übersicht" routerLink="/" />
    </p-message>
  }
</p-card>
````

Der Fortschritt wird über `/api/health` beobachtet:

| Phase | Erwarteter Zustand |
|---|---|
| Container startet | `/api/health` nicht erreichbar → „Gateway startet …" |
| BACnet wartet | `state = degraded`, Check `bacnet-connection = degraded` |
| Alles bereit | `state = online` → Weiterleitung zur Übersicht |

> **Hinweis:** Nach dem Übernehmen wird der Container **ersetzt** (neue `devices:`-Zuordnung).
> Das ist die einzige Unterbrechung (~10 s) und wird hier angekündigt.

### 3.8 Zustand im Store

````ts
@Injectable({ providedIn: 'root' })
export class SetupStore {
  private readonly api = inject(SetupService);

  readonly step = signal<SetupStep>('welcome');
  readonly ports = signal<SerialPort[]>([]);
  readonly selectedPort = signal<string | null>(null);
  readonly passAllDevices = signal(true);
  readonly bacnet = signal<BacnetSetup>({ baudrate: 38400, masterId: 1 });
  readonly mqtt = signal<MqttSetup>({ host: '', port: 1883, username: '', password: '', useTls: false });
  readonly security = signal<SecuritySetup>({ username: '', password: '', passwordRepeat: '' });

  readonly bacnetResult = signal<TestResult | null>(null);
  readonly mqttResult = signal<TestResult | null>(null);
  readonly applying = signal(false);
  readonly applied = signal(false);

  readonly isComplete = computed(() =>
    !!this.selectedPort() &&
    this.bacnetResult()?.ok === true &&
    this.mqttResult()?.ok === true &&
    this.security().password.length >= 12,
  );

  async reloadPorts(): Promise<void> {
    this.ports.set(await this.api.getPorts());
  }
}
````

> **Regel:** Der Assistent ist **wiederholbar** – ein Administrator kann ihn jederzeit über
> „Erneut einrichten" in der Konfigurationsseite öffnen, um den Serial-Port oder den
> Broker zu wechseln.

## 4. Dashboard (Statusübersicht)

### 4.1 Header

| Element | Inhalt | Datenquelle |
|---|---|---|
| **Health-Banner** | `online` (grün) / `degraded` (gelb) / `offline` (rot) | `GET /api/health` + SSE |
| **Verbindungs-Chips** | BACnet: verbunden / getrennt; MQTT: verbunden / getrennt; Broker-Name | `GET /api/status` |
| **Version + Uptime** | `1.0.0`, `2 h 14 min` | `GET /api/status` |
| **Schreibzähler** | `17 / 200` (Fortschrittsbalken) | `GET /api/status` |

**Fehlerbanner (Anforderung 5):**

````html
@if (health.state() === 'degraded') {
  <p-message severity="warn" styleClass="w-full">
    <p class="font-semibold">Verbindung eingeschränkt</p>
    <p>{{ health.summary() }}</p>
  </p-message>
}
@if (health.state() === 'offline') {
  <p-message severity="error" styleClass="w-full">
    <p class="font-semibold">Gateway nicht erreichbar</p>
    <p>Seit {{ health.unreachableSince() }} keine Verbindung. Automatischer Neustart läuft.</p>
  </p-message>
}
````

> Bei `offline` kommt der Fehler aus dem **letzten** SSE-Event bzw. aus dem lokalen Cache –
> der Server ist ja nicht erreichbar. Die UI speichert den letzten bekannten Status im
> `localStorage`, damit der Nutzer nach einem Browser-Neuladen sieht, was los war.

### 4.2 Kacheln (Optimus UI `p-card`)

| Kachel | Inhalt | Aktion |
|---|---|---|
| **Betriebsart** | `Zone/OperationMode` | Select |
| **Raumsollwert** | `Zone/SetPointNormal` (°C) | Number |
| **Kühlung** | `Zone/HeatingCoolingMode` | Select |
| **Warmwasser** | `Warmwater/…` | Select |
| **Wärmepumpe** | `States/HeatPump/HeatPumpStatus` | – (read-only) |
| **Lüftung** | `Zone/…` | – |

> **Rule:** Auf dem Dashboard stehen nur **Frequently-Used**-Items. Der vollständige Katalog
> steht unter „Geräte".

### 4.3 Schnellaktionen

| Aktion | Verhalten |
|---|---|
| **Gateway neu starten** | `POST /api/service/restart` → Confirm-Dialog → Statusanzeige „wird neu gestartet …" |
| **BACnet neu verbinden** | `POST /api/service/reconnect/bacnet` |
| **MQTT neu verbinden** | `POST /api/service/reconnect/mqtt` |
| **Konfiguration öffnen** | Navigation zu `/config` |
| **Erneut einrichten** | Navigation zu `/setup` (Assistent erneut durchlaufen) |

> **Bestätigung beim Neustart:** `p-confirmdialog` mit dem Hinweis, dass alle Werte für
> ~10 s nicht verfügbar sind.

## 5. Item-Browser (vollständiger Katalog)

| Bereich | Inhalt |
|---|---|
| Gruppiert nach | `Zone`, `States`, `Warmwater`, `Config`, `Tests` |
| Suche | Volltext über Name, Beschreibung, Topic |
| Filter | „nur schreibbar", „nur Experten-Modus", „veraltet (stale)" |
| Ansicht | Tabelle (`p-table`) oder Karten (`p-card`) |
| Spalten | Name, Wert, Einheit, Bereich (Min/Max), Topic, Schreibbarkeit, Alter |

### Schreiben

| Werttyp | Optimus-UI-Komponente | Verhalten |
|---|---|---|
| Analog | `p-inputnumber` | Min/Max aus dem Item; `p-button` „Speichern" |
| Enum | `p-select` | Optionen aus `EnumValues`; Wert ist der **Name** |
| Read-only | – | Wert-Anzeige + Copy-Topic-Button |

**Fehlerbehandlung beim Schreiben:**

````ts
async save(item: ItemDto, value: unknown) {
  this.saving.set(item.id, true);
  try {
    await this.items.write(item.id, value);
    this.toast.add({ severity: 'success', summary: 'Gespeichert', detail: item.displayName });
  } catch (e) {
    const err = toApiError(e);
    this.toast.add({ severity: 'error', summary: err.error, detail: err.message });
    // Wert im Formular zurücksetzen auf den Serverwert
    this.items.reload(item.id);
  } finally {
    this.saving.delete(item.id);
  }
}
````

> **Wichtig:** Bei Fehlern wird der **Serverwert** wiederhergestellt – die UI zeigt nie einen
> optimistischen Wert, den das Gerät nicht angenommen hat. Das ist der Unterschied zu einem
> Optimistic-Update und für eine Heizungssteuerung die richtige Wahl.

## 6. Konfigurationsseite

| Bereich | Felder | Persistenz |
|---|---|---|
| **Allgemein** | `BaseTopic`, `PollIntervalSeconds`, `UseMqttRetain` | `PUT /api/config` |
| **Schreibschutz** | `MaxNumberOfWritesPer24h`, `ExpertMode` | `PUT /api/config` |
| **MQTT** | Broker-Liste (Host, Port, User, TLS) | nur Konfigurationsdatei → **Neustart nötig** |
| **BACnet** | Port, Baudrate, Device-ID | nur Konfigurationsdatei → **Neustart nötig** |
| **Diagnose** | Neustart, Reconnects, Log-Level | – |

- `ExpertMode` → `p-toggleswitch` **mit** `p-confirmdialog` (es ermöglicht Schreibzugriffe auf
  sensible Parameter).
- Felder, die einen Neustart erfordern, werden mit einem Hinweis-Chip markiert
  (`p-tag severity="warn"` → „Neustart erforderlich").

## 7. Diagnoseseite

| Ansicht | Inhalt |
|---|---|
| **Health-Checks** | Tabelle aller Checks mit Status, Letzter Erfolg, Nachricht |
| **Fehlerhistorie** | Letzte 50 Fehler (Zeit, Severity, Quelle, Code, Meldung), Filter |
| **Schreibvorgänge** | Zähler, Limit, Zeitfenster |
| **Verbindungen** | Broker-Status, Reconnect-Zähler, Publish-/Receive-Zähler |
| **Rohdaten** | Live-Ansicht der Topics (`ComfoBox/#`) über eine SSE-Debug-Schnittstelle |

## 8. Datenfluss & Signals

````ts
// core/state/items.store.ts
@Injectable({ providedIn: 'root' })
export class ItemsStore {
  private readonly api = inject(ItemsService);
  private readonly events = inject(EventsService);

  readonly items = signal<ItemDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    // SSE → lokaler Store
    this.events.value$.subscribe(evt => {
      if (evt.type === 'value') {
        this.items.update(list =>
          list.map(i => i.id === evt.item.id ? { ...i, value: evt.value, lastUpdated: evt.timestamp, stale: false } : i)
        );
      }
    });
  }

  async load() { … }
}
````

**Regeln:**

| Regel | Begründung |
|---|---|
| `signal`/`computed` statt RxJS-Subscriptions im Template | Angular-Idiome, zoneless-fähig |
| SSE für Live-Werte, REST nur für initiale Ladung und Aktionen | weniger HTTP-Last |
| `provideZonelessChangeDetection()` | moderne Angular-22-Empfehlung, weniger Overhead |
| Kein globales State-Framework | 127 Items + Status – Signal-Stores reichen |

## 9. Theming

| Preset | Einsatz |
|---|---|
| **Aura** | Default – modern, gut lesbar |
| Lara | klassisch, nüchtern |
| Material | Material-Design-Nutzer |
| Nora | kompakt, hohe Informationsdichte |

Dark Mode: `provideOptimus` unterstützt Dark-Mode über die Presets (`darkModeSelector`).
Die UI folgt der Systemeinstellung (`prefers-color-scheme`).

> **Festlegung für dieses Projekt: Aura als Default.** Begründung: gute Lesbarkeit der
> vielen Temperatur- und Zahlenwerte auf einem Heizungs-Dashboard.

## 10. Accessibility

| Aspekt | Maßnahme |
|---|---|
| Bedienbarkeit per Tastatur | Optimus UI ist a11y-konform; `p-table` mit Keyboard-Navigation |
| Kontrast | Preset Aura erfüllt WCAG AA |
| Screenreader | `aria-label` auf allen Icon-Buttons |
| Fokus-Management | `p-dialog` mit `focusOnShow` |
| Sprache | UI-Texte **deutsch**, Code-Bezeichner englisch |
| Assistent | Schritt-für-Schritt mit `aria-current`, Fortschritt als Text, nicht nur als Balken |

## 11. Build & Auslieferung

### 11.1 Build

````bash
cd src/ComfoBox.Web
npm ci
npm run build -- --configuration production
# Ergebnis: dist/ComfoBox.Web/browser
````

### 11.2 Statische Auslieferung aus dem .NET-Host

**Ein** Image enthält Gateway **und** UI. ASP.NET Core liefert die SPA aus
(→ [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md)):

````csharp
// src/ComfoBox.Gateway/Program.cs
app.UseDefaultFiles();                 // liefert index.html
app.UseStaticFiles();                  // liefert die Assets aus wwwroot

// SPA-Fallback: unbekannte Routen → index.html (Angular-Routing)
app.MapFallbackToFile("index.html");
````

````xml
<!-- src/ComfoBox.Gateway/ComfoBox.Gateway.csproj -->
<ItemGroup>
  <Content Include="..\ComfoBox.Web\dist\ComfoBox.Web\browser\**"
           Link="wwwroot\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
````

> **Vorteil gegenüber einem nginx-Container:** kein Reverse-Proxy, kein CORS, kein
> `proxy_buffering`-Problem bei SSE (der Browser spricht direkt mit dem ASP.NET-Host),
> ein Port, ein Image, ein Healthcheck.

### 11.3 Docker-Stage

````dockerfile
FROM node:22-alpine AS web-build
WORKDIR /app
COPY src/ComfoBox.Web/package*.json ./
RUN npm ci
COPY src/ComfoBox.Web/ ./
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY src/ ./
RUN dotnet publish src/ComfoBox.Gateway/ComfoBox.Gateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk add --no-cache tini curl \
 && addgroup -S comfobox -g 1001 \
 && adduser -S comfobox -u 1001 -G comfobox
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=web-build /app/dist/ComfoBox.Web/browser ./wwwroot
RUN mkdir -p /var/lib/comfobox /var/log/comfobox \
 && chown -R comfobox:comfobox /app /var/lib/comfobox /var/log/comfobox
USER comfobox
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=25s --retries=3 \
  CMD curl -fsS http://localhost:8080/api/health || exit 1
ENTRYPOINT ["/sbin/tini", "--"]
CMD ["dotnet", "ComfoBox.Gateway.dll"]
````

## 12. Ablösung des WPF-DemoClients

| Funktion | DemoClient (WPF) | Neue UI |
|---|---|---|
| Port/Baudrate wählen | ✓ | ✓ (Assistent, Schritt 1–2) |
| „Read All" | ✓ | ✓ (Dashboard-„Aktualisieren") |
| Freier BACnet-Objekt-Leser | ✓ | **entfällt** – nur Expertenfunktion; ersetzt durch den Item-Browser |
| TreeView der Item-Gruppen | ✓ | ✓ (Item-Browser) |
| **Schreiben** | ✓ (jede Änderung schreibt sofort) | ✓ (explizit mit Bestätigung) |
| **Status/Health** | ✗ | ✓ |
| **Konfiguration** | ✗ | ✓ |
| **Ersteinrichtung** | ✗ | ✓ (Getting Started) |
| **Läuft im Browser / auf dem Pi** | ✗ | ✓ |

> Der „freie BACnet-Objekt-Leser" (freie Objekt-ID eingeben) ist im Ist-Zustand für das
> **Reverse-Engineering** neuer ComfoBox-Objekte nützlich. Er wird **nicht** in die
> produktive UI übernommen, sondern bleibt als separates Werkzeug unter
> `tools/bacnet-probe` (Konsolen-Tool) verfügbar.

## 13. Hilfe-Bereich (Benutzerdokumentation)

> Vollständige Spezifikation:
> [benutzerdokumentation.md](benutzerdokumentation.md) – eine Quelle, drei Kanäle
> ([ADR-010](../05-entscheidungen/adr-010-benutzerdokumentation.md)).

### 13.1 Route

| Route | Inhalt | Anmeldung |
|---|---|---|
| `/help` | Index aller Hilfeseiten | **nein** |
| `/help/:slug` | eine Hilfeseite (z. B. `/help/mqtt`) | **nein** |

> **Warum ohne Anmeldung?** Die Anleitungen werden vor allem **vor** der Ersteinrichtung
> gebraucht (Serial-Port, Baudrate, Broker einrichten). Sie enthalten keine Geheimnisse
> und keinen Gerätezustand. Der Assistent verlinkt genau dorthin.

### 13.2 Woher kommt der Inhalt?

Die Seiten werden zur **Build-Zeit** aus `docs/benutzer/` erzeugt
([`tools/build-docs.mjs`](../../tools/build-docs.mjs)) und liegen danach statisch im
Image unter `wwwroot/help/`:

````
wwwroot/help/
├── index.html          ← Übersicht (Slug "index")
├── index.json          ← Suchindex (Titel, Beschreibung, Tags, Volltext)
├── style.css
├── erste-schritte.html
├── anschluss.html
├── installation.html
├── mqtt.html
├── home-assistant.html
├── bedienung.html
├── ha-addon.html
├── synology.html
├── fehlersuche.html
└── api.html
````

Der Client lädt nur `<slug>.html` und `index.json` – **kein** Server-Rendering,
keine Laufzeit-Generierung.

### 13.3 Suche

Die Volltextsuche läuft **clientseitig** über `index.json` (siehe
[benutzerdokumentation.md §3.4](benutzerdokumentation.md#34-suchindex)). Kein Server,
keine Anfrage pro Tastendruck.

### 13.4 Verlinkung aus dem Setup-Assistenten

Der Assistent verlinkt an den Stellen, an denen Nutzer typischerweise festhängen, direkt
auf die passende Hilfeseite – Details in
[benutzerdokumentation.md §3.5](benutzerdokumentation.md#35-hilfe-auf-der-setup-seite).

### 13.5 Sicherheit

Das gerenderte HTML wird mit `DomSanitizer.bypassSecurityTrustHtml` eingebunden. Das ist
vertretbar, weil der Inhalt aus dem **eigenen Image** stammt (zur Build-Zeit erzeugt) und
nicht aus einer Benutzereingabe. Sollte die Doku später zur Laufzeit aus dem Netz
geladen, **muss** auf `DomSanitizer.sanitize` gewechselt werden.

## 14. Definition of Done

- [ ] `ng add @openng/optimus-ui` erfolgreich, `provideOptimus({ theme: { preset: Aura } })` gesetzt
- [ ] **Assistent**: alle 6 Schritte durchlaufbar, Zustand bleibt beim Zurück-Navigieren erhalten
- [ ] **Assistent**: Portliste zeigt Beschreibung + stabilen Pfad, aktualisiert sich bei Hotplug
- [ ] **Assistent**: BACnet- und MQTT-Test mit kontextbezogenen Fehlermeldungen
- [ ] **Assistent**: Passwortregeln, Stärke-Anzeige, keine Speicherung des Klartexts im State
- [ ] **Assistent**: `PUT /api/setup/config` hasht das Passwort serverseitig
- [ ] **Assistent**: nach `POST /api/setup/apply` wird der Neustart beobachtet und der Erfolg gemeldet
- [ ] **Assistent** ist nach der Ersteinrichtung passwortgeschützt und wiederholbar
- [ ] Dashboard zeigt Health, Verbindungen, Version, Uptime, Schreibzähler
- [ ] Fehlerbanner bei `degraded` / `offline` **sichtbar und aussagekräftig**
- [ ] Item-Browser zeigt alle 127 Items mit aktuellem Wert, gruppiert, mit Suche
- [ ] Analog- und Enum-Werte sind **schreibbar** mit Validierung (Min/Max, Options)
- [ ] Schreibfehler werden angezeigt und der Serverwert wiederhergestellt
- [ ] Konfigurationsseite ändert `ExpertMode`, Poll-Intervall, Schreiblimit
- [ ] Neustart-Button mit Bestätigung
- [ ] Live-Werte via SSE (ohne Polling-Schleife im Browser)
- [ ] Login mit Benutzername/Passwort (Cookie-Sitzung)
- [ ] `/help` und `/help/:slug` erreichbar **ohne** Anmeldung
- [ ] Hilfeseiten werden aus `wwwroot/help/` geladen, Suche läuft clientseitig über `index.json`
- [ ] Der Setup-Assistent verlinkt an den 5 kritischen Stellen auf die passende Hilfeseite
- [ ] Responsive von 360 px (Handy) bis 1920 px
- [ ] Lighthouse-Score ≥ 90 (Performance/Accessibility)
- [ ] Kein Zone.js-Overhead (`provideZonelessChangeDetection`)
