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

$runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/workflows/$Workflow/runs?branch=$Branch&per_page=3" -Headers $auth
foreach ($r in $runs.workflow_runs) {
    $out.Add("=== Lauf $($r.id) - $($r.status) / $($r.conclusion) - $($r.created_at) - event=$($r.event) ===")
    $out.Add("    SHA: $($r.head_sha)")
    $out.Add("    URL: $($r.html_url)")

    $jobs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs/$($r.id)/jobs" -Headers $auth
    foreach ($j in $jobs.jobs) {
        $out.Add("    Job: $($j.name) -> $($j.status)/$($j.conclusion) (Start: $($j.started_at))")
        foreach ($s in $j.steps) {
            $out.Add("        $($s.number). $($s.name): $($s.status)/$($s.conclusion)")
        }
    }
    $out.Add('')
}

$out | Out-File (Join-Path $env:TEMP 'job-status.txt') -Encoding utf8
Write-Host 'geschrieben'
