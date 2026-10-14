param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$tag = "v$Version"

function Invoke-Git([string[]]$Arguments) {
    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') selhal (kód $LASTEXITCODE)."
    }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git není nainstalovaný nebo není v PATH.'
}

$repositoryRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $repositoryRoot) {
    throw 'Projekt musí být uložený v Git repozitáři.'
}
if (([IO.Path]::GetFullPath($repositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) -ne
    ([IO.Path]::GetFullPath($PSScriptRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))) {
    throw 'Tento projekt musí být v kořeni svého Git repozitáře.'
}

$remoteUrl = (& git remote get-url origin 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $remoteUrl) {
    throw 'Chybí Git remote origin. Nastavte URL GitHub repozitáře.'
}
if ($remoteUrl -notmatch 'github\.com[:/]') {
    throw "Remote origin není GitHub: $remoteUrl"
}

$changes = (& git status --porcelain)
if ($changes) {
    throw 'Pracovní strom obsahuje necommitované změny. Nejdřív je commitněte.'
}

& git rev-parse -q --verify "refs/tags/$tag" *> $null
if ($LASTEXITCODE -eq 0) {
    throw "Tag $tag už existuje lokálně."
}

$remoteTag = (& git ls-remote --tags origin "refs/tags/$tag")
if ($LASTEXITCODE -ne 0) {
    throw 'Nepodařilo se ověřit tagy na GitHubu.'
}
if ($remoteTag) {
    throw "Tag $tag už existuje na GitHubu."
}

Invoke-Git @('push', 'origin', 'HEAD')
Invoke-Git @('tag', '-a', $tag, '-m', "Release $tag")
Invoke-Git @('push', 'origin', $tag)

Write-Host "Tag $tag byl odeslán. GitHub Actions nyní vytvoří Release."
