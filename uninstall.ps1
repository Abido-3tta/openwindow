# OpenWindow - Uninstaller Script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
& "$ScriptDir\install.ps1" -Uninstall
