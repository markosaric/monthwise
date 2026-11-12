#!/usr/bin/env bash
# Monthwise Docker one-click on macOS (and Linux): start, stop, update and remove Monthwise.
# Usage: monthwise.sh start [--open] | stop | update [version] | status | logs | network on|off | remove [--data]
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$HERE/compose.yml"
IMAGE_REPO="ghcr.io/markosaric/monthwise"
GITHUB_REPO="markosaric/monthwise"
# The release this package belongs to. The release workflow replaces "dev" with its version, so a
# new installation runs that exact release instead of whatever is newest (FR-8, Appendix 2).
RELEASE_VERSION="dev"
VOLUME="monthwise-pgdata"
KEEP_UPDATE_BACKUPS=3

if [[ "$(uname -s)" == "Darwin" ]]; then
  BASE_DIR="$HOME/Library/Application Support/Monthwise"
else
  BASE_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/monthwise"
fi
CONFIG_DIR="${MONTHWISE_CONFIG_DIR:-$BASE_DIR/docker}"
SECRETS_DIR="$CONFIG_DIR/secrets"
ENV_FILE="$CONFIG_DIR/compose.env"
# Next to the configuration folder, not inside it: the app container mounts that folder.
BACKUP_DIR="$BASE_DIR/docker-update-backups"

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

default_image() {
  [[ "$RELEASE_VERSION" != "dev" ]] \
    || fail "This copy is not from a release package, so it has no version. Use a release package, or choose one: monthwise.sh update <version>"
  printf '%s:%s' "$IMAGE_REPO" "$RELEASE_VERSION"
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
  # The Monthwise folder is private to this user account; the containers read the files below it.
  mkdir -p "$BASE_DIR" "$CONFIG_DIR"
  chmod 700 "$BASE_DIR"
  chmod 755 "$CONFIG_DIR"
  if [[ ! -f "$ENV_FILE" ]]; then
    local image
    image="$(default_image)"
    {
      printf 'MONTHWISE_CONFIG_DIR=%s\n' "$CONFIG_DIR"
      printf 'MONTHWISE_IMAGE=%s\n' "$image"
      printf 'MONTHWISE_BIND=127.0.0.1\n'
      printf 'MONTHWISE_PORT=8080\n'
    } > "$ENV_FILE"
  fi
  ensure_secrets
  protect_secrets
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
  say ""
  say "Monthwise created its secrets (database password and card number encryption keys) in:"
  say "  $SECRETS_DIR"
  say "Copy these files into your password manager now. Without them the stored card numbers"
  say "and IBANs can never be read again."
  say ""
}

