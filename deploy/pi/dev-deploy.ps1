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

    Damit lässt sich prüfen, ob CH340 und ComfoBox überhaupt sprechen,
    bevor die Container-Konfiguration stimmt.

.PARAMETER Pi
    Adresse oder Name des Pi, z. B. `192.168.1.50` oder `raspberrypi.local`.

.PARAMETER User
    Der SSH-Benutzer (Vorgabe: `pi`).

.PARAMETER Port
    Port der Web-Oberfläche auf dem Pi (Vorgabe: 8099).

.PARAMETER SkipWeb
    Die Angular-Oberfläche nicht neu bauen (dann bleibt die alte Anzeige,
    die API ist aktuell).

.PARAMETER NoBuild
    Den letzten Stand erneut kopieren, ohne neu zu bauen.

.PARAMETER Logs
    Nur das Protokoll des laufenden Prozesses anzeigen.

.PARAMETER Stop
    Den Prozess auf dem Pi beenden.

.EXAMPLE
    pwsh -File deploy/pi/dev-deploy.ps1 -Pi 192.168.1.50

.EXAMPLE
    pwsh -File deploy/pi/dev-deploy.ps1 -Pi 192.168.1.50 -NoBuild -Logs
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
$publishDir = Join-Path $PSScriptRoot '.artifacts\publish'
$archivePath = Join-Path $PSScriptRoot '.artifacts\comfobox-dev.tar.gz'
$remote = "$User@$Pi"
$sshArgs = @('-p', $SshPort, '-o', 'ConnectTimeout=10', '-o', 'StrictHostKeyChecking=accept-new')

function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Invoke-Remote([string]$Command) {
    & ssh @sshArgs $remote $Command

    if ($LASTEXITCODE -ne 0) {
        throw "SSH-Befehl fehlgeschlagen (Exit $LASTEXITCODE)."
    }
}

# ---------------------------------------------------------------------------
# Protokoll anzeigen
# ---------------------------------------------------------------------------
if ($Logs) {
    Write-Step 'Protokoll des Pi'
    & ssh @sshArgs $remote 'tail -n 100 "$HOME/comfobox-dev/run.log" 2>/dev/null || echo "Kein Protokoll vorhanden."'
    exit 0
}

# ---------------------------------------------------------------------------
# Beenden
# ---------------------------------------------------------------------------
if ($Stop) {
    Write-Step 'Prozess auf dem Pi beenden'

    Invoke-Remote @'
if [ -f "$HOME/comfobox-dev/app.pid" ]; then
    kill "$(cat "$HOME/comfobox-dev/app.pid")" 2>/dev/null || true
    rm -f "$HOME/comfobox-dev/app.pid"
fi
pkill -f "comfobox-dev/app/ComfoBox.Gateway" 2>/dev/null || true
echo "gestoppt"
'@

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

New-Item -ItemType Directory -Force -Path (Split-Path $archivePath) | Out-Null

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
# 3. Kopieren
# ---------------------------------------------------------------------------
Write-Step "Auf den Pi kopieren ($remote)"

Invoke-Remote 'mkdir -p "$HOME/comfobox-dev"'

& scp -P $SshPort -o ConnectTimeout=10 -o StrictHostKeyChecking=accept-new `
    $archivePath "${remote}:comfobox-dev/app.tar.gz"

if ($LASTEXITCODE -ne 0) {
    throw 'Das Paket konnte nicht kopiert werden.'
}

# ---------------------------------------------------------------------------
# 4. Entpacken und starten
# ---------------------------------------------------------------------------
Write-Step 'Auf dem Pi entpacken und starten'

$start = @'
set -e
cd "$HOME/comfobox-dev"

# Alten Prozess beenden - sonst haelt er den seriellen Port.
if [ -f app.pid ]; then
    kill "$(cat app.pid)" 2>/dev/null || true
    rm -f app.pid
fi
pkill -f "comfobox-dev/app/ComfoBox.Gateway" 2>/dev/null || true
sleep 1

rm -rf app
mkdir -p app
tar -xzf app.tar.gz -C app
rm -f app.tar.gz
chmod +x app/ComfoBox.Gateway

# Daten (config.json, Schreibzaehler, Logs) bleiben zwischen den Laeufen.
mkdir -p data/logs

if id -nG | tr ' ' '\n' | grep -qx dialout; then
    echo "  dialout: ja"
else
    echo "  WARNUNG: der Benutzer ist nicht in der Gruppe dialout - der serielle Port wird nicht aufgehen."
    echo "           Abhilfe: sudo usermod -aG dialout $(id -un)  (danach neu anmelden)"
fi

export COMFOBOX_Gateway__DataDirectory="$HOME/comfobox-dev/data"
export COMFOBOX_Logging__Directory="$HOME/comfobox-dev/data/logs"
export COMFOBOX_BACNET__Baudrate=38400
export ASPNETCORE_URLS="http://0.0.0.0:__PORT__"
export DOTNET_gcServer=0

nohup ./app/ComfoBox.Gateway > run.log 2>&1 &
echo $! > app.pid
echo "  gestartet (PID $(cat app.pid))"
'@

Invoke-Remote ($start.Replace('__PORT__', [string]$Port))

# ---------------------------------------------------------------------------
# 5. Warten, bis der Gateway antwortet
# ---------------------------------------------------------------------------
Write-Step 'Auf die Antwort des Gateway warten'

$healthy = $false
for ($i = 1; $i -le 45; $i++) {
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
    & ssh @sshArgs $remote 'tail -n 40 "$HOME/comfobox-dev/run.log"'
    exit 1
}

# ---------------------------------------------------------------------------
# 6. Zustand und Hinweise
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
Write-Host "  Ports        http://${Pi}:${Port}/api/config/serial-ports"
Write-Host "  Protokoll    pwsh -File $($MyInvocation.MyCommand.Path) -Pi $Pi -Logs"
Write-Host "  Beenden      pwsh -File $($MyInvocation.MyCommand.Path) -Pi $Pi -Stop"
Write-Host ''
Write-Host '  Der Prozess läuft als der SSH-Benutzer - kein Docker, keine Gruppe im Container.' -ForegroundColor DarkGray
