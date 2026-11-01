# OpenWindow - PowerShell Application Launcher
param(
    [string]$Configuration = "Release",
    [switch]$Silent,
    [switch]$Tray
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

$ExePath = Join-Path $ProjectRoot "src\CodeByatta.UI\bin\$Configuration\net8.0-windows\CodeByatta.UI.exe"

# If compiled exe already exists, launch directly
if (Test-Path $ExePath) {
    $argsList = @()
    if ($Silent -or $Tray) { $argsList += "--silent" }
    Write-Host "Launching OpenWindow ($Configuration)..." -ForegroundColor Cyan
    Start-Process -FilePath $ExePath -ArgumentList $argsList
    Write-Host "OpenWindow started successfully." -ForegroundColor Green
    exit 0
}

Write-Host "Compiled binary not found. Building and launching via .NET SDK..." -ForegroundColor Yellow
$runArgs = @("run", "--project", (Join-Path $ProjectRoot "src\CodeByatta.UI\CodeByatta.UI.csproj"), "-c", $Configuration)
if ($Silent -or $Tray) { $runArgs += @("--", "--silent") }

& $DotnetExe $runArgs
