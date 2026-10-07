a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKA_NS="kafka"
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

echo "🔑 Create Keycloak app secrets Kafka"
kubectl apply --server-side -f "${SCRIPT_DIR}/manifest-eso-secrets-setup.yaml" 1>/dev/null
echo "⏳ Waiting for secrets to become ready"
"${SCRIPT_DIR}/../../../utilities/wait-for-k8s-resource-existence.sh" --namespace "${KAFKA_NS}" --type secret --resource-name "kafka-broker-client" $VERBOSE_FLAG
"${SCRIPT_DIR}/../../../utilities/wait-for-k8s-resource-existence.sh" --namespace "${KAFKA_NS}" --type secret --resource-name "kafka-admin-user" $VERBOSE_FLAG

echo "🔑 Create Keycloak realm and clients"
"${SCRIPT_DIR}/keycloak-realm/script-create-kafka-realm-and-admin.sh" $VERBOSE_FLAG
"${SCRIPT_DIR}/keycloak-realm/script-create-kafka-clients.sh" $VERBOSE_FLAG

echo "🏗️ Deploying Kafka cluster"
# kubectl apply -f https://strimzi.io/examples/latest/kafka/kafka-single-node.yaml -n kafka 1>/dev/null
kubectl apply -f "${SCRIPT_DIR}/manifest-kafka-cluster.yaml" -n "${KAFKA_NS}" 1>/dev/null
echo "⏳ Waiting for Kafka cluster to be ready"
kubectl wait kafka.kafka.strimzi.io/our-kafka-cluster --for=condition=Ready --timeout=300s -n "${KAFKA_NS}"
