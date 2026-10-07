#!/usr/bin/env bash
# --------------------------------------------------------------
# Create a Keycloak client that will be used by a Kafka tenant.
# This script ONLY creates the client, its service-account groups
# and the audience-mapper.  No resources or policies are created
# here - they belong to the *broker* client (kafka-broker) and will
# be added later with kc-grant-access.sh.
# --------------------------------------------------------------
# set -euo pipefail

# -----------------------------------------------------------------
# INPUTS (in the same order you used before)
# -----------------------------------------------------------------
KC_URL="not_set"           # e.g. http://keycloak.keycloak.svc.cluster.local:8080
KC_REALM="not_set"         # e.g. kafka
KC_ADMIN_TOKEN="not_set"   # admin bearer token (realm-admin role)
TENANT_NAME="not_set"      # logical tenant name, e.g. schema-registry
TENANT_SECRET="not_set"    # client_secret the tenant will use
LOG_ENABLED="logdisabled"
while [ $# -gt 0 ]; do
  case "$1" in
    --kc-url*|-k*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KC_URL="$1"
      ;;
    --token*|-t*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KC_ADMIN_TOKEN="$1"
      ;;
    --realm*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KC_REALM="$1"
      ;;
    --tenant-name*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      TENANT_NAME="$1"
      ;;
    --tenant-secret*|-s*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      TENANT_SECRET="$1"
      ;;
    --verbose*|-v*)
      # This is flag, don't consume next value # if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      LOG_ENABLED="logenabled"
      ;;
    *)
      >&2 printf "Error: Invalid parameter\n"
      exit 1
      ;;
  esac
  shift
done
# -----------------------------------------------------------------

log() {
  if [ "$LOG_ENABLED" = "logenabled" ]; then
    # Simple timestamped logger
    _now=$(date +%H:%M:%S)
    printf '[%s] %s\n' "$_now" "$*"
  fi
}
# -----------------------------------------------------------------
# Helper: resolve a client’s UUID from its clientId
# -----------------------------------------------------------------
kc_client_uuid() {
  local clientId=$1
  curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
        "${KC_URL}/admin/realms/${KC_REALM}/clients?clientId=${clientId}" |
    jq -r '.[0].id'
}

# -----------------------------------------------------------------
# 1️⃣  Create the OIDC client (if it does not already exist)
# -----------------------------------------------------------------
CLIENT_ID="${TENANT_NAME}-db-id"
CLIENT_CLIENT_ID="${TENANT_NAME}"

log "🧑‍🎨 Creating client \"${CLIENT_CLIENT_ID}\" with db id \"${CLIENT_ID}\" in realm \"${KC_REALM}\" at kc instance \"${KC_URL}\""

if [[ "$(kc_client_uuid "$CLIENT_CLIENT_ID")" == "null" || -z "$(kc_client_uuid "$CLIENT_CLIENT_ID")" ]]; then
  log "⚙️  Creating OIDC client $CLIENT_CLIENT_ID"
  curl -s -X POST "${KC_URL}/admin/realms/${KC_REALM}/clients" \
    -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
    -H "Content-Type: application/json" \
    -d '{
      "id": "'$CLIENT_ID'",
      "clientId": "'$CLIENT_CLIENT_ID'",
      "name": "'$CLIENT_CLIENT_ID'",
      "description": "Kafka OAuth client for tenant '$CLIENT_CLIENT_ID'",
      "protocol": "openid-connect",
      "publicClient": false,
      "bearerOnly": false,
      "directAccessGrantsEnabled": false,
      "serviceAccountsEnabled": true,
      "standardFlowEnabled": false,
      "authorizationServicesEnabled": false,
      "clientAuthenticatorType": "client-secret",
      "secret": "'$TENANT_SECRET'",
      "attributes": {"access.token.lifespan":"600"},
      "defaultClientScopes": ["openid","profile"],
      "optionalClientScopes": [],
      "protocolMappers": []
    }' 1>/dev/null
else
  log "✅  Client $CLIENT_CLIENT_ID already exists - skipping creation"
fi

