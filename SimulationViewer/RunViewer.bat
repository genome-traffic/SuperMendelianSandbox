@echo off
cd /d "%~dp0"
echo Starting SMS Gene Drive Simulator at http://localhost:5050 ...
echo Close this window to stop the server.
start http://localhost:5050/
dotnet run --urls "http://localhost:5050"
