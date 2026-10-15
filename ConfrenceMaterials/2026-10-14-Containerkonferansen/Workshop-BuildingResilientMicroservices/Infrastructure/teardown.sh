a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)
. "${SCRIPT_DIR}/infra/container-runtime.sh"
kind delete cluster --name workshop-cluster
# ${CONTAINER_RUNTIME} stop kind-registry
# ${CONTAINER_RUNTIME} container remove kind-registry
