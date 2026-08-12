# https://strimzi.io/quickstarts/

a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKA_NS=kafka
RELEASE_NAME_KAFBAT_UI=kafbat-ui

echo "🪣 Create namespace for kafka"
kubectl apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Namespace
metadata:
  labels:
    kubernetes.io/metadata.name: $KAFKA_NS
    name: $KAFKA_NS
  name: $KAFKA_NS
EOF

# kubectl create namespace kafka
echo "🕵️ Check if Kafka Strimzi install maifest stale and update it"
REFRESH_URL="https://strimzi.io/install/latest?namespace=kafka" "$SCRIPT_DIR/../../utilities/refresh-file.sh" "$SCRIPT_DIR/manifest-strimzi-install.yaml"
kubectl create -f "$SCRIPT_DIR/manifest-strimzi-install.yaml" -n $KAFKA_NS 1>/dev/null

echo "⏳ Waiting for Kafka Strimzi operator to be ready"
# sleep 20
# ToDo: Figure out what to wait for
# Given the docs `kubectl logs deployment/strimzi-cluster-operator -n kafka -f`
# probably kubectl -n $KAFKA_NS wait --for=condition=available deployment/strimzi-cluster-operator --timeout=40s 1>/dev/null
kubectl -n $KAFKA_NS wait --for=condition=available deployment/strimzi-cluster-operator --timeout=40s 1>/dev/null

echo "🔑 Create Keycloak app secrets Kafka"
kubectl apply --server-side -f "$SCRIPT_DIR/manifest-eso-secrets-setup.yaml" 1>/dev/null
echo "⏳ Waiting for secrets to become ready"
"$SCRIPT_DIR/../../utilities/wait-for-k8s-resource-existence.sh" $KAFKA_NS secret kafka-admin-user

echo "🏗️ Deploying Kafka cluster"
# kubectl apply -f https://strimzi.io/examples/latest/kafka/kafka-single-node.yaml -n kafka 1>/dev/null
kubectl apply -f "$SCRIPT_DIR/manifest-kafka-cluster.yaml" -n kafka 1>/dev/null
echo "⏳ Waiting for Kafka cluster to be ready"
kubectl wait kafka.kafka.strimzi.io/our-kafka-cluster --for=condition=Ready --timeout=300s -n kafka

echo "🏗️ Deploying Schema registry"
kubectl apply -f "$SCRIPT_DIR/manifest-kafka-schema-registry.yaml" -n kafka 1>/dev/null
echo "⏳ Waiting for schema registry to be ready"
kubectl -n $KAFKA_NS wait --for=condition=available deployments/schema-registry --timeout=20s 1>/dev/null

echo "🌐 Schema registry: http://schema-registry.localho.st:8080/apis/ccompat/v7"

echo "🧑‍🎨 Setting up Kafbat UI: Adding helm repo"
helm repo add kafbat-ui https://kafbat.github.io/helm-charts 1>/dev/null
echo "🧑‍🎨 Setting up Kafbat UI: Updating helm repo"
helm repo update 1>/dev/null
echo "🧑‍🎨 Setting up Kafbat UI: Install chart using config from map"
helm install $RELEASE_NAME_KAFBAT_UI kafbat-ui/kafka-ui \
  --namespace $KAFKA_NS \
  -f "$SCRIPT_DIR/values-kafbat-ui.yaml"
kubectl apply -f - >/dev/null <<EOF
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: hr-kafbat-ui
  namespace: $KAFKA_NS
spec:
  parentRefs:
  - name: ourgateway
    namespace: networking-gateway
    kind: Gateway
  hostnames:
  - kafka-ui.localho.st
  - kafka-ui.127.0.0.1.nip.io
  rules:
  - backendRefs:
    - name: $RELEASE_NAME_KAFBAT_UI-kafka-ui
      port: 80
EOF
echo "🌐 Kafka UI: http://kafka-ui.localho.st:8080"

# kubectl -n kafka run kafka-producer -ti --image=quay.io/strimzi/kafka:1.1.0-kafka-4.3.0 --rm=true --restart=Never -- bin/kafka-console-producer.sh --bootstrap-server my-cluster-kafka-bootstrap:9092 --topic my-topic

# kubectl -n kafka run kafka-consumer -ti --image=quay.io/strimzi/kafka:1.1.0-kafka-4.3.0 --rm=true --restart=Never -- bin/kafka-console-consumer.sh --bootstrap-server my-cluster-kafka-bootstrap:9092 --topic my-topic --from-beginning
