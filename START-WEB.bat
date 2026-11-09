@echo off
cd /d "%~dp0Part3-Web\RaceDay.Web"
echo Starting RaceDay web on http://localhost:5090 (the API must be running on http://localhost:5080)
dotnet run
pause
