param(
    [string]$ProjectRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$PackageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Installing SEE_INSADE GPU acceleration package..." -ForegroundColor Cyan
Write-Host "Project root: $ProjectRoot"

$items = @(
    "SEE_INSADE.csproj",
    "Core\Config\ConfigManager.cs",
    "Core\Imaging\ImageProcessor.cs",
    "Core\Imaging\Gpu\GpuImageProcessor.cs",
    "Core\Imaging\Gpu\GpuFilterKernels.cs",
    "UI\MainWindows\MainWindow.GpuAcceleration.cs",
    "README_GPU_ACCELERATION_EXPERIMENTAL.md"
)

foreach ($item in $items) {
    $source = Join-Path $PackageRoot $item
    $target = Join-Path $ProjectRoot $item
    $targetDir = Split-Path -Parent $target

    if (!(Test-Path $source)) {
        throw "Missing package file: $source"
    }

    if (!(Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir | Out-Null
    }

    Copy-Item -Path $source -Destination $target -Force
    Write-Host "Copied $item" -ForegroundColor DarkGray
}

Write-Host "Done. Now run:" -ForegroundColor Green
Write-Host "dotnet restore .\SEE_INSADE.csproj"
Write-Host "dotnet build .\SEE_INSADE.csproj"
