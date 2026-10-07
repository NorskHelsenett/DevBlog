echo "Deleting Keycloak realm for user login"
KEYCLOAK_URL="http://keycloak.localho.st:8080"
KC_ADMIN_USERNAME=admin
KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
REALM="kafka"

echo "Fetching admin user token"
TOKEN=$(curl -s \
  -d "client_id=admin-cli" \
  -d "username=${KC_ADMIN_USERNAME}" \
  -d "password=${KC_ADMIN_PASSWORD}" \
  -d "grant_type=password" \
  "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
  | jq -r .access_token)
# echo "Keycloak admin access token raw: ${TOKEN}"
# echo "Keycloak admin access token decoded:"
# echo $TOKEN | jq -R 'split(".") | .[1] | @base64d | fromjson'

curl -X DELETE "${KEYCLOAK_URL}/admin/realms/${REALM}" \
     -H "Authorization: Bearer ${TOKEN}" \
     -H "Accept: application/json"
