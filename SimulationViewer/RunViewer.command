#!/bin/bash
cd "$(dirname "$0")"
echo "Starting SimulationViewer at http://localhost:5050 ..."
echo "Close this window to stop the server."
open http://localhost:5050/
dotnet run --urls "http://localhost:5050"
