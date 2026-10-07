a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KEYCLOAK_URL="http://keycloak.localho.st:8080"
KC_ADMIN_USERNAME=admin
KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
NEW_REALM_NAME=kafka
NEW_REALM_ADMIN_USERNAME=realm-admin

# echo "Fetching admin user token"
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

"${SCRIPT_DIR}/../../../../utilities/keycloak-create-realm-with-admin.sh" \
  --kc-url "${KEYCLOAK_URL}"\
  --token "${TOKEN}" \
  --realm "${NEW_REALM_NAME}" \
  --new-admin-username "${NEW_REALM_ADMIN_USERNAME}"
