#!/usr/bin/env bash
# --------------------------------------------------------------
# Grant a tenant client access to a Kafka resource (topic or
# consumer-group).  This script creates:
#   • The *resource* under the broker client (kafka-broker)
#   • The *group-policies* (reader / writer) under the broker client
#   • The *scope-permissions* that bind the tenant's policies to the
#     newly-created resource.
#
# It can be called repeatedly - it checks for existing objects and
# skips creation, making it safe for ad-hoc “add this new topic” work.
# --------------------------------------------------------------
# set -euo pipefail

# -----------------------------------------------------------------
# 1️⃣ INPUT PARAMETERS (keep the order, they are positional)
# -----------------------------------------------------------------
KC_URL="not_set"            # e.g. http://keycloak.keycloak.svc.cluster.local:8080
KC_REALM="not_set"          # e.g. kafka
KC_ADMIN_TOKEN="not_set"    # admin bearer token
TENANT_CLIENT_ID="not_set"  # the logical clientId created with script-1 (e.g. schema-registry)
RESOURCE_TYPE="not_set"     # "topic" or "group"
RESOURCE_NAME="not_set"     # exact Kafka name (e.g. schema-registry-storage-events)
ACCESS_MODE="not_set"       # "read", "write" or "readwrite"
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
    --tenant-client-id|-c*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      TENANT_CLIENT_ID="$1"
      ;;
    --resource-kind*|-k*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      RESOURCE_TYPE="$1"
      ;;
    --resource-name*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      RESOURCE_NAME="$1"
      ;;
    --access-mode*|-m*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      ACCESS_MODE="$1"
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

json_array() { local IFS=,; printf '[%s]' "$(printf '"%s",' "$@" | sed 's/,$//')"; }

kc_client_uuid() {
  local clientId=$1
  curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
        "${KC_URL}/admin/realms/${KC_REALM}/clients?clientId=${clientId}" |
    jq -r '.[0].id'
}

# -----------------------------------------------------------------
# 3️⃣ Resolve the *broker* client (the resource-server) and the tenant UUID
# -----------------------------------------------------------------
BROKER_CLIENT_ID=$(kubectl -n keycloak get secret kafka-broker-client -o jsonpath="{.data.client-id}" | base64 -d)
BROKER_UUID=$(kc_client_uuid "$BROKER_CLIENT_ID")
if [[ -z "${BROKER_UUID}" || "${BROKER_UUID}" == "null" ]]; then
  log "❌ Could not resolve broker client UUID - abort"
  exit 1
fi

TENANT_UUID=$(kc_client_uuid "${TENANT_CLIENT_ID}")
if [[ -z "${TENANT_UUID}" || "${TENANT_UUID}" == "null" ]]; then
  log "❌ Tenant client ${TENANT_CLIENT_ID} does not exist - abort"
  exit 1
fi

# -----------------------------------------------------------------
# 4️⃣ Build names for the two group-policies (they already exist for
#    the tenant because script-1 created the groups, but the policies
#    must live under the *broker* client - we create them lazily if missing)
# -----------------------------------------------------------------
READER_POLICY_NAME="${TENANT_CLIENT_ID}-reader-policy"
WRITER_POLICY_NAME="${TENANT_CLIENT_ID}-writer-policy"

# -----------------------------------------------------------------
# 5️⃣ Helper: create a group-policy under the *broker* client (idempotent)
# -----------------------------------------------------------------
create_policy_if_missing() {
  local policy_name=$1
  local group_uuid=$2   # UUID of the Keycloak group (reader or writer)

  # Does the policy already exist under the broker client?
  if curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
       "${KC_URL}/admin/realms/${KC_REALM}/clients/${BROKER_UUID}/authz/resource-server/policy/group" |
       jq -e ".[] | select(.name==\"$policy_name\")" >/dev/null; then
    log "✅  Policy ${policy_name} already exists - skipping"
    return
  fi

  log "🔐 Creating policy $policy_name (under broker client)"
  curl -s -X POST "${KC_URL}/admin/realms/${KC_REALM}/clients/${BROKER_UUID}/authz/resource-server/policy/group" \
    -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
    -H "Content-Type: application/json" \
    -d '{
      "name": "'$policy_name'",
      "description": "Policy for group '$policy_name'",
      "type": "group",
      "logic": "POSITIVE",
      "decisionStrategy": "UNANIMOUS",
      "groups": [{"id":"'$group_uuid'","extendChildren":false}]
    }' 1>/dev/null
}
# -----------------------------------------------------------------
# 6️⃣ Resolve the UUIDs of the tenant's *reader* and *writer* groups
# -----------------------------------------------------------------
PARENT_GROUP_UUID=$(curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  "${KC_URL}/admin/realms/${KC_REALM}/groups?search=$TENANT_CLIENT_ID" |
  jq -r '.[0].id')

READER_GROUP_UUID=$(curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  "${KC_URL}/admin/realms/${KC_REALM}/groups/$PARENT_GROUP_UUID/children?search=${TENANT_CLIENT_ID}-reader" |
  jq -r '.[0].id')
WRITER_GROUP_UUID=$(curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
  "${KC_URL}/admin/realms/${KC_REALM}/groups/$PARENT_GROUP_UUID/children?search=${TENANT_CLIENT_ID}-writer" |
  jq -r '.[0].id')

# -----------------------------------------------------------------
# 7️⃣ Ensure the two policies exist (under the broker client)
# -----------------------------------------------------------------
create_policy_if_missing "$READER_POLICY_NAME" "$READER_GROUP_UUID"
create_policy_if_missing "$WRITER_POLICY_NAME" "$WRITER_GROUP_UUID"

