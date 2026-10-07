KEYCLOAK_URL="http://keycloak.localho.st:8080"
# KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
REALM="kafka"
REALM_ADMIN_USERNAME="realm-admin"
REALM_ADMIN_PASSWORD="password"

# KEYCLOAK_URL="http://localhost:8080"
# REALM="kafka"

# kafka-broker-client kafka-tenant-b-client kafka-schema-registry-client
# TENANT_SECRET_NAME=kafka-broker-client
# TENANT_SECRET_NAME=kafka-tenant-b-client
TENANT_SECRET_NAME=kafka-schema-registry-client
KC_TENANT_CLIENT_ID=$(kubectl -n keycloak get secret "${TENANT_SECRET_NAME}" -o jsonpath="{.data.client-id}" | base64 -d)
KC_TENANT_CLIENT_SECRET=$(kubectl -n keycloak get secret "${TENANT_SECRET_NAME}" -o jsonpath="{.data.client-secret}" | base64 -d)
echo "Fetching auth token"
echo "    Kc instance: ${KEYCLOAK_URL}"
echo "    Kc realm: ${REALM}"
echo "    ClientID: ${KC_TENANT_CLIENT_ID}"
echo "    Client secret: ${KC_TENANT_CLIENT_SECRET}"

TOKEN_RESPONSE=$(curl -s "${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token" \
  -d "client_id=${KC_TENANT_CLIENT_ID}" \
  -d "client_secret=${KC_TENANT_CLIENT_SECRET}" \
  -d "grant_type=urn:ietf:params:oauth:grant-type:uma-ticket" \
  -d "audience=kafka-broker")
echo "Token response: ${TOKEN_RESPONSE}"
ACCESS_TOKEN_FROM_RESPONSE=$(echo "${TOKEN_RESPONSE}" | jq -r .access_token)
echo "Access token from response: ${ACCESS_TOKEN_FROM_RESPONSE}"

# AUTH_TOKEN=$(curl -s "${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token" \
#   -d "client_id=${KC_TENANT_CLIENT_ID}" \
#   -d "client_secret=${KC_TENANT_CLIENT_SECRET}" \
#   -d "grant_type=urn:ietf:params:oauth:grant-type:uma-ticket" \
#   -d "audience=kafka-broker" \
#   | jq -r .access_token)
# echo "Keycloak token raw: ${AUTH_TOKEN}"
# echo "Keycloak client access token decoded:"
# echo $TOKEN | jq -R 'split(".") | .[1] | @base64d | fromjson'

echo $ACCESS_TOKEN_FROM_RESPONSE | jq -R 'split(".") | .[1] | @base64d | fromjson'
