@echo off
rem Pulls the content Google Sheet into data\ (validates first, prints what changed). Works from any folder.
setlocal
cd /d "%~dp0.."
dotnet run --project src\Sim -- pull-sheets
if errorlevel 3 (
  choice /m "Pull anyway and drop those changes in the data folder"
  if not errorlevel 2 dotnet run --project src\Sim -- pull-sheets --force
)
pause
