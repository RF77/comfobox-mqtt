param(
    [string]$Repo = 'RF77/comfobox-mqtt',
    [string]$Tag = 'v1.0.2',
    [string]$Name = 'ComfoBox Gateway 1.0.2'
)

$ErrorActionPreference = 'Stop'

$credInput = "protocol=https`nhost=github.com`n`n"
$credOut = $credInput | git credential fill 2>$null
$tokenLine = $credOut | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
if (-not $tokenLine) { throw 'Kein GitHub-Token im Credential-Store gefunden.' }
$token = $tokenLine.Substring(9)
$auth = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json';
           'X-GitHub-Api-Version' = '2022-11-28' }

$out = New-Object System.Collections.Generic.List[string]

# Existiert das Release schon?
$exists = $false
try {
    $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/tags/$Tag" -Headers $auth
    $exists = $true
    $out.Add("Release $Tag existiert bereits (ID $($rel.id), $($rel.assets.Count) Assets).")
}
catch {
    $out.Add("Release $Tag existiert noch nicht - es wird angelegt.")
}

if (-not $exists) {
    $body = @{
        tag_name         = $Tag
        name             = $Name
        body             = "MQTT-Gateway fuer die Zehnder ComfoBox Series 5 (RS485/BACnet MSTP) mit Web-Oberflaeche.`n`nSiehe [CHANGELOG.md](https://github.com/$Repo/blob/$Tag/CHANGELOG.md)."
        draft            = $false
        prerelease       = $false
    } | ConvertTo-Json -Depth 5

    $rel = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$Repo/releases" `
        -Headers $auth -Body $body -ContentType 'application/json'
    $out.Add("Release angelegt: $Tag (ID $($rel.id))")
    Start-Sleep -Seconds 2
}

$out | Out-File (Join-Path $env:TEMP 'release.txt') -Encoding utf8
Write-Host 'geschrieben'
