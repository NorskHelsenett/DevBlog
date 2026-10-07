# https://github.com/cloudnative-pg/charts/tree/main/charts/cloudnative-pg
# helm repo add cnpg https://cloudnative-pg.github.io/charts
# helm repo update

# helm upgrade --install cnpg \
#   --namespace cnpg-system \
#   --create-namespace \
#   cnpg/cloudnative-pg

a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

CNPG_NS=cnpg-system
RELEASE_NAME=local
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      CNPG_NS="$1"
      ;;
    --release*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      RELEASE_NAME="$1"
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

echo "🕵 Check if Cloud Native PG install maifest stale and update it"
"$SCRIPT_DIR/../../utilities/refresh-chart-manifest.sh" \
  --url "https://cloudnative-pg.github.io/charts" \
  --repo "cnpg" \
  --chart "cloudnative-pg" \
  --release "${RELEASE_NAME}" \
  --namespace "${CNPG_NS}" \
  --values-file "${SCRIPT_DIR}/values.yaml" \
  --manifest-file "${SCRIPT_DIR}/manifest-cngp.yaml"

# kubectl create namespace "$CNPG_NS" 1>/dev/null
echo "🪣 Create namespace for CNPG"
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Namespace
metadata:
  labels:
    kubernetes.io/metadata.name: $CNPG_NS
    name: $CNPG_NS
  name: $CNPG_NS
EOF
# --server-side is needed because circumvents problem of metadata too long
# 1>/dev/null makes it not chatty as hell, remove for debug logs
echo "🏗️ Apply Cloud Native Posgre manifest"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-cngp.yaml" 1>/dev/null

echo "⌛️ Waiting for cnpg deployment to become ready"
kubectl -n cnpg-system wait --for=condition=available deployments/$RELEASE_NAME-cloudnative-pg --timeout=40s 1>/dev/null
