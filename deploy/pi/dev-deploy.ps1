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

        # Laeuft noch etwas anderes auf dem Port? Meist der Container - der
        # haelt auch den seriellen Port, dann ist der Test ohnehin sinnlos.
        if command -v ss >/dev/null 2>&1 && ss -ltn 2>/dev/null | grep -q ":$PORT "; then
            echo "FEHLER: auf Port $PORT lauscht bereits ein Prozess." >&2
            echo "        Meist der Container:" >&2
            echo "          sudo docker compose -f /opt/comfobox/compose.yaml stop" >&2
            echo "        Sonst nachsehen mit:  sudo ss -ltnp | grep :$PORT" >&2
            exit 4
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

        # Den seriellen Adapter gleich setzen. Ohne Angabe kommt der Wert aus
        # der appsettings.json ("COM3") - auf Linux oeffnet der Transport dann
        # einen Port, den es nicht gibt, und meldet das als Fehler.
        SERIAL_DEVICE=""
        for d in /dev/ttyUSB0 /dev/ttyACM0 /dev/ttyUSB1 /dev/ttyACM1; do
            if [ -e "$d" ]; then
                SERIAL_DEVICE="$d"
                break
            fi
        done

        export COMFOBOX_Gateway__DataDirectory="$BASE/data"
        export COMFOBOX_Logging__Directory="$BASE/data/logs"
        export ASPNETCORE_URLS="http://0.0.0.0:$PORT"
        export DOTNET_gcServer=0

        if [ -n "$SERIAL_DEVICE" ]; then
            export COMFOBOX_Bacnet__Port="$SERIAL_DEVICE"
            echo "  serieller Port: $SERIAL_DEVICE"
        else
            echo "  WARNUNG: kein /dev/ttyUSB* oder /dev/ttyACM* gefunden."
            echo "           Der Adapter kann im Betrieb gesteckt werden."
        fi

        nohup "$APPDIR/ComfoBox.Gateway" > "$LOGFILE" 2>&1 &
        echo $! > "$PIDFILE"
        PID=$(cat "$PIDFILE")

        # Warten, bis der Port wirklich lauscht.
        #
        # Nicht nach 3 Sekunden aufgeben und nicht blind 2 Minuten warten: auf
        # einem Pi 3 braucht ein selbst-enthaltenes .NET-Programm einige
        # Sekunden. Entscheidend ist, ob der Prozess **noch lebt** und ob der
        # Port aufgeht - beides wird hier im Sekundentakt geprueft.
        i=0
        LISTENING=0
        while [ "$i" -lt 60 ]; do
            if ! kill -0 "$PID" 2>/dev/null; then
                echo "" >&2
                echo "  FEHLER: der Prozess ist nach ${i}s beendet worden." >&2
                echo "  --- Protokoll ($LOGFILE) ---" >&2
                tail -n 40 "$LOGFILE" >&2 || true
                exit 3
            fi

            if command -v ss >/dev/null 2>&1; then
                if ss -ltn 2>/dev/null | grep -q ":$PORT "; then
                    LISTENING=1
                    break
                fi
            fi

            i=$((i + 1))
            if [ $((i % 10)) -eq 0 ]; then
                echo "  ... laeuft noch (${i}s)"
            fi
            sleep 1
        done

        if [ "$LISTENING" = "1" ]; then
            echo "  gestartet (PID $PID, Port $PORT, nach ${i}s)"

            # Zusaetzlich lokal pruefen: lauscht heisst nicht, dass der
            # Gateway auch antwortet. Und "antwortet lokal" heisst nicht,
            # dass er von aussen erreichbar ist - dazwischen kann eine
            # Firewall stehen (ufw laesst Docker-Ports durch, eigene
            # Prozesse nicht).
            if command -v curl >/dev/null 2>&1; then
                j=0
                LOCAL_OK=0
                while [ "$j" -lt 12 ]; do
                    if curl -fsS -m 1 "http://localhost:$PORT/api/health" >/dev/null 2>&1; then
                        LOCAL_OK=1
                        break
                    fi
                    j=$((j + 1))
                    sleep 1
                done

                if [ "$LOCAL_OK" = "1" ]; then
                    echo "  antwortet lokal: http://localhost:$PORT/api/health"
                else
                    echo "  WARNUNG: Port offen, aber /api/health antwortet lokal nicht." >&2
                    echo "  --- Protokoll ($LOGFILE) ---" >&2
                    tail -n 25 "$LOGFILE" >&2 || true
                fi
            fi
        else
            echo "" >&2
            echo "  FEHLER: der Prozess laeuft (PID $PID), aber Port $PORT lauscht nicht." >&2
            echo "  --- Protokoll ($LOGFILE) ---" >&2
            tail -n 40 "$LOGFILE" >&2 || true
            exit 5
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

    switch ($startCode) {
        2 { Write-Warning 'Das Paket fehlt auf dem Pi - bitte ohne -NoBuild erneut starten.' }
        4 { Write-Warning "Auf Port $Port lauscht bereits ein Prozess - meist der Container. " +
                          'Abhilfe: sudo docker compose -f /opt/comfobox/compose.yaml stop' }
        3 { Write-Warning 'Der Prozess ist beim Start beendet worden - das Protokoll steht oben.' }
        5 { Write-Warning "Der Prozess laeuft, aber Port $Port lauscht nicht - das Protokoll steht oben." }
        default { Write-Warning "Der Start ist fehlgeschlagen (Exit $startCode)." }
    }

    exit 1
}

