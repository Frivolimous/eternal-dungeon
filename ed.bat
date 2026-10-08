@echo off
rem Eternal Dungeon: one menu for every everyday command. Double-click it, or run "ed" from the repo root.
rem "ed 3" runs menu option 3 straight away. docs\commands.md explains each option.
setlocal EnableExtensions
cd /d "%~dp0"
if "%GODOT%"=="" set "GODOT=C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
set "REPLAYS=%APPDATA%\Godot\app_userdata\Eternal Dungeon\replays"

if not "%~1"=="" (
  set "PICK=%~1"
  call :run
  exit /b
)

:menu
cls
echo.
echo   Eternal Dungeon
echo   ---------------
echo    1  Play the game
echo.
echo    2  Pull the Google Sheet into data      (after editing the sheet)
echo    3  Push data to the Google Sheet        (after data changed here)
echo    4  Export data to TSV files in sheets\
echo    5  Import TSV files from sheets\ into data
echo.
echo    6  Resize art masters into a style
echo    7  Check data and art
echo.
echo    8  Simulate one battle (with its log)
echo    9  Batch-test an encounter (win rate and more)
echo   10  Play a saved replay in the simulator
echo   11  Run the tests
echo.
echo   12  Export the Windows build
echo.
echo    0  Quit
echo.
set "PICK="
set /p "PICK=  Choose: "
if "%PICK%"=="0" exit /b
if /i "%PICK%"=="q" exit /b
call :run
rem Pull and Push pause by themselves.
if not "%PICK%"=="2" if not "%PICK%"=="3" (
  echo.
  pause
)
goto menu

:run
if "%PICK%"=="1" goto play
if "%PICK%"=="2" goto pull
if "%PICK%"=="3" goto push
if "%PICK%"=="4" goto export_tsv
if "%PICK%"=="5" goto import_tsv
if "%PICK%"=="6" goto resize_art
if "%PICK%"=="7" goto check
if "%PICK%"=="8" goto battle
if "%PICK%"=="9" goto batch
if "%PICK%"=="10" goto replay
if "%PICK%"=="11" goto tests
if "%PICK%"=="12" goto build
echo   No option "%PICK%".
exit /b 1

:play
if not exist "%GODOT%" (
  echo   Godot not found at %GODOT% ^(set GODOT to its path^)
  exit /b 1
)
echo   Building the game...
dotnet build game -v q -nologo || exit /b 1
start "Eternal Dungeon" "%GODOT%" --path game
exit /b

:pull
call tools\pull-sheets.bat
exit /b

:push
call tools\push-sheets.bat
exit /b

:export_tsv
call tools\export-sheets.bat
exit /b

:import_tsv
dotnet run --project src\Sim -- import-tsv sheets
exit /b

:resize_art
set "STYLE=ink"
set /p "STYLE=  Style name [ink]: "
call tools\resize-art.bat %STYLE%
exit /b

:check
dotnet run --project src\Sim -- data
echo.
dotnet run --project src\Sim -- assets
exit /b

:battle
dotnet run --project src\Sim -- encounters
echo.
set "ENC=goblin_patrol"
set /p "ENC=  Encounter id [goblin_patrol]: "
set "SEED=42"
set /p "SEED=  Seed [42]: "
set "LEVEL=brief"
set /p "LEVEL=  Log: brief or full [brief]: "
dotnet run --project src\Sim -- run --encounter %ENC% --seed %SEED% --log-level %LEVEL%
exit /b

:batch
dotnet run --project src\Sim -- encounters
echo.
set "ENC=goblin_patrol"
set /p "ENC=  Encounter id [goblin_patrol]: "
set "RUNS=1000"
set /p "RUNS=  Battles [1000]: "
dotnet run --project src\Sim -- batch --encounter %ENC% --runs %RUNS%
exit /b

:replay
if exist "%REPLAYS%\" (
  echo   Saved replays ^(newest last^):
  dir /b /od "%REPLAYS%\*.replay.json" 2>nul
) else (
  echo   No replays saved yet ^(the game's Save replay button puts them in %REPLAYS%^).
  exit /b
)
echo.
set "FILE="
set /p "FILE=  Replay file name (or a full path): "
if "%FILE%"=="" exit /b
if exist "%REPLAYS%\%FILE%" set "FILE=%REPLAYS%\%FILE%"
dotnet run --project src\Sim -- replay "%FILE%"
exit /b

:tests
dotnet test
exit /b

:build
powershell -ExecutionPolicy Bypass -File tools\build.ps1
echo.
echo   The game is in builds\windows\ ^(copy the whole folder^).
exit /b
