# https://strimzi.io/quickstarts/

a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKBAT_UI_NS=kafka
RELEASE_NAME_KAFBAT_UI=kafbat-ui

echo "Removing hostrule"
kubectl delete httproutes.gateway.networking.k8s.io -n "${KAFKBAT_UI_NS}" hr-kafbat-ui
kubectl delete externalsecrets.external-secrets.io -n "${KAFKBAT_UI_NS}" create-kafbat-ui-kafka-client-secret
# kubectl delete secret -n $KAFKBAT_UI_NS kafbat-ui-kafka-client # Delete when deleting the external secret which manages it
echo "Helm unistalling kafbat kafkaui chart"
helm uninstall --cascade foreground --wait --namespace "${KAFKBAT_UI_NS}" "${RELEASE_NAME_KAFBAT_UI}"


echo "Deleting Keycloak realm for user login"
KEYCLOAK_URL="http://keycloak.localho.st:8080"
KC_ADMIN_USERNAME=admin
KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
REALM="kafka-ui"

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

curl -X DELETE "${KEYCLOAK_URL}/admin/realms/${REALM}" \
     -H "Authorization: Bearer ${TOKEN}" \
     -H "Accept: application/json"
