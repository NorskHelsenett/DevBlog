# Get scritp dir
a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)
# echo "Script dir: ${SCRIPT_DIR}"
. "${SCRIPT_DIR}/infra/container-runtime.sh"
echo "Using container runtime: ${CONTAINER_RUNTIME}"
# CURRENT_WORK_DIR=$(pwd)
# echo "PWD: ${CURRENT_WORK_DIR}"
kind create cluster --config "${SCRIPT_DIR}/infra/kind/kindCluster.yaml"
"${SCRIPT_DIR}/infra/oci-registry/install-registry.sh"
"${SCRIPT_DIR}/infra/dns/install-custom-dns-rules.sh"
"${SCRIPT_DIR}/infra/argocd/install-argocd.sh"
"${SCRIPT_DIR}/infra/gateway/install-gateway-haproxy.sh"
"${SCRIPT_DIR}/infra/external-secrets-operator/install-external-secrets-operator.sh"
"${SCRIPT_DIR}/infra/cloud-native-postgre/install-cnpg.sh"
"${SCRIPT_DIR}/infra/keycloak/install-keycloak.sh"
"${SCRIPT_DIR}/infra/kafka/install-kafka.sh"
# "${SCRIPT_DIR}/infra/observability/install-observability.sh"

"${SCRIPT_DIR}/utilities/extract-kubernetes-secret.sh" --namespace "kafka" --secret "ws-kafka-cluster-mtls-superuser" --output-dir "${SCRIPT_DIR}/credentials/kafka/"
"${SCRIPT_DIR}/utilities/extract-kubernetes-secret.sh" --namespace "kafka" --secret "ws-kafka-cluster-mtls-trustbundle" --output-dir "${SCRIPT_DIR}/credentials/kafka/"
