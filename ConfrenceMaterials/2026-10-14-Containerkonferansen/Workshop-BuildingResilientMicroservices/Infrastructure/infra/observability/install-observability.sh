# https://prometheus-community.github.io/helm-charts
# chart: kube-prometheus-stack

a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

PROMETHEUS_NS=prometheusns
RELEASE_NAME=local

echo "🕵 Check if KubePrometheus install maifest stale and update it"
"$SCRIPT_DIR/../../utilities/refresh-chart-manifest.sh" \
  --url "https://prometheus-community.github.io/helm-charts" \
  --repo "promcom" \
  --chart "kube-prometheus-stack" \
  --release "${RELEASE_NAME}" \
  --namespace "${PROMETHEUS_NS}" \
  --values-file "${SCRIPT_DIR}/values.yaml" \
  --manifest-file "${SCRIPT_DIR}/manifest-kube-prometheus.yaml"

echo "🪣 Create namespace for Kube-prometheus stack"
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Namespace
metadata:
  labels:
    kubernetes.io/metadata.name: $PROMETHEUS_NS
    name: $PROMETHEUS_NS
  name: $PROMETHEUS_NS
EOF

echo "🏗️ Apply Kubernetes Prometheus manifest to get crds"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-kube-prometheus.yaml" &>/dev/null
echo "🏗️ Re-Apply Kubernetes Prometheus manifest after crds existing"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-kube-prometheus.yaml" 1>/dev/null

echo "🚪 Create httproute for Grafana"
kubectl apply -f - >/dev/null <<EOF
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: hr-grafana
  namespace: $PROMETHEUS_NS
spec:
  parentRefs:
  - name: ourgateway
    namespace: networking-gateway
    kind: Gateway
  hostnames:
  - grafana.localhost.nhn.no
  - grafana.localho.st
  rules:
  - backendRefs:
    - name: $RELEASE_NAME-grafana
      port: 80
EOF
