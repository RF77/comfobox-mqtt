# ADR-012: Zugriffskontrolle der REST-API – Bearer-Token, Administrator und LAN-Modus

| | | |
|---|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | API, Sicherheit, Integration |

## Kontext

Die REST-API ([ADR-005](adr-005-api-stil.md)) ist die Schnittstelle für drei sehr
unterschiedliche Nutzer:

| Nutzer | Bedarf | Umgebung |
|---|---|---|
| **Web-UI** im Browser | alle Endpunkte, inklusive Schreiben und Neustart | LAN |
| **Home-Assistant-Integration** | Items lesen/schreiben, Status lesen | LAN |
| **Assistent (Getting Started)** | Konfiguration schreiben, Passwort setzen | Ersteinrichtung |

Daraus folgen drei Anforderungen:

1. **Schreibende Endpunkte dürfen nicht offen sein**, sobald der Gateway
   konfiguriert ist – sonst kann jeder im LAN die Heizung verstellen oder den
   Gateway neu starten.
2. **Die Ersteinrichtung muss ohne Anmeldung funktionieren** – der erste
   Administrator existiert ja noch nicht.
3. **Der LAN-Betrieb darf nicht erzwungen werden** – auf einer Synology im
   Heimnetz ist eine Anmeldung oft nur nervig (Sonderfall Q2 in
   [ADR-005](adr-005-api-stil.md)).

Zusätzlich muss die **Home-Assistant-Integration** ([ADR-009](adr-009-ha-integration-ohne-mqtt.md))
mit einem **API-Token** arbeiten können, das über die REST-API verwaltet wird –
ohne MQTT-Broker und ohne Basic-Auth im Browser.

## Entscheidung

**Die Zugriffskontrolle wird als explizite Prüfung im Endpunkt umgesetzt
(`ApiAccessGuard`), nicht über die ASP.NET-Authentifizierungs-Middleware.**

### Zwei Identitäten

| Identität | Herkunft | Rolle |
|---|---|---|
| **API-Token** | `Authorization: Bearer <token>` | `admin`, wenn `IsAdmin` |
| **Administrator** | `Authorization: Basic <base64(user:pass)>` | immer `admin` |

Beide werden gegen `config.json` geprüft: der Administrator über einen
PBKDF2-Hash (`pbkdf2-sha256$600000$…`), die Tokens über denselben Hash.

### Die Regeln

| Endpunkt-Gruppe | Vor der Ersteinrichtung | Danach |
|---|---|---|
| `/api/setup/*` | **offen** | **immer** `admin` erforderlich |
| Administrative Endpunkte (`/api/service/restart`, `/api/security/tokens`) | offen | `admin` erforderlich **nur wenn** `Api:RequireAuthentication = true` |
| Diagnose und Status | offen | offen |
| `/api/items` (lesen) | offen | offen |
| `PUT /api/items/{id}` | offen | offen (der 24-h-Zähler ist der Schreibschutz) |

> **Warum sind die Setup-Endpunkte nach der Ersteinrichtung *immer* geschützt?**
> Sie können die Konfiguration überschreiben – und damit auch das
> Administrator-Passwort. Ein offener Setup-Endpunkt wäre eine
> Passwort-Änderung durch jeden im LAN.

### Ein ungültiges Zugangsmittel wird immer abgelehnt

> **Regel (Phase 8d, 2026-11-01):** Wer **ein** `Authorization`-Header vorlegt
> und damit **nicht** authentifiziert wird, bekommt **immer** `401` – auch auf
> den offenen Endpunkten.

| Situation | Antwort |
|---|---|
| **kein** `Authorization`-Header | wie bisher: `200` (LAN-Modus) |
| **gültiger** Bearer-Token oder Basic-Auth | `200` |
| **ungültiger** Bearer-Token oder Basic-Auth | `401` |

