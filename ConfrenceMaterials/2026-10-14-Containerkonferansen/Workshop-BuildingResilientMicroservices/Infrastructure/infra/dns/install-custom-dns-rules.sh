a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

echo "🧑‍🎨 Applying new coredns config map"
kubectl apply -f "$SCRIPT_DIR/manifest-coredns-configmap.yaml" 1>/dev/null
echo "🔌 Restarting coredns deployment"
kubectl rollout restart deployment coredns -n kube-system 1>/dev/null
