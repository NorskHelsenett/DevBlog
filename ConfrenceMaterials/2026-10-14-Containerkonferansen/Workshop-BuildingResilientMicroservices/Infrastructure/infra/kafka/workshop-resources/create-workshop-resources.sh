a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKA_NS=kafka
WORKSHOP_NS=workshop
SCHEMA_REGISTRY_ADDRESS="http://schema-registry.localho.st:8080/apis/ccompat/v7"
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      WORKSHOP_NS="$1"
      ;;
    --kafka-namespace*|-k*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KAFKA_NS="$1"
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

echo "🪣 Create ${WORKSHOP_NS} namespace"
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Namespace
metadata:
  labels:
    kubernetes.io/metadata.name: $WORKSHOP_NS
    name: $WORKSHOP_NS
  name: $WORKSHOP_NS
EOF

kubectl apply -f "${SCRIPT_DIR}/manifest-eso-secret-store.yaml" -n "${WORKSHOP_NS}" 1>/dev/null
kubectl apply -f "${SCRIPT_DIR}/manifest-eso-copy-secrets.yaml" -n "${WORKSHOP_NS}" 1>/dev/null

function AddSchemaToRegistry {
  while [ $# -gt 0 ]; do
    case "$1" in
      --name*|-n*)
        if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
        SCHEMA_NAME="$1"
        ;;
      --schema-file*|-f*)
        if [[ "$1" != *=* ]]; then shift; fi
        SCHMEA_FILE="$1"
        ;;
      --type*|-t*)
        if [[ "$1" != *=* ]]; then shift; fi
        SCHEMA_TYPE="$1"
        ;;
      *)
        >&2 printf "Error: Invalid argument\n"
        exit 1
        ;;
    esac
    shift
  done

  log "============== Creating Example Schema ${SCHEMA_NAME} =================="
  log "Creating schema payload ${SCHEMA_NAME}"
  JSON_PAYLOAD=$(jq -Rs --arg st "$SCHEMA_TYPE" '{schema: ., schemaType: $st}' "${SCHMEA_FILE}")

  log "Posting schema to registry"
  curl -X POST "${SCHEMA_REGISTRY_ADDRESS}/subjects/${SCHEMA_NAME}/versions" \
    -H "Content-Type: application/vnd.schemaregistry.v1+json" \
    -d "${JSON_PAYLOAD}"
  log "============== Done Creating Example Schema ${SCHEMA_NAME} ============="
}

log "Registering workshop schemas"
AddSchemaToRegistry --name "ws-cluster-1-local-read-v1-value" --schema-file "${SCRIPT_DIR}/WsSchema.proto" --type "PROTOBUF"
AddSchemaToRegistry --name "ws-cluster-1-local-write-v1-value" --schema-file "${SCRIPT_DIR}/WsSchema.proto" --type "PROTOBUF"
AddSchemaToRegistry --name "ws-cluster-2-local-write-v1-value" --schema-file "${SCRIPT_DIR}/WsSchema.proto" --type "PROTOBUF"
AddSchemaToRegistry --name "ws-cluster-2-local-read-v1-value" --schema-file "${SCRIPT_DIR}/WsSchema.proto" --type "PROTOBUF"
