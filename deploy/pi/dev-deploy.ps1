#Requires -Version 7.0
<#
.SYNOPSIS
    Schiebt den Gateway als eigenständiges Programm auf einen Raspberry Pi.

.DESCRIPTION
    Gedacht für die Entwicklung: kein Docker, kein Registry-Pull, kein
    Release. Der Gateway wird für linux-arm64 veröffentlicht, per SSH kopiert
    und direkt gestartet.

    Warum das der schnellste Weg ist:
      * kein arm64-Container-Build (der läuft sonst unter QEMU und dauert
        eine halbe Stunde),
      * kein Image-Push und kein Pull auf dem Pi,
      * der Prozess läuft als der SSH-Benutzer - ist der in `dialout`,
        funktioniert der serielle Port ohne jede Container-Gruppe.

    **Ein Passwort pro Verbindung.** Es sind genau zwei Verbindungen: ein
    `scp` (Paket + Startskript) und ein `ssh` (Skript ausführen). Mit einem
    hinterlegten Schlüssel entfällt auch das:
        ssh-copy-id -p 22 pi@<adresse>

.PARAMETER Pi
    Adresse oder Name des Pi, z. B. `10.10.1.20` oder `raspberrypi.local`.

.PARAMETER User
    Der SSH-Benutzer (Vorgabe: `pi`).

.PARAMETER SshPort
    Der SSH-Port (Vorgabe: 22).

.PARAMETER Port
    Port der Web-Oberfläche auf dem Pi (Vorgabe: 8099).

.PARAMETER SkipWeb
    Die Angular-Oberfläche nicht neu bauen (die API wird trotzdem aktuell).

.PARAMETER NoBuild
    Den letzten Stand erneut kopieren, ohne neu zu bauen.

.PARAMETER Logs
    Nur das Protokoll des laufenden Prozesses anzeigen.

.PARAMETER Stop
    Den Prozess auf dem Pi beenden.

.EXAMPLE
    pwsh -File deploy/pi/dev-deploy.ps1 -Pi 10.10.1.20

.EXAMPLE
    pwsh -File deploy/pi/dev-deploy.ps1 -Pi 10.10.1.20 -SkipWeb -NoBuild

.EXAMPLE
    pwsh -File deploy/pi/dev-deploy.ps1 -Pi 10.10.1.20 -Logs
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Pi,

    [string]$User = 'pi',

    [int]$SshPort = 22,

    [int]$Port = 8099,

    [switch]$SkipWeb,

    [switch]$NoBuild,

    [switch]$Logs,

    [switch]$Stop
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$artifactsDir = Join-Path $PSScriptRoot '.artifacts'
$publishDir = Join-Path $artifactsDir 'publish'
$archivePath = Join-Path $artifactsDir 'comfobox-dev.tar.gz'
$startScriptPath = Join-Path $artifactsDir 'comfobox-dev-start.sh'
$remoteScriptName = 'comfobox-dev-start.sh'

$remote = "$User@$Pi"
$sshArgs = @('-p', $SshPort, '-o', 'ConnectTimeout=10', '-o', 'StrictHostKeyChecking=accept-new')
$scpArgs = @('-P', $SshPort, '-o', 'ConnectTimeout=10', '-o', 'StrictHostKeyChecking=accept-new')

function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host "==> $Text" -ForegroundColor Cyan
}

<#
    Führt ein **einzeiliges** Kommando auf dem Pi aus und gibt den Exit-Code
    zurück.

    Bewusst kein mehrzeiliges Kommando: PowerShell übergibt Argumente an
    `ssh.exe` nach den Windows-Regeln und schützt eingebettete
    Anführungszeichen nicht. Ein mehrzeiliges Skript kommt dadurch verstümmelt
    an - der Aufruf endet ohne jede Ausgabe mit „Exit -1". Genau deshalb
    liegt der Ablauf als Datei daneben und wird mit `sh` gestartet.
