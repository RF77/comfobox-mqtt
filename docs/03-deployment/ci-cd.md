# CI/CD

## 1. Ziele

| Ziel | Tool |
|---|---|
| Build & Test bei jedem PR | GitHub Actions |
| Sicherheits-Scan der Abhängigkeiten | `dotnet list package --vulnerable` + Trivy |
| Image-Build bei Tag | Docker Buildx + GHCR |
| Automatische Releases | GitHub Releases + GHCR-Tags |

## 2. Pipeline-Übersicht

```
Push / PR
   │
   ├─▶ build-test        (ubuntu-latest, .NET 10)
   │     ├─ restore (Cache, --locked-mode)
   │     ├─ build (Release, warnings as errors)
   │     ├─ test (alle Projekte, Coverage-Report)
   │     └─ vulnerability scan
   │
   ├─▶ web               (ubuntu-latest, Node 22)
   │     ├─ npm ci
   │     ├─ lint + build
   │     └─ (optional) Bundle-Size-Check
   │
   ├─▶ container         (ubuntu-latest, Docker)
   │     ├─ buildx build (linux/amd64)
   │     ├─ Eigenschaften prüfen (kein Root, tini, UI im Image)
   │     └─ Container starten, Health + SPA + SIGTERM prüfen
   │
   └─▶ (nur bei Tag) docker-publish
         ├─ buildx build (linux/amd64, linux/arm64)
         ├─ Trivy-Scan
         ├─ push → ghcr.io
         └─ GitHub Release erstellen

Push Tag "v*"
   │
   └─▶ synology-spk     (ubuntu-latest)
         ├─ Shell-Skripte prüfen (bash -n + Parser-Test)
         ├─ SPK bauen (~40 KB) + packages.json
         ├─ Release-Asset anhängen (gh release upload)
         └─ optional: per SSH auf die NAS kopieren
```

> **Der Legacy-Code (`src_old/`) wird von der CI nicht angefasst.** Der frühere
> Job `legacy-check` ist entfernt, CodeQL ignoriert `src_old/` (→ `paths-ignore`).
> Grund: `src_old/` ist Archiv (net452, C# 6), der Code lief seit dem 24.10.
> nicht mehr und ist nur noch Referenz für Protokollverhalten und
> Item-Metadaten.

### ⚠️ Ein roter Lauf kann bedeuten: die Workflow-Datei ist ungültig

> **Das ist die teuerste Falle der CI.** Ein syntaktisch gültiges YAML ist
> **nicht** dasselbe wie ein von GitHub akzeptierter Workflow.

| Symptom | Bedeutung |
|---|---|
| Lauf steht auf **Failure**, aber es gibt **0 Jobs** | GitHub hat die Datei **schon bei der Validierung** abgelehnt |
| Keine Logs, kein „Annotations"-Panel mit Job-Details | die Validierung ist gescheitert, nicht der Code |
| Der Lauf dauert **0 Sekunden** | es wurde nie etwas ausgeführt |

Die Fehlermeldung steht in der **Annotation** des Laufs
(→ Run-Seite → „Annotations"):

| Meldung | Ursache | Lösung |
|---|---|---|
| `Unrecognized function: 'hashFiles'` | `hashFiles` steht in einer **Job**-Bedingung (`if:` auf Job-Ebene) | Bedingung entfernen oder den Job **immer** laufen lassen |
| `Unexpected value 'paths-ignore'` | `paths-ignore` steht auf **Top-Level** der Datei | nach **`on:`** verschieben (dort filtert es die Trigger) |
| `Unexpected value 'schedule'` | `schedule:` ist unter `pull_request:` verschachtelt | als **Geschwister** von `push`/`pull_request` einrücken |

**Merksatz:** Ein Lauf mit **0 Jobs** ist **nie** ein Code-Problem. Erst die
Annotation lesen, dann den Code.

> **Die Konsequenz hier:** Durch den `hashFiles`-Fehler lief die CI **seit
> Phase 0 nie** – alle Läufe waren „rot" ohne dass ein Job gestartet wurde.
> Das ist auch der Grund, warum ein Fehler im Code so lange unbemerkt blieb.

## 3. Workflow: `build-test.yml`

````yaml
name: build-test

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true

jobs:
  build:
    name: Build & Test (.NET)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          cache: true
          cache-dependency-path: '**/packages.lock.json'

      - name: Restore
        run: dotnet restore src/ComfoBox.sln --locked-mode

      - name: Build
        run: dotnet build src/ComfoBox.sln -c Release --no-restore -warnaserror

      - name: Test
        run: dotnet test src/ComfoBox.sln -c Release --no-build --collect:"XPlat Code Coverage"

      - name: Vulnerability scan (NuGet)
        run: |
          dotnet list src/ComfoBox.sln package --vulnerable --include-transitive --format json > vuln.json
          if grep -q '"vulnerabilities": *\[.\+\]' vuln.json; then
            echo "::error::Vulnerable packages found"; exit 1
          fi

      - name: Upload coverage
        uses: actions/upload-artifact@v4
        with:
          name: coverage
          path: '**/TestResults/**'

  web:
    name: Build (Angular)
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: src/ComfoBox.Web
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: npm
          cache-dependency-path: src/ComfoBox.Web/package-lock.json
      - run: npm ci
      - run: npm run lint
      - run: npm run build -- --configuration production
````

> **`--locked-mode`** erzwingt, dass die Lock-Files zu den Projektdateien passen. Das macht den
> Build **reproduzierbar** und verhindert, dass eine transitive Abhängigkeit unbemerkt
> aktualisiert wird.

## 4. Workflow: `docker-publish.yml`

> **Ein Image, nicht zwei** → [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md).
> Gateway **und** Web-Oberfläche liegen in einem Image; es gibt kein `comfobox-web`.

````yaml
name: docker-publish

on:
  push:
    tags: [ 'v*' ]
  workflow_dispatch:
    inputs:
      tag:
        description: 'Image-Tag (z. B. 1.1.0)'
        required: true

env:
  REGISTRY: ghcr.io
  IMAGE_NAMESPACE: ${{ github.repository_owner }}

jobs:
  publish:
    runs-on: ubuntu-latest
    permissions:
      contents: write
      packages: write
    steps:
      - uses: actions/checkout@v4

      - uses: docker/setup-qemu-action@v3

      - uses: docker/setup-buildx-action@v3

      - uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAMESPACE }}/comfobox-gateway
          tags: |
            type=ref,event=tag
            type=raw,value=latest,enable=${{ startsWith(github.ref, 'refs/heads/main') }}
            type=sha,format=long

      - uses: docker/build-push-action@v6
        with:
          # Der Kontext ist das Repository-Wurzelverzeichnis: das Image
          # braucht das .NET-Projekt UND die Angular-Oberfläche.
          context: .
          file: ./src/ComfoBox.Gateway/Dockerfile
          push: true
          platforms: linux/amd64,linux/arm64
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha,scope=gateway
          cache-to: type=gha,mode=max,scope=gateway

      - name: Trivy scan
        uses: aquasecurity/trivy-action@0.28.0
        with:
          image-ref: ${{ env.REGISTRY }}/${{ env.IMAGE_NAMESPACE }}/comfobox-gateway:${{ github.ref_name }}
          severity: CRITICAL,HIGH
          exit-code: '1'
          ignore-unfixed: true
