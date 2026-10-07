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
      --value*|-v*)
        if [[ "$1" != *=* ]]; then shift; fi
        SCHEMA_VALUE="$1"
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
  log "Escaping double quotes in schema ${SCHEMA_NAME}"
  # Note double escape of the quotes (\\")
  local schema_escaped=$(echo "${SCHEMA_VALUE}" | sed 's/"/\\"/g')
  local schema_payload_file="${SCRIPT_DIR}/schema.json"

  log "Writing request body containing escaped schema to file"
  echo '{'                                      >  "${schema_payload_file}"
  echo "  \"schema\": \"${schema_escaped}\","   >> "${schema_payload_file}"
  echo "  \"schemaType\":\"${SCHEMA_TYPE}\""    >> "${schema_payload_file}"
  echo '}'                                      >> "${schema_payload_file}"
  # Valid schema types at the moment are ["JSON","PROTOBUF","AVRO"] (curl --silent -X GET http://schema-registry.localho.st:8080/apis/ccompat/v7/schemas/types)

  # log "removing newlines in data to post"
  # awk '{printf "%s",$0}' "${schema_payload_file}" > "${schema_payload_file}.tmp" && mv "${schema_payload_file}.tmp" "${schema_payload_file}"

  log "Posting schema to registry"
  curl -sS -X POST -H "Content-Type: application/json" \
    --data @"${schema_payload_file}" \
    "${SCHEMA_REGISTRY_ADDRESS}/subjects/${SCHEMA_NAME}/versions" 1>/dev/null
  rm "${schema_payload_file}"
  log "============== Done Creating Example Schema ${SCHEMA_NAME} ============="
}

CONTENT_AVRO=$(cat "${SCRIPT_DIR}/Person.avsc")
CONTENT_PROTO=$(cat "${SCRIPT_DIR}/Person.proto")
CONTENT_JSON=$(cat "${SCRIPT_DIR}/Person.json")
log "Registering avro schema"
AddSchemaToRegistry --name "example-person-avro" --value "${CONTENT_AVRO}" --type "AVRO"
log "Registering proto schema"
AddSchemaToRegistry --name "example-person-protobuf" --value "${CONTENT_PROTO}" --type "PROTOBUF"
log "Registering json schema"
AddSchemaToRegistry --name "example-person-json" --value "${CONTENT_JSON}" --type "JSON"

REGISTERED_SUBJECTS=$(curl -sS "${SCHEMA_REGISTRY_ADDRESS}/subjects" | jq)
PROTOBUF_VERSION_SUBJECTS=$(curl -sS "${SCHEMA_REGISTRY_ADDRESS}/subjects/example-person-protobuf/versions" | jq)
PROTOBUF_LATEST_VERSION=$(echo "${PROTOBUF_VERSION_SUBJECTS}" | jq '.[-1]')
REGISTERED_PROTOBUF_SCHEMA_CONTENT=$(curl -sS "${SCHEMA_REGISTRY_ADDRESS}/subjects/example-person-protobuf/versions/${PROTOBUF_LATEST_VERSION}" | jq)
log "Registered subjects: ${REGISTERED_SUBJECTS}"
log "Protobuf example schema versions: ${PROTOBUF_VERSION_SUBJECTS}"
log "Protobuf example schema latest version: ${PROTOBUF_LATEST_VERSION}"
log "Example protobuf schema first version content: ${REGISTERED_PROTOBUF_SCHEMA_CONTENT}"
