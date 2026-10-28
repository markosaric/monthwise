#!/usr/bin/env bash
# Monthwise Docker one-click on macOS (and Linux): start, stop, update and remove Monthwise.
# Usage: monthwise.sh start [--open] | stop | update [version] | status | logs | network on|off | remove [--data]
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$HERE/compose.yml"
IMAGE_REPO="ghcr.io/markosaric/monthwise"
VOLUME="monthwise-pgdata"

if [[ "$(uname -s)" == "Darwin" ]]; then
  BASE_DIR="$HOME/Library/Application Support/Monthwise"
else
  BASE_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/monthwise"
fi
CONFIG_DIR="${MONTHWISE_CONFIG_DIR:-$BASE_DIR/docker}"
SECRETS_DIR="$CONFIG_DIR/secrets"
ENV_FILE="$CONFIG_DIR/compose.env"

say() { printf '%s\n' "$*"; }
fail() { printf 'Monthwise: %s\n' "$*" >&2; exit 1; }

compose() { docker compose --project-name monthwise --file "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"; }

env_value() { # env_value KEY DEFAULT
  local value
  value="$(grep -E "^$1=" "$ENV_FILE" 2>/dev/null | tail -1 | cut -d= -f2- || true)"
  printf '%s' "${value:-$2}"
}

set_env_value() { # set_env_value KEY VALUE
  local tmp
  tmp="$(mktemp)"
  grep -vE "^$1=" "$ENV_FILE" > "$tmp" 2>/dev/null || true
  printf '%s=%s\n' "$1" "$2" >> "$tmp"
  mv "$tmp" "$ENV_FILE"
}

address() { printf 'http://localhost:%s' "$(env_value MONTHWISE_PORT 8080)"; }

ensure_docker() {
  command -v docker > /dev/null || fail "Docker is not installed. Install Docker Desktop or OrbStack first (see the user manual)."
  docker info > /dev/null 2>&1 && return
  if [[ "$(uname -s)" == "Darwin" ]]; then
    say "Starting Docker..."
    open -a OrbStack 2> /dev/null || open -a Docker 2> /dev/null || true
  fi
  for _ in $(seq 1 60); do
    docker info > /dev/null 2>&1 && return
    sleep 2
  done
  fail "Docker is not running. Start Docker Desktop (or OrbStack) and try again."
}

ensure_config() {
  # The Monthwise folder is private to this user account; the container reads the files below it.
  mkdir -p "$BASE_DIR" "$CONFIG_DIR"
  chmod 700 "$BASE_DIR"
  chmod 755 "$CONFIG_DIR"
  if [[ ! -f "$ENV_FILE" ]]; then
    {
      printf 'MONTHWISE_CONFIG_DIR=%s\n' "$CONFIG_DIR"
      printf 'MONTHWISE_IMAGE=%s:latest\n' "$IMAGE_REPO"
      printf 'MONTHWISE_BIND=127.0.0.1\n'
      printf 'MONTHWISE_PORT=8080\n'
    } > "$ENV_FILE"
  fi
  ensure_secrets
}

random_base64() { openssl rand -base64 32; }

