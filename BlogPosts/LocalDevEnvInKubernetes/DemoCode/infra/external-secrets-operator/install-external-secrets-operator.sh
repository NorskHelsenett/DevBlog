a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

ESO_NS=external-secrets-operator
RELEASE_NAME=local

echo "🕵️ Check if ESO install maifest stale and update it"
REFRESH_REPO_URL="https://charts.external-secrets.io" \
    REFRESH_REPO_NAME="external-secrets" \
    REFRESH_CHART_NAME="external-secrets" \
    REFRESH_RELEASE_NAME="$RELEASE_NAME" \
    REFRESH_NAMESPACE="$ESO_NS" \
    "$SCRIPT_DIR/../../utilities/refresh-chart-manifest.sh" \
    "$SCRIPT_DIR/manifest-external-secrets-operator.yaml"

# kubectl create namespace "$ESO_NS" 1>/dev/null
echo "🪣 Create namespace for External Secrets Operator"
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Namespace
metadata:
  labels:
    kubernetes.io/metadata.name: $ESO_NS
    name: $ESO_NS
  name: $ESO_NS
EOF
# --server-side is needed because circumvents problem of metadata too long
# 1>/dev/null makes it not chatty as hell, remove for debug logs
echo "🏗️ Apply ESO manifest"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-external-secrets-operator.yaml" 1>/dev/null

echo "⏳ waiting for ESO Deployments to become available"
kubectl -n $ESO_NS wait --for=condition=available deployments/$RELEASE_NAME-external-secrets --timeout=90s 1>/dev/null
kubectl -n $ESO_NS wait --for=condition=available deployments/$RELEASE_NAME-external-secrets-cert-controller --timeout=90s 1>/dev/null
kubectl -n $ESO_NS wait --for=condition=available deployments/$RELEASE_NAME-external-secrets-webhook --timeout=90s 1>/dev/null

# release-name-external-secrets
# release-name-external-secrets-cert-controller
# release-name-external-secrets-webhook
# kubectl -n external-secrets-operator wait --for=condition=available deployments/release-name-external-secrets --timeout=40s 1>/dev/null

echo "🎨 Creating External secret in-cluster store and example secrets"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-eso-in-cluster-store-resources.yaml" 1>/dev/null

"$SCRIPT_DIR/../../utilities/wait-for-k8s-resource-existence.sh" ns-external-secrets-examples secret example-composite-credentials
# timeout=300; interval=2; end=$(( $(date +%s)+timeout ))
# while ! kubectl get secret "example-composite-credentials" -n "ns-external-secrets-examples" >/dev/null 2>&1; do
#   (( $(date +%s) >= end )) && { echo "❌ timed out"; exit 1; }
#   echo "⏳ waiting for $secret …"; sleep "$interval"
# done
echo "✅ Example secret utilizing store created"
