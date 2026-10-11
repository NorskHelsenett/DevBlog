a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

SCHEMA_REGISTRY_ADDRESS="http://schema-registry.localho.st:8080/apis/ccompat/v7"
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --registry-address*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      SCHEMA_REGISTRY_ADDRESS="$1"
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

echo "📚 Adding sample schemas to registry"

log "Registering avro schema"
AddSchemaToRegistry --name "example-person-avro" --schema-file "${SCRIPT_DIR}/Person.avsc" --type "AVRO"
log "Registering proto schema"
AddSchemaToRegistry --name "example-person-protobuf" --schema-file "${SCRIPT_DIR}/Person.proto" --type "PROTOBUF"
log "Registering json schema"
AddSchemaToRegistry --name "example-person-json" --schema-file "${SCRIPT_DIR}/Person.json" --type "JSON"

REGISTERED_SUBJECTS=$(curl -sS "${SCHEMA_REGISTRY_ADDRESS}/subjects" | jq)
PROTOBUF_VERSION_SUBJECTS=$(curl -sS "${SCHEMA_REGISTRY_ADDRESS}/subjects/example-person-protobuf/versions" | jq)
PROTOBUF_LATEST_VERSION=$(echo "${PROTOBUF_VERSION_SUBJECTS}" | jq '.[-1]')
REGISTERED_PROTOBUF_SCHEMA_CONTENT=$(curl -sS "${SCHEMA_REGISTRY_ADDRESS}/subjects/example-person-protobuf/versions/${PROTOBUF_LATEST_VERSION}" | jq)
log "Registered subjects: ${REGISTERED_SUBJECTS}"
log "Protobuf example schema versions: ${PROTOBUF_VERSION_SUBJECTS}"
log "Protobuf example schema latest version: ${PROTOBUF_LATEST_VERSION}"
log "Example protobuf schema first version content: ${REGISTERED_PROTOBUF_SCHEMA_CONTENT}"
