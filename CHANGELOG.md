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
- Phase 0: Generator `tools/generate-topics/generate-topics.mjs` – reproduziert
  `docs/topics.json` byteidentisch (127 Items) und prüft sie in CI
- Phase 1: `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` – 1:1-Port des Legacy-Codecs
  (Fremdcode, MIT; Herkunft und Änderungen in `Bacnet/HEADER-LIZENZ.txt`)
- Phase 1: Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  mit 25 Frames, erzeugt vom Legacy-Codec über `tools/port-bacnet/oracle/`
- Phase 1: `ComfoBox.Bacnet.Diagnostics.BacnetLog` – `ILogger`-Fassade für den
  statischen Fremdcode (ersetzt `System.Diagnostics.Trace`, Befund A16)
- Phase 1: `tools/port-bacnet/port-bacnet-base.mjs` – mechanischer, idempotenter Port
- Phase 1: `ComfoBox.Bacnet.BacnetClient` – **async** BACnet-Client
  (`ReadPropertyAsync`, `WritePropertyAsync`, `WhoIsAsync`, `IamReceived`-Ereignis)
- Phase 1: typisierte BACnet-Fehler (`BacnetTimeoutException`, `BacnetDeviceErrorException`,
  `BacnetDeviceAbortException`, `BacnetDecodeException`) statt `Exception("…")`
- Phase 1: `ComfoBox.Bacnet.BacnetMstpTransport` – MS/TP-Transport mit Port-Verwaltung,
  Reconnect und exponentiellem Backoff (5 s → 30 s)
- Phase 1: `ComfoBox.Bacnet.Serial` – schmale Port-Schnittstelle (`ISerialPort`,
  `ISerialPortFactory`, `SerialPortAdapter`) als Testnaht
- Phase 1: `MstpStateMachine` – 1:1-Port des Legacy-MS/TP-Automaten
- Phase 1: 19 neue Tests (MS/TP-Framing, Serial-Verhalten inkl. A8-Regression,
  Client-Request/Antwort/Fehler) → 45 Tests grün

### Changed
- CI: `dotnet` läuft jetzt in `src/` – dadurch greift `global.json` (SDK ist gepinnt)
- CI: der Job „Legacy-Projekte (net452) kompilieren weiterhin" wurde durch eine
  Archiv-Prüfung ersetzt – `src_old/` wird **nicht** gebaut (Projektregel §5)
- Phase 1: der BACnet-Codec enthält kein BACnet/IP (`BVLC`) und kein PTP mehr –
  der Stack läuft ausschließlich über MS/TP
- Phase 1: der neue BACnet-Code nutzt durchgängig `ILogger` statt `Trace`/`Console`

### Fixed
- Phase 1: **Befund A8** – ein Serial-Portfehler löste im Legacy einen
  Tight-Spin-Loop mit 100 % CPU aus. Der neue Zustandsautomat endet bei einem
  Portfehler, der Transport verbindet sich mit Backoff neu (5 s → 30 s)
- Phase 1: **Befund A19** – die Sende-Warteschlange des Legacy war eine
  ungeschützte `LinkedList`, auf die Aufrufer und Zustandsautomat gleichzeitig
  zugriffen. Jetzt `ConcurrentQueue`
- Phase 1: **Befund A23** – der MS/TP-Thread läuft mit `ThreadPriority.Highest`.
  Der neue Transport nutzt `TaskCreationOptions.LongRunning` (Standardpriorität)
- Phase 1: die Invoke-ID Vergabe im Client war nicht threadsicher (`m_invoke_id++`)
  → jetzt `Interlocked`

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
