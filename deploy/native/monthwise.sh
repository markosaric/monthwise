#!/usr/bin/env bash
# Monthwise native background service on macOS (FR-9): PostgreSQL and Monthwise start at login,
# without Docker. Needs Homebrew and the GitHub CLI (gh auth login, once).
# Usage: monthwise.sh install [version] | start | stop | update [version] | status | logs
#        | network on|off | uninstall [--data]
set -euo pipefail

REPO="markosaric/monthwise"
PG_FORMULA="postgresql@17"
PG_PORT=5442
APP_PORT=8080
BASE_DIR="$HOME/Library/Application Support/Monthwise"
NATIVE_DIR="$BASE_DIR/native"
APP_DIR="$NATIVE_DIR/app"
CONFIG_DIR="$NATIVE_DIR/config"
SECRETS_DIR="$CONFIG_DIR/secrets"
SETTINGS_FILE="$CONFIG_DIR/settings.json"
PG_DATA="$NATIVE_DIR/pgdata"
LOG_DIR="$NATIVE_DIR/logs"
BACKUP_DIR="$NATIVE_DIR/update-backups"
KEEP_UPDATE_BACKUPS=3
AGENTS_DIR="$HOME/Library/LaunchAgents"
PG_AGENT="com.monthwise.postgres"
APP_AGENT="com.monthwise.app"

say() { printf '%s\n' "$*"; }
fail() { printf 'Monthwise: %s\n' "$*" >&2; exit 1; }

[[ "$(uname -s)" == "Darwin" ]] || fail "This script is for macOS. On Windows use monthwise.ps1."

pg_bin() { printf '%s/bin' "$(brew --prefix "$PG_FORMULA")"; }

runtime() { [[ "$(uname -m)" == "arm64" ]] && printf 'osx-arm64' || printf 'osx-x64'; }

address() {
  local port
  port="$(sed -nE 's/.*"Urls": *"http:\/\/[^:]+:([0-9]+)".*/\1/p' "$SETTINGS_FILE" 2> /dev/null)"
  printf 'http://localhost:%s' "${port:-$APP_PORT}"
}

require_tools() {
  command -v brew > /dev/null || fail "Homebrew is needed: see https://brew.sh"
  command -v gh > /dev/null || fail "The GitHub CLI is needed: brew install gh, then gh auth login"
  gh auth status > /dev/null 2>&1 || fail "Sign in to GitHub once: gh auth login"
}

