<#
.SYNOPSIS
    Construit localement la distribution LIVEX et son ZIP public.

.DESCRIPTION
    Aucune compilation n'a lieu dans GitHub Actions. Ce script publie les
    binaires, assemble le staging, compresse un ZIP par composant, écrit
    distribution.json puis produit un unique ZIP public dans dist/.

.PARAMETER Version
    Version du produit (ex. 1.0.0).

.PARAMETER Tag
    Tag de release utilisé dans les URLs des actifs externes (ex. livex-v1.0.0).

.PARAMETER Rid
    Identifiant de runtime : win-x64 ou linux-x64.

.PARAMETER Profile
    core (PRISM téléchargé) ou full (PRISM embarqué).

.PARAMETER Upload
    Téléverse le ZIP final et les actifs externes sur la release GitHub.

.EXAMPLE
    pwsh scripts/release/build-release.ps1 -Version 1.0.0 -Tag livex-v1.0.0 -Upload
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Tag = "livex-v$Version",
    [ValidateSet("win-x64", "linux-x64")][string]$Rid = "win-x64",
    [ValidateSet("core", "full")][string]$Profile = "core",
    [switch]$Upload,
    [switch]$SkipComponents
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Staging = Join-Path $Root "dist/staging/$Rid"
$DistInputs = Join-Path $Root "dist/inputs"
$Python = if ($env:PYTHON) { $env:PYTHON } else { "python" }

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Échec de l'étape : $Name (code $LASTEXITCODE)"
    }
}

function Publish-DotNet([string]$Project, [string]$Output) {
    if (Test-Path $Output) { Remove-Item -LiteralPath $Output -Recurse -Force }
    dotnet publish $Project -c Release -r $Rid --self-contained true -o $Output
}

New-Item -ItemType Directory -Force -Path $Staging, $DistInputs | Out-Null

if (-not $SkipComponents) {
    Invoke-Step "Publication du Launcher" {
        Publish-DotNet (Join-Path $Root "launcher/Launcher.App/Launcher.App.csproj") (Join-Path $Staging "launcher")
    }

    Invoke-Step "Publication de l'installateur" {
        Publish-DotNet (Join-Path $Root "installer/Setup.App/Setup.App.csproj") (Join-Path $Staging "setup")
    }

    Invoke-Step "Publication de SYNE" {
        Publish-DotNet (Join-Path $Root "syne/Simulation.Console/Simulation.Console.csproj") (Join-Path $Staging "syne")
    }

    Invoke-Step "Staging d'ECHOS" {
        $target = Join-Path $Staging "echos"
        if (Test-Path $target) { Remove-Item -LiteralPath $target -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        $exclude = @(".venv", ".pytest_cache", "__pycache__", ".coverage", "htmlcov")
        Get-ChildItem -LiteralPath (Join-Path $Root "echos") -Force |
            Where-Object { $exclude -notcontains $_.Name } |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse -Force }

        $python = Join-Path $target ".venv/Scripts/python.exe"
        if (-not (Test-Path $python)) {
            python -m venv (Join-Path $target ".venv")
            $python = Join-Path $target ".venv/Scripts/python.exe"
        }
        & $python -m pip install --upgrade pip
        & $python -m pip install -r (Join-Path $target "requirements.txt")
        if ($LASTEXITCODE -ne 0) { throw "Installation des dépendances ECHOS en échec" }
    }

    Invoke-Step "Staging de SYNE-Mock" {
        $target = Join-Path $Staging "syne-mock"
        if (Test-Path $target) { Remove-Item -LiteralPath $target -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        $exclude = @("node_modules", ".git", "test")
        Get-ChildItem -LiteralPath (Join-Path $Root "syne-mock") -Force |
            Where-Object { $exclude -notcontains $_.Name } |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse -Force }
    }
}

# Les actifs externes (PRISM, LDK) peuvent être absents d'un poste de build :
# le script les déclare alors « indisponibles » au lieu d'échouer.
Write-Host "==> Vérification des entrées externes" -ForegroundColor Cyan
& $Python (Join-Path $Root "scripts/release/check_assets.py") --dist-dir $DistInputs
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Des actifs externes sont absents ou non conformes : ils seront marqués indisponibles."
}

$SetupExe = if ($Rid -eq "win-x64") {
    Join-Path $Staging "setup/livex-setup.exe"
} else {
    Join-Path $Staging "setup/livex-setup"
}

Invoke-Step "Assemblage de la distribution" {
    & $Python (Join-Path $Root "scripts/release/build_distribution.py") `
        --rid $Rid `
        --profile $Profile `
        --tag $Tag `
        --version $Version `
        --staging $Staging `
        --dist-inputs $DistInputs `
        --setup-exe $SetupExe
}

$FinalZip = Join-Path $Root "dist/LIVEX-$Version-$Rid-$Profile.zip"
if (-not (Test-Path $FinalZip)) {
    throw "ZIP public introuvable : $FinalZip"
}

Write-Host "ZIP public : $FinalZip" -ForegroundColor Green
Get-Item $FinalZip | Format-List Name, Length, LastWriteTime

if ($Upload) {
    Invoke-Step "Téléversement du ZIP public" {
        gh release upload $Tag $FinalZip --clobber
    }

    if ($Profile -eq "core") {
        $external = Join-Path $Root "installer/build/external-inputs.json"
        if (Test-Path $external) {
            $files = (Get-Content $external -Raw | ConvertFrom-Json).inputs |
                ForEach-Object { Join-Path $DistInputs $_.file } |
                Where-Object { Test-Path $_ }
            if ($files) {
                Invoke-Step "Téléversement des actifs externes" {
                    gh release upload $Tag @files --clobber
                }
            }
        }
    }
}
