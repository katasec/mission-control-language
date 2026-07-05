#!/usr/bin/env bash
# Clean slate: tear down containers AND the data volume, then bring everything back up.
# The init script re-runs on the empty volume (DB + app role), then migrations re-apply.
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose down -v
exec ./scripts/dev-up.sh
