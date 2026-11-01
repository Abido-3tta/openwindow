@echo off
setlocal enabledelayedexpansion

echo ==========================================================
echo  OpenWindow - Application Launcher (.NET 8.0 WPF)
echo ==========================================================

:: 1. Discover .NET 8.0 SDK / Runtime
set "DOTNET_CMD="
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_CMD=%USERPROFILE%\.dotnet\dotnet.exe"
    set "PATH=%USERPROFILE%\.dotnet;%PATH%"
) else (
    where dotnet >nul 2>nul
    if !errorlevel! equ 0 (
        set "DOTNET_CMD=dotnet"
    )
)

set "EXE_PATH=%~dp0src\CodeByatta.UI\bin\Release\net8.0-windows\CodeByatta.UI.exe"
if not exist "%EXE_PATH%" (
    set "EXE_PATH=%~dp0src\CodeByatta.UI\bin\Debug\net8.0-windows\CodeByatta.UI.exe"
)

:: If compiled executable exists, launch it directly for optimal performance
if exist "%EXE_PATH%" (
    echo [INFO] Launching %EXE_PATH%...
    start "" "%EXE_PATH%" %*
    echo [SUCCESS] OpenWindow launched!
    exit /b 0
)

:: Otherwise build and run via dotnet
if not "%DOTNET_CMD%"=="" (
    echo [INFO] Compiled binary not found. Building and running via %DOTNET_CMD%...
    "%DOTNET_CMD%" run --project "%~dp0src\CodeByatta.UI\CodeByatta.UI.csproj" -c Release --no-restore %*
    exit /b !errorlevel!
)

echo [ERROR] No binary or .NET SDK found! Run build.cmd first.
exit /b 1
