#!/usr/bin/env bash
# Provision the Fantasy Football RAG Azure AI Search environment (Build W2 — Ingestion).
# Region eastus2 · Free tier (vector search supported, $0 cost) · roles-only auth (no admin keys).
# Idempotent: existing service/role assignments are detected and skipped.
#
# Teardown when done experimenting:
#   ./scripts/teardown-azure.sh

set -euo pipefail

# --- config ---
RG=ff-rag-rg
LOC=eastus2                   # RG + Foundry live here; don't change (RG location is immutable)
SEARCH_LOC=eastus             # Search service region — free tier had no capacity in eastus2
SEARCH_NAME=ff-search-cneff   # globally-unique service name; bump if create conflicts
SKU=free                      # $0, supports vector fields; one-way door to a paid tier (recreate to upgrade)

echo "==> 1/3  Resource group: $RG ($LOC)"
az group create --name "$RG" --location "$LOC" --output none

echo "==> 2/3  Search service: $SEARCH_NAME ($SKU)"
if az search service show --name "$SEARCH_NAME" --resource-group "$RG" --output none 2>/dev/null; then
  echo "    already exists, skipping"
else
  az search service create \
    --name "$SEARCH_NAME" --resource-group "$RG"  --location "$SEARCH_LOC" \
    --sku "$SKU" --disable-local-auth true --output none
fi

# Roles-only data plane: the signed-in user needs both control-plane (index/schema
# management) and data-plane (document read/write) access.
echo "==> 3/3  Assign RBAC roles to the signed-in user"
USER_OID=$(az ad signed-in-user show --query id -o tsv)
SEARCH_ID=$(az search service show --name "$SEARCH_NAME" --resource-group "$RG" --query id -o tsv)

for ROLE in "Search Service Contributor" "Search Index Data Contributor"; do
  if az role assignment list --assignee "$USER_OID" --scope "$SEARCH_ID" \
       --query "[?roleDefinitionName=='$ROLE']" -o tsv 2>/dev/null | grep -q .; then
    echo "    '$ROLE' already assigned, skipping"
  else
    az role assignment create \
      --assignee "$USER_OID" \
      --role "$ROLE" \
      --scope "$SEARCH_ID" --output none
    echo "    assigned '$ROLE' (allow 1-5 min to propagate before first use)"
  fi
done

ENDPOINT="https://$SEARCH_NAME.search.windows.net"

echo ""
echo "==> Search service endpoint: $ENDPOINT"
echo ""
echo "Paste this into src/appsettings.json:"
echo "  \"Search\": { \"Endpoint\": \"$ENDPOINT\", \"IndexName\": \"ff-corpus\" }"
