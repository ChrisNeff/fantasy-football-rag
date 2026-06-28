#!/usr/bin/env bash
set -euo pipefail

# Deletes the whole Foundry resource group (resource + project + deployments).
RESOURCE_GROUP="ff-rag-rg"

echo ""
echo "============================================="
echo " Azure Teardown — fantasy-football-rag"
echo "============================================="
echo ""
echo " This will permanently delete resource group: $RESOURCE_GROUP"
echo "   - Foundry resource + project"
echo "   - gpt-5-mini + text-embedding-3-small deployments"
echo ""
read -r -p " Type the resource group name to confirm: " CONFIRM

if [[ "$CONFIRM" != "$RESOURCE_GROUP" ]]; then
  echo "Confirmation did not match. Aborting."
  exit 1
fi

echo ""
echo "==> Deleting resource group: $RESOURCE_GROUP (background)..."
az group delete --name "$RESOURCE_GROUP" --yes --no-wait

echo "==> Deletion initiated. Check status with:"
echo "    az group show --name $RESOURCE_GROUP --query properties.provisioningState"
