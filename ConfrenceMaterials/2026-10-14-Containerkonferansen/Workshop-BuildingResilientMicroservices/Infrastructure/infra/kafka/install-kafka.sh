# https://strimzi.io/quickstarts/

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

echo "🪣 Create namespace for kafka"
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Namespace
metadata:
  labels:
    kubernetes.io/metadata.name: $KAFKA_NS
    name: $KAFKA_NS
  name: $KAFKA_NS
EOF

"${SCRIPT_DIR}/strimzi/install-strimzi.sh" --namespace "${KAFKA_NS}" $VERBOSE_FLAG

# "${SCRIPT_DIR}/kafka-cluster-oauth/install-kafka-cluster-oauth.sh" --namespace "${KAFKA_NS}"
"${SCRIPT_DIR}/kafka-cluster-mtls/install-kafka-cluster-mtls.sh" --namespace "${KAFKA_NS}" $VERBOSE_FLAG

"${SCRIPT_DIR}/schema-registry/install-schema-registry.sh" --namespace "${KAFKA_NS}" $VERBOSE_FLAG

"${SCRIPT_DIR}/kafbat-ui/install-kafka-ui.sh" --namespace "${KAFKA_NS}" $VERBOSE_FLAG
