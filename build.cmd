@echo off
setlocal enabledelayedexpansion

echo ==========================================================
echo  OpenWindow - Turnkey Build Engine (.NET 8.0 WPF)
echo ==========================================================

:: 1. Discover .NET 8.0 SDK
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

if "%DOTNET_CMD%"=="" (
    echo [ERROR] No .NET SDK found! Please ensure Microsoft .NET 8.0 SDK is installed.
    exit /b 1
)

echo [INFO] Using .NET executable: %DOTNET_CMD%
"%DOTNET_CMD%" --version

:: 2. Build Solution in Release Configuration
echo.
echo [1/2] Building OpenWindow.sln (Release)...
"%DOTNET_CMD%" build "%~dp0OpenWindow.sln" -c Release --nologo
if !errorlevel! neq 0 (
    echo [ERROR] Build failed!
    exit /b !errorlevel!
)

:: 3. Run Automated Tests
echo.
echo [2/2] Running Unit and Integration Tests...
"%DOTNET_CMD%" test "%~dp0OpenWindow.sln" -c Release --no-build --nologo
if !errorlevel! neq 0 (
    echo [ERROR] Tests failed!
    exit /b !errorlevel!
)

echo.
echo ==========================================================
echo  SUCCESS: OpenWindow built and verified (0 errors, 0 warnings)
echo ==========================================================
exit /b 0
