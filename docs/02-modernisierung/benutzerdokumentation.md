# Benutzerdokumentation

> Anforderung: *„Doku für Gateway selbst … als HTML für den Benutzer lesbar. Help für HA in
> HACS natürlich auch und evt. auch für Synology."*

## 1. Zweck

Die Doku existiert heute **nur** als Markdown im Repository. Sie richtet sich an
Entwickler, nicht an den Betreiber einer Heizungsanlage.

Ziel: **eine** Doku-Quelle, **drei** Auslieferungskanäle:

| Kanal | Form | Empfänger |
|---|---|---|
| **A – Web-UI** | HTML, im Browser | Betreiber (in der App) |
| **B – HACS** | Markdown, im HA-Dialog | HA-Nutzer |
| **C – Synology** | Markdown/HTML, verlinkt aus dem Paketzentrum | NAS-Nutzer |

> **Prinzip:** Die Doku wird **einmal** geschrieben (Markdown, deutsch) und **an drei
> Stellen** ausgeliefert. Kein doppelter Text, keine drei Sprachversionen.

## 2. Woher kommt der Inhalt?

### 2.1 Die zwei Doku-Welten

| Welt | Ort | Zielgruppe | Sprache |
|---|---|---|---|
| **Spezifikation** | `docs/01-…` bis `docs/05-…` | Entwickler, Reviewer | Deutsch, sehr technisch |
| **Benutzerdokumentation** | `docs/benutzer/` | Betreiber | Deutsch, anleitungsgerecht |

> **Die Benutzerdoku ist ein eigenes Verzeichnis**, nicht die Spezifikation.
> Die Spezifikation erklärt *wie* es gebaut wird, die Benutzerdoku erklärt *wie man es
> benutzt*. Ein Nutzer, der die Spezifikation öffnet, ist verloren.

### 2.2 Struktur von `docs/benutzer/`

````
docs/benutzer/
├── README.md                  # Startseite + Inhaltsverzeichnis
├── 01-erste-schritte.md       # Installation → erste Werte
├── 02-anschluss.md            # Hardware, Verdrahtung, Baudrate
├── 03-installation.md         # Die vier Installationsstufen
├── 04-mqtt.md                 # Broker, Topics, Payloads
├── 05-home-assistant.md       # MQTT-Discovery + HACS-Integration
├── 06-bedienung.md            # UI, Items schreiben, Schreibschutz
├── 07-ha-addon.md             # HA-Add-on
├── 08-synology.md             # SPK-Installation auf der DS923+
├── 09-fehlersuche.md          # Symptom → Ursache → Lösung
└── 10-api.md                  # REST-/SSE-Referenz
````

### 2.3 Front-Matter

Jede Seite beginnt mit YAML-Front-Matter – das steuert Navigation, Suche und Metadaten:

````markdown
---
title: Erste Schritte
description: Installation, Anschluss und die ersten gelesenen Werte
order: 10
icon: pi pi-rocket
audience: user
tags: [installation, anleitung]
---
````

| Feld | Pflicht | Bedeutung |
|---|---|---|
| `title` | ja | Überschrift in der Navigation |
| `description` | ja | Kurztext für Suchergebnisse und Meta-Tags |
| `order` | ja | Sortierung (aufsteigend) |
| `icon` | nein | Optimus-UI-Icon (PrimeNG-Syntax `pi pi-…`) |
| `audience` | ja | `user` (Benutzerdoku) oder `dev` (Spezifikation) |
| `tags` | nein | Freitext für die Suche |

## 3. Kanal A – Dokumentation in der Web-UI

### 3.1 Wo erscheint sie?

```
┌────────────────────────────────────────────────────────┐
│  ComfoBox Gateway                    [● online]  [☰]  │
├──────────────┬─────────────────────────────────────────┤
│  Dashboard   │                                         │
│  Geräte      │   ┌───────────────────────────────────┐  │
│  Konfiguration│  │  Hilfe                            │  │
│  Diagnose    │  │                                   │  │
│ ──────────── │  │  Erste Schritte                   │  │
│  📖 Hilfe    │  │  ───────────────────────────────  │  │
│              │  │  Inhalt der gewählten Seite       │  │
│              │  │                                   │  │
│              │  │  [◀ Zurück]  [Weiter ▶]          │  │
│              │  └───────────────────────────────────┘  │
└──────────────┴─────────────────────────────────────────┘
```

