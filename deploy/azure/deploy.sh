#!/usr/bin/env bash
# Deploys a Monthwise release to Azure in one step (FR-10). Runs in Azure Cloud Shell or anywhere
# with the Azure CLI signed in (az login). Creates the secrets on the first deployment and reuses
# them from Key Vault afterwards, so the data stays readable.
#
# Usage: deploy.sh <version>      e.g. deploy.sh 1.2.0
#        deploy.sh --new-setup-code  a new setup code, when no household admin can sign in (login FR-9)
# Environment:
#   GHCR_USERNAME, GHCR_TOKEN   GitHub user and a token with only read:packages (required)
#   BUDGET_EMAIL                email address for the budget alert (required)
#   MONTHWISE_RESOURCE_GROUP    default rg-monthwise
#   MONTHWISE_LOCATION          default westeurope (EU)
#   MONTHWISE_BUDGET            monthly budget, default 25
set -euo pipefail

say() { printf '%s\n' "$*"; }

# A setup code (login FR-1, FR-9): 24 characters without look-alikes such as 0 and O, in groups of four.
new_setup_code() {
  local chars
  chars="$(head -c 4000 /dev/urandom | LC_ALL=C tr -dc 'ABCDEFGHJKMNPQRSTUVWXYZ23456789')"
  printf '%s-%s-%s-%s-%s-%s' "${chars:0:4}" "${chars:4:4}" "${chars:8:4}" "${chars:12:4}" "${chars:16:4}" "${chars:20:4}"
}

show_setup_code_command() {
  say ""
  say "Read the setup code with:"
  say "  az keyvault secret show --vault-name $1 --name setup-code --query value -o tsv"
  say "Open Monthwise and enter it to create the first user. If no household admin can sign in any"
  say "more, it also resets a household admin's password. It works once; keep it private."
  say ""
}

# Writes a new setup code to Key Vault and restarts Monthwise so it reads it at once.
new_setup_code_command() {
  local group="${MONTHWISE_RESOURCE_GROUP:-rg-monthwise}" vault identity code_file revision
  az account show > /dev/null || { say "Sign in first: az login"; exit 1; }
  vault="$(az keyvault list --resource-group "$group" --query '[0].name' -o tsv)"
  [[ -n "$vault" ]] || { say "No Monthwise installation in resource group $group."; exit 1; }
  code_file="$(mktemp)"
  trap 'rm -f "$code_file"' EXIT
  chmod 600 "$code_file"
  new_setup_code > "$code_file"
  # From a file, so the code is never on the command line.
  az keyvault secret set --vault-name "$vault" --name setup-code --file "$code_file" --encoding utf-8 --output none
  identity="$(az identity list --resource-group "$group" --query '[0].id' -o tsv)"
  # Setting the reference again makes the app read the new version now instead of within 30 minutes.
  az containerapp secret set --resource-group "$group" --name ca-monthwise --output none \
    --secrets "setup-code=keyvaultref:https://$vault.vault.azure.net/secrets/setup-code,identityref:$identity"
  revision="$(az containerapp show --resource-group "$group" --name ca-monthwise \
    --query properties.latestRevisionName -o tsv)"
  az containerapp revision restart --resource-group "$group" --name ca-monthwise --revision "$revision" --output none
  say "Monthwise has a new setup code and restarts with it."
  show_setup_code_command "$vault"
}

if [[ "${1:-}" == "--new-setup-code" ]]; then
  new_setup_code_command
  exit 0
fi

VERSION="${1:?Usage: deploy.sh <version>, e.g. deploy.sh 1.2.0}"
VERSION="${VERSION#v}"
: "${GHCR_USERNAME:?Set GHCR_USERNAME to your GitHub user}"
: "${GHCR_TOKEN:?Set GHCR_TOKEN to a GitHub token with only the read:packages scope}"
: "${BUDGET_EMAIL:?Set BUDGET_EMAIL to the address for the budget alert}"
RESOURCE_GROUP="${MONTHWISE_RESOURCE_GROUP:-rg-monthwise}"
LOCATION="${MONTHWISE_LOCATION:-westeurope}"
BUDGET="${MONTHWISE_BUDGET:-25}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

