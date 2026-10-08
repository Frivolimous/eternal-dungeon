@echo off
rem Exports every data table as a TSV into sheets\ (for spreadsheets). Works from any folder.
setlocal
cd /d "%~dp0.."
echo Exporting sheets to tsv...
dotnet run --project src\Sim -- export-tsv sheets || exit /b 1
echo Complete. The tsv files are in the sheets folder.