xml_escape() { sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g' <<< "$1"; }

# write_agent LABEL PROGRAM ARG... : a launchd agent that starts at login and is restarted if it stops.
write_agent() {
  local label="$1"; shift
  local plist="$AGENTS_DIR/$label.plist" arg
  mkdir -p "$AGENTS_DIR"
  {
    printf '<?xml version="1.0" encoding="UTF-8"?>\n'
    printf '<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">\n'
    printf '<plist version="1.0">\n<dict>\n'
    printf '  <key>Label</key><string>%s</string>\n' "$label"
    printf '  <key>ProgramArguments</key>\n  <array>\n'
    for arg in "$@"; do printf '    <string>%s</string>\n' "$(xml_escape "$arg")"; done
    printf '  </array>\n'
    printf '  <key>RunAtLoad</key><true/>\n'
    printf '  <key>KeepAlive</key><true/>\n'
    printf '  <key>ThrottleInterval</key><integer>10</integer>\n'
    printf '  <key>StandardOutPath</key><string>%s</string>\n' "$(xml_escape "$LOG_DIR/$label.out.log")"
    printf '  <key>StandardErrorPath</key><string>%s</string>\n' "$(xml_escape "$LOG_DIR/$label.out.log")"
    printf '</dict>\n</plist>\n'
  } > "$plist"
}

agent_loaded() { launchctl print "gui/$(id -u)/$1" > /dev/null 2>&1; }

start_agent() {
  agent_loaded "$1" || launchctl bootstrap "gui/$(id -u)" "$AGENTS_DIR/$1.plist"
  launchctl kickstart "gui/$(id -u)/$1" > /dev/null 2>&1 || true
}

stop_agent() {
  if agent_loaded "$1"; then launchctl bootout "gui/$(id -u)/$1" 2> /dev/null || true; fi
}

latest_log() {
  # shellcheck disable=SC2012 # file names are monthwise-YYYYMMDD.log, no odd characters
  ls -t "$LOG_DIR"/monthwise-*.log 2> /dev/null | head -1
}

wait_until_ready() {
  for _ in $(seq 1 45); do
    curl --silent --fail --max-time 2 "$(address)/api/health" > /dev/null 2>&1 && return 0
    sleep 2
  done
  return 1
}

download_release() { # download_release VERSION TARGET_DIR
  local version="$1" target="$2" tmp asset tag=()
  tmp="$(mktemp -d)"
  asset="monthwise-*-$(runtime).zip"
  if [[ "$version" != "latest" ]]; then tag=("v${version#v}"); fi
  gh release download ${tag[@]+"${tag[@]}"} --repo "$REPO" --pattern "$asset" --pattern SHA256SUMS --dir "$tmp"
  (cd "$tmp" && grep -- "-$(runtime).zip" SHA256SUMS | shasum -a 256 -c -) > /dev/null \
    || fail "The downloaded package does not match its checksum."
  rm -rf "$target"
  mkdir -p "$target"
  unzip -q "$tmp"/monthwise-*-"$(runtime)".zip -d "$target"
  chmod +x "$target/Monthwise.Api"
  # Downloaded with gh, not a browser, but clear the quarantine flag in case it was set.
  xattr -dr com.apple.quarantine "$target" 2> /dev/null || true
  rm -rf "$tmp"
}

create_secrets() {
  local password
  mkdir -p "$SECRETS_DIR"
  chmod 700 "$CONFIG_DIR" "$SECRETS_DIR"
  password="$(openssl rand -hex 24)"
  printf '%s' "$password" > "$SECRETS_DIR/postgres_password"
  printf 'Host=127.0.0.1;Port=%s;Database=monthwise;Username=monthwise;Password=%s' "$PG_PORT" "$password" \
    > "$SECRETS_DIR/ConnectionStrings__Monthwise"
  printf 'k1' > "$SECRETS_DIR/SensitiveData__ActiveKeyId"
  openssl rand -base64 32 | tr -d '\n' > "$SECRETS_DIR/SensitiveData__EncryptionKeys__k1"
  openssl rand -base64 32 | tr -d '\n' > "$SECRETS_DIR/SensitiveData__HashKey"
  chmod 600 "$SECRETS_DIR"/*
  say ""
  say "Monthwise created its secrets (database password and card number encryption keys) in:"
  say "  $SECRETS_DIR"
  say "Copy these files into your password manager now. Without them the stored card numbers"
  say "and IBANs can never be read again."
  say ""
}

write_settings() { # write_settings BIND_ADDRESS
  cat > "$SETTINGS_FILE" << EOF
{
  "Urls": "http://$1:$APP_PORT",
  "Database": { "MigrateOnStartup": true, "StartupWaitSeconds": 60 },
  "Logging": { "File": { "Path": "$LOG_DIR", "RetainedDays": 14 } }
}
EOF
}

init_database() {
  local bin="$1"
  "$bin/initdb" --pgdata "$PG_DATA" --username monthwise --pwfile "$SECRETS_DIR/postgres_password" \
    --auth scram-sha-256 --encoding UTF8 --locale-provider icu --icu-locale en-US --locale C > /dev/null
  {
    printf "port = %s\n" "$PG_PORT"
    printf "listen_addresses = '127.0.0.1'\n"
    printf "unix_socket_directories = ''\n"
    printf "logging_collector = off\n"
  } >> "$PG_DATA/postgresql.conf"
}

cmd_install() {
  local version="${1:-latest}" bin
  require_tools
  [[ -x "$APP_DIR/Monthwise.Api" ]] && fail "Monthwise is already installed. Use: monthwise.sh update"
  if [[ -d "$PG_DATA" && ! -f "$SECRETS_DIR/SensitiveData__HashKey" ]]; then
    fail "The database exists but the secrets are missing in:
  $SECRETS_DIR
Put back the files saved in your password manager. New keys would make the stored card numbers
and IBANs unreadable, so none are created."
  fi
  say "Installing PostgreSQL 17 with Homebrew..."
  brew list "$PG_FORMULA" > /dev/null 2>&1 || brew install "$PG_FORMULA"
  bin="$(pg_bin)"

  mkdir -p "$BASE_DIR" "$NATIVE_DIR" "$CONFIG_DIR" "$LOG_DIR"
  chmod 700 "$BASE_DIR"
  [[ -f "$SECRETS_DIR/SensitiveData__HashKey" ]] || create_secrets
  [[ -f "$SETTINGS_FILE" ]] || write_settings 127.0.0.1

  if [[ ! -d "$PG_DATA" ]]; then
    say "Creating the Monthwise database..."
    init_database "$bin"
  fi
  write_agent "$PG_AGENT" "$bin/postgres" -D "$PG_DATA"
  start_agent "$PG_AGENT"
  for _ in $(seq 1 30); do "$bin/pg_isready" -h 127.0.0.1 -p "$PG_PORT" -q && break; sleep 1; done
  PGPASSWORD="$(cat "$SECRETS_DIR/postgres_password")" "$bin/psql" -h 127.0.0.1 -p "$PG_PORT" -U monthwise \
    -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = 'monthwise'" | grep -q 1 \
    || PGPASSWORD="$(cat "$SECRETS_DIR/postgres_password")" "$bin/createdb" -h 127.0.0.1 -p "$PG_PORT" -U monthwise monthwise

  say "Downloading Monthwise ($version)..."
  download_release "$version" "$APP_DIR"
  write_agent "$APP_AGENT" "$APP_DIR/Monthwise.Api" --config-dir "$CONFIG_DIR"
  start_agent "$APP_AGENT"
  cp "${BASH_SOURCE[0]}" "$NATIVE_DIR/monthwise.sh" 2> /dev/null || true
  chmod +x "$NATIVE_DIR/monthwise.sh"

  wait_until_ready || fail "Monthwise did not become ready. Its log: monthwise.sh logs"
  say "Monthwise is running at $(address) and starts by itself when you log in."
  say "Manage it with: \"$NATIVE_DIR/monthwise.sh\" start|stop|update|status|logs|network|uninstall"
}

# pg_tool TOOL ARG... : a PostgreSQL tool of this installation, signed in to its database server.
pg_tool() {
  local tool="$1"
  shift
  PGPASSWORD="$(cat "$SECRETS_DIR/postgres_password")" "$(pg_bin)/$tool" -h 127.0.0.1 -p "$PG_PORT" -U monthwise "$@"
}

start_database() {
  start_agent "$PG_AGENT"
  for _ in $(seq 1 30); do
    "$(pg_bin)/pg_isready" -h 127.0.0.1 -p "$PG_PORT" -q && return 0
    sleep 1
  done
  return 1
}

running_version() {
  curl --silent --max-time 2 "$(address)/api/health" 2> /dev/null | sed -nE 's/.*"version":"([^"]*)".*/\1/p'
}

# Saves a complete copy of the database before an update (FR-3a, Appendix 3) and sets
# UPDATE_BACKUP to its file. Only this user account can read it.
make_update_backup() { # make_update_backup FROM_VERSION
  local file
  mkdir -p "$BACKUP_DIR" && chmod 700 "$BACKUP_DIR" || return 1
  file="$BACKUP_DIR/monthwise-$(date +%Y%m%d-%H%M%S)-$(tr -c 'A-Za-z0-9._-' '_' <<< "$1" | sed 's/_$//').dump"
  start_database || return 1
  # Written to .partial first, so a half-written file never looks like a backup.
  if ! (umask 077 && pg_tool pg_dump -d monthwise --format=custom --file "$file.partial"); then
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
  stop_agent "$APP_AGENT"
  start_database || return 1
  pg_tool dropdb --if-exists monthwise_restore 2> /dev/null || return 1
  pg_tool createdb monthwise_restore || return 1
  pg_tool pg_restore -d monthwise_restore --no-owner --exit-on-error "$1" || return 1
  pg_tool psql -d postgres -v ON_ERROR_STOP=1 -q \
    -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'monthwise'" \
    -c 'ALTER DATABASE monthwise RENAME TO monthwise_before_restore' \
    -c 'ALTER DATABASE monthwise_restore RENAME TO monthwise' > /dev/null || return 1
  pg_tool dropdb --force monthwise_before_restore
}

cmd_update() {
  local version="${1:-latest}" from_version
  require_tools
  [[ -x "$APP_DIR/Monthwise.Api" ]] || fail "Monthwise is not installed. Use: monthwise.sh install"
  from_version="$(running_version)"
  say "Downloading Monthwise ($version)..."
  download_release "$version" "$NATIVE_DIR/app.new"

  # The app is stopped first, so nothing changes in the database after its backup.
  say "Saving an update backup of the database..."
  stop_agent "$APP_AGENT"
  if ! make_update_backup "${from_version:-unknown}"; then
    rm -rf "$NATIVE_DIR/app.new"
    start_agent "$APP_AGENT"
    fail "The update backup failed, so nothing was updated. Its log: monthwise.sh logs"
  fi
  say "Update backup: $UPDATE_BACKUP"

  rm -rf "$NATIVE_DIR/app.previous"
  mv "$APP_DIR" "$NATIVE_DIR/app.previous"
  mv "$NATIVE_DIR/app.new" "$APP_DIR"
  start_agent "$APP_AGENT"
  if wait_until_ready; then
    say "Monthwise is updated: $(running_version)"
    return
  fi

  say "The new version did not start. Putting the data back from the update backup and going back to the previous version..."
  if restore_update_backup "$UPDATE_BACKUP"; then
    rm -rf "$APP_DIR"
    mv "$NATIVE_DIR/app.previous" "$APP_DIR"
    start_agent "$APP_AGENT"
    if wait_until_ready; then
      fail "The update failed. Your data is as it was before the update, and the previous version is running again.
Update backup: $UPDATE_BACKUP
Log: monthwise.sh logs"
    fi
    fail "The update failed. Your data is as it was before the update, but the previous version did not start either.
Update backup: $UPDATE_BACKUP
Log: monthwise.sh logs"
  fi
  # The new version stays installed: it refuses to work on a database it could not bring up to date.
  stop_agent "$APP_AGENT"
  fail "The update failed, and putting the data back failed too, so Monthwise is stopped.
Your data is in the update backup: $UPDATE_BACKUP
The user manual explains how to restore it by hand (\"Restoring an update backup by hand\")."
}

cmd_network() {
  [[ -f "$SETTINGS_FILE" ]] || fail "Monthwise is not installed."
  case "${1:-}" in
    on) write_settings 0.0.0.0 ;;
    off) write_settings 127.0.0.1 ;;
    *) fail "Usage: monthwise.sh network on|off" ;;
  esac
  stop_agent "$APP_AGENT"
  start_agent "$APP_AGENT"
  if [[ "$1" == "on" ]]; then
    say "Other devices on your network can now connect to port $APP_PORT of this computer."
    say "Until login exists, anyone who can reach it can see and change all data."
  else
    say "Only this computer can connect to Monthwise."
  fi
}

