param(
    [string]$Repo = 'RF77/comfobox-mqtt',
    [string]$Workflow = 'docker-publish.yml',
    [string]$Branch = 'Upgrade'
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

$runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/workflows/$Workflow/runs?branch=$Branch&per_page=5" -Headers $auth
foreach ($r in $runs.workflow_runs) {
    $out.Add(("Lauf {0,-10} {1,-12} {2}  {3}" -f $r.status, $r.conclusion, $r.head_sha.Substring(0, 7), $r.created_at))
}

# Ist das Image 1.0.2 in der Registry?
$out.Add('')
$out.Add('--- Image-Tags in GHCR ---')
try {
    $tok = (Invoke-RestMethod 'https://ghcr.io/token?scope=repository:rf77/comfobox-gateway:pull&service=ghcr.io').token
    $tags = Invoke-RestMethod -Uri 'https://ghcr.io/v2/rf77/comfobox-gateway/tags/list' -Headers @{ Authorization = "Bearer $tok" }
    $out.Add(($tags.tags -join ', '))
}
catch {
    $out.Add("Registry-Abfrage fehlgeschlagen: $($_.Exception.Message)")
}

$out | Out-File (Join-Path $env:TEMP 'build-status.txt') -Encoding utf8
Write-Host 'geschrieben'
