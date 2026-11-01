# OpenWindow - PowerShell Automated Build & Verification Script
param(
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$DotnetExe = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    "$env:USERPROFILE\.dotnet\dotnet.exe"
} elseif (Get-Command dotnet -ErrorAction SilentlyContinue) {
    (Get-Command dotnet).Source
} else {
    "dotnet"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " OpenWindow - Building Solution ($Configuration)" -ForegroundColor Cyan
Write-Host " Using .NET SDK: $DotnetExe" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Build
& $DotnetExe build (Join-Path $ProjectRoot "OpenWindow.sln") -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 2. Test
if (-not $SkipTests) {
    Write-Host "`nRunning Test Suite (37 automated tests)..." -ForegroundColor Yellow
    & $DotnetExe test (Join-Path $ProjectRoot "OpenWindow.sln") -c $Configuration --no-build --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

Write-Host "`nSUCCESS: Solution built and verified cleanly!" -ForegroundColor Green
