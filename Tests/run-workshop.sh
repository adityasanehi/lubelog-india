#!/usr/bin/env bash
set -euo pipefail
: "${MCP_SOURCE:?Set MCP_SOURCE to the MCP source checkout}"
app_source=$(cd "$(dirname "$0")/.." && pwd)
test_root=$(mktemp -d)
cleanup() {
  if [[ -n ${app_pid:-} ]]; then kill "$app_pid" 2>/dev/null || true; fi
  if [[ -n ${mcp_pid:-} ]]; then kill "$mcp_pid" 2>/dev/null || true; fi
  rm -rf "$test_root"
}
trap cleanup EXIT
mkdir -p "$test_root/app" "$test_root/mcp"
tar -C "$app_source" --exclude=.git --exclude=data --exclude=bin --exclude=obj -cf - . | tar -xf - -C "$test_root/app"
tar -C "$MCP_SOURCE" --exclude=.git --exclude=bin --exclude=obj -cf - . | tar -xf - -C "$test_root/mcp"
cd "$test_root/app"
mkdir -p data
if [[ -n ${WORKSHOP_TEST_DB:-} ]]; then cp "$WORKSHOP_TEST_DB" data/cartracker.db; fi
dotnet build Tests/WorkshopChecks.csproj -c Release -v quiet > "$test_root/build.log" 2>&1 || { tail -n 30 "$test_root/build.log"; exit 1; }
dotnet Tests/bin/Release/net10.0/WorkshopChecks.dll unit
dotnet Tests/bin/Release/net10.0/WorkshopChecks.dll seed
dotnet build "$test_root/mcp/LubeLogMCP.sln" -c Release -v quiet
export APP_URL=http://127.0.0.1:18080 MCP_URL=http://127.0.0.1:18081/api/mcp
ASPNETCORE_URLS=$APP_URL LUBELOGGER_INVARIANT_API=true dotnet bin/Release/net10.0/CarCareTracker.dll > "$test_root/app.log" 2>&1 &
app_pid=$!
(cd "$test_root/mcp" && ASPNETCORE_URLS=http://127.0.0.1:18081 LUBELOG_INSTANCE=$APP_URL dotnet LubeLogMCP/bin/Release/net10.0/LubeLogMCP.dll) > "$test_root/mcp.log" 2>&1 &
mcp_pid=$!
dotnet Tests/bin/Release/net10.0/WorkshopChecks.dll
