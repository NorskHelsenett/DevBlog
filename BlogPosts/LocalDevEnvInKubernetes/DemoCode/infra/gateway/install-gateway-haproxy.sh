a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

GW_CONTROLLER_NS="ingress-controller"

# echo "🪣 Create namespace for gateway controller"
# kubectl apply -f - >/dev/null <<EOF
# apiVersion: v1
# kind: Namespace
# metadata:
#   labels:
#     kubernetes.io/metadata.name: $GW_CONTROLLER_NS
#     name: $GW_CONTROLLER_NS
#   name: $GW_CONTROLLER_NS
# EOF

# echo "🕵️ Check if HAProxy-Ingress install maifest stale and update it"
# REFRESH_REPO_URL="https://haproxy-ingress.github.io/charts" \
#     REFRESH_REPO_NAME="haproxy-ingress" \
#     REFRESH_CHART_NAME="haproxy-ingress" \
#     REFRESH_RELEASE_NAME="haproxy-ingress" \
#     REFRESH_NAMESPACE="$GW_CONTROLLER_NS" \
#     "$SCRIPT_DIR/../../utilities/refresh-chart-manifest.sh" \
#     "$SCRIPT_DIR/manifest-haproxy-ingress.yaml"
# echo "🏗️ Apply HAProxy-Ingress manifest"
# kubectl apply --server-side -f "$SCRIPT_DIR/manifest-haproxy-ingress.yaml" 1>/dev/null

# HaProxyIngress installation guide: https://haproxy-ingress.github.io/docs/getting-started/
echo "🛻 Adding HAProxy Ingress helm chart repo"
helm repo add haproxy-ingress https://haproxy-ingress.github.io/charts 1>/dev/null
echo "🏗️ Installing HAProxy Ingress helm chart"
helm upgrade haproxy-ingress haproxy-ingress/haproxy-ingress \
  --install \
  --create-namespace --namespace $GW_CONTROLLER_NS \
  --version 0.16.1 \
  -f "$SCRIPT_DIR/haproxy-ingress-values.yaml" 1>/dev/null

# HaProxyIngress gateway API support setup guide: https://haproxy-ingress.github.io/docs/configuration/gateway-api/
# echo "🏗️ Installing Gateway API CRDs"
# kubectl apply -f https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.0.0/experimental-install.yaml 1>/dev/null
echo "🕵️ Check if Gateway API CRDs stale and update them"
REFRESH_URL="https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.0.0/experimental-install.yaml" \
  "$SCRIPT_DIR/../../utilities/refresh-file.sh" \
  "$SCRIPT_DIR/manifest-kubernetes-gw-api-crds.yaml"
echo "🏗️ Installing Gateway API CRDs"
kubectl apply -f "$SCRIPT_DIR/manifest-kubernetes-gw-api-crds.yaml" 1>/dev/null

echo "🚧 Restarting HAProxy Ingress so it can find the newly installed APIs"
kubectl --namespace $GW_CONTROLLER_NS delete pod -lapp.kubernetes.io/name=haproxy-ingress 1>/dev/null

echo "📦 Create gatewayclass (namespaceless, one for all)"
kubectl apply -f - >/dev/null <<EOF
apiVersion: gateway.networking.k8s.io/v1
kind: GatewayClass
metadata:
  name: haproxy
spec:
  controllerName: haproxy-ingress.github.io/controller
EOF

echo "🪣 Create namespace for gateway, to demo using gateway in other ns"
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Namespace
metadata:
  labels:
    kubernetes.io/metadata.name: networking-gateway
    name: networking-gateway
  name: networking-gateway
EOF

echo "⛩️ Create gateway in ns accepting routes set up in any other namespace"
kubectl apply -f - >/dev/null <<EOF
apiVersion: gateway.networking.k8s.io/v1
kind: Gateway
metadata:
  name: ourgateway
  namespace: networking-gateway
spec:
  gatewayClassName: haproxy
  listeners:
  - name: http-80
    port: 80
    protocol: HTTP
    allowedRoutes:
      namespaces:
        from: All
EOF

# echo "🪣 Test gateway create namespace for test resources"
# kubectl apply -f - >/dev/null <<EOF
# apiVersion: v1
# kind: Namespace
# metadata:
#   labels:
#     kubernetes.io/metadata.name: test-networking-gateway
#     name: test-networking-gateway
#   name: test-networking-gateway
# EOF
# kubectl --namespace test-networking-gateway create deployment httpbin --image kennethreitz/httpbin
# kubectl --namespace test-networking-gateway expose deployment httpbin --port=80

# echo "Test gateway create httproute"
# kubectl apply -f - >/dev/null <<EOF
# apiVersion: gateway.networking.k8s.io/v1
# kind: HTTPRoute
# metadata:
#   name: httpbin
#   namespace: test-networking-gateway
# spec:
#   parentRefs:
#   - name: ourgateway
#     namespace: networking-gateway
#     kind: Gateway
#   hostnames:
#   - httpbin-from-gateway.local
#   rules:
#   - backendRefs:
#     - name: httpbin
#       port: 80
# EOF

# Wait a bit, and you'll see `curl -v -H "Host: httpbin-from-gateway.local" http://localhost:8080` working
# while `curl -v -H "Host: httpbin-from-gateway.fail" http://localhost:8080` fails

echo "🚪 Create httproute for ArgoCD"
kubectl apply -f - >/dev/null <<EOF
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: hr-argocd-server
  namespace: argocd
spec:
  parentRefs:
  - name: ourgateway
    namespace: networking-gateway
    kind: Gateway
  hostnames:
  - argocd.localho.st
  - argocd.127.0.0.1.nip.io
  rules:
  - backendRefs:
    - name: argocd-server
      port: 80
EOF
