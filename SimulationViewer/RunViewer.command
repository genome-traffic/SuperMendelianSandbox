#!/bin/bash
cd "$(dirname "$0")"
lsof -ti:5050 | xargs kill 2>/dev/null
sleep 1
echo "Starting SMS Gene Drive Simulator at http://localhost:5050 ..."
echo "Close this window to stop the server."
open http://localhost:5050/
dotnet run --urls "http://localhost:5050"
