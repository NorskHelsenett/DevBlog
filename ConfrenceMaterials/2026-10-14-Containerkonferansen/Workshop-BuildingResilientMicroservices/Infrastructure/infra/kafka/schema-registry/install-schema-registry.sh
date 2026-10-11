a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKA_NS=kafka
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
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

###
VERSION=3.3.3
APICURIO_OPERATOR_MANIFEST_FILE="${SCRIPT_DIR}/manifest-install-apicurio-operator.yaml"
"${SCRIPT_DIR}/../../../utilities/refresh-file.sh" \
  --url "https://raw.githubusercontent.com/Apicurio/apicurio-registry/${VERSION}/operator/install/install.yaml" \
  --file "${APICURIO_OPERATOR_MANIFEST_FILE}" \
  $VERBOSE_FLAG
awk -v ns="${KAFKA_NS}" '{ gsub(/PLACEHOLDER_NAMESPACE/, ns) }1' "${APICURIO_OPERATOR_MANIFEST_FILE}" \
  > "${APICURIO_OPERATOR_MANIFEST_FILE}.tmp" \
  && mv "${APICURIO_OPERATOR_MANIFEST_FILE}.tmp" "${APICURIO_OPERATOR_MANIFEST_FILE}"
kubectl apply -n "${KAFKA_NS}" -f "${SCRIPT_DIR}/manifest-install-apicurio-operator.yaml"

# "${SCRIPT_DIR}/../../../utilities/refresh-file.sh" \
#   --url "https://raw.githubusercontent.com/Apicurio/apicurio-registry/refs/tags/${VERSION}/operator/controller/src/main/deploy/examples/simple.apicurioregistry3.yaml" \
#   --file "${SCRIPT_DIR}/manifest-install-apicurio-operator-controller-deployment.yaml"
# kubectl -n "${KAFKA_NS}" apply -f "${SCRIPT_DIR}/manifest-install-apicurio-operator-controller-deployment.yaml"
###

echo "🏗️ Deploying Schema registry"
kubectl apply -f "${SCRIPT_DIR}/manifest-kafka-schema-registry-mtls.yaml" -n "${KAFKA_NS}" 1>/dev/null
echo "⏳ Waiting for schema registry App to be ready"
"$SCRIPT_DIR/../../../utilities/wait-for-k8s-resource-existence.sh" --namespace "${KAFKA_NS}" --type "deployments.apps" --resource-name "schema-registry-app-deployment" $VERBOSE_FLAG
echo "⏳ Waiting for schema registry UI to be ready"
"$SCRIPT_DIR/../../../utilities/wait-for-k8s-resource-existence.sh" --namespace "${KAFKA_NS}" --type "deployments.apps" --resource-name "schema-registry-ui-deployment" $VERBOSE_FLAG
# "$SCRIPT_DIR/../../../utilities/wait-for-k8s-resource-existence.sh" --namespace "${KAFKA_NS}" --type "deployments.apps" --resource-name "schema-registry"

# kubectl -n "${KAFKA_NS}" wait --for=condition=available deployments/schema-registry --timeout=20s 1>/dev/null
kubectl -n "${KAFKA_NS}" wait --for=condition=available deployments/schema-registry-app-deployment --timeout=20s 1>/dev/null
kubectl -n "${KAFKA_NS}" rollout status deployment/schema-registry-app-deployment --timeout=2m


TIMEOUTSECONDS=60
start=$(date +%s)
end=$(( start + TIMEOUT ))
while :   # “:” is the POSIX no‑op command, equivalent to “while true”
do
    # Does the resource already exist?
    if curl -fsS "http://schema-registry.localho.st:8080/apis/ccompat/v7/subjects" | jq . >/dev/null 2>&1; then
        printf '\r\e[K✅ Schema registry is ready to receive input\n'
        exit 0
    fi

    now=$(date +%s)
    if [ "$now" -ge "$end" ]; then
        printf '\r\e[K❌ Timed-out after %s seconds - schema registry did not come up and start serving its api in time.\n' "${TIMEOUT}"
        exit 1
    fi
    # --------- progress line (single‑line update) ----------
    elapsed=$(( now - start ))
    remaining=$(( end - now ))
    printf '\r\e[K⏳ Waiting for "%s" resource "%s" in namespace "%s"... %ds elapsed, %ds left' \
           "${RESOURCE_TYPE}" "${RESOURCE_NAME}" "${NAMESPACE}" "$elapsed" "$remaining"
    sleep "${INTERVAL}"
done

echo "🌐 Schema registry: http://schema-registry.localho.st:8080/apis/ccompat/v7"
echo "📚 Adding sample schemas to registry"
"${SCRIPT_DIR}/example-schemas/script-register-example-schemas.sh"
