# OpenWindow - Automated Installer Script
param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

$AppName = "OpenWindow"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\OpenWindow"
$LatestDist = Get-ChildItem -Directory (Join-Path $PSScriptRoot "dist\OpenWindow-*-win-x64") -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$SourceDir = if ($LatestDist -and (Test-Path (Join-Path $LatestDist.FullName "CodeByatta.UI.exe"))) {
    $LatestDist.FullName
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

function Invoke-WindowRestoration {
    Write-Host "Restoring external windows to clean state..." -ForegroundColor Cyan
    # 0. If CodeByatta.UI is installed, trigger clean CLI restore before force-killing
    if (Test-Path $ExePath) {
        try {
            Start-Process -FilePath $ExePath -ArgumentList "--restore-all" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
        } catch { }
    }

    # 1. Stop existing process if running to release all hooks and mutex
    Get-Process -Name "CodeByatta.UI" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300

    # 2. Native Win32 P/Invoke failsafe to clear leftover topmost/layered attributes across all windows
    $typeDef = @"
    using System;
    using System.Runtime.InteropServices;

    public class WinRestorerFailsafe {
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TOPMOST = 0x00000008L;
        public const long WS_EX_LAYERED = 0x00080000L;
        public const long WS_EX_TRANSPARENT = 0x00000020L;
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint LWA_ALPHA = 0x00000002;

        public static void RestoreWindows() {
            EnumWindows((hwnd, param) => {
                try {
                    if (!IsWindowVisible(hwnd)) return true;
                    long exStyle = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
                    
                    if ((exStyle & WS_EX_LAYERED) != 0) {
                        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
                        exStyle &= ~(WS_EX_TRANSPARENT | WS_EX_LAYERED);
                        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)exStyle);
                        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                    }

                    if ((exStyle & WS_EX_TOPMOST) != 0) {
                        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                    }
                } catch { }
                return true;
            }, IntPtr.Zero);
        }
    }
"@
    try {
        Add-Type -TypeDefinition $typeDef -ErrorAction SilentlyContinue
        [WinRestorerFailsafe]::RestoreWindows()
    } catch { }
}

if ($Uninstall) {
    Write-Host "Uninstalling $AppName..." -ForegroundColor Yellow
    
    # Clean up window states before killing
    Invoke-WindowRestoration

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

# 1. Clean up windows & stop existing process if running
Invoke-WindowRestoration
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
