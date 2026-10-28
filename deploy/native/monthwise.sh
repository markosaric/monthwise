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

cmd_update() {
  local version="${1:-latest}"
  require_tools
  [[ -x "$APP_DIR/Monthwise.Api" ]] || fail "Monthwise is not installed. Use: monthwise.sh install"
  say "Downloading Monthwise ($version)..."
  download_release "$version" "$NATIVE_DIR/app.new"
  stop_agent "$APP_AGENT"
  rm -rf "$NATIVE_DIR/app.previous"
  mv "$APP_DIR" "$NATIVE_DIR/app.previous"
  mv "$NATIVE_DIR/app.new" "$APP_DIR"
  start_agent "$APP_AGENT"
  if wait_until_ready; then
    say "Monthwise is updated: $(curl --silent "$(address)/api/health" | sed -E 's/.*"version":"([^"]*)".*/\1/')"
  else
    say "The new version did not start; going back to the previous one."
    stop_agent "$APP_AGENT"
    rm -rf "$APP_DIR"
    mv "$NATIVE_DIR/app.previous" "$APP_DIR"
    start_agent "$APP_AGENT"
    fail "Update failed. The previous version is running again; its log: monthwise.sh logs"
  fi
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
    read -r -p "Delete all Monthwise data and its secrets in $NATIVE_DIR? This cannot be undone. Type DELETE: " answer
    if [[ "$answer" == "DELETE" ]]; then
      rm -rf "$NATIVE_DIR"
      say "Monthwise and its data are removed."
      return
    fi
  fi
  say "Monthwise is uninstalled. The data, secrets and logs are kept in $NATIVE_DIR."
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
