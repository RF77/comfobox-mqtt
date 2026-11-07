param(
    [string]$Image = 'ghcr.io/rf77/comfobox-gateway',
    [string]$Tag = '1.0.2'
)

$ErrorActionPreference = 'Stop'
$out = New-Object System.Collections.Generic.List[string]

# Anonymer Token (genau so, wie die NAS es macht)
$tok = (Invoke-RestMethod "https://ghcr.io/token?scope=repository:rf77/comfobox-gateway:pull&service=ghcr.io").token
$out.Add("Token: anonym geholt: $($null -ne $tok)")

$accept = 'application/vnd.oci.image.index.v1+json,application/vnd.docker.distribution.manifest.list.v2+json,application/vnd.docker.distribution.manifest.v2+json'

$m = Invoke-RestMethod -Uri "https://ghcr.io/v2/rf77/comfobox-gateway/manifests/$Tag" `
    -Headers @{ Authorization = "Bearer $tok"; Accept = $accept }
$out.Add("mediaType: $($m.mediaType)")
$out.Add("manifests: $($m.manifests.Count)")
foreach ($man in $m.manifests) {
    $p = $man.platform
    if ($p.os -eq 'unknown') { continue }
    $out.Add("  os=$($p.os) arch=$($p.architecture) variant=$($p.variant)  digest=$($man.digest)")
}

$out.Add('')
$out.Add('--- Anonymes Manifest-HEAD (HTTP-Status) ---')
$code = curl.exe -s -o NUL -w '%{http_code}' -H "Authorization: Bearer $tok" -H "Accept: $accept" `
    "https://ghcr.io/v2/rf77/comfobox-gateway/manifests/$Tag"
$out.Add("  HTTP $code")

$out | Out-File (Join-Path $env:TEMP 'manifest2.txt') -Encoding utf8
Write-Host 'geschrieben'