# -----------------------------------------------------------------
# 8️⃣ Create the *resource* (topic or group) under the broker client
# -----------------------------------------------------------------
if [[ "$RESOURCE_TYPE" == "topic" ]]; then
  KC_RESOURCE_TYPE="Topic"
  # Scopes needed for a topic - always include Describe DescribeConfigs
  case "$ACCESS_MODE" in
    read)       SCOPES='[{"name":"Read"},{"name":"Describe"},{"name":"DescribeConfigs"}]';;
    write)      SCOPES='[{"name":"Write"},{"name":"Describe"},{"name":"DescribeConfigs"}]';;
    readwrite)  SCOPES='[{"name":"Read"},{"name":"Write"},{"name":"Describe"},{"name":"DescribeConfigs"}]';;
    *) echo "❌ Unknown ACCESS_MODE $ACCESS_MODE"; exit 1;;
  esac
elif [[ "$RESOURCE_TYPE" == "group" ]]; then
  KC_RESOURCE_TYPE="Group"
  # Groups only need Read+Describe for now (the same for read/write)
  SCOPES='[{"name":"Read"},{"name":"Describe"},{"name":"DescribeConfigs"}]'
else
  echo "❌ RESOURCE_TYPE must be 'topic' or 'group'"
  exit 1
fi

# Idempotent resource creation
if curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
     "${KC_URL}/admin/realms/${KC_REALM}/clients/${BROKER_UUID}/authz/resource-server/resource" |
     jq -e ".[] | select(.name==\"${KC_RESOURCE_TYPE}:${RESOURCE_NAME}\")" >/dev/null; then
  log "✅  Resource ${KC_RESOURCE_TYPE}:${RESOURCE_NAME} already exists - skipping"
else
  log "📦 Creating resource ${KC_RESOURCE_TYPE}:${RESOURCE_NAME}"
  curl -s -X POST "${KC_URL}/admin/realms/${KC_REALM}/clients/${BROKER_UUID}/authz/resource-server/resource" \
    -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
    -H "Content-Type: application/json" \
    -d '{
      "name": "'${KC_RESOURCE_TYPE}':'${RESOURCE_NAME}'",
      "displayName": "'${KC_RESOURCE_TYPE}':'${RESOURCE_NAME}'",
      "uris": ["'${KC_RESOURCE_TYPE}':'${RESOURCE_NAME}'"],
      "scopes": '$SCOPES',
      "ownerManagedAccess": false
    }' 1>/dev/null
fi

# -----------------------------------------------------------------
# 9️⃣ Helper: create a *scope permission* (idempotent)
# -----------------------------------------------------------------
create_permission_if_missing() {
  local perm_name=$1
  local resource_str=$2   # e.g. Topic:my-topic
  local scopes_json=$3    # e.g. ["Read","Describe"]
  local policy_name=$4    # e.g. schema-registry-reader-policy

  # Does permission already exist?
  if curl -s -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
       "${KC_URL}/admin/realms/${KC_REALM}/clients/${BROKER_UUID}/authz/resource-server/permission/scope" |
       jq -e ".[] | select(.name==\"$perm_name\")" >/dev/null; then
    log "✅  Permission $perm_name already exists - skipping"
    return
  fi

  log "🔐 Creating permission $perm_name (policy $policy_name)"
  curl -s -X POST "${KC_URL}/admin/realms/${KC_REALM}/clients/${BROKER_UUID}/authz/resource-server/permission/scope" \
    -H "Authorization: Bearer ${KC_ADMIN_TOKEN}" \
    -H "Content-Type: application/json" \
    -d '{
      "name": "'$perm_name'",
      "description": "Auto-generated permission for '$TENANT_CLIENT_ID'",
      "resources": ["'$resource_str'"],
      "scopes": '$scopes_json',
      "policies": ["'$policy_name'"],
      "decisionStrategy": "AFFIRMATIVE"
    }' 1>/dev/null
}

# -----------------------------------------------------------------
# 10️⃣ Build permissions according to the requested ACCESS_MODE
# -----------------------------------------------------------------
RESOURCE_STR="${KC_RESOURCE_TYPE}:${RESOURCE_NAME}"

if [[ "$ACCESS_MODE" == "readwrite" ]]; then
  # Two separate permissions - one for the reader policy, one for the writer policy
  PERM_READ_NAME="${TENANT_CLIENT_ID}-${RESOURCE_TYPE}-${RESOURCE_NAME}-read"
  PERM_WRITE_NAME="${TENANT_CLIENT_ID}-${RESOURCE_TYPE}-${RESOURCE_NAME}-write"
  create_permission_if_missing "${PERM_READ_NAME}"  "${RESOURCE_STR}" '["Read","Describe","DescribeConfigs"]' "${READER_POLICY_NAME}"
  create_permission_if_missing "${PERM_WRITE_NAME}" "${RESOURCE_STR}" '["Write","Describe","DescribeConfigs"]' "${WRITER_POLICY_NAME}"
else
  # Single permission bound to the appropriate policy
  PERM_NAME="${TENANT_CLIENT_ID}-${RESOURCE_TYPE}-${RESOURCE_NAME}-${ACCESS_MODE}"
  SCOPES_FOR_PERMISSION=$(json_array $(echo "$SCOPES" | jq -r '.[].name'))
  if [[ "$ACCESS_MODE" == "read" ]]; then
    POLICY_USED="${READER_POLICY_NAME}"
  else   # write
    POLICY_USED="${WRITER_POLICY_NAME}"
  fi
  create_permission_if_missing "${PERM_NAME}" "${RESOURCE_STR}" "${SCOPES_FOR_PERMISSION}" "${POLICY_USED}"
fi

log "🎉  All requested artefacts for ${RESOURCE_TYPE} \"${RESOURCE_NAME}\" are in place."
