$ErrorActionPreference = 'Stop'
$out = New-Object System.Collections.Generic.List[string]

$tok = (Invoke-RestMethod 'https://ghcr.io/token?scope=repository:rf77/comfobox-gateway:pull&service=ghcr.io').token
$h = 'application/vnd.oci.image.index.v1+json,application/vnd.docker.distribution.manifest.list.v2+json'
$m = Invoke-RestMethod -Uri 'https://ghcr.io/v2/rf77/comfobox-gateway/manifests/1.0.1' -Headers @{ Authorization = "Bearer $tok"; Accept = $h }

$out.Add("mediaType: $($m.mediaType)")
$out.Add("manifests: $($m.manifests.Count)")
foreach ($man in $m.manifests) {
    $p = $man.platform
    $out.Add("  os=$($p.os) arch=$($p.architecture) variant=$($p.variant)")
}

$out | Out-File (Join-Path $env:TEMP 'manifest.txt') -Encoding utf8
Write-Host 'ok'