````

> **Multi-Arch ist Pflicht:** Die DS923+ ist x86_64, das **HA-Add-on läuft auf dem
> Raspberry Pi 5 (arm64)**. Ein reines `amd64`-Image liefert dort einen
> „exec format error". QEMU emuliert arm64 beim Build – der Image-Aufbau ist
> architekturunabhängig, deshalb verdoppelt sich die Build-Zeit nur für die
> .NET-Schicht.

## 4.1 Job `container` in `build-test.yml`

Bei jedem PR wird das Image **einmal für amd64** gebaut und **echt** geprüft:

| Prüfung | Zweck |
|---|---|
| `User == comfobox` | kein Root im Container |
| `Entrypoint[0] == /sbin/tini` | PID 1 leitet SIGTERM weiter → LWT wird ausgelöst |
| `/app/wwwroot/index.html` existiert | die Angular-UI ist im Image |
| `GET /api/health` → 200 mit `"state"` | der Gateway antwortet |
| `GET /` liefert HTML | die SPA wird aus dem .NET-Host ausgeliefert |
| Health-Status = `healthy` | der Docker-Healthcheck greift |
| `docker stop` → „Application is shutting down" | sauberes Herunterfahren |

## 4.2 Workflow: `synology-spk.yml`

> **Das Synology-Paket bekommt einen eigenen Workflow.** So lässt sich das
> `.spk` auch **ohne** Image-Publish erzeugen (z. B. nur eine Paket-Korrektur),
> und die Release-Notizen werden nicht doppelt gepflegt.

| Trigger | Wirkung |
|---|---|
| Push auf Tag `v*` | SPK bauen, als Release-Asset anhängen, auf die NAS kopieren |
| `workflow_dispatch` | SPK on demand bauen (Version, Revision, Release, NAS-Kopie) |
| `workflow_call` | Aufruf aus anderen Workflows |

### Wohin die Artefakte?

