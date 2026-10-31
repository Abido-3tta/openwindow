# OpenWindow - Automated Release Packaging Script
# Produces standalone, self-contained desktop native packages for Windows x64 and x86
param(
    [string]$Version = "1.1.2"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "src\CodeByatta.UI\CodeByatta.UI.csproj"
$DistDir = Join-Path $ProjectRoot "dist"

$DotnetExe = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    "$env:USERPROFILE\.dotnet\dotnet.exe"
} elseif (Get-Command dotnet -ErrorAction SilentlyContinue) {
    (Get-Command dotnet).Source
} else {
    "dotnet"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " OpenWindow - Building Release v$Version" -ForegroundColor Cyan
Write-Host " Using .NET Executable: $DotnetExe" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Clean dist directory
if (Test-Path $DistDir) {
    Remove-Item -Path $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$Targets = @("win-x64", "win-x86")

foreach ($rid in $Targets) {
    Write-Host "`n[1/3] Publishing Standalone Native Package for $rid..." -ForegroundColor Yellow
    $TargetFolder = Join-Path $DistDir "OpenWindow-v$Version-$rid"
    
    # Run dotnet publish with self-contained runtime
    & $DotnetExe publish $ProjectFile `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=false `
        -o $TargetFolder

    Write-Host "[2/3] Adding Installer, Uninstaller, and Documentation..." -ForegroundColor Yellow
    Copy-Item (Join-Path $ProjectRoot "install.ps1") -Destination $TargetFolder
    Copy-Item (Join-Path $ProjectRoot "uninstall.ps1") -Destination $TargetFolder
    Copy-Item (Join-Path $ProjectRoot "LICENSE") -Destination $TargetFolder
    Copy-Item (Join-Path $ProjectRoot "README.md") -Destination $TargetFolder

    Write-Host "[3/3] Compressing to ZIP Archive..." -ForegroundColor Yellow
    $ZipPath = Join-Path $DistDir "OpenWindow-v$Version-$rid.zip"
    Compress-Archive -Path "$TargetFolder\*" -DestinationPath $ZipPath -CompressionLevel Optimal
    Write-Host "Created: $ZipPath" -ForegroundColor Green
}

# Generate SHA256 Checksums
Write-Host "`nGenerating SHA-256 Checksums..." -ForegroundColor Cyan
$ChecksumFile = Join-Path $DistDir "checksums.txt"
Get-ChildItem -Path $DistDir -Filter "*.zip" | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
    "$hash  $($_.Name)" | Out-File -FilePath $ChecksumFile -Append -Encoding utf8
    Write-Host "$($_.Name) -> $hash"
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Release v$Version successfully built and packaged in $DistDir" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
