# Link-Checker für die Dokumentation.
# Prüft relative Datei-Links und GitHub-Style-Anker gegen die Überschriften.
# Aufruf: pwsh -File tools/check-links.ps1 [-Root <repo-root>]

param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Get-Slug([string]$heading) {
    $s = $heading.Trim().ToLowerInvariant()
    $s = $s -replace '[^\p{L}\p{Nd}\s\-_]', ''
    $s = $s -replace '\s', '-'
    return $s
}

# 1. Anker je Datei einsammeln
$anchorCache = @{}
function Get-Anchors([string]$path) {
    if ($anchorCache.ContainsKey($path)) { return $anchorCache[$path] }
    $set = @{}
    if (Test-Path $path) {
        foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
            if ($line -match '^(#{1,6})\s+(.*?)\s*$') {
                $slug = Get-Slug $Matches[2]
                if ($set.ContainsKey($slug)) {
                    $n = 1
                    while ($set.ContainsKey("$slug-$n")) { $n++ }
                    $slug = "$slug-$n"
                }
                $set[$slug] = $true
            }
            # explizite HTML-Anker (z. B. <a id="a3-…"></a> in Tabellenzeilen)
            foreach ($m in [regex]::Matches($line, '<a\s+(?:id|name)="([^"]+)"')) {
                $set[$m.Groups[1].Value] = $true
            }
        }
    }
    $anchorCache[$path] = $set
    return $set
}

# 2. Alle Markdown-Dateien
$files = Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.md' |
    Where-Object { $_.FullName -notmatch '[\\/](node_modules|bin|obj|dist|\.git)[\\/]' }

$problems = @()
$linkCount = 0

foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $rel = $file.FullName.Substring($Root.Length).TrimStart('\', '/')

    # Code-Fences ausblenden (Beispiel-Links in Codeblöcken sind kein Linkfehler)
    $content = [regex]::Replace($content, '(?ms)^(`{3,})[^\n]*\n.*?^\1\s*$', '')

    # Inline-Links [text](target) und Bilder ![alt](target)
    $matches = [regex]::Matches($content, '!?(?:\[[^\]]*\])\(\s*<?([^)\s]+)>?(?:\s+"[^"]*")?\s*\)')
    foreach ($m in $matches) {
        $target = $m.Groups[1].Value
        if ($target -match '^(https?:|mailto:|#|file:|ftp:)') {
            if ($target.StartsWith('#')) {
                $anchor = $target.Substring(1)
                $anchors = Get-Anchors $file.FullName
                if ($anchors.Count -gt 0 -and -not $anchors.ContainsKey($anchor)) {
                    $problems += "$rel : Anker '$anchor' nicht gefunden ($($file.Name))"
                }
            }
            continue
        }
        $linkCount++

        $pathPart = [System.Uri]::UnescapeDataString($target)
        $anchorPart = $null
        if ($pathPart -match '^(.*?)#(.*)$') { $pathPart = $Matches[1]; $anchorPart = $Matches[2] }

        if ([string]::IsNullOrWhiteSpace($pathPart)) {
            $anchors = Get-Anchors $file.FullName
            if ($anchors.Count -gt 0 -and -not $anchors.ContainsKey($anchorPart)) {
                $problems += "$rel : Anker '#$anchorPart' nicht gefunden (im selben Dokument)"
            }
            continue
        }

        $resolved = Join-Path $file.DirectoryName $pathPart
        if (-not (Test-Path -LiteralPath $resolved)) {
            $problems += "$rel : Datei fehlt -> $target"
            continue
        }

        if ($anchorPart) {
            $anchors = Get-Anchors (Resolve-Path -LiteralPath $resolved).Path
            if ($anchors.Count -gt 0 -and -not $anchors.ContainsKey($anchorPart)) {
                $problems += "$rel : Anker '#$anchorPart' fehlt in $pathPart"
            }
        }
    }
}

Write-Host "Geprüfte Dateien : $($files.Count)"
Write-Host "Geprüfte Links   : $linkCount"
Write-Host "Probleme         : $($problems.Count)"
if ($problems.Count -gt 0) {
    Write-Host ''
    $problems | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host 'OK - alle relativen Links und Anker sind gueltig.'
