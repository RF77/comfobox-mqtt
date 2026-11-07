param(
    [string]$Tag = 'v1.0.1',
    [string]$SpkName = 'ComfoBoxGateway_noarch-dsm7_1.0.1-0007.spk',
    [string]$ExpectedMd5 = '360d76387e0b0b5188336e0110aab666'
)

$ErrorActionPreference = 'Stop'
$tmp = Join-Path $env:TEMP 'spk-check'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$base = "https://github.com/RF77/comfobox-mqtt/releases/download/$Tag"

$out = New-Object System.Collections.Generic.List[string]

# --- Paketquelle -----------------------------------------------------------
$pkgPath = Join-Path $tmp 'packages.json'
Invoke-WebRequest -Uri "$base/packages.json" -OutFile $pkgPath -UseBasicParsing
$out.Add("packages.json: $((Get-Item $pkgPath).Length) B")

# --- SPK -------------------------------------------------------------------
$spkPath = Join-Path $tmp 'pkg.spk'
Invoke-WebRequest -Uri "$base/$SpkName" -OutFile $spkPath -UseBasicParsing
$md5 = (Get-FileHash $spkPath -Algorithm MD5).Hash.ToLower()
$sha = (Get-FileHash $spkPath -Algorithm SHA256).Hash.ToLower()
$out.Add("SPK: $((Get-Item $spkPath).Length) B")
$out.Add("SPK-MD5    : $md5")
$out.Add("erwartet   : $ExpectedMd5")
$out.Add("MATCH      : $($md5 -eq $ExpectedMd5)")
$out.Add("SPK-SHA256 : $sha")
$out.Add('')
$out.Add('--- packages.json (Release) ---')
$out.Add((Get-Content $pkgPath -Raw))
$out.Add('')
$out.Add('--- conf/ und scripts/ im SPK ---')

$listing = & 'C:\Program Files\Git\bin\bash.exe' -c "cd '$($tmp -replace '\\','/')' && tar tf pkg.spk"
foreach ($l in $listing) { if ($l -match '^(conf/|scripts/|package\.tgz|INFO|PACKAGE_ICON)') { $out.Add("  $l") } }

$out | Out-File (Join-Path $tmp 'result.txt') -Encoding utf8
Write-Host 'geschrieben'
