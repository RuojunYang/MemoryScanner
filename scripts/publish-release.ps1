# Build Release publish output and zip for GitHub Release (Windows x64, framework-dependent).
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$PublishDir = Join-Path $Root "dist\MemoryScanner-win-x64"
$ZipPath = Join-Path $Root "dist\MemoryScanner-win-x64.zip"

dotnet publish .\src\MemoryScanner\MemoryScanner.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishReadyToRun=true `
    -o $PublishDir

if (-not (Test-Path (Join-Path $PublishDir "MemoryScanner.exe"))) {
    throw "MemoryScanner.exe not found under $PublishDir"
}

New-Item -ItemType Directory -Force -Path (Split-Path $ZipPath) | Out-Null
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force
Write-Host "Created: $ZipPath"
Write-Host "Upload this file to GitHub Releases."