protect_secrets() {
  # On macOS, Docker Desktop and OrbStack let the containers read what this user account can read,
  # so each secret file is private to it (FR-6, Appendix 1); this also fixes older installations.
  # On Linux a bind mount keeps the file owner, so the postgres and app users in the containers
  # could not read a 600 file; there the files stay 644 inside the private Monthwise folder.
  if [[ "$(uname -s)" == "Darwin" ]]; then
    chmod 600 "$SECRETS_DIR"/*
  else
    chmod 644 "$SECRETS_DIR"/*
  fi
}

ensure_image() {
  local image
  image="$(env_value MONTHWISE_IMAGE "")"
  [[ -n "$image" ]] || image="$(default_image)"
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

db_exec() { compose exec -T db "$@"; }

start_database() {
  compose up --detach db > /dev/null 2>&1 || return 1
  for _ in $(seq 1 30); do
    db_exec pg_isready -U monthwise -d monthwise -q > /dev/null 2>&1 && return 0
    sleep 2
  done
  return 1
}

# Saves a complete copy of the database before an update (FR-3a, Appendix 3) and sets
# UPDATE_BACKUP to its file. Only this user account can read it.
make_update_backup() { # make_update_backup FROM_VERSION
  local file
  mkdir -p "$BACKUP_DIR" && chmod 700 "$BACKUP_DIR" || return 1
  file="$BACKUP_DIR/monthwise-$(date +%Y%m%d-%H%M%S)-$(tr -c 'A-Za-z0-9._-' '_' <<< "$1" | sed 's/_$//').dump"
  start_database || return 1
  # Written to .partial first, so a half-written file never looks like a backup.
  if ! (umask 077 && db_exec pg_dump -U monthwise -d monthwise --format=custom > "$file.partial"); then
    rm -f "$file.partial"
    return 1
  fi
  mv "$file.partial" "$file"
  UPDATE_BACKUP="$file"
  prune_update_backups
}

prune_update_backups() {
  # The names start with the date and time, so the glob lists them oldest first.
  local backups=() i
  shopt -s nullglob
  backups=("$BACKUP_DIR"/monthwise-*.dump)
  shopt -u nullglob
  for ((i = 0; i < ${#backups[@]} - KEEP_UPDATE_BACKUPS; i++)); do
    rm -f "${backups[i]}"
  done
}

# Puts the database back exactly as it was in the update backup, with the app stopped. The copy is
# restored into a database of its own and swapped in only once it is complete, so a failed restore
# leaves the database as it is.
restore_update_backup() { # restore_update_backup FILE
  compose stop app > /dev/null 2>&1 || return 1
  start_database || return 1
  db_exec dropdb -U monthwise --if-exists monthwise_restore 2> /dev/null || return 1
  db_exec createdb -U monthwise monthwise_restore || return 1
  db_exec pg_restore -U monthwise -d monthwise_restore --no-owner --exit-on-error < "$1" || return 1
  db_exec psql -U monthwise -d postgres -v ON_ERROR_STOP=1 -q \
    -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'monthwise'" \
    -c 'ALTER DATABASE monthwise RENAME TO monthwise_before_restore' \
    -c 'ALTER DATABASE monthwise_restore RENAME TO monthwise' > /dev/null || return 1
  db_exec dropdb -U monthwise --force monthwise_before_restore
}

cmd_update() {
  local version="${1:-}" previous_image
  ensure_docker
  ensure_config
  login_registry
  if [[ -z "$version" ]]; then
    version="$(gh release view --repo "$GITHUB_REPO" --json tagName --jq .tagName)" \
      || fail "Finding the newest release failed. Name the version instead: monthwise.sh update <version>"
  fi
  previous_image="$(env_value MONTHWISE_IMAGE "$IMAGE_REPO:latest")"

  # Always an exact version, also for installations that followed :latest before.
  set_env_value MONTHWISE_IMAGE "$IMAGE_REPO:${version#v}"
  say "Downloading ${version#v}..."
  if ! compose pull app; then
    set_env_value MONTHWISE_IMAGE "$previous_image"
    fail "Downloading ${version#v} failed, so nothing was updated."
  fi

  # The app is stopped first, so nothing changes in the database after its backup.
  say "Saving an update backup of the database..."
  compose stop app > /dev/null 2>&1 || true
  if ! make_update_backup "${previous_image##*:}"; then
    set_env_value MONTHWISE_IMAGE "$previous_image"
    compose up --detach > /dev/null 2>&1 || true
    fail "The update backup failed, so nothing was updated. Its log: monthwise.sh logs db"
  fi
  say "Update backup: $UPDATE_BACKUP"

  say "Updating to ${version#v}..."
  if compose up --detach && wait_until_ready; then
    say "Monthwise is updated: $(curl --silent "$(address)/api/health" | sed -E 's/.*"version":"([^"]*)".*/\1/')"
    return
  fi

  say "The new version did not start. Putting the data back from the update backup and going back to ${previous_image##*:}..."
  if restore_update_backup "$UPDATE_BACKUP"; then
    set_env_value MONTHWISE_IMAGE "$previous_image"
    if compose up --detach && wait_until_ready; then
      fail "The update failed. Your data is as it was before the update, and ${previous_image##*:} is running again.
Update backup: $UPDATE_BACKUP
Log: monthwise.sh logs"
    fi
    fail "The update failed. Your data is as it was before the update, but ${previous_image##*:} did not start either.
Update backup: $UPDATE_BACKUP
Log: monthwise.sh logs"
  fi
  # The new version stays chosen: it refuses to work on a database it could not bring up to date.
  compose stop app > /dev/null 2>&1 || true
  fail "The update failed, and putting the data back failed too, so Monthwise is stopped.
Your data is in the update backup: $UPDATE_BACKUP
The user manual explains how to restore it by hand (\"Restoring an update backup by hand\")."
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
    read -r -p "Delete all Monthwise data in Docker volume $VOLUME and its update backups? This cannot be undone. Type DELETE: " answer
    if [[ "$answer" == "DELETE" ]]; then
      docker volume rm "$VOLUME"
      rm -rf "$BACKUP_DIR"
      say "The data and its update backups are deleted. The secrets in $SECRETS_DIR are kept; delete them yourself if you no longer need them."
    else
      say "The data is kept."
    fi
  else
    say "Monthwise is removed from Docker. The data is kept in Docker volume $VOLUME, the update backups in $BACKUP_DIR."
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
