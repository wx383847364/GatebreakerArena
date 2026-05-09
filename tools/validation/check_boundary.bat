@echo off
setlocal

set "SCRIPT_DIR=%~dp0"

where py >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    py -3 "%SCRIPT_DIR%check_boundary.py" %*
) else (
    python "%SCRIPT_DIR%check_boundary.py" %*
)

set "EXIT_CODE=%ERRORLEVEL%"
exit /b %EXIT_CODE%
