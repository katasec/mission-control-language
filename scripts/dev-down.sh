#!/usr/bin/env bash
# Stop the local dev environment. Data volume is preserved; use dev-reset.sh for a clean slate.
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose stop
echo "Dev environment stopped."
