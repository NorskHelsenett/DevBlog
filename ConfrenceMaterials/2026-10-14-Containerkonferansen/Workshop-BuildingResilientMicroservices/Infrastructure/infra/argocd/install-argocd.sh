#!/usr/bin/env bash
a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

ARGO_NS="argocd"
ARGO_AUTH="none"
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      ARGO_NS="$1"
      ;;
    --auth*|-a*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      ARGO_AUTH="$1"
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

echo "🕵️ Check if ArgoCD install maifest stale and update it"
"${SCRIPT_DIR}/../../utilities/refresh-file.sh" \
  --url "https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml" \
  --file "${SCRIPT_DIR}/manifest-argocd-installation.yaml"

echo "🔌 Installing ArgoCD"
kubectl create namespace "${ARGO_NS}" 1>/dev/null
kubectl apply -n "${ARGO_NS}" --server-side --force-conflicts -f "${SCRIPT_DIR}/manifest-argocd-installation.yaml" 1>/dev/null

if [ "$ARGO_AUTH" = "none" ]; then
    echo "🔑 Configuring anonymous access enabled"
    kubectl patch configmap argocd-cm -n "${ARGO_NS}" --type strategic -p '{"data":{"users.anonymous.enabled":"true"}}' 1>/dev/null
    echo "🔑 Setting default role for anonymous not logged in users to admin"
    kubectl patch configmap argocd-rbac-cm -n "${ARGO_NS}" --type strategic -p '{"data":{"policy.default":"role:admin"}}' 1>/dev/null

    echo "🔓 Enabling insecure mode (HTTP + anonymous access)..."
    kubectl patch deployment argocd-server -n "${ARGO_NS}" --type='json' -p='[{"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--insecure"}]' 1>/dev/null

    echo "🪟Making ArgoCd available as node port reachable from host without port forward or gateway"
    kubectl patch -n "${ARGO_NS}" service argocd-server -p '{
        "spec": {
            "type":"NodePort",
            "ports":[
                {"name":"http", "nodePort":30080, "port": 80, "protocol":"TCP", "targetPort":8080}
            ]
        }}' 1>/dev/null

    # kubectl apply -f "$SCRIPT_DIR/argocd-kind-cluster-secret.yaml" 1>/dev/null


    echo "⏳ Waiting for server restart..."
    kubectl rollout status deployment/argocd-server -n "${ARGO_NS}" --timeout=60s 1>/dev/null

    echo "🗑️ Delete ArgoCDs cursed network policies which among other things prevents itself from reaching out and fetching things like charts"
    # # kubectl get networkpolicies.networking.k8s.io -n argocd -o name | xargs -I {}  kubectl -n argocd delete {} 1>/dev/null
    kubectl delete --all networkpolicies.networking.k8s.io -n "${ARGO_NS}" 1>/dev/null

    echo "✅ ArgoCD is now running in local dev mode!"
    # echo "📝 Port forward: kubectl port-forward svc/argocd-server -n argocd 8080:80"
    echo "🌐 UI: http://localhost:30080"

    log "Create secret for reading from registry for argocd"
    CONTAINER_REGISTRY_SECRET=$(echo -n '{"auths":{"local-registry.default.svc.cluster.local:5000":{"username":"","password":"","email":"none","auth":""}}}' | base64)
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: local-reg-secret
  namespace: $ARGO_NS
type: kubernetes.io/dockerconfigjson
data:
  .dockerconfigjson: $CONTAINER_REGISTRY_SECRET
EOF
# cat <<EOF | kubectl apply -f -
#     # kubectl apply -f - >/dev/null <<EOF
#     # apiVersion: v1
#     # kind: Secret
#     # metadata:
#     #   name: local-reg-secret
#     #   namespace: $ARGO_NS
#     # type: kubernetes.io/dockerconfigjson
#     # data:
#     #   .dockerconfigjson: $CONTAINER_REGISTRY_SECRET
# EOF

    JSON='
    {
      "data": {
        "helm.repositories": "- name: local-oci\n  url: http://local-registry.default.svc.cluster.local:5000\n  insecure: true\n",
        "repositories": "- type: helm\n  url: http://local-registry.default.svc.cluster.local:5000\n  insecure: true\n"
      }
    }'

    kubectl -n "${ARGO_NS}" patch configmap argocd-cm --type merge -p "${JSON}"

    kubectl -n "${ARGO_NS}" rollout restart deployment argocd-server
    kubectl -n "${ARGO_NS}" rollout restart deployment argocd-repo-server
    kubectl -n "${ARGO_NS}" rollout restart deployment argocd-application-controller
else
    echo "⏳ Waiting for ArgoCD pods to be ready..."
    kubectl wait --for=condition=available deployment/argocd-server -n "${ARGO_NS}" --timeout=300s 1>/dev/null

    echo "🔑 Extracting ArgoCD admin password..."
    ADMIN_PASS=$(kubectl get secret argocd-initial-admin-secret -n "${ARGO_NS}" -o jsonpath="{.data.password}" | base64 -d)
    echo "✅ ArgoCD admin password: $ADMIN_PASS"
    echo "🌐 ArgoCD UI (if exposed): http://localhost:8080"
    echo "📝 Set port-forward: kubectl port-forward svc/argocd-server -n ${ARGO_NS} 8080:443"
fi
