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

echo "🕵 Check if Cloud Native PG install maifest stale and update it"
REFRESH_REPO_URL="https://cloudnative-pg.github.io/charts" \
    REFRESH_REPO_NAME="cnpg" \
    REFRESH_CHART_NAME="cloudnative-pg" \
    REFRESH_RELEASE_NAME="$RELEASE_NAME" \
    REFRESH_NAMESPACE="$CNPG_NS" \
    "$SCRIPT_DIR/../../utilities/refresh-chart-manifest.sh" \
    "$SCRIPT_DIR/manifest-cngp.yaml"

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
