# Changelog

Alle wesentlichen Änderungen an diesem Projekt.

Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung an [Semantic Versioning](https://semver.org/lang/de/).

## [Unreleased]

### Added
- Phase 0: `src/global.json` (SDK-Pinning auf 10.0.400)
- Phase 0: `src/Directory.Packages.props` (Central Package Management)
- Phase 0: `src/Directory.Build.props` (Nullable, `TreatWarningsAsErrors`, C# 14, Lock-Dateien)
- Phase 0: `src/ComfoBox.sln` mit sieben SDK-Projekten
  (`ComfoBox.Domain`, `ComfoBox.Bacnet`, `ComfoBox.Gateway` + vier Testprojekte)
- Phase 0: CI-Workflow `.github/workflows/build-test.yml` (Build, Test, Coverage, NuGet-Vulnerability-Scan)

### Changed
- CI: `dotnet` läuft jetzt in `src/` – dadurch greift `global.json` (SDK ist gepinnt)
- CI: der Job „Legacy-Projekte (net452) kompilieren weiterhin" wurde durch eine
  Archiv-Prüfung ersetzt – `src_old/` wird **nicht** gebaut (Projektregel §5)

### Fixed
- keine

## [0.1.0] – 2026-10-31

### Added
- Vollständige Spezifikation (`docs/02-modernisierung/`)
- 11 Entscheidungsprotokolle (ADR-001 … ADR-011)
- Aufgaben-Dokument `docs/06-aufgaben/` mit Phasen 0–8d
- Benutzerdokumentation (`docs/benutzer/`) mit Build-Skript
- Home-Assistant-Integration (HACS) und HA-Add-on-Manifest
- Synology-SPK-Paket mit Lifecycle-Skripten und host-seitigem Supervisor

### Changed
- Legacy-Bestand von `src/` nach `src_old/` verschoben (Archiv, wird nicht gebaut)

[Unreleased]: https://github.com/example/comfobox-mqtt/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/example/comfobox-mqtt/releases/tag/v0.1.0
