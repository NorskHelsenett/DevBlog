a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKA_NS=kafka
WORKSHOP_NS=workshop
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
