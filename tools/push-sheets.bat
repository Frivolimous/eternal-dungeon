@echo off
rem Pushes data\ to the content Google Sheet (keeps _ columns and the sheet's row order). Works from any folder.
setlocal
cd /d "%~dp0.."
dotnet run --project src\Sim -- push-sheets
if errorlevel 3 (
  choice /m "Push anyway and overwrite those edits on the sheet"
  if not errorlevel 2 dotnet run --project src\Sim -- push-sheets --force
)
pause
