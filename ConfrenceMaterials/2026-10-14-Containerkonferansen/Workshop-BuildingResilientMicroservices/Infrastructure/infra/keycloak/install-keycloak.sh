a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KC_NS=keycloak
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KC_NS="$1"
      ;;
    --verbose*|-v*)
      # This is flag, don't consume next value # if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
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

echo "🪣 Create namespace for Keycloak"
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Namespace
metadata:
  labels:
    kubernetes.io/metadata.name: $KC_NS
    name: $KC_NS
  name: $KC_NS
EOF

echo "🔑 Create Keycloak secrets"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-eso-keycloak-internal-secrets.yaml" 1>/dev/null
echo "🔑 Create Keycloak app secrets Kafka"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-eso-app-secrets-kafka.yaml" 1>/dev/null
echo "📝 Create Keycloak startup script config map"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-keycloak-startup-script-config-map.yaml" 1>/dev/null

echo "⏳ Waiting for secrets to become ready"
"$SCRIPT_DIR/../../utilities/wait-for-k8s-resource-existence.sh" --namespace $KC_NS --type secret --resource-name kc-db
"$SCRIPT_DIR/../../utilities/wait-for-k8s-resource-existence.sh" --namespace $KC_NS --type secret --resource-name kafka-admin-user

echo "🏗️ Apply Keycloak databse manifest"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-keycloak-database.yaml" 1>/dev/null
kubectl -n $KC_NS wait --for=condition=available deployment/postgres --timeout=20s 1>/dev/null
# echo "🏗️ Apply Keycloak provisioning job manifest"
# kubectl apply --server-side -f "$SCRIPT_DIR/manifest-keycloak-provisioning-job.yaml" 1>/dev/null
# echo "⏳ Waiting for provisioning job to complete"
# kubectl -n $KC_NS wait --for=condition=complete job/keycloak-provisioning-job --timeout=5m

echo "🏗️ Apply Keycloak manifest"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-keycloak.yaml" 1>/dev/null

echo "⏳ Waiting for KC server startup"
kubectl -n $KC_NS rollout status statefulset keycloak --timeout 5m

log "Keycloak URL: http://keycloak.localhost.nhn.no:8080/"
log "Keycloak boostrap admin username: admin"
KC_BS_ADMIN_PW=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
log "Keycloak boostrap admin password: ${KC_BS_ADMIN_PW}"
log "Get password by running:"
log 'kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d'
log "Test by running:"
log "curl --request POST --url http://keycloak.localhost.nhn.no:8080/realms/local/protocol/openid-connect/token --header 'Content-Type: application/x-www-form-urlencoded' --data client_id=demo_app --data username=demo-admin --data password=${KC_BS_ADMIN_PW} --data realm=local --data grant_type=password"
echo "✅ Keycloak is now configured, up, and running!"
# echo "🚪 Create httproute for Keycloak"
# kubectl apply -f - >/dev/null <<EOF
# apiVersion: gateway.networking.k8s.io/v1
# kind: HTTPRoute
# metadata:
#   name: hr-keycloak
#   namespace: keycloak
# spec:
#   parentRefs:
#   - name: ourgateway
#     namespace: networking-gateway
#     kind: Gateway
#   hostnames:
#   - keycloak.localhost.nhn.no
#   - keycloak.localho.st
#   rules:
#   - backendRefs:
#     - name: keycloak
#       port: 8080
# EOF

# curl -s "http://keycloak.localhost.nhn.no:8080/realms/kafka/protocol/openid-connect/token" -d "client_id=kafka-tenant-a" -d "client_secret=$(kubectl -n keycloak get secrets kafka-client -o jsonpath="{.data.client-secret}" | base64 -d)" -d "grant_type=client_credentials" | jq -r .access_token

# TOKEN=$(curl -s \
#   -d "client_id=admin-cli" \
#   -d "username=admin" \
#   -d "password=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)" \
#   -d "grant_type=password" \
#   "http://keycloak.localhost.nhn.no:8080/realms/master/protocol/openid-connect/token" \
#   | jq -r .access_token)

# BROKER_UUID=$(curl -s -H "Authorization: Bearer $TOKEN" \
#   "http://keycloak.localhost.nhn.no:8080/admin/realms/kafka/clients" \
#   | jq -r '.[] | select(.clientId=="kafka-broker") | .id')

# AUTH_TOKEN=$(curl -s "http://keycloak.localhost.nhn.no:8080/realms/kafka/protocol/openid-connect/token" -d "client_id=kafka-tenant-a" -d "client_secret=$(kubectl -n keycloak get secrets kafka-client -o jsonpath="{.data.client-secret}" | base64 -d)" -d "grant_type=urn:ietf:params:oauth:grant-type:uma-ticket" -d "audience=kafka-broker" | jq -r .access_token)
# curl -s "http://keycloak.localhost.nhn.no:8080/realms/kafka/protocol/openid-connect/token" -d "client_id=kafka-tenant-a" -d "client_secret=$(kubectl -n keycloak get secrets kafka-client -o jsonpath="{.data.client-secret}" | base64 -d)" -d "grant_type=urn:ietf:params:oauth:grant-type:uma-ticket" -d "audience=kafka-broker" | jq -r .access_token | jq -R 'split(".") | .[1] | @base64d | fromjson'
