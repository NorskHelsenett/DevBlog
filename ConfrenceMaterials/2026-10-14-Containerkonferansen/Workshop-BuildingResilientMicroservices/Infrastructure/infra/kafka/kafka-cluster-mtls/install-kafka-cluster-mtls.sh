a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKA_NS="kafka"
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

echo "🏗️ Deploying Kafka cluster"
kubectl apply -f "$SCRIPT_DIR/manifest-kafka-cluster.yaml" -n "${KAFKA_NS}" 1>/dev/null
echo "⏳ Waiting for Kafka cluster to be ready"
kubectl wait kafka.kafka.strimzi.io/our-mtls-kafka-cluster --for=condition=Ready --timeout=300s -n "${KAFKA_NS}"
