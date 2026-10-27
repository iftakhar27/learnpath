#!/usr/bin/env bash
# Boots the API against the local dev database and writes docs/openapi.json.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build src/LearnPath.Api -v q >/dev/null
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/LearnPath.Api --no-build --no-launch-profile --urls http://localhost:5091 >/dev/null 2>&1 &
pid=$!
trap 'kill $pid 2>/dev/null' EXIT
for _ in $(seq 1 60); do curl -sf http://localhost:5091/health >/dev/null && break; sleep 1; done
curl -sf http://localhost:5091/swagger/v1/swagger.json | python3 -m json.tool --indent 2 > docs/openapi.json
echo "wrote docs/openapi.json"