#>
function Invoke-Ssh {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command
    )

    # `| Out-Host` leitet die Ausgabe an die Konsole weiter, **ohne** sie in
    # den Ausgabestrom zu schreiben.
    #
    # Ohne das landet jede Zeile, die ssh auf stdout schreibt, im Rückgabewert:
    # `$code = Invoke-Ssh '...'` wäre dann ein Array aus Textzeilen **und** dem
    # Exit-Code. Der Vergleich `$code -ne 0` ist damit immer wahr - der Lauf
    # bricht ab, obwohl der Start geklappt hat (die Meldung enthielt den Text
    # von ssh und die 0 am Ende).
    & ssh @sshArgs $remote $Command | Out-Host

    return $LASTEXITCODE
}

# ---------------------------------------------------------------------------
# Protokoll anzeigen
# ---------------------------------------------------------------------------
if ($Logs) {
    Write-Step "Protokoll auf $Pi"

    # Einzeilig, ohne Anführungszeichen - siehe Invoke-Ssh.
    $code = Invoke-Ssh 'tail -n 120 ~/comfobox-dev/run.log'

    if ($code -ne 0) {
        Write-Host '  (noch kein Protokoll vorhanden)'
    }

    exit 0
}

# ---------------------------------------------------------------------------
# Beenden
# ---------------------------------------------------------------------------
if ($Stop) {
    Write-Step "Prozess auf $Pi beenden"

    $code = Invoke-Ssh 'if [ -f ~/comfobox-dev-start.sh ]; then sh ~/comfobox-dev-start.sh stop; else pkill -f comfobox-dev/app/ComfoBox.Gateway || true; fi'

    if ($code -ne 0) {
        Write-Warning "Das Beenden meldete Exit $code - bitte mit 'ps aux | grep ComfoBox' prüfen."
    }

    exit 0
}