| Aspekt | Entscheidung |
|---|---|
| Route | `/help` (Hauptseite) und `/help/:slug` (einzelne Seite) |
| Zugriff | **nach** dem Login (geschützt), aber **vor** der Einrichtung erreichbar |
| Suche | Volltext über Titel, Beschreibung, Tags und Inhalt |
| Drucken | eigene Druck-Stylesheet (`@media print`) – ohne Menü |
| Sprache | **Deutsch** (die Oberfläche ist deutsch) |
| Offline | die Inhalte liegen als Dateien im Container → kein Internet nötig |

### 3.2 Wie kommen die Inhalte in das Image?

**Zwei Optionen, eine ist richtig:**

| Option | Bewertung |
|---|---|
| **A – Zur Build-Zeit in HTML umwandeln** ✅ | Statische Dateien, kein Markdown-Parser im Browser, keine zusätzliche Runtime-Abhängigkeit, indexierbar, schnell |
| B – Markdown im Browser rendern (`marked`) | kleiner, aber 40 KB JS extra und das Markdown landet ungerendert im Quellcode |
| C – Markdown im Backend rendern | Serverlast für reine Leseinhalte, unnötig |

**Umsetzung (Option A):**

````
docs/benutzer/*.md
        │
        │  npm run docs:build   (tools/build-docs.mjs)
        ▼
src/ComfoBox.Web/public/help/
├── index.html            # Startseite mit Inhaltsverzeichnis
├── erste-schritte.html
├── anschluss.html
└── …
        │
        │  ng build  →  dist/ComfoBox.Web/browser/help/**
        ▼
wwwroot/help/**          (im selben Image)
````

**Build-Skript** (`tools/build-docs.mjs`):

| Aspekt | Entscheidung |
|---|---|
| Laufzeit | Node 22 (läuft im Docker-Build) |
| Abhängigkeiten | **keine** – der Markdown-Parser ist eine Dev-Dependency des Web-Projekts |
| Ausgabe | HTML mit eingebettetem CSS (ein Stylesheet für alle Seiten) |
| Navigation | jede Seite bekommt Vor/Zurück/Links auf Nachbarseiten |
| Metadaten | `index.json` mit Titel, Beschreibung, Tags → für die Suche im UI |
| Fehler | **Build bricht ab**, wenn eine Seite kein Front-Matter hat (vergisst man das, merkt man sofort) |
| Tabellen/Grafiken | GitHub-Flavored-Markdown wird unterstützt |

> **Warum zur Build-Zeit:** Das Image enthält dann nur noch fertiges HTML. Kein Parser im
> Browser, kein CDN, kein Internet-Zwang. Und: **Doku-Updates sind ganz normale
> Code-Updates** – gleicher Release, gleiche Version.

### 3.3 Die Hilfeseite in Angular

````ts
// features/help/help.routes.ts
export const HELP_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./help-index').then(m => m.HelpIndexComponent),
  },
  {
    path: ':slug',
    loadComponent: () => import('./help-page').then(m => m.HelpPageComponent),
  },
];
````

````ts
// features/help/help-page.ts
@Component({
  selector: 'app-help-page',
  template: `
    <p-card>
      <ng-template #header>
        <div class="flex items-center justify-between">
          <h2 class="text-xl font-semibold">{{ meta()?.title }}</h2>
          <p-tag [value]="meta()?.audience === 'user' ? 'Anleitung' : 'Referenz'" />
        </div>
      </ng-template>

      <!-- Das HTML kommt aus dem Image; es ist zur Build-Zeit erzeugt. -->
      <div class="help-content" [innerHTML]="html()"></div>

      <ng-template #footer>
        <div class="flex justify-between">
          <p-button label="Zurück" icon="pi pi-arrow-left" [disabled]="!prev()" (click)="go(prev())" />
          <p-button label="Weiter" icon="pi pi-arrow-right" [text]="true" [disabled]="!next()" (click)="go(next())" />
        </div>
      </ng-template>
    </p-card>
  `,
})
export class HelpPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly slug = signal<string>('');
  readonly html = signal<SafeHtml>('');
  readonly meta = signal<HelpMeta | null>(null);

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const slug = params.get('slug') ?? 'index';
      this.slug.set(slug);
      this.http.get(`/help/${slug}.html`, { responseType: 'text' })
        .subscribe(html => this.html.set(this.sanitize(html)));
      this.http.get<HelpMeta[]>('/help/index.json')
        .subscribe(all => this.meta.set(all.find(m => m.slug === slug) ?? null));
    });
  }

  /** Das HTML stammt aus dem eigenen Image – es ist kein Fremdinhalt. */
  private sanitize(html: string): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }
}
````

