KEYCLOAK_URL="http://keycloak.localho.st:8080"
# KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
REALM="kafka-ui"
REALM_ADMIN_USERNAME="realm-admin"
REALM_ADMIN_PASSWORD="password"

VERBOSE_FLAG="logdisabled"
while [ $# -gt 0 ]; do
  case "$1" in
    --verbose*|-v*)
      VERBOSE_FLAG="--verbose"
      ;;
    *)
      >&2 printf "Error: Invalid parameter\n"
      exit 1
      ;;
  esac
  shift
done

log() {
  if [ -n "${VERBOSE_FLAG}" ]; then
    # Simple timestamped logger
    _now=$(date +%H:%M:%S)
    printf '[%s] %s\n' "$_now" "$*"
  fi
}

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

CLIENT_NAME="kafka-ui"
CLIENT_SECRET="todo-set-up-with-eso-and-retrieve-with-kubectl-here"
CLIENT_KCID="kafka-ui-client-db-id"

payload=$(jq -n \
  --arg clientId "$CLIENT_NAME" \
  --arg id "$CLIENT_KCID" \
  --arg clientSecret "$CLIENT_SECRET" \
  '{
    id: $id,
    clientId: $clientId,
    secret: $clientSecret,
    publicClient: "true",
    directAccessGrantsEnabled: "true",
    enabled: "true",
    redirectUris: ["*"],
    webOrigins: ["*"]
  }')

log "⚙️  Creating public client \"$CLIENT_NAME\" (id=$CLIENT_KCID) in realm $REALM"
curl -sS -X POST "${KEYCLOAK_URL}/admin/realms/${REALM}/clients" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "${payload}" -w "\nHTTP %{http_code}\n" 1>/dev/null

payload=$(jq -n \
  '{
    id: "openid_scope_db_id",
    name: "openid",
    protocol: "openid-connect",
    attributes: {
      "include.in.token.scope": "true"
    }
  }')

log "⚙️  Creating client-scope \"openid\" (id=openid_scope_db_id)"
curl -sS -X POST "${KEYCLOAK_URL}/admin/realms/${REALM}/client-scopes" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "${payload}" -w "\nHTTP %{http_code}\n" 1>/dev/null

log "⚙️  Adding default scope $OPENID_SCOPE_ID to client $CLIENT_NAME"
curl -sS -X PUT "${KEYCLOAK_URL}/admin/realms/${REALM}/clients/${CLIENT_KCID}/default-client-scopes/openid_scope_db_id" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '' -w "\nHTTP %{http_code}\n" 1>/dev/null

# log "--- Mappers ---"
log "Adding protocol mapper to client ${CLIENT_NAME} in realm ${REALM} so that groups in kc are included in tokes in an array named \"groups\""

curl -sS -i -X POST "${KEYCLOAK_URL}/admin/realms/${REALM}/clients/${CLIENT_KCID}/protocol-mappers/models" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "name":"groups",
        "protocol":"openid-connect",
        "protocolMapper":"oidc-group-membership-mapper",
        "consentRequired":"false",
        "config":{
          "access.token.claim":"true",
          "claim.name":"groups",
          "full.path":"false",
          "id.token.claim":"true",
          "jsonType.label": "String",
          "multivalued":"true",
          "userinfo.token.claim":"true"
        }
      }' \
  -w "\nHTTP %{http_code}\n" 1>/dev/null

log "Adding protocol mapper to client ${CLIENT_NAME} in realm ${REALM} so that realm roles in kc are included in tokes in an array named \"roles\""
curl -sS -i -X POST "${KEYCLOAK_URL}/admin/realms/${REALM}/clients/${CLIENT_KCID}/protocol-mappers/models" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "name":"roles-realm",
        "protocol":"openid-connect",
        "protocolMapper":"oidc-usermodel-realm-role-mapper",
        "consentRequired":"false",
        "config":{
          "access.token.claim":"true",
          "claim.name":"roles",
          "full.path":"false",
          "id.token.claim":"true",
          "introspection.token.claim":"true",
          "jsonType.label":"String",
          "multivalued":"true"
        }
      }' \
  -w "\nHTTP %{http_code}\n" 1>/dev/null

log "Adding protocol mapper to client ${CLIENT_NAME} in realm ${REALM} so that client roles in kc are included in tokes in an array named \"roles\""
curl -sS -i -X POST "${KEYCLOAK_URL}/admin/realms/${REALM}/clients/${CLIENT_KCID}/protocol-mappers/models" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
        "name":"roles-client",
        "protocol":"openid-connect",
        "protocolMapper":"oidc-usermodel-client-role-mapper",
        "consentRequired":"false",
        "config":{
          "access.token.claim":"true",
          "claim.name":"roles",
          "full.path":"false",
          "id.token.claim":"true",
          "introspection.token.claim":"true",
          "jsonType.label":"String",
          "multivalued":"true"
        }
      }' \
  -w "\nHTTP %{http_code}\n" 1>/dev/null

# log "all mappers"
# curl -s "${KEYCLOAK_URL}/admin/realms/${REALM}/clients/${CLIENT_KCID}/protocol-mappers/models" \
#   -H "Authorization: Bearer ${TOKEN}" | jq

# curl --request POST --url ${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token --header 'Content-Type: application/x-www-form-urlencoded' --data client_id=${CLIENT_NAME} --data client_secret=${CLIENT_SECRET} --data username=${REALM_ADMIN_USERNAME} --data password=password --data realm=kafka-ui --data grant_type=password | jq -r .access_token | jq -R 'split(".") | .[1] | @base64d | fromjson'

