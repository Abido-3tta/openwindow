# OpenWindow - Automated Installer Script
param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

$AppName = "OpenWindow"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\OpenWindow"
$SourceDir = if (Test-Path (Join-Path $PSScriptRoot "dist\OpenWindow-v1.1.0-win-x64\CodeByatta.UI.exe")) {
    Join-Path $PSScriptRoot "dist\OpenWindow-v1.1.0-win-x64"
} elseif (Test-Path (Join-Path $PSScriptRoot "publish\CodeByatta.UI.exe")) {
    Join-Path $PSScriptRoot "publish"
} elseif (Test-Path (Join-Path $PSScriptRoot "src\CodeByatta.UI\bin\Release\net8.0-windows\win-x64\CodeByatta.UI.exe")) {
    Join-Path $PSScriptRoot "src\CodeByatta.UI\bin\Release\net8.0-windows\win-x64"
} elseif (Test-Path (Join-Path $PSScriptRoot "CodeByatta.UI.exe")) {
    $PSScriptRoot
} else {
    $PSScriptRoot
}
$ExePath = Join-Path $InstallDir "CodeByatta.UI.exe"

if ($Uninstall) {
    Write-Host "Uninstalling $AppName..." -ForegroundColor Yellow
    
    # Terminate running instances
    Get-Process -Name "CodeByatta.UI" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1

    # Remove Registry Run Key
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "OpenWindow" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "CodeByattaWindowsTop" -ErrorAction SilentlyContinue

    # Remove Shortcuts
    $StartMenuDir = [Environment]::GetFolderPath("Programs")
    $DesktopDir = [Environment]::GetFolderPath("Desktop")
    
    Remove-Item (Join-Path $StartMenuDir "$AppName.lnk") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $DesktopDir "$AppName.lnk") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $StartMenuDir "CodeByatta Windows Top.lnk") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $DesktopDir "CodeByatta Windows Top.lnk") -Force -ErrorAction SilentlyContinue

    # Remove Directory
    if (Test-Path $InstallDir) {
        Remove-Item -Path $InstallDir -Recurse -Force
    }

    Write-Host "$AppName successfully uninstalled." -ForegroundColor Green
    exit 0
}

Write-Host "Installing $AppName..." -ForegroundColor Cyan

# 1. Stop existing process if running
Get-Process -Name "CodeByatta.UI" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# 2. Clean and Prepare Install Directory
if (Test-Path $InstallDir) {
    Write-Host "Cleaning previous installation files from $InstallDir..." -ForegroundColor Yellow
    Remove-Item -Path "$InstallDir\*" -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# 3. Copy Published Binaries
Write-Host "Copying application binaries to $InstallDir..."
Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force

# 4. Create Desktop & Start Menu Shortcuts via WScript.Shell
$StartMenuDir = [Environment]::GetFolderPath("Programs")
$DesktopDir = [Environment]::GetFolderPath("Desktop")

# Remove any legacy shortcuts or directories from before rebranding
Remove-Item (Join-Path $StartMenuDir "CodeByatta Windows Top.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $DesktopDir "CodeByatta Windows Top.lnk") -Force -ErrorAction SilentlyContinue
$LegacyInstallDir = Join-Path $env:LOCALAPPDATA "Programs\CodeByatta Windows Top"
if (Test-Path $LegacyInstallDir) {
    Remove-Item -Path $LegacyInstallDir -Recurse -Force -ErrorAction SilentlyContinue
}

$WshShell = New-Object -ComObject WScript.Shell

$StartMenuShortcut = $WshShell.CreateShortcut((Join-Path $StartMenuDir "$AppName.lnk"))
$StartMenuShortcut.TargetPath = $ExePath
$StartMenuShortcut.WorkingDirectory = $InstallDir
$StartMenuShortcut.Description = "Modern Windows Multitasking Suite"
$StartMenuShortcut.Save()

$DesktopDir = [Environment]::GetFolderPath("Desktop")
$DesktopShortcut = $WshShell.CreateShortcut((Join-Path $DesktopDir "$AppName.lnk"))
$DesktopShortcut.TargetPath = $ExePath
$DesktopShortcut.WorkingDirectory = $InstallDir
$DesktopShortcut.Description = "Modern Windows Multitasking Suite"
$DesktopShortcut.Save()

# 5. Register Startup in Windows Registry
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "CodeByattaWindowsTop" -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "OpenWindow" -Value "`"$ExePath`""

Write-Host "Installation completed successfully!" -ForegroundColor Green
Write-Host "Installed Path: $ExePath"
Write-Host "Start Menu and Desktop shortcuts created."
Write-Host "Registered in Windows Startup."

# 6. Launch installed application via WMI (completely decoupled from terminal process tree)
Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = "`"$ExePath`"" } | Out-Null
Write-Host "$AppName is now running in the system tray." -ForegroundColor Cyan
