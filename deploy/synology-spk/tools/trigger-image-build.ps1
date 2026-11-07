param(
    [string]$Repo = 'RF77/comfobox-mqtt',
    [string]$Workflow = 'docker-publish.yml',
    [string]$Ref = 'Upgrade',
    [string]$ImageTag = '1.0.2'
)

$ErrorActionPreference = 'Stop'

# --- Token holen (nie ausgeben) ---------------------------------------------
$credInput = "protocol=https`nhost=github.com`n`n"
$credOut = $credInput | git credential fill 2>$null
$tokenLine = $credOut | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
if (-not $tokenLine) { throw 'Kein GitHub-Token im Credential-Store gefunden.' }
$token = $tokenLine.Substring(9)
$auth = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json';
           'X-GitHub-Api-Version' = '2022-11-28' }

# --- Workflow-Dispatch ausloesen --------------------------------------------
$body = @{ ref = $Ref; inputs = @{ tag = $ImageTag } } | ConvertTo-Json -Depth 5
$uri = "https://api.github.com/repos/$Repo/actions/workflows/$Workflow/dispatches"

try {
    Invoke-RestMethod -Method Post -Uri $uri -Headers $auth -Body $body -ContentType 'application/json'
    Write-Host "Image-Build fuer Tag '$ImageTag' angestossen (Branch $Ref)."
}
catch {
    Write-Host "FEHLER: $($_.Exception.Message)"
    throw
}

# --- Status anzeigen --------------------------------------------------------
Start-Sleep -Seconds 5
$runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/workflows/$Workflow/runs?branch=$Ref&per_page=3" -Headers $auth
foreach ($r in $runs.workflow_runs) {
    Write-Host ("  {0,-10} {1,-22} {2}" -f $r.status, $r.conclusion, $r.head_sha.Substring(0, 7))
}