> **Warum?** Die Home-Assistant-Integration prüft ihren Token über
> `GET /api/status`. Ohne diese Regel würde ein **falscher** Token dort als
> gültig durchgehen – die Einrichtung würde erfolgreich melden, und der
> Nutzer bekäme erst Wochen später den eigentlichen Fehler zu sehen. Ein
> ungültiger Token muss **sofort** als solcher erkannt werden.
>
> Das entspricht der HTTP-Semantik: eine vorgelegte, aber ungültige
> Identität wird abgelehnt – auch bei einer ansonsten offenen Ressource.
>
> **Ohne** Header ändert sich **nichts**: der LAN-Modus (Dokumentierter
> Betrieb) und die Web-Oberfläche (Sitzungs-Cookie) bleiben unberührt.

### Das Tokenformat

```
cbk_<16 hex Zeichen>_<43 Zeichen Base64Url>
```

Die Id steht **vor** dem Geheimnis, damit die Suche nach dem Token eine
Tabellen-Suche ist und nicht für jedes Token eine PBKDF2-Rechnung braucht
(600.000 Iterationen ≈ 0,3 s). Die PBKDF2-Rechnung selbst wird für kurze Zeit
zwischengespeichert; bei jeder Änderung des Token-Bestands wird der Cache
verworfen – ein widerrufener Token wirkt dadurch **sofort** nicht mehr.

## Alternativen

| Alternative | Warum verworfen |
|---|---|
| **ASP.NET-Authentifizierungs-Middleware** (`AddAuthentication` + Policies) | Die Reihenfolge der Middleware (Authentication → Authorization → Endpunkt) ist eine typische Fehlerquelle; außerdem verlangt das Framework, dass jeder administrative Endpunkt eine Policy bekommt. Die explizite Prüfung ist direkt im Endpunkt sichtbar und ohne Middleware-Reihenfolge testbar. |
| **Nur Basic-Auth** | Der Browser müsste bei jedem Aufruf nachfragen; die HA-Integration kann keinen Benutzernamen verwalten. |
| **Nur API-Token** | Der erste Token kann nicht erzeugt werden, solange niemand angemeldet ist – das Henne-Ei-Problem. Basic-Auth löst es. |
| **Session-Cookie** (in ADR-005 für die Web-UI vorgesehen) | Für die **Programmatic**-Nutzer (HA-Integration) ungeeignet. Die Web-UI bekommt ihr Cookie in Phase 5 (`5.10`) obendrauf – die API bleibt token-fähig. |
| **Immer `admin` erforderlich** | Macht den LAN-Betrieb unbenutzbar (Q2). |

## Konsequenzen

**Positiv**

- Die Zugriffsregeln stehen an **einer** Stelle (`ApiAccessGuard`) und sind
  direkt testbar (die Tests rufen die echten HTTP-Endpunkte auf).
- Die HA-Integration braucht nur einen Bearer-Token – kein Broker, kein
  Benutzername.
- Der Klartext eines Tokens ist **einmal** sichtbar; gespeichert wird nur der
  Hash.
- Der LAN-Modus bleibt möglich (`Api:RequireAuthentication = false`).

**Negativ**

- Der Prüfcode steht **in** jedem Endpunkt (`guard.RequireAdmin(ctx)`). Wird
  ein Endpunkt neu gebaut, muss man daran denken.
- `ApiAccessGuard` ist kein ASP.NET-`IAuthorizationHandler` – die Integration
  in eine spätere Cookie-/Rollen-Lösung (Phase 5, Aufgabe 5.10) ist eine
  Änderung an **einer** Stelle, aber nichttrivial.
- Die PBKDF2-Prüfung ist teuer (≈ 0,3 s). Für die HA-Integration (10-s-Takt)
  ist das unkritisch, für eine spätere Cookie-Session erst recht.

## Folgeaufträge

- **Phase 5, Aufgabe 5.10**: Login + Cookie-Auth für die Web-UI. Der Cookie
  wird als zusätzliche Identität neben Bearer und Basic ergänzt – die
  bestehende Token-Mechanik bleibt unverändert.
- **Phase 8d**: Token-Verwaltung in der Oberfläche (Erzeugen, Name, widerrufen).
