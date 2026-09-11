@echo off
setlocal
title Gatebreaker Arena - Android Build and Install
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build\build_and_install_android.ps1" %*
set "BUILD_RESULT=%ERRORLEVEL%"
echo.
if "%BUILD_RESULT%"=="0" (echo Build and install completed.) else (echo Failed. Please check the message above.)
pause
exit /b %BUILD_RESULT%
