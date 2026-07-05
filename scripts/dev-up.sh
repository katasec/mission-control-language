#!/usr/bin/env bash
# Bring up the local dev environment: Postgres (docker compose) → wait healthy →
# apply EF migrations → seed. The container init script owns DB + app role;
# EF migrations own the schema. Seeding is done by the Blazor host on startup
# (Development only), not here.
set -euo pipefail
cd "$(dirname "$0")/.."

CONTAINER=forge-rooms-postgres
DATA_PROJECT=src/ForgeMission.Rooms.Data

docker compose up -d postgres

echo "Waiting for Postgres to become healthy..."
for _ in $(seq 1 60); do
  status=$(docker inspect -f '{{.State.Health.Status}}' "$CONTAINER" 2>/dev/null || echo starting)
  [ "$status" = "healthy" ] && break
  sleep 1
done
if [ "$status" != "healthy" ]; then
  echo "ERROR: $CONTAINER did not become healthy (status: $status)" >&2
  exit 1
fi
echo "Postgres is healthy."

# Apply EF migrations once they exist (added in Phase 38.1 Task 3).
if [ -d "$DATA_PROJECT/Migrations" ]; then
  if ! dotnet ef --version >/dev/null 2>&1; then
    echo "Installing dotnet-ef tool..."
    dotnet tool install --global dotnet-ef
  fi
  dotnet ef database update --project "$DATA_PROJECT"
else
  echo "No EF migrations yet — skipping 'dotnet ef database update'."
fi

echo "Dev environment is up."
