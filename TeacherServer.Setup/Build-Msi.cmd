@echo off
setlocal
powershell.exe -ExecutionPolicy Bypass -File "%~dp0Build-Msi.ps1" %*
endlocal
