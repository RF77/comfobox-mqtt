# ---------------------------------------------------------------------------
# upload-release-assets.ps1 – hängt SPK + packages.json an ein GitHub-Release
#
# Aufruf:  pwsh -File upload-release-assets.ps1 -Tag v1.0.1 -Spk <pfad> -PackagesJson <pfad> -Delete <name>
#
# Der Token kommt aus dem Git-Credential-Store (git credential fill) und
# wird **nie** ausgegeben.
# ---------------------------------------------------------------------------
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$Spk,
    [Parameter(Mandatory = $true)][string]$PackagesJson,
    [string]$Diagnose = '',
    [string]$Delete = ''
)

$ErrorActionPreference = 'Stop'
$repo = 'RF77/comfobox-mqtt'

# --- Token holen (nie ausgeben) ---------------------------------------------
$credInput = "protocol=https`nhost=github.com`n`n"
$credOut = $credInput | git credential fill 2>$null
$tokenLine = $credOut | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
if (-not $tokenLine) { throw 'Kein GitHub-Token im Credential-Store gefunden.' }
$token = $tokenLine.Substring(9)
$auth = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json';
           'X-GitHub-Api-Version' = '2022-11-28' }

# --- Release finden ---------------------------------------------------------
$rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$Tag" -Headers $auth
Write-Host "Release: $($rel.name) (id $($rel.id), $($rel.tag_name))"

# --- Altes Asset loeschen ---------------------------------------------------
if ($Delete) {
    $old = $rel.assets | Where-Object { $_.name -eq $Delete }
    foreach ($a in $old) {
        Invoke-RestMethod -Method Delete -Uri "https://api.github.com/repos/$repo/releases/assets/$($a.id)" -Headers $auth
        Write-Host "Geloescht: $($a.name)"
    }
}

# --- Asset ersetzen (gleicher Name -> erst loeschen) ------------------------
function Upload-Asset {
    param([string]$Path, [string]$Name)

    $existing = $rel.assets | Where-Object { $_.name -eq $Name }
    foreach ($a in $existing) {
        Invoke-RestMethod -Method Delete -Uri "https://api.github.com/repos/$repo/releases/assets/$($a.id)" -Headers $auth
        Write-Host "Ersetzt: $Name"
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $contentType = 'application/octet-stream'
    if ($Name -like '*.json') { $contentType = 'application/json' }
    if ($Name -like '*.sh')   { $contentType = 'text/x-shellscript' }

    $uri = "https://uploads.github.com/repos/$repo/releases/$($rel.id)/assets?name=$([uri]::EscapeDataString($Name))"
    Invoke-RestMethod -Method Post -Uri $uri -Headers $auth -ContentType $contentType -Body $bytes | Out-Null
    Write-Host "Hochgeladen: $Name ($($bytes.Length) B)"
}

Upload-Asset -Path $PackagesJson -Name 'packages.json'
Upload-Asset -Path $Spk -Name (Split-Path -Leaf $Spk)
if ($Diagnose) {
    Upload-Asset -Path $Diagnose -Name 'dsm-diagnose.sh'
}

# --- Kontrolle --------------------------------------------------------------
$rel2 = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$Tag" -Headers $auth
Write-Host ''
Write-Host '--- Assets jetzt ---'
foreach ($a in $rel2.assets) {
    Write-Host ("  {0,-55} {1,8} B  {2}" -f $a.name, $a.size, $a.state)
}