> **Sicherheit:** `bypassSecurityTrustHtml` ist hier vertretbar, weil der Inhalt aus dem
> **eigenen** Image stammt (zur Build-Zeit aus `docs/benutzer/` erzeugt), nicht aus einer
> Benutzereingabe. Sollte die Doku später aus dem Netz geladen, muss auf
> `DomSanitizer.sanitize` gewechselt werden – siehe Kommentar im Code.

### 3.4 Suchindex

````json
[
  {
    "slug": "erste-schritte",
    "title": "Erste Schritte",
    "description": "Installation, Anschluss und die ersten gelesenen Werte",
    "order": 10,
    "tags": ["installation", "anleitung"],
    "content": "volltext für die Suche …"
  }
]
````

Die Suche läuft **clientseitig** über dem Index – kein Server, keine Anfrage pro Tastendruck.

### 3.5 Hilfe auf der Setup-Seite

Der Getting-Started-Assistent verlinkt an den Stellen, wo Nutzer typischerweise
feststecken, direkt auf die passende Hilfeseite:

| Stelle | Link |
|---|---|
| Schritt 1 (Serial-Port), kein Port gefunden | `/help/anschluss#adapter-erkennen` |
| Schritt 2 (BACnet), `no_iam` | `/help/anschluss#baudrate` |
| Schritt 3 (MQTT), Broker nicht erreichbar | `/help/mqtt#broker-einrichten` |
| Schritt 4 (Web-Zugang) | `/help/erste-schritte#zugang` |
| Dashboard, Gateway `degraded` | `/help/fehlersuche` |

## 4. Kanal B – HACS

| Artefakt | Ort | Inhalt |
|---|---|---|
| **HACS-Dialog** | `custom_components/comfobox/README.md` | Installation in 5 Sätzen, Gerät, Voraussetzungen, Links |
| **Config-Flow-Texte** | `custom_components/comfobox/strings.json` + `translations/` | Feldbezeichner und Fehlermeldungen |
| **Service-Beschreibungen** | `strings.json` | `services.*` |
| **Ausführliche Doku** | `docs/benutzer/05-home-assistant.md` | verlinkt aus dem HACS-Dialog |

> **Regel:** Der HACS-Dialog zeigt **maximal eine Seite** – README.md. Alles Weitere wird
> verlinkt. HACS-READMEs werden in der Kachel angezeigt; eine lange Doku dort ist
> unlesbar.

> **Die deutsche Fassung** kommt aus `docs/benutzer/05-home-assistant.md`. Der HACS-Dialog
> selbst ist englisch (HA-Standard) – die `strings.json` hat aber bereits deutsche
> Fehlertexte, damit der Nutzer in seiner HA-Sprache Fehler versteht.

## 5. Kanal C – Synology

| Artefakt | Ort | Inhalt |
|---|---|---|
| **Paketbeschreibung** | `deploy/synology-spk/src/INFO` | `description_ger` (kurz, im Paketzentrum) |
| **Hilfe-Link** | `deploy/synology-spk/src/INFO` → `helpurl` | zeigt auf die Benutzerdoku |
| **Paket-README** | `deploy/synology-spk/README.md` | Installation, Pfade, Sicherheit, Grenzen |
| **Ausführliche Doku** | `docs/benutzer/08-synology.md` | verlinkt |

> **Zu `helpurl`:** Das Feld muss auf eine **stabile, öffentlich erreichbare** URL zeigen.
> Aktuell: `https://github.com/RF77/comfobox-mqtt/tree/main/docs` → wird auf
> `docs/benutzer/08-synology.md` umgestellt, sobald die Doku existiert.

### 5.1 Was das DSM-Paketcenter anzeigt

| Element | Inhalt | Quelle |
|---|---|---|
| Name | „ComfoBox Gateway" | `displayname` |
| Kurzbeschreibung | 2 Zeilen | `description` |
| Version | `1.0.0-0001` | `version` |
| Anbieter | RF77 | `maintainer` |
| Hilfe | Link auf die Doku | `helpurl` |
| Symbol | Paket-Icon | `PACKAGE_ICON.PNG` |

> Das **Icon** fehlt noch in `deploy/synology-spk/src/` – es ist eine Pflichtdatei für
> das Paketzentrum (Aufgabe 8.1.1).