# ---------------------------------------------------------------------------
# 1. Bauen
# ---------------------------------------------------------------------------
if (-not $NoBuild) {
    # Die Oberfläche zuerst: das Gateway-Projekt nimmt sie beim Publish mit.
    if (-not $SkipWeb) {
        $webDir = Join-Path $repoRoot 'src\ComfoBox.Web'

        if (Test-Path (Join-Path $webDir 'node_modules')) {
            Write-Step 'Web-Oberfläche bauen'

            Push-Location $webDir
            try {
                & npm run build

                if ($LASTEXITCODE -ne 0) {
                    throw 'Der Angular-Build ist fehlgeschlagen.'
                }
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-Warning 'src/ComfoBox.Web/node_modules fehlt - die Oberfläche wird nicht gebaut.'
            Write-Warning 'Abhilfe: cd src/ComfoBox.Web; npm ci'
        }
    }

    Write-Step 'Gateway für linux-arm64 veröffentlichen'

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    # Ein Publish für eine bestimmte Plattform ergänzt die Lock-Dateien um
    # RID-spezifische Einträge. Das ist für den Test richtig, gehört aber
    # nicht in den Commit - die Dateien werden deshalb vorher gesichert und
    # hinterher byteweise zurückgeschrieben.
    $lockFiles = Get-ChildItem -Path (Join-Path $repoRoot 'src') -Filter 'packages.lock.json' -Recurse
    $lockContent = @{}

    foreach ($file in $lockFiles) {
        $lockContent[$file.FullName] = [System.IO.File]::ReadAllBytes($file.FullName)
    }

    try {
        & dotnet publish (Join-Path $repoRoot 'src\ComfoBox.Gateway\ComfoBox.Gateway.csproj') `
            --configuration Release `
            --runtime linux-arm64 `
            --self-contained true `
            --output $publishDir `
            --nologo `
            -p:Version=1.0.4-dev

        if ($LASTEXITCODE -ne 0) {
            throw 'dotnet publish ist fehlgeschlagen.'
        }
    }
    finally {
        foreach ($entry in $lockContent.GetEnumerator()) {
            [System.IO.File]::WriteAllBytes($entry.Key, $entry.Value)
        }
    }
}
elseif (-not (Test-Path $publishDir)) {
    throw "Es gibt keinen veröffentlichten Stand unter '$publishDir'. Ohne -NoBuild bauen."
}

# ---------------------------------------------------------------------------
# 2. Packen
# ---------------------------------------------------------------------------
Write-Step 'Paket schnüren'

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

& tar -czf $archivePath -C $publishDir .

if ($LASTEXITCODE -ne 0) {
    throw 'Das Paket konnte nicht erstellt werden.'
}

$size = [math]::Round((Get-Item $archivePath).Length / 1MB, 1)
Write-Host "  $size MB"

# ---------------------------------------------------------------------------
# 3. Startskript schreiben
# ---------------------------------------------------------------------------
Write-Step 'Startskript schreiben'

# Das Skript liegt als Datei neben dem Paket und wird mit `sh` gestartet.
# Damit gibt es kein mehrzeiliges SSH-Kommando (siehe Invoke-Ssh) - und der
# Ablauf lässt sich auf dem Pi mit `cat ~/comfobox-dev-start.sh` ansehen.
$startScript = @'
#!/bin/sh
# ComfoBox Gateway - Testlauf auf dem Pi.
# Erzeugt von deploy/pi/dev-deploy.ps1 auf dem Entwicklungsrechner.
#
#   sh ~/comfobox-dev-start.sh start [PORT]
#   sh ~/comfobox-dev-start.sh stop
#   sh ~/comfobox-dev-start.sh logs [ZEILEN]
set -e

BASE="$HOME/comfobox-dev"
APPDIR="$BASE/app"
ARCHIVE="$HOME/comfobox-dev.tar.gz"
PIDFILE="$BASE/app.pid"
LOGFILE="$BASE/run.log"
ACTION="${1:-start}"

stop_app() {
    if [ -f "$PIDFILE" ]; then
        PID=$(cat "$PIDFILE" 2>/dev/null || true)
        if [ -n "$PID" ] && kill -0 "$PID" 2>/dev/null; then
            kill "$PID" 2>/dev/null || true
            i=0
            while [ "$i" -lt 10 ] && kill -0 "$PID" 2>/dev/null; do
                sleep 1
                i=$((i + 1))
            done
            kill -0 "$PID" 2>/dev/null && kill -9 "$PID" 2>/dev/null || true
            echo "  vorheriger Prozess beendet (PID $PID)"
        fi
        rm -f "$PIDFILE"
    fi

    # Ein Prozess aus einem frueheren Lauf ohne PID-Datei.
    pkill -f "$APPDIR/ComfoBox.Gateway" 2>/dev/null || true
}

case "$ACTION" in
    stop)
        stop_app
        echo "gestoppt"
        ;;

    logs)
        LINES="${2:-120}"
        if [ -f "$LOGFILE" ]; then
            tail -n "$LINES" "$LOGFILE"
        else
            echo "Kein Protokoll vorhanden ($LOGFILE)."
        fi
        ;;

    start)
        PORT="${2:-8099}"

        if [ ! -f "$ARCHIVE" ]; then
            echo "FEHLER: $ARCHIVE fehlt - bitte erneut kopieren." >&2
            exit 2
        fi

        stop_app
        sleep 1

        rm -rf "$APPDIR"
        mkdir -p "$APPDIR" "$BASE/data/logs"
        tar -xzf "$ARCHIVE" -C "$APPDIR"
        rm -f "$ARCHIVE"
        chmod +x "$APPDIR/ComfoBox.Gateway" 2>/dev/null || true

        if id -nG 2>/dev/null | tr ' ' '\n' | grep -qx dialout; then
            echo "  Gruppe dialout: vorhanden"
        else
            echo "  WARNUNG: der Benutzer ist nicht in der Gruppe dialout."
            echo "           Der serielle Port wird so nicht aufgehen."
            echo "           Abhilfe: sudo usermod -aG dialout $(id -un); danach neu anmelden."
        fi

        export COMFOBOX_Gateway__DataDirectory="$BASE/data"
        export COMFOBOX_Logging__Directory="$BASE/data/logs"
        export COMFOBOX_BACNET__Baudrate=38400
        export ASPNETCORE_URLS="http://0.0.0.0:$PORT"
        export DOTNET_gcServer=0

        nohup "$APPDIR/ComfoBox.Gateway" > "$LOGFILE" 2>&1 &
        echo $! > "$PIDFILE"

        sleep 3

        if kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
            echo "  gestartet (PID $(cat "$PIDFILE"), Port $PORT)"
        else
            echo "  FEHLER: der Prozess ist sofort beendet worden. Protokoll:" >&2
            tail -n 30 "$LOGFILE" >&2 || true
            exit 3
        fi
        ;;

    *)
        echo "Unbekannte Aktion '$ACTION' (start, stop, logs)." >&2
        exit 2
        ;;
