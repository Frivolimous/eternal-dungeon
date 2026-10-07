@echo off
rem Resizes art masters into a style folder at the exact sizes in docs\art-requests.md.
rem   tools\resize-art.bat ink                  masters from art-source\ink   -> game\assets\styles\ink
rem   tools\resize-art.bat ink "D:\Art\ink"     masters from another folder   -> game\assets\styles\ink
rem Name each master by its id (portrait_warrior.png). Set GODOT to use a different Godot executable.
setlocal
if "%~1"=="" (
  echo Usage: tools\resize-art.bat ^<style^> [masters folder]
  echo   The masters folder defaults to art-source\^<style^>.
  exit /b 1
)
cd /d "%~dp0.."
set "STYLE=%~1"
set "MASTERS=%~2"
if "%MASTERS%"=="" set "MASTERS=art-source\%STYLE%"
if not exist "%MASTERS%\" (
  echo No folder %MASTERS%
  exit /b 1
)
rem Godot runs inside game\, so hand it a full path.
for %%I in ("%MASTERS%") do set "MASTERS=%%~fI"
if "%GODOT%"=="" set "GODOT=C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
if not exist "%GODOT%" (
  echo Godot not found at %GODOT% ^(set GODOT to its path^)
  exit /b 1
)

echo Building the game so the art list is current...
dotnet build game -v q -nologo || exit /b 1
"%GODOT%" --headless --path game -- --resize-art "%MASTERS%" "%STYLE%"
echo.
dotnet run --project src\Sim -- assets