## 6. Sprache & Tonalität

| Regel | Begründung |
|---|---|
| **Deutsch**, Sie-Form | Zielgruppe sind Betreiber, keine Entwickler |
| Anleitungssatz zuerst | „Klicken Sie auf **Installieren**", nicht „Die Installation erfolgt über…" |
| **Kein** Jargon ohne Erklärung | „MS/TP-Token" → „das Protokoll, über das die Heizung angesprochen wird" |
| Fehler: **Symptom → Ursache → Lösung** | die Benutzer kommen mit dem Symptom, nicht mit der Ursache |
| Screenshots als Markdown-Bilder | `![](../images/….png)` mit Alt-Text |
| Keine Implementierungsdetails | kein `SemaphoreSlim`, kein `MQTTnet 5.2.0.1603` |
| Zahlen **deutsch** formatieren | `21,5 °C` – aber in **Payloads** immer `21.5` (dieser Unterschied wird erklärt) |

### 6.1 Tonfall-Beispiele

| ❌ So nicht | ✅ So |
|---|---|
| „Der BACnet-Stack verwendet `System.IO.Ports`." | „Die Heizung wird über ein USB-Adapter-Kabel angeschlossen." |
| „Payloads werden invariant formatiert." | „Die Werte verwenden immer einen Punkt als Dezimaltrennzeichen – auch auf deutschen Systemen." |
| „MQTT-Broker erforderlich." | „Für die Verbindung zu Home Assistant brauchen Sie einen MQTT-Broker. Wenn Sie nur die Web-Oberfläche nutzen, ist keiner nötig." |

## 7. Was **nicht** in die Benutzerdoku gehört

| Inhalt | Wo stattdessen |
|---|---|
| Architektur-Entscheidungen | `docs/05-entscheidungen/` |
| Migrations-Roadmap | `docs/04-migration/` |
| Ist-Zustand mit Code-Stellen | `docs/01-ist-zustand/` |
| BACnet-Protokolldetails | `docs/02-modernisierung/bacnet-portierung.md` |
| Topic-Liste (alle 127) | `docs/topics.md` (generiert) – verlinkt, nicht abgeschrieben |

> **Die Benutzerdoku verlinkt die Spezifikation, sie kopiert sie nicht.**

## 8. Pflege

| Regel | Mechanismus |
|---|---|
| Front-Matter vollständig | Build bricht ab → vergessenes Front-Matter fällt sofort auf |
| Neue Seite → Index aktualisieren | Build prüft, dass jede Seite im Index steht |
| Tote Links | der Link-Validator (`tools/check-links.ps1`) prüft auch `docs/benutzer/**` |
| Doku im selben PR wie die Änderung | Review-Regel (siehe [docs.instructions.md](../../.github/instructions/docs.instructions.md)) |
| Versionsstand | Kopfzeile jeder Seite: „Stand: 1.0.0" – automatisch beim Build eingesetzt |

## 9. Definition of Done

- [ ] `docs/benutzer/` existiert mit README + 10 Seiten
- [ ] Jede Seite hat vollständiges Front-Matter
- [ ] `tools/build-docs.mjs` erzeugt `help/*.html` + `help/index.json`
- [ ] Build bricht bei fehlendem Front-Matter **und** bei toten Links ab
- [ ] Angular-Route `/help/:slug` rendert die Seiten mit Vor/Zurück-Navigation
- [ ] Volltextsuche über den Index
- [ ] Druck-Stylesheet vorhanden
- [ ] Setup-Assistent verlinkt an 5 Stellen auf passende Hilfeseiten
- [ ] HACS-README verlinkt auf `05-home-assistant.md`
- [ ] `INFO` → `helpurl` zeigt auf `08-synology.md`
- [ ] `PACKAGE_ICON.PNG` vorhanden (für das Paketzentrum)
- [ ] `tools/check-links.ps1` prüft `docs/benutzer/**` mit

## 10. Verwandte Dokumente

- [ui-angular-optimus.md](ui-angular-optimus.md) § 13 – der Hilfe-Bereich in der UI
- [installationsstufen.md](../03-deployment/installationsstufen.md) – die vier Wege
- [ha-integration.md](ha-integration.md) – die HA-Integration
- [synology-paketcenter.md](../03-deployment/synology-paketcenter.md) – das SPK
- [ADR-010](../05-entscheidungen/adr-010-benutzerdokumentation.md)
