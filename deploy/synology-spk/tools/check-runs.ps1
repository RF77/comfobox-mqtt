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
$now = [DateTimeOffset]::UtcNow

$runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/workflows/$Workflow/runs?branch=$Branch&per_page=8" -Headers $auth
$out.Add("Jetzt: $($now.ToString('HH:mm:ss')) UTC")
$out.Add('')

foreach ($r in $runs.workflow_runs) {
    # WICHTIG: Die GitHub-API liefert UTC. Ohne Angabe der Zeitzone
    # interpretiert [DateTimeOffset]::Parse die Zeit als **lokal** - die
    # Dauer waere dann um die Zeitzonendifferenz daneben (im November
    # eine Stunde, im Sommer zwei).
    $start = if ($r.run_started_at) {
        [DateTimeOffset]::Parse($r.run_started_at, [cultureinfo]::InvariantCulture,
                                [System.Globalization.DateTimeStyles]::AdjustToUniversal)
    } else { $now }
    $dur = [int]($now - $start).TotalMinutes
    $out.Add("ID $($r.id)  $($r.status)/$($r.conclusion)  Start=$($r.run_started_at) UTC  vor ${dur} min  SHA=$($r.head_sha.Substring(0,7))")
}
$out.Add('')

# Der neueste Lauf im Detail
$latest = $runs.workflow_runs | Sort-Object { [DateTimeOffset]::Parse($_.run_started_at) } -Descending | Select-Object -First 1
if ($latest) {
    $out.Add("=== Neuester Lauf: $($latest.id) ===")
    $jobs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs/$($latest.id)/jobs" -Headers $auth
    foreach ($j in $jobs.jobs) {
        $out.Add("Job: $($j.name) -> $($j.status)/$($j.conclusion)")
        foreach ($s in $j.steps) { $out.Add("   $($s.number). $($s.name): $($s.status)/$($s.conclusion)") }
    }
}

$out | Out-File (Join-Path $env:TEMP 'runs.txt') -Encoding utf8
Write-Host 'geschrieben'
