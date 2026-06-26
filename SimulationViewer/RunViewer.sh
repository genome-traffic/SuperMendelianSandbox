#!/bin/bash
cd "$(dirname "$0")"
fuser -k 5050/tcp 2>/dev/null
sleep 1
echo "Starting SMS Gene Drive Simulator at http://localhost:5050 ..."
echo "Close this terminal to stop the server."
xdg-open http://localhost:5050/ 2>/dev/null || echo "Open http://localhost:5050 in your browser."
dotnet run --urls "http://localhost:5050"
