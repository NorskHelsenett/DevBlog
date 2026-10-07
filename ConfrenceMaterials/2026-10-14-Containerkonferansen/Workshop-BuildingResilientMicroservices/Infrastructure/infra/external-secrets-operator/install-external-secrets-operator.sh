a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

ESO_NS=external-secrets-operator
RELEASE_NAME=local
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      ESO_NS="$1"
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

echo "🕵️ Check if ESO install maifest stale and update it"
"$SCRIPT_DIR/../../utilities/refresh-chart-manifest.sh" \
  --url "https://charts.external-secrets.io" \
  --repo "external-secrets" \
  --chart "external-secrets" \
  --release "${RELEASE_NAME}" \
  --namespace "${ESO_NS}" \
  --values-file "${SCRIPT_DIR}/values.yaml" \
  --manifest-file "${SCRIPT_DIR}/manifest-external-secrets-operator.yaml"

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

"$SCRIPT_DIR/../../utilities/wait-for-k8s-resource-existence.sh" --namespace ns-external-secrets-examples --type secret --resource-name example-composite-credentials
# timeout=300; interval=2; end=$(( $(date +%s)+timeout ))
# while ! kubectl get secret "example-composite-credentials" -n "ns-external-secrets-examples" >/dev/null 2>&1; do
#   (( $(date +%s) >= end )) && { echo "❌ timed out"; exit 1; }
#   echo "⏳ waiting for $secret …"; sleep "$interval"
# done
echo "✅ Example secret utilizing store created"
