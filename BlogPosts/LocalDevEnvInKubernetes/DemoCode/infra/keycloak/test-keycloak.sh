KEYCLOAK_URL="http://keycloak.localho.st:8080"
KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)

echo "Fetching admin user token"
TOKEN=$(curl -s \
  -d "client_id=admin-cli" \
  -d "username=admin" \
  -d "password=${KC_ADMIN_PASSWORD}" \
  -d "grant_type=password" \
  "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
  | jq -r .access_token)
echo "Keycloak admin access token raw: ${TOKEN}"
echo "Keycloak admin access token decoded:"
echo $TOKEN | jq -R 'split(".") | .[1] | @base64d | fromjson'

REALM="kafka"

echo "Fetching broker client internal kc id"
BROKER_KC_ID=$(curl -s -H "Authorization: Bearer ${TOKEN}" \
  "${KEYCLOAK_URL}/admin/realms/${REALM}/clients" \
  | jq -r '.[] | select(.clientId=="kafka-broker") | .id')
echo "Broker clinent internal KC ID: ${BROKER_KC_ID}"

echo "Fetching auth token for client"
CLIENT_AUTH_TOKEN=$(curl -s "${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token" \
  -d "client_id=kafka-tenant-a" \
  -d "client_secret=$(kubectl -n keycloak \
    get secrets kafka-client \
    -o jsonpath="{.data.client-secret}" \
    | base64 -d)" \
  -d "grant_type=urn:ietf:params:oauth:grant-type:uma-ticket" \
  -d "audience=kafka-broker" \
  | jq -r .access_token)
echo "Kafka tenant client auth token raw: ${CLIENT_AUTH_TOKEN}"
echo "Kafka tenant client auth token decoded:"
curl -s "${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token" -d "client_id=kafka-tenant-a" -d "client_secret=$(kubectl -n keycloak get secrets kafka-client -o jsonpath="{.data.client-secret}" | base64 -d)" -d "grant_type=urn:ietf:params:oauth:grant-type:uma-ticket" -d "audience=kafka-broker" | jq -r .access_token | jq -R 'split(".") | .[1] | @base64d | fromjson'