# ---------------------------------------------------------------------------
# 6. Warten, bis der Gateway antwortet
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
        if ($i % 5 -eq 0) {
            Write-Host "  ... noch keine Antwort ($(($i - 1) * 2) s)" -ForegroundColor DarkGray
        }

        Start-Sleep -Seconds 2
    }
}

if (-not $healthy) {
    Write-Host ''
    Write-Warning 'Der Gateway antwortet nicht - hier die Fakten vom Pi:'
    Write-Host ''

    # Einzeilig, damit die Argumentübergabe an ssh.exe nichts zerlegt.
    $null = Invoke-Ssh ("echo '--- Prozess ---'; ps -o pid,etime,rss,cmd -C ComfoBox.Gateway 2>/dev/null || pgrep -a -f comfobox-dev; " +
        "echo '--- Port $Port ---'; (ss -ltnp 2>/dev/null || netstat -ltnp 2>/dev/null) | grep ':$Port' || echo 'lauscht NICHT'; " +
        "echo '--- lokal ---'; curl -s -m 3 -o /dev/null -w 'HTTP %{http_code}\n' http://localhost:$Port/api/health || echo 'keine Antwort'; " +
        "echo '--- Protokoll ---'; tail -n 25 ~/comfobox-dev/run.log 2>/dev/null || echo 'kein Protokoll'")

    Write-Host ''
    Write-Host '  Ursachen, die dazu passen:' -ForegroundColor DarkGray
    Write-Host "    * 'lauscht NICHT'           -> Start haengt oder ist zu langsam; Protokoll ansehen" -ForegroundColor DarkGray
    Write-Host "    * Prozess fehlt             -> Protokoll zeigt die Ausnahme (z. B. ICU, Rechte)" -ForegroundColor DarkGray
    Write-Host "    * 'Couldn't find a valid ICU package' -> sudo apt-get install -y libicu" -ForegroundColor DarkGray
    Write-Host "    * antwortet lokal, aber von hier nicht -> Firewall (ufw)" -ForegroundColor DarkGray
    Write-Host "      Abhilfe:  sudo ufw allow $Port/tcp   (kurz pruefen: sudo ufw status)" -ForegroundColor DarkGray
    Write-Host "    * fremder Prozess auf $Port -> der Container haelt den Port noch" -ForegroundColor DarkGray
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
