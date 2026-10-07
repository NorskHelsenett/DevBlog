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
# kubectl create namespace kafka

# helm repo add strimzi https://strimzi.io/charts/
# helm repo update
# helm install strimzi-operator strimzi/strimzi-kafka-operator \
#   --namespace strimzi --create-namespace \
#   --set watchAnyNamespace=true
echo "🕵️ Check if Kafka Strimzi install maifest stale and update it"
"${SCRIPT_DIR}/../../../utilities/refresh-file.sh" \
  --url "https://strimzi.io/install/latest?namespace=kafka" \
  --file "${SCRIPT_DIR}/manifest-strimzi-install.yaml"
kubectl create -f "${SCRIPT_DIR}/manifest-strimzi-install.yaml" -n $KAFKA_NS 1>/dev/null

echo "⏳ Waiting for Kafka Strimzi operator to be ready"
# sleep 20
# ToDo: Figure out what to wait for
# Given the docs `kubectl logs deployment/strimzi-cluster-operator -n kafka -f`
# probably kubectl -n $KAFKA_NS wait --for=condition=available deployment/strimzi-cluster-operator --timeout=40s 1>/dev/null
kubectl -n $KAFKA_NS wait --for=condition=available deployment/strimzi-cluster-operator --timeout=40s 1>/dev/null

helm install my-strimzi-access-operator oci://quay.io/strimzi-helm/strimzi-access-operator