# -----------------------------------------------------------------
# 2️⃣  Audience mapper - make the broker client the target audience
# -----------------------------------------------------------------
log "🔗 Adding audience mapper (kafka-broker) to client $CLIENT_CLIENT_ID"
curl -s -X POST "${KC_URL}/admin/realms/${KC_REALM}/clients/$CLIENT_ID/protocol-mappers/models" \
  -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "kafka-audience",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-audience-mapper",
    "consentRequired": false,
    "config": {
      "included.client.audience": "kafka-broker",
      "id.token.claim": "true",
      "access.token.claim": "true"
    }
  }' 1>/dev/null

# -----------------------------------------------------------------
# 3️⃣  Create parent group + reader / writer sub-groups
# -----------------------------------------------------------------
PARENT_GROUP_NAME="${TENANT_NAME}"
READER_GROUP_NAME="${TENANT_NAME}-reader"
WRITER_GROUP_NAME="${TENANT_NAME}-writer"

log "👥 Creating parent group ${PARENT_GROUP_NAME}"
curl -s -X POST "${KC_URL}/admin/realms/${KC_REALM}/groups" \
  -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name": "'$PARENT_GROUP_NAME'"}' 1>/dev/null

# Find parent UUID
PARENT_GROUP_UUID=$(curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  "${KC_URL}/admin/realms/${KC_REALM}/groups?search=${PARENT_GROUP_NAME}" |
  jq -r '.[0].id')

# Reader subgroup
log "👥 Creating reader subgroup ${READER_GROUP_NAME}"
curl -s -X POST "${KC_URL}/admin/realms/${KC_REALM}/groups/$PARENT_GROUP_UUID/children" \
  -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name": "'$READER_GROUP_NAME'"}' 1>/dev/null
READER_GROUP_UUID=$(curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  "${KC_URL}/admin/realms/${KC_REALM}/groups/$PARENT_GROUP_UUID/children?search=$READER_GROUP_NAME" |
  jq -r '.[0].id')

# Writer subgroup
log "👥 Creating writer subgroup $WRITER_GROUP_NAME"
curl -s -X POST "${KC_URL}/admin/realms/${KC_REALM}/groups/$PARENT_GROUP_UUID/children" \
  -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"name\": \"$WRITER_GROUP_NAME\"}" 1>/dev/null
WRITER_GROUP_UUID=$(curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  "${KC_URL}/admin/realms/${KC_REALM}/groups/$PARENT_GROUP_UUID/children?search=$WRITER_GROUP_NAME" |
  jq -r '.[0].id')

# -----------------------------------------------------------------
# 4️⃣  Attach the service-account user to both sub-groups
# -----------------------------------------------------------------
SERVICE_ACCOUNT_UUID=$(curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  "${KC_URL}/admin/realms/${KC_REALM}/clients/$CLIENT_ID/service-account-user" |
  jq -r '.id')

log "🔗 Adding service-account to $READER_GROUP_NAME"
curl -s -X PUT "${KC_URL}/admin/realms/${KC_REALM}/users/$SERVICE_ACCOUNT_UUID/groups/$READER_GROUP_UUID" \
  -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" 1>/dev/null

log "🔗 Adding service-account to $WRITER_GROUP_NAME"
curl -s -X PUT "${KC_URL}/admin/realms/${KC_REALM}/users/$SERVICE_ACCOUNT_UUID/groups/$WRITER_GROUP_UUID" \
  -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" 1>/dev/null

# -----------------------------------------------------------------
# 5️⃣  DONE - client is ready.  All authorisation artefacts (policies,
#      resources, permissions) will be created later with
#      kc-grant-access.sh.
# -----------------------------------------------------------------
log "🎉  Tenant client ${CLIENT_CLIENT_ID} set-up complete."
log "   • Client UUID               : ${CLIENT_ID}"
log "   • Parent group UUID         : ${PARENT_GROUP_UUID}"
log "   • Reader group UUID         : ${READER_GROUP_UUID}"
log "   • Writer group UUID         : ${WRITER_GROUP_UUID}"
log "   • Service-account UUID      : ${SERVICE_ACCOUNT_UUID}"
