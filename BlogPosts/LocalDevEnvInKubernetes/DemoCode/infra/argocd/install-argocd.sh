#!/usr/bin/env bash
set -euo pipefail

ARGO_NS="argocd"

a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

echo "🕵️ Check if ArgoCD install maifest stale and update it"
REFRESH_URL="https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml" "$SCRIPT_DIR/../../utilities/refresh-file.sh" "$SCRIPT_DIR/manifest-argocd-installation.yaml"

echo "🔌 Installing ArgoCD"
kubectl create namespace "$ARGO_NS" 1>/dev/null
kubectl apply -n "$ARGO_NS" --server-side --force-conflicts -f "$SCRIPT_DIR/manifest-argocd-installation.yaml" 1>/dev/null

# echo "⏳ Waiting for ArgoCD pods to be ready..."
# kubectl wait --for=condition=available deployment/argocd-server -n "$ARGO_NS" --timeout=300s

envkind="development"
if [ "$envkind" = "development" ]; then
    echo "🔑 Configuring anonymous access enabled"
    kubectl patch configmap argocd-cm -n "$ARGO_NS" --type strategic -p '{"data":{"users.anonymous.enabled":"true"}}' 1>/dev/null
    echo "🔑 Setting default role for anonymous not logged in users to admin"
    kubectl patch configmap argocd-rbac-cm -n "$ARGO_NS" --type strategic -p '{"data":{"policy.default":"role:admin"}}' 1>/dev/null

    echo "🔓 Enabling insecure mode (HTTP + anonymous access)..."
    kubectl patch deployment argocd-server -n "$ARGO_NS" --type='json' -p='[{"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--insecure"}]' 1>/dev/null

    echo "🪟Making ArgoCd available as node port reachable from host without port forward or gateway"
    kubectl patch -n "$ARGO_NS" service argocd-server -p '{
        "spec": {
            "type":"NodePort",
            "ports":[
                {"name":"http", "nodePort":30080, "port": 80, "protocol":"TCP", "targetPort":8080}
            ]
        }}' 1>/dev/null

    # kubectl apply -f "$SCRIPT_DIR/argocd-kind-cluster-secret.yaml" 1>/dev/null

    echo "⏳ Waiting for server restart..."
    kubectl rollout status deployment/argocd-server -n argocd --timeout=60s 1>/dev/null

    echo "🗑️ Delete ArgoCDs cursed network policies which among other things prevents itself from reaching out and fetching things like charts"
    # # kubectl get networkpolicies.networking.k8s.io -n argocd -o name | xargs -I {}  kubectl -n argocd delete {} 1>/dev/null
    kubectl delete --all networkpolicies.networking.k8s.io -n "$ARGO_NS" 1>/dev/null

    echo "✅ ArgoCD is now running in local dev mode!"
    echo "📝 Port forward: kubectl port-forward svc/argocd-server -n argocd 3080:80"
    echo "🌐 UI: http://localhost:30081"
else
    echo "⏳ Waiting for ArgoCD pods to be ready..."
    kubectl wait --for=condition=available deployment/argocd-server -n "$ARGO_NS" --timeout=300s 1>/dev/null

    echo "🔑 Extracting ArgoCD admin password..."
    ADMIN_PASS=$(kubectl get secret argocd-initial-admin-secret -n "$ARGO_NS" -o jsonpath="{.data.password}" | base64 -d)
    echo "✅ ArgoCD admin password: $ADMIN_PASS"
    echo "🌐 ArgoCD UI (if exposed): http://localhost:8080"
    echo "📝 Set port-forward: kubectl port-forward svc/argocd-server -n $ARGO_NS 8080:443"
fi

# Has to be run after gateway CRDs are deployed
# echo "Create httproute for ArgoCD"
# kubectl apply -f - <<EOF
# apiVersion: gateway.networking.k8s.io/v1
# kind: HTTPRoute
# metadata:
#   name: argocd-server
#   namespace: $ARGO_NS
# spec:
#   parentRefs:
#   - name: ourgateway
#     namespace: networking-gateway
#     kind: Gateway
#   hostnames:
#   - argocd.localho.st
#   - argocd.127.0.0.1.nip.io
#   rules:
#   - backendRefs:
#     - name: argocd-server
#       port: 80
# EOF
