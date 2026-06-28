#!/usr/bin/env bash
# Provision the Fantasy Football RAG Foundry environment (Build W1 — Scaffold).
# Region eastus2 · single Foundry project · gpt-5-mini (chat) + text-embedding-3-small (embed).
# Idempotent: existing resource/project are detected and skipped; deployments are no-ops if present.
#
# Teardown when done experimenting (deployments bill while they exist):
#   ./scripts/teardown-azure.sh

set -euo pipefail

# --- config ---
RG=ff-rag-rg
LOC=eastus2
ACCT=ff-foundry-cneff        # globally-unique custom subdomain; bump if create conflicts
PROJ=ff-rag
CHAT_MODEL=gpt-5-mini
EMBED_MODEL=text-embedding-3-small
SKU=GlobalStandard           # modern Foundry default; current GPT-5 models require it

echo "==> 1/6  Resource group: $RG ($LOC)"
az group create --name "$RG" --location "$LOC" --output none

echo "==> 2/6  Foundry resource: $ACCT"
if az cognitiveservices account show --name "$ACCT" --resource-group "$RG" --output none 2>/dev/null; then
  echo "    already exists, skipping"
else
  az cognitiveservices account create \
    --name "$ACCT" --resource-group "$RG" \
    --kind AIServices --sku s0 --location "$LOC" \
    --custom-domain "$ACCT" --public-network-access Enabled --yes --output none
fi

echo "==> 3/6  Project: $PROJ"
if az cognitiveservices account project show --name "$ACCT" --resource-group "$RG" --project-name "$PROJ" --output none 2>/dev/null; then
  echo "    already exists, skipping"
else
  az cognitiveservices account project create \
    --name "$ACCT" --resource-group "$RG" \
    --project-name "$PROJ" --location "$LOC" --output none
fi

# Resolve the latest available version for each model in this region
# (sort the version strings, take the last = newest).
CHAT_VER=$(az cognitiveservices model list -l "$LOC" \
  --query "[?model.name=='$CHAT_MODEL'].model.version | sort(@) | [-1]" -o tsv)
EMBED_VER=$(az cognitiveservices model list -l "$LOC" \
  --query "[?model.name=='$EMBED_MODEL'].model.version | sort(@) | [-1]" -o tsv)
echo "    resolved versions: $CHAT_MODEL=$CHAT_VER  $EMBED_MODEL=$EMBED_VER"

echo "==> 4/6  Deploy chat model: $CHAT_MODEL ($CHAT_VER)"
az cognitiveservices account deployment create \
  --name "$ACCT" --resource-group "$RG" \
  --deployment-name "$CHAT_MODEL" --model-name "$CHAT_MODEL" \
  --model-version "$CHAT_VER" \
  --model-format OpenAI --sku-name "$SKU" --sku-capacity 10 --output none

echo "==> 5/6  Deploy embedding model: $EMBED_MODEL ($EMBED_VER)"
az cognitiveservices account deployment create \
  --name "$ACCT" --resource-group "$RG" \
  --deployment-name "$EMBED_MODEL" --model-name "$EMBED_MODEL" \
  --model-version "$EMBED_VER" \
  --model-format OpenAI --sku-name "$SKU" --sku-capacity 10 --output none

# Keyless auth proves identity but the data plane still needs an RBAC role.
# Grant the signed-in user the chat/embeddings data action on this resource.
echo "==> 6/6  Assign 'Cognitive Services OpenAI User' to the signed-in user"
USER_OID=$(az ad signed-in-user show --query id -o tsv)
ACCT_ID=$(az cognitiveservices account show --name "$ACCT" --resource-group "$RG" --query id -o tsv)
if az role assignment list --assignee "$USER_OID" --scope "$ACCT_ID" \
     --query "[?roleDefinitionName=='Cognitive Services OpenAI User']" -o tsv 2>/dev/null | grep -q .; then
  echo "    role already assigned, skipping"
else
  az role assignment create \
    --assignee "$USER_OID" \
    --role "Cognitive Services OpenAI User" \
    --scope "$ACCT_ID" --output none
  echo "    assigned (allow 1-5 min to propagate before the first model call)"
fi

echo ""
echo "==> Deployments:"
az cognitiveservices account deployment list \
  --name "$ACCT" --resource-group "$RG" \
  --query "[].{Deployment:name, Model:properties.model.name, State:properties.provisioningState}" \
  -o table

ENDPOINT=$(az cognitiveservices account show \
  --name "$ACCT" --resource-group "$RG" \
  --query "properties.endpoint" -o tsv)

echo ""
echo "==> Resource endpoint: $ENDPOINT"
echo ""
echo "Export these for the app:"
echo "  export AZURE_OPENAI_ENDPOINT=\"$ENDPOINT\""
echo "  export CHAT_DEPLOYMENT=\"$CHAT_MODEL\""
