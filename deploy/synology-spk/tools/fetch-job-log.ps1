param(
    [string]$Repo = 'RF77/comfobox-mqtt',
    [string]$RunId = '42424418882'
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

# Job-ID ermitteln
$runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs/$RunId" -Headers $auth
$jobs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs/$RunId/jobs" -Headers $auth
$jobId = $jobs.jobs[0].id
$out.Add("Job-ID: $jobId  ($($jobs.jobs[0].name))")
$out.Add("Laufzeit: $($jobs.jobs[0].started_at) -> $($jobs.jobs[0].completed_at)")
$out.Add('')

# Das Job-Log kommt als ZIP
$zip = Join-Path $env:TEMP 'joblog.zip'
$outFile = Join-Path $env:TEMP 'joblog'
if (Test-Path $outFile) { Remove-Item -Recurse -Force $outFile }
New-Item -ItemType Directory -Force -Path $outFile | Out-Null

try {
    Invoke-WebRequest -Uri "https://api.github.com/repos/$Repo/actions/jobs/$jobId/logs" `
        -Headers $auth -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $outFile -Force

    $logFile = Get-ChildItem -Path $outFile -Recurse -Filter '*.txt' |
        Sort-Object Length -Descending | Select-Object -First 1
    if ($logFile) {
        $out.Add("=== Log: $($logFile.FullName) ($($logFile.Length) B) ===")
        $content = Get-Content $logFile.FullName
        $out.Add("--- letzte 60 Zeilen ---")
        foreach ($line in ($content | Select-Object -Last 60)) { $out.Add([string]$line) }
        $out.Add('')
        $out.Add("--- Zeilen mit 'ERROR'/'error'/'failed' (letzte 15) ---")
        foreach ($m in ($content | Select-String -Pattern 'ERROR|error|failed|Failed' | Select-Object -Last 15)) {
            $out.Add([string]$m.Line)
        }
    }
    else {
        $out.Add('Keine Logdatei gefunden.')
        Get-ChildItem -Path $outFile -Recurse | ForEach-Object { $out.Add("  $($_.FullName)") }
    }
}
catch {
    $out.Add("FEHLER beim Abruf: $($_.Exception.Message)")
}

$out | Out-File (Join-Path $env:TEMP 'job-log.txt') -Encoding utf8
Write-Host 'geschrieben'
