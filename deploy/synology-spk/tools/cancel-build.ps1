param(
    [string]$Repo = 'RF77/comfobox-mqtt',
    [long]$RunId = 0
)

$ErrorActionPreference = 'Stop'

$credInput = "protocol=https`nhost=github.com`n`n"
$credOut = $credInput | git credential fill 2>$null
$tokenLine = $credOut | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
if (-not $tokenLine) { throw 'Kein GitHub-Token im Credential-Store gefunden.' }
$token = $tokenLine.Substring(9)
$auth = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json';
           'X-GitHub-Api-Version' = '2022-11-28' }

if ($RunId -eq 0) {
    $runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/workflows/docker-publish.yml/runs?per_page=1" -Headers $auth
    $RunId = $runs.workflow_runs[0].id
}

Write-Host "Breche Lauf $RunId ab ..."
try {
    Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$Repo/actions/runs/$RunId/cancel" -Headers $auth | Out-Null
    Write-Host "Abbruch angefordert."
}
catch {
    Write-Host "FEHLER: $($_.Exception.Message)"
}

Start-Sleep -Seconds 4
$run = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs/$RunId" -Headers $auth
Write-Host "Status: $($run.status) / $($run.conclusion)"