cmd_uninstall() {
  stop_agent "$APP_AGENT"
  stop_agent "$PG_AGENT"
  rm -f "$AGENTS_DIR/$APP_AGENT.plist" "$AGENTS_DIR/$PG_AGENT.plist"
  rm -rf "$APP_DIR" "$NATIVE_DIR/app.previous" "$NATIVE_DIR/app.new"
  if [[ "${1:-}" == "--data" ]]; then
    read -r -p "Delete all Monthwise data, its update backups and its secrets in $NATIVE_DIR? This cannot be undone. Type DELETE: " answer
    if [[ "$answer" == "DELETE" ]]; then
      rm -rf "$NATIVE_DIR"
      say "Monthwise and its data are removed."
      return
    fi
  fi
  say "Monthwise is uninstalled. The data, update backups, secrets and logs are kept in $NATIVE_DIR."
}

case "${1:-}" in
  install) shift; cmd_install "$@" ;;
  start) start_agent "$PG_AGENT"; start_agent "$APP_AGENT"; wait_until_ready && say "Monthwise is running at $(address)" ;;
  stop) stop_agent "$APP_AGENT"; stop_agent "$PG_AGENT"; say "Monthwise is stopped until you log in again or run: monthwise.sh start" ;;
  update) shift; cmd_update "$@" ;;
  status) if curl --silent --max-time 2 "$(address)/api/health"; then echo; else say "Monthwise is not answering at $(address)."; fi ;;
  logs) tail -n 200 "$(latest_log)" ;;
  network) shift; cmd_network "$@" ;;
  uninstall) shift; cmd_uninstall "$@" ;;
  *) say "Usage: monthwise.sh install [version] | start | stop | update [version] | status | logs | network on|off | uninstall [--data]"; exit 2 ;;
esac
