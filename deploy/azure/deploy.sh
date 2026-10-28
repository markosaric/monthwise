#!/usr/bin/env bash
# Deploys a Monthwise release to Azure in one step (FR-10). Runs in Azure Cloud Shell or anywhere
# with the Azure CLI signed in (az login). Creates the secrets on the first deployment and reuses
# them from Key Vault afterwards, so the data stays readable.
#
# Usage: deploy.sh <version>      e.g. deploy.sh 1.2.0
# Environment:
#   GHCR_USERNAME, GHCR_TOKEN   GitHub user and a token with only read:packages (required)
#   BUDGET_EMAIL                email address for the budget alert (required)
#   MONTHWISE_RESOURCE_GROUP    default rg-monthwise
#   MONTHWISE_LOCATION          default westeurope (EU)
#   MONTHWISE_BUDGET            monthly budget, default 25
set -euo pipefail

VERSION="${1:?Usage: deploy.sh <version>, e.g. deploy.sh 1.2.0}"
VERSION="${VERSION#v}"
: "${GHCR_USERNAME:?Set GHCR_USERNAME to your GitHub user}"
: "${GHCR_TOKEN:?Set GHCR_TOKEN to a GitHub token with only the read:packages scope}"
: "${BUDGET_EMAIL:?Set BUDGET_EMAIL to the address for the budget alert}"
RESOURCE_GROUP="${MONTHWISE_RESOURCE_GROUP:-rg-monthwise}"
LOCATION="${MONTHWISE_LOCATION:-westeurope}"
BUDGET="${MONTHWISE_BUDGET:-25}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

say() { printf '%s\n' "$*"; }

az account show > /dev/null || { say "Sign in first: az login"; exit 1; }
say "Subscription: $(az account show --query name -o tsv). Resource group: $RESOURCE_GROUP ($LOCATION)."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

vault="$(az keyvault list --resource-group "$RESOURCE_GROUP" --query '[0].name' -o tsv)"
if [[ -z "$vault" ]]; then
  say "First deployment: creating the database password and the card number encryption keys."
  postgres_password="$(openssl rand -hex 24)"
  encryption_key="$(openssl rand -base64 32)"
  hash_key="$(openssl rand -base64 32)"
  budget_start="$(date -u +%Y-%m-01)"
else
  say "Reusing the secrets in Key Vault $vault."
  read_secret() { az keyvault secret show --vault-name "$vault" --name "$1" --query value -o tsv; }
  postgres_password="$(read_secret postgres-password)"
  encryption_key="$(read_secret encryption-key-k1)"
  hash_key="$(read_secret hash-key)"
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
    "encryptionKey": { "value": "$encryption_key" },
    "hashKey": { "value": "$hash_key" },
    "deployerObjectId": { "value": "$(az ad signed-in-user show --query id -o tsv)" },
    "budgetEmail": { "value": "$BUDGET_EMAIL" },
    "budgetAmount": { "value": $BUDGET },
    "budgetStartDate": { "value": "$budget_start" }
  }
}
EOF

deploy() {
  az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "monthwise-$VERSION-$(date -u +%Y%m%d%H%M%S)" \
    --template-file "$HERE/main.bicep" \
    --parameters "@$parameters" \
    --query 'properties.outputs.url.value' -o tsv
}

say "Deploying Monthwise $VERSION (this takes several minutes the first time)..."
# On the first deployment the app's access to Key Vault can take a minute to apply: try again once.
url="$(deploy)" || { say "Retrying in 60 seconds..."; sleep 60; url="$(deploy)"; }

if [[ -z "$vault" ]]; then
  vault="$(az keyvault list --resource-group "$RESOURCE_GROUP" --query '[0].name' -o tsv)"
  say ""
  say "Save the encryption keys in your password manager now (Key Vault $vault):"
  say "  az keyvault secret show --vault-name $vault --name encryption-key-k1 --query value -o tsv"
  say "  az keyvault secret show --vault-name $vault --name hash-key --query value -o tsv"
  say ""
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
exit 1
