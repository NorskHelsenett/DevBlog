# https://strimzi.io/quickstarts/

a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKBAT_UI_NS=kafka
RELEASE_NAME_KAFBAT_UI=kafbat-ui
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KAFKBAT_UI_NS="$1"
      ;;
    --release*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      RELEASE_NAME_KAFBAT_UI="$1"
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

echo "🧑‍🎨 Setting up Kafbat UI: Adding helm repo"
helm repo add kafbat-ui https://kafbat.github.io/helm-charts 1>/dev/null
echo "🧑‍🎨 Setting up Kafbat UI: Updating helm repo"
helm repo update 1>/dev/null
# kubectl apply -f "${SCRIPT_DIR}/manifest-eso-secrets-setup.yaml" -n $KAFKBAT_UI_NS 1>/dev/null
kubectl apply -f "${SCRIPT_DIR}/manifest-kafka-resources.yaml" -n $KAFKBAT_UI_NS 1>/dev/null

echo "⏳ Waiting for secrets to become ready"
# "$SCRIPT_DIR/../../../utilities/wait-for-k8s-resource-existence.sh" --namespace "${KAFKBAT_UI_NS}" --type secret --resource-name kafka-kafbat-ui-client

echo "🔐 Creating realm, groups, and users in Keycloak"
"${SCRIPT_DIR}/script-create-kafbatui-realm-and-admin.sh" $VERBOSE_FLAG
"${SCRIPT_DIR}/script-create-kafbatui-client.sh" $VERBOSE_FLAG
"${SCRIPT_DIR}/script-create-kafbatui-groups-and-users.sh" $VERBOSE_FLAG

echo "🧑‍🎨 Setting up Kafbat UI: Install chart using config from map"
helm install "${RELEASE_NAME_KAFBAT_UI}" \
  kafbat-ui/kafka-ui \
  --namespace "${KAFKBAT_UI_NS}" \
  -f "${SCRIPT_DIR}/values-kafbat-ui-mtls.yaml"

echo "🚪 Create httproute for Kafbat UI"
kubectl apply -f - >/dev/null <<EOF
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: hr-kafbat-ui
  namespace: $KAFKBAT_UI_NS
spec:
  parentRefs:
  - name: ourgateway
    namespace: networking-gateway
    kind: Gateway
  hostnames:
  - kafka-ui.localhost.nhn.no
  - kafka-ui.localho.st
  rules:
  - backendRefs:
    - name: $RELEASE_NAME_KAFBAT_UI-kafka-ui
      port: 80
EOF
echo "🌐 Kafka UI: http://kafka-ui.localho.st:8080"
