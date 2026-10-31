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
   └─▶ (nur bei Tag) docker-publish
         ├─ buildx build (linux/amd64)
         ├─ Trivy-Scan
         ├─ push → ghcr.io
         └─ GitHub Release erstellen
```

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
    strategy:
      matrix:
        include:
          - name: gateway
            context: ./src/ComfoBox.Gateway
            dockerfile: ./src/ComfoBox.Gateway/Dockerfile
          - name: web
            context: ./src/ComfoBox.Web
            dockerfile: ./src/ComfoBox.Web/Dockerfile
    steps:
      - uses: actions/checkout@v4

      - uses: docker/setup-buildx-action@v3

      - uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAMESPACE }}/comfobox-${{ matrix.name }}
          tags: |
            type=ref,event=tag
            type=raw,value=latest,enable=${{ startsWith(github.ref, 'refs/heads/main') }}

      - uses: docker/build-push-action@v6
        with:
          context: ${{ matrix.context }}
          file: ${{ matrix.dockerfile }}
          push: true
          platforms: linux/amd64
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha,scope=${{ matrix.name }}
          cache-to: type=gha,mode=max,scope=${{ matrix.name }}

      - name: Trivy scan
        uses: aquasecurity/trivy-action@0.28.0
        with:
          image-ref: ${{ env.REGISTRY }}/${{ env.IMAGE_NAMESPACE }}/comfobox-${{ matrix.name }}:${{ github.ref_name }}
          severity: CRITICAL,HIGH
          exit-code: '1'
          ignore-unfixed: true
````

> **Nur `linux/amd64`:** Die DS923+ ist x86_64. `linux/arm64` würde die Build-Zeit verdoppeln,
> ohne Nutzen. Bei Bedarf später ergänzen (`platforms: linux/amd64,linux/arm64`).

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