ensure_secrets() {
  if [[ -f "$SECRETS_DIR/SensitiveData__HashKey" ]]; then
    return
  fi
  if docker volume inspect "$VOLUME" > /dev/null 2>&1; then
    fail "The database exists but the secrets are missing in:
  $SECRETS_DIR
Put back the files saved in your password manager (see the user manual). New keys would make
the stored card numbers and IBANs unreadable, so none are created."
  fi
  command -v openssl > /dev/null || fail "openssl is needed to create the secrets."
  mkdir -p "$SECRETS_DIR"
  chmod 755 "$SECRETS_DIR"
  local password
  password="$(openssl rand -hex 24)"
  printf '%s' "$password" > "$SECRETS_DIR/postgres_password"
  printf 'Host=db;Port=5432;Database=monthwise;Username=monthwise;Password=%s' "$password" \
    > "$SECRETS_DIR/ConnectionStrings__Monthwise"
  printf 'k1' > "$SECRETS_DIR/SensitiveData__ActiveKeyId"
  random_base64 | tr -d '\n' > "$SECRETS_DIR/SensitiveData__EncryptionKeys__k1"
  random_base64 | tr -d '\n' > "$SECRETS_DIR/SensitiveData__HashKey"
  chmod 644 "$SECRETS_DIR"/*
  say ""
  say "Monthwise created its secrets (database password and card number encryption keys) in:"
  say "  $SECRETS_DIR"
  say "Copy these files into your password manager now. Without them the stored card numbers"
  say "and IBANs can never be read again."
  say ""
}

ensure_image() {
  local image
  image="$(env_value MONTHWISE_IMAGE "$IMAGE_REPO:latest")"
  docker image inspect "$image" > /dev/null 2>&1 && return
  login_registry
  docker pull "$image" || fail "Downloading $image failed. Allow GitHub packages once: gh auth refresh --scopes read:packages"
}

login_registry() {
  # The repository is private: sign in to GHCR with the GitHub CLI (gh auth login, once).
  command -v gh > /dev/null || fail "The GitHub CLI (gh) is needed to download Monthwise. Install it and run: gh auth login"
  gh auth token | docker login ghcr.io --username "$(gh api user --jq .login)" --password-stdin > /dev/null \
    || fail "Signing in to ghcr.io failed. Allow GitHub packages once: gh auth refresh --scopes read:packages"
}

wait_until_ready() {
  local url
  url="$(address)/api/health"
  for _ in $(seq 1 45); do
    if curl --silent --fail --max-time 2 "$url" > /dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done
  say "Monthwise did not become ready. Its log: monthwise.sh logs"
  return 1
}

cmd_start() {
  ensure_docker
  ensure_config
  ensure_image
  compose up --detach
  say "Waiting for Monthwise..."
  wait_until_ready
  say "Monthwise is running at $(address)"
  if [[ "${1:-}" == "--open" ]]; then
    if command -v open > /dev/null; then open "$(address)"; else xdg-open "$(address)" > /dev/null 2>&1 || true; fi
  fi
}

cmd_stop() {
  ensure_docker
  compose stop
  say "Monthwise is stopped. The data is kept."
}

cmd_update() {
  ensure_docker
  ensure_config
  if [[ -n "${1:-}" ]]; then
    set_env_value MONTHWISE_IMAGE "$IMAGE_REPO:${1#v}"
  fi
  login_registry
  compose pull app
  compose up --detach
  wait_until_ready
  say "Monthwise is updated: $(curl --silent "$(address)/api/health" | sed -E 's/.*"version":"([^"]*)".*/\1/')"
}

cmd_network() {
  [[ -f "$ENV_FILE" ]] || fail "Start Monthwise once first: monthwise.sh start"
  case "${1:-}" in
    on) set_env_value MONTHWISE_BIND 0.0.0.0 ;;
    off) set_env_value MONTHWISE_BIND 127.0.0.1 ;;
    *) fail "Usage: monthwise.sh network on|off" ;;
  esac
  ensure_docker
  compose up --detach
  if [[ "$1" == "on" ]]; then
    say "Other devices on your network can now connect to port $(env_value MONTHWISE_PORT 8080) of this computer."
    say "Until login exists, anyone who can reach it can see and change all data."
  else
    say "Only this computer can connect to Monthwise."
  fi
}

cmd_remove() {
  ensure_docker
  compose down
  if [[ "${1:-}" == "--data" ]]; then
    read -r -p "Delete all Monthwise data in Docker volume $VOLUME? This cannot be undone. Type DELETE: " answer
    if [[ "$answer" == "DELETE" ]]; then
      docker volume rm "$VOLUME"
      say "The data is deleted. The secrets in $SECRETS_DIR are kept; delete them yourself if you no longer need them."
    else
      say "The data is kept."
    fi
  else
    say "Monthwise is removed from Docker. The data is kept in Docker volume $VOLUME."
  fi
}

case "${1:-}" in
  start) shift; cmd_start "$@" ;;
  stop) cmd_stop ;;
  update) shift; cmd_update "$@" ;;
  status) ensure_docker; compose ps; curl --silent --max-time 2 "$(address)/api/health" && echo ;;
  logs) ensure_docker; compose logs --tail 200 "${@:2}" ;;
  network) shift; cmd_network "$@" ;;
  remove) shift; cmd_remove "$@" ;;
  *) say "Usage: monthwise.sh start [--open] | stop | update [version] | status | logs | network on|off | remove [--data]"; exit 2 ;;
esac
