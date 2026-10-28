#!/usr/bin/env bash
# Smoke checks of a running release (NFR-6): healthy within 60 seconds, the front-end on screen
# addresses, API errors for unknown API addresses. Used by CI and after a manual installation.
# Usage: smoke-test.sh [base-url]    default http://localhost:8080
set -euo pipefail

BASE_URL="${1:-http://localhost:8080}"

check() { # check DESCRIPTION CONDITION...
  local description="$1"; shift
  if "$@"; then printf 'ok    %s\n' "$description"; else printf 'FAIL  %s\n' "$description"; exit 1; fi
}

healthy() {
  for _ in $(seq 1 30); do
    curl --silent --fail --max-time 2 "$BASE_URL/api/health" | grep -q '"status":"Healthy"' && return 0
    sleep 2
  done
  return 1
}

serves_front_end() { curl --silent --fail "$BASE_URL$1" | grep -qi '<!doctype html>'; }

api_not_found() {
  local status type
  read -r status type < <(curl --silent --output /dev/null --write-out '%{http_code} %{content_type}' "$BASE_URL/api/does-not-exist")
  [[ "$status" == "404" && "$type" == application/problem+json* ]]
}

check "health is Healthy within 60 seconds" healthy
check "/ serves the front-end" serves_front_end /
check "/month serves the front-end" serves_front_end /month
check "/api/does-not-exist answers 404 problem details" api_not_found
printf 'Version: %s\n' "$(curl --silent "$BASE_URL/api/health" | sed -E 's/.*"version":"([^"]*)".*/\1/')"
