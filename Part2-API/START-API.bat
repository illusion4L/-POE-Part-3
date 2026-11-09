@echo off
cd /d "%~dp0RaceDay.Api"
echo Starting RaceDay API on http://localhost:5080/swagger
start "" http://localhost:5080/swagger
dotnet run
pause