| Artefakt | Ablage | Limit |
|---|---|---|
| `.spk` (~40 KB) | **GitHub-Release-Asset** | 2 GB pro Datei |
| `packages.json` | **GitHub-Release-Asset** | 2 GB pro Datei |
| Container-Image | **GHCR** (`ghcr.io/rf77/comfobox-gateway`) | Registry-Speicher |
| Paket-Archiv | **NAS** (per SSH, optional) | Volume des NAS |

> **Das 500-MB-Limit des GitHub-Free-Kontos gilt nur für das Repository
> selbst** (die Git-Objekte) – nicht für Release-Assets und nicht für die
> Container-Registry. Das SPK ist mit ~40 KB vernachlässigbar, das Image liegt
> in GHCR. Wer trotzdem eine Kopie **auf dem NAS** haben will, hinterlegt die
> Secrets `NAS_SSH_HOST`, `NAS_SSH_USER`, `NAS_SSH_KEY` und `NAS_TARGET_DIR`
> – der Workflow kopiert das Paket dann direkt auf das DSM.

## 5. Release-Prozess

````bash
# Version vorbereiten
git checkout -b release/1.1.0
# CHANGELOG.md aktualisieren, Version in Directory.Build.props setzen
git commit -am "release: 1.1.0"
git tag -a v1.1.0 -m "Version 1.1.0"
git push origin release/1.1.0
git push origin v1.1.0
````

Der Tag löst `docker-publish.yml` aus → Images `ghcr.io/<user>/comfobox-gateway:1.1.0`
und `:latest` + GitHub Release.

### Update auf der DS923+

````bash
cd /volume1/docker/comfobox
docker compose pull
docker compose up -d
docker image prune -f
````

## 6. Weitere sinnvolle Workflows

| Workflow | Trigger | Inhalt |
|---|---|---|
| `codeql.yml` | PR, Push | CodeQL für C# **und** TypeScript |
| `dependency-review.yml` | PR | prüft neue Abhängigkeiten auf bekannte CVEs |
| `docker-nightly.yml` | Zeitplan | nightly Build von `main` → `:nightly` |
| `labeler.yml` | PR | Auto-Labels nach Pfad |
| `stale.yml` | Zeitplan | Issue-/PR-Aufräumen |

> **CodeQL** ist besonders sinnvoll: Das Projekt ist netzwerkexponiert (MQTT + HTTP).

## 7. Branch- und Commit-Konventionen

| Aspekt | Konvention |
|---|---|
| Branches | `main` (protected), `feature/…`, `fix/…`, `release/…` |
| Commits | [Conventional Commits](https://www.conventionalcommits.org/) |
| PR-Titel | `type(scope): description` |
| Merge | Squash-Merge in `main` |
| Versionierung | [SemVer](https://semver.org/) – `MAJOR.MINOR.PATCH` |

### Conventional Commits

````
feat(mqtt): MQTTnet 5 mit TLS-Unterstützung
fix(bacnet): Spin-Loop bei Serial-Fehler behoben (Befund A8)
docs(spec): Health-Konzept dokumentiert
refactor(domain): Item-Katalog deklarativ umgestellt
test(contract): Topic-Kompatibilität gegen docs/topics.json
build(docker): Multi-Stage-Build mit tini
chore(deps): MQTTnet auf 5.2.0.1603 aktualisiert
````

## 8. Schutzregeln für `main`

| Regel | Einstellung |
|---|---|
| Status-Checks | `build-test`, `Angular Build`, `CodeQL` müssen grün sein |
| Branch-Schutz | Push auf `main` nur per PR |
| Lösch-Schutz | Branch kann nicht gelöscht werden |
| Force-Push | verboten |
| Konfliktlösung | nur über „Rebase and merge" oder „Squash and merge" |

## 9. Release-Notes

`CHANGELOG.md` im Keep-a-Changelog-Format:

````markdown
# Changelog

## [1.1.0] - 2026-11-15

### Added
- Home-Assistant-MQTT-Discovery für alle 127 Items
- Neustart aus Home Assistant (Button) und aus der Web-UI
- Health-/Status-Topics mit LWT

### Fixed
- Kulturabhängige Zahlen in MQTT-Payloads (Befund A5)
- Enum-Schreibvorgänge mit ungültiger Payload (Befund A6)
- Kein `Connected=1` nach MQTT-Reconnect (Befund A10)

### Changed
- MQTT-Client: M2Mqtt → MQTTnet 5
- .NET Framework 4.5.2 → .NET 10
````

## 10. Benötigte GitHub-Secrets

| Secret | Zweck |
|---|---|
| `GITHUB_TOKEN` (automatisch) | Push nach GHCR |
| `PAT_GHCR` *(optional)* | falls ein separates Paket-Repo genutzt wird |

> Keine Broker-Credentials in GitHub Secrets – die gehören in die `.env` auf dem NAS.
