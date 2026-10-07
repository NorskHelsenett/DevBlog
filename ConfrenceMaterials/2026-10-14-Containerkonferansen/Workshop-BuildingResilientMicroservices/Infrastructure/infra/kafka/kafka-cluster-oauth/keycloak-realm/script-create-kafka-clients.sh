a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

LOG_ENABLED="logdisabled"
while [ $# -gt 0 ]; do
  case "$1" in
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

log() {
  if [ "$LOG_ENABLED" = "logenabled" ]; then
    # Simple timestamped logger
    _now=$(date +%H:%M:%S)
    printf '[%s] %s\n' "$_now" "$*"
  fi
}

KEYCLOAK_URL="http://keycloak.localho.st:8080"
# KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
REALM="kafka"
REALM_ADMIN_USERNAME="realm-admin"
REALM_ADMIN_PASSWORD="password"

log "Fetching admin user token"
TOKEN=$(curl -s \
  -d "client_id=admin-cli" \
  -d "username=${REALM_ADMIN_USERNAME}" \
  -d "password=${REALM_ADMIN_PASSWORD}" \
  -d "grant_type=password" \
  "${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token" \
  | jq -r .access_token)
# log "Keycloak realm admin access token raw: ${TOKEN}"
# log "Keycloak realm admin access token decoded:"
# log $TOKEN | jq -R 'split(".") | .[1] | @base64d | fromjson'

log "Creating client scopes protocol and openid-connect in realm"
curl -s -X POST "${KEYCLOAK_URL}/admin/realms/${REALM}/client-scopes" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name":"openid-connect","protocol":"openid-connect"}' 1>/dev/null

log "👥 Kafka: Create broker client"
KC_BROKER_CLIENT_ID=$(kubectl -n keycloak get secret kafka-broker-client -o jsonpath="{.data.client-id}" | base64 -d)
log "👥 Kafka Broker client: Retreived client ID"
KC_BROKER_CLIENT_SECRET=$(kubectl -n keycloak get secret kafka-broker-client -o jsonpath="{.data.client-secret}" | base64 -d)
log "👥 Kafka Broker client: Retreived client secret"
"${SCRIPT_DIR}/script-create-kafka-broker-client.sh" \
  --kc-url "${KEYCLOAK_URL}" \
  --realm "${REALM}" \
  --token "${TOKEN}" \
  --broker-client-name "${KC_BROKER_CLIENT_ID}" \
  --broker-client-secret "${KC_BROKER_CLIENT_SECRET}"

log "👥 Kafka: Create tenant clients"
KC_SCHEMA_REGISTRY_CLIENT_ID=$(kubectl -n keycloak get secret kafka-schema-registry-client -o jsonpath="{.data.client-id}" | base64 -d)
KC_SCHEMA_REGISTRY_CLIENT_SECRET=$(kubectl -n keycloak get secret kafka-schema-registry-client -o jsonpath="{.data.client-secret}" | base64 -d)
"${SCRIPT_DIR}/script-kc-create-tenant-client.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
  --tenant-name "${KC_SCHEMA_REGISTRY_CLIENT_ID}" --tenant-secret "${KC_SCHEMA_REGISTRY_CLIENT_SECRET}"
for t in schema-registry-storage-events \
         schema-registry-storage-journal \
         schema-registry-storage-snapshots; do
  log "Creating sr topic permission for topic ${t}"
  "${SCRIPT_DIR}/script-kc-create-tenant-permission.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
    --tenant-client-id "${KC_SCHEMA_REGISTRY_CLIENT_ID}" --resource-kind topic --resource-name "$t" --access-mode readwrite
done
log "Creating sr group permission"
"${SCRIPT_DIR}/script-kc-create-tenant-permission.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
   --tenant-client-id "${KC_SCHEMA_REGISTRY_CLIENT_ID}" --resource-kind group --resource-name schema-registry --access-mode read

KC_KAFKA_UI_CLIENT_ID=$(kubectl -n keycloak get secret kafka-kafbat-ui-client -o jsonpath="{.data.client-id}" | base64 -d)
KC_KAFKA_UI_CLIENT_SECRET=$(kubectl -n keycloak get secret kafka-kafbat-ui-client -o jsonpath="{.data.client-secret}" | base64 -d)
log "Creating KUI permissions"
"${SCRIPT_DIR}/script-kc-create-tenant-client.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
  --tenant-name "${KC_KAFKA_UI_CLIENT_ID}" --tenant-secret "${KC_KAFKA_UI_CLIENT_SECRET}"
for t in kafbat-ui-auditlogs; do
  "${SCRIPT_DIR}/script-kc-create-tenant-permission.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
    --tenant-client-id "${KC_KAFKA_UI_CLIENT_ID}" --resource-kind topic --resource-name "$t" --access-mode readwrite
done
"${SCRIPT_DIR}/script-kc-create-tenant-permission.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
   --tenant-client-id "${KC_KAFKA_UI_CLIENT_ID}" --resource-kind group --resource-name "${KC_KAFKA_UI_CLIENT_ID}" --access-mode read

KC_TENANT_A_CLIENT_ID=$(kubectl -n keycloak get secret kafka-tenant-a-client -o jsonpath="{.data.client-id}" | base64 -d)
KC_TENANT_A_CLIENT_SECRET=$(kubectl -n keycloak get secret kafka-tenant-a-client -o jsonpath="{.data.client-secret}" | base64 -d)
log "Creating Tenant A permissions"
"${SCRIPT_DIR}/script-kc-create-tenant-client.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
  --tenant-name "${KC_TENANT_A_CLIENT_ID}" --tenant-secret "${KC_TENANT_A_CLIENT_SECRET}"
for t in kafbat-ui-auditlogs; do
  "${SCRIPT_DIR}/script-kc-create-tenant-permission.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
    --tenant-client-id "${KC_TENANT_A_CLIENT_ID}" --resource-kind topic --resource-name "$t" --access-mode readwrite
done
"${SCRIPT_DIR}/script-kc-create-tenant-permission.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
   --tenant-client-id "${KC_TENANT_A_CLIENT_ID}" --resource-kind group --resource-name "${KC_TENANT_A_CLIENT_ID}" --access-mode read

KC_TENANT_B_CLIENT_ID=$(kubectl -n keycloak get secret kafka-tenant-b-client -o jsonpath="{.data.client-id}" | base64 -d)
KC_TENANT_B_CLIENT_SECRET=$(kubectl -n keycloak get secret kafka-tenant-b-client -o jsonpath="{.data.client-secret}" | base64 -d)
log "Creating Tenant B permissions"
"${SCRIPT_DIR}/script-kc-create-tenant-client.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
  --tenant-name "${KC_TENANT_B_CLIENT_ID}" --tenant-secret "${KC_TENANT_B_CLIENT_SECRET}"
for t in kafbat-ui-auditlogs; do
  "${SCRIPT_DIR}/script-kc-create-tenant-permission.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
    --tenant-client-id "${KC_TENANT_B_CLIENT_ID}" --resource-kind topic --resource-name "$t" --access-mode readwrite
done
"${SCRIPT_DIR}/script-kc-create-tenant-permission.sh" --kc-url "${KEYCLOAK_URL}" --realm "${REALM}" --token "${TOKEN}" \
   --tenant-client-id "${KC_TENANT_B_CLIENT_ID}" --resource-kind group --resource-name "${KC_TENANT_B_CLIENT_ID}" --access-mode read
log "Creating kafka clinet permissions: Done"
