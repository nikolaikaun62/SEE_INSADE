param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ProjectRoot)) {
    throw "ProjectRoot not found: $ProjectRoot"
}

$source = Split-Path -Parent $MyInvocation.MyCommand.Path

$items = @(
    "SEE_INSADE.csproj",
    "Core",
    "UI",
    "README_GPU_ACCELERATION_EXPERIMENTAL.md",
    "README_FIX_BUILD_ERRORS.md",
    "README_PRO_UI_FEATURES.md"
)

foreach ($item in $items) {
    $src = Join-Path $source $item
    $dst = Join-Path $ProjectRoot $item

    if (-not (Test-Path $src)) {
        Write-Warning "Skip missing source item: $src"
        continue
    }

    if (Test-Path $src -PathType Container) {
        Copy-Item $src $dst -Recurse -Force
    }
    else {
        Copy-Item $src $dst -Force
    }
}

Write-Host "SEE_INSADE PRO UI update installed." -ForegroundColor Green
Write-Host "Recommended:" -ForegroundColor Cyan
Write-Host "dotnet clean .\SEE_INSADE.csproj"
Write-Host "Remove-Item -Recurse -Force .\bin, .\obj -ErrorAction SilentlyContinue"
Write-Host "dotnet restore .\SEE_INSADE.csproj"
Write-Host "dotnet build .\SEE_INSADE.csproj"