az account show > /dev/null || { say "Sign in first: az login"; exit 1; }
say "Subscription: $(az account show --query name -o tsv). Resource group: $RESOURCE_GROUP ($LOCATION)."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

# Appendix 5: the database is private and Monthwise has its own login. Azure cannot move a database
# server or an app environment made before that into a private network, so stop before any change.
server="$(az postgres flexible-server list --resource-group "$RESOURCE_GROUP" --query '[0].name' -o tsv)"
if [[ -n "$server" && -z "$(az postgres flexible-server show --resource-group "$RESOURCE_GROUP" --name "$server" \
  --query 'network.delegatedSubnetResourceId' -o tsv)" ]]; then
  say "This installation was made before Monthwise kept its Azure database private. Azure cannot move"
  say "it in place, so nothing was changed. To move it with all your data (user manual, \"Azure\"):"
  say "  1. In Monthwise, open Settings and export your data. Keep the file and its password."
  say "  2. Deploy into a new resource group:"
  say "       MONTHWISE_RESOURCE_GROUP=rg-monthwise-2 $0 $VERSION"
  say "  3. Open the new address, import the export file in Settings and check your data."
  say "  4. Delete the old installation: az group delete --name $RESOURCE_GROUP"
  exit 1
fi

vault="$(az keyvault list --resource-group "$RESOURCE_GROUP" --query '[0].name' -o tsv)"
if [[ -z "$vault" ]]; then
  say "First deployment: creating the database password and the card number encryption keys."
  postgres_password="$(openssl rand -hex 24)"
  app_database_password="$(openssl rand -hex 24)"
  encryption_key="$(openssl rand -base64 32)"
  hash_key="$(openssl rand -base64 32)"
  setup_code="$(new_setup_code)"
  setup_code_created=1
  budget_start="$(date -u +%Y-%m-01)"
else
  say "Reusing the secrets in Key Vault $vault."
  read_secret() { az keyvault secret show --vault-name "$vault" --name "$1" --query value -o tsv; }
  postgres_password="$(read_secret postgres-password)"
  app_database_password="$(read_secret app-database-password)"
  encryption_key="$(read_secret encryption-key-k1)"
  hash_key="$(read_secret hash-key)"
  # Installations from before login get their setup code now.
  setup_code="$(read_secret setup-code 2> /dev/null || true)"
  if [[ -z "$setup_code" ]]; then
    setup_code="$(new_setup_code)"
    setup_code_created=1
  fi
  budget_start="$(az consumption budget show --budget-name monthwise-monthly --resource-group "$RESOURCE_GROUP" \
    --query 'timePeriod.start' -o tsv 2> /dev/null | cut -c1-10)"
  budget_start="${budget_start:-$(date -u +%Y-%m-01)}"
fi

# Secure parameters go in a file only this user can read, never on the command line.
parameters="$(mktemp)"
trap 'rm -f "$parameters"' EXIT
chmod 600 "$parameters"
cat > "$parameters" << EOF
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "version": { "value": "$VERSION" },
    "ghcrUsername": { "value": "$GHCR_USERNAME" },
    "ghcrToken": { "value": "$GHCR_TOKEN" },
    "postgresPassword": { "value": "$postgres_password" },
    "appDatabasePassword": { "value": "$app_database_password" },
    "encryptionKey": { "value": "$encryption_key" },
    "hashKey": { "value": "$hash_key" },
    "setupCode": { "value": "$setup_code" },
    "deployerObjectId": { "value": "$(az ad signed-in-user show --query id -o tsv)" },
    "budgetEmail": { "value": "$BUDGET_EMAIL" },
    "budgetAmount": { "value": $BUDGET },
    "budgetStartDate": { "value": "$budget_start" }
  }
}
EOF

