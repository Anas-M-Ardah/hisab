@echo off
setlocal
set "HisabArchitecture=%PROCESSOR_ARCHITECTURE%"
if defined PROCESSOR_ARCHITEW6432 set "HisabArchitecture=%PROCESSOR_ARCHITEW6432%"
set "HisabExecutable=%~dp0app\Hisab.exe"
if /I "%HisabArchitecture%"=="ARM64" set "HisabExecutable=%~dp0app\arm64\Hisab.exe"
if /I "%HisabArchitecture%"=="x86" set "HisabExecutable=%~dp0app\x86\Hisab.exe"
if not exist "%HisabExecutable%" (
  echo Hisab executable missing. Extract the complete ZIP first.
  pause
  exit /b 1
)
start "" "%HisabExecutable%"
endlocal