esac
'@

# LF-Zeilenenden und UTF-8 ohne BOM: ein \r im Skript führt auf dem Pi zu
# "Bad interpreter" - und ein BOM vor '#!/bin/sh' ebenfalls.
$startScript = $startScript -replace "`r`n", "`n"
[System.IO.File]::WriteAllText($startScriptPath, $startScript, (New-Object System.Text.UTF8Encoding $false))
Write-Host "  $startScriptPath"

# ---------------------------------------------------------------------------
# 4. Kopieren - ein scp, also eine Passwortabfrage
# ---------------------------------------------------------------------------
Write-Step "Auf den Pi kopieren ($remote)"

& scp @scpArgs $archivePath $startScriptPath "${remote}:"

if ($LASTEXITCODE -ne 0) {
    throw "Kopieren fehlgeschlagen (Exit $LASTEXITCODE)."
}

# ---------------------------------------------------------------------------
# 5. Starten - ein ssh, also eine Passwortabfrage
# ---------------------------------------------------------------------------
Write-Step 'Auf dem Pi entpacken und starten'

$startCode = Invoke-Ssh "sh ~/$remoteScriptName start $Port"

if ($startCode -ne 0) {
    Write-Host ''
    Write-Warning "Der Start ist fehlgeschlagen (Exit $startCode)."
    Write-Host '  Protokoll des Pi:' -ForegroundColor DarkGray
    $null = Invoke-Ssh 'tail -n 40 ~/comfobox-dev/run.log'
    exit 1
}

# ---------------------------------------------------------------------------
# 6. Warten, bis der Gateway antwortet
# ---------------------------------------------------------------------------
Write-Step 'Auf die Antwort des Gateway warten'

$healthy = $false
for ($i = 1; $i -le 60; $i++) {
    try {
        $response = Invoke-RestMethod -Uri "http://${Pi}:${Port}/api/health" -TimeoutSec 3
        $healthy = $true
        Write-Host "  Zustand: $($response.state) · Version $($response.version)"
        break
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $healthy) {
    Write-Warning 'Der Gateway antwortet nicht. Protokoll:'

    # Die Ausgabe erscheint direkt auf der Konsole (siehe Invoke-Ssh).
    $null = Invoke-Ssh 'tail -n 40 ~/comfobox-dev/run.log'
    exit 1
}

# ---------------------------------------------------------------------------
# 7. Zustand und Hinweise
# ---------------------------------------------------------------------------
try {
    $status = Invoke-RestMethod -Uri "http://${Pi}:${Port}/api/status" -TimeoutSec 5
    Write-Host "  BACnet verbunden: $($status.bacnet.connected) · Port: $($status.bacnet.port)"
    Write-Host "  MQTT verbunden:   $($status.mqtt.connected)"
}
catch {
    Write-Warning "Der Status konnte nicht gelesen werden: $($_.Exception.Message)"
}

Write-Host ''
Write-Host "  Oberfläche   http://${Pi}:${Port}" -ForegroundColor Green
Write-Host "  Port-Liste   http://${Pi}:${Port}/api/config/serial-ports"
Write-Host "  Protokoll    pwsh -File $($MyInvocation.MyCommand.Path) -Pi $Pi -Logs"
Write-Host "  Beenden      pwsh -File $($MyInvocation.MyCommand.Path) -Pi $Pi -Stop"
Write-Host ''
Write-Host '  Der Prozess läuft als der SSH-Benutzer - kein Docker, keine Gruppe im Container.' -ForegroundColor DarkGray
Write-Host "  Ohne Passwortabfrage:  ssh-copy-id -p $SshPort $remote" -ForegroundColor DarkGray