# deploy <setupOnly>: setupOnly=true deploys everything except the app itself (Appendix 5).
deploy() {
  az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "monthwise-$VERSION-$(date -u +%Y%m%d%H%M%S)" \
    --template-file "$HERE/main.bicep" \
    --parameters "@$parameters" \
    --parameters setupOnly="$1" \
    --query 'properties.outputs.url.value' -o tsv
}

# Runs the setup job that creates Monthwise's own database login and hands it the database
# (db-setup.sql), and waits until it has finished.
set_up_database() {
  local execution status
  execution="$(az containerapp job start --resource-group "$RESOURCE_GROUP" --name caj-monthwise-db-setup \
    --query name -o tsv)"
  for _ in $(seq 1 60); do
    status="$(az containerapp job execution show --resource-group "$RESOURCE_GROUP" \
      --name caj-monthwise-db-setup --job-execution-name "$execution" --query properties.status -o tsv)"
    case "$status" in
      Succeeded) return 0 ;;
      Failed | Stopped | Degraded) break ;;
    esac
    sleep 5
  done
  say "Setting up Monthwise's database login did not succeed (status: ${status:-unknown}). Its log:"
  say "  az containerapp job logs show -g $RESOURCE_GROUP -n caj-monthwise-db-setup --execution $execution --container db-setup"
  return 1
}

# The managed database's own backups are the update backup on Azure (FR-3a, Appendix 3): this is
# the moment to restore to if the new release cannot bring the database up to date.
before_deploy="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
say "Deploying Monthwise $VERSION (this takes several minutes the first time)..."
# The database, the network and the setup job first, then Monthwise's own login, then the app, so
# the app never starts without its login. On the first deployment access to Key Vault can take a
# minute to apply: try again once.
deploy true > /dev/null || { say "Retrying in 60 seconds..."; sleep 60; deploy true > /dev/null; }
say "Setting up Monthwise's own database login..."
set_up_database || { sleep 30; set_up_database; } || exit 1
url="$(deploy false)"

if [[ -z "$vault" ]]; then
  vault="$(az keyvault list --resource-group "$RESOURCE_GROUP" --query '[0].name' -o tsv)"
  say ""
  say "Save the encryption keys in your password manager now (Key Vault $vault):"
  say "  az keyvault secret show --vault-name $vault --name encryption-key-k1 --query value -o tsv"
  say "  az keyvault secret show --vault-name $vault --name hash-key --query value -o tsv"
  say ""
fi
if [[ -n "${setup_code_created:-}" ]]; then
  show_setup_code_command "$vault"
fi

say "Waiting for Monthwise at $url ..."
for _ in $(seq 1 60); do
  if health="$(curl --silent --fail --max-time 10 "$url/api/health")"; then
    say "Monthwise is running: $health"
    exit 0
  fi
  sleep 5
done
say "Monthwise did not answer yet. Its log: az containerapp logs show -g $RESOURCE_GROUP -n ca-monthwise"
say ""
say "If the log says that bringing the database up to date failed, Monthwise does not work on the"
say "database and changes nothing more. To get the data back as it was before this deployment,"
say "restore the database to $before_deploy as a new server and point Monthwise to it"
say "(user manual, \"Azure's own backups\"):"
server="$(az postgres flexible-server list --resource-group "$RESOURCE_GROUP" --query '[0].name' -o tsv)"
say "  az postgres flexible-server restore --resource-group $RESOURCE_GROUP --name <new server name> \\"
say "    --source-server $server --restore-time $before_deploy \\"
say "    --subnet $(az postgres flexible-server show --resource-group "$RESOURCE_GROUP" --name "$server" \
  --query network.delegatedSubnetResourceId -o tsv) \\"
say "    --private-dns-zone $(az postgres flexible-server show --resource-group "$RESOURCE_GROUP" --name "$server" \
  --query network.privateDnsZoneArmResourceId -o tsv)"
exit 1
