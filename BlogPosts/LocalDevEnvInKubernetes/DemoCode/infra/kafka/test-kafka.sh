a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KAFKA_NS=kafka

echo "🏗️ Deploy topic for testing"
kubectl apply -f "$SCRIPT_DIR/manifest-kafka-topics-tenant-a.yaml" -n kafka 1>/dev/null

echo "🗑️ Cleaning up after previous runs if leftovers"
kubectl -n $KAFKA_NS delete deployment/kafka-cli
echo "🏗️ Deploying deployment that can run tests"
kubectl apply -f "$SCRIPT_DIR/manifest-test-pod-kafka.yaml" -n kafka 1>/dev/null
echo "⏳ Waiting for Kafka test deployment to be ready"
# kubectl wait kafka.kafka.strimzi.io/our-kafka-cluster --for=condition=Ready --timeout=300s -n kafka
kubectl wait --for=condition=available deployments/kafka-cli -n "$KAFKA_NS" --timeout=300s 1>/dev/null

echo "🚪 enter the test env by running: \"kubectl -n $KAFKA_NS exec -it deployments/kafka-cli -- /bin/bash\""

# Manual test steps:
# kubectl -n kafka exec -it deployments/kafka-cli -- /bin/bash
# cat > /tmp/client.properties <<EOF
# bootstrap.servers=${BROKERS_BOOTSTRAP_ADDRESS}
# security.protocol=SASL_SSL
# sasl.mechanism=OAUTHBEARER
# sasl.jaas.config=org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginModule required;
# sasl.login.callback.handler.class=org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginCallbackHandler
# sasl.oauthbearer.client.credentials.client.id=${KAFKA_CLIENT_CLIENTID}
# sasl.oauthbearer.client.credentials.client.secret=${KAFKA_CLIENT_SECRET}
# sasl.oauthbearer.token.endpoint.url=${TOKEN_URL}
# ssl.truststore.type=PKCS12
# ssl.truststore.location=/certs/truststore.p12
# ssl.truststore.password=${TRUSTSTORE_PASSWORD}
# EOF
# /opt/kafka/bin/kafka-topics.sh --bootstrap-server "${BROKERS_BOOTSTRAP_ADDRESS}" --command-config /tmp/client.properties --list
# echo "Expected output: \"kafka-tenant-a-testtopic\""

# kubectl -n kafka exec -it pods/our-kafka-cluster-dual-role-0 -- /bin/bash
# ls /var/lib/kafka/data-0/kafka-log0/ -1p
# echo 'Expected output:
# __cluster_metadata-0/
# bootstrap.checkpoint
# cleaner-offset-checkpoint
# kafka-tenant-a-testtopic-0/
# kafka-tenant-a-testtopic-1/
# kafka-tenant-a-testtopic-2/
# log-start-offset-checkpoint
# meta.properties
# not-owned-by-or-accessible-to-anyone-0/
# not-owned-by-or-accessible-to-anyone-1/
# not-owned-by-or-accessible-to-anyone-2/
# recovery-point-offset-checkpoint
# replication-offset-checkpoint
# '
# ----- helpers ------------------------------------------------
log() {
    # Simple timestamped logger
    _now=$(date +%H:%M:%S)
    printf '[%s] %s\n' "$_now" "$*"
}
die() {
    log "❌ $*" >&2
    exit 1
}
ok() {
    log "✅ $*"
}
fail() {
    log "❌ $*"
}
# ---------- 1️⃣  Create client.properties & list topics ----------
log "▶️  Creating /tmp/client.properties inside deployment/$CLI_DEPLOYMENT ..."
kubectl -n "$KAFKA_NS" exec -i deployment/kafka-cli -- /bin/sh <<'POD_IN_EOF'
cat > /tmp/client.properties <<PROPS_EOF
bootstrap.servers=${BROKERS_BOOTSTRAP_ADDRESS}
security.protocol=SASL_SSL
sasl.mechanism=OAUTHBEARER
sasl.jaas.config=org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginModule required;
sasl.login.callback.handler.class=org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginCallbackHandler
sasl.oauthbearer.client.credentials.client.id=${KAFKA_CLIENT_CLIENTID}
sasl.oauthbearer.client.credentials.client.secret=${KAFKA_CLIENT_SECRET}
sasl.oauthbearer.token.endpoint.url=${TOKEN_URL}
ssl.truststore.type=PKCS12
ssl.truststore.location=/certs/truststore.p12
ssl.truststore.password=${TRUSTSTORE_PASSWORD}
PROPS_EOF
POD_IN_EOF

log "▶️  Listing topics via kafka-topics.sh ..."
TOPIC_OUTPUT=$(kubectl -n "$KAFKA_NS" exec -i deployment/kafka-cli -- \
    /opt/kafka/bin/kafka-topics.sh \
    --bootstrap-server "our-kafka-cluster-kafka-bootstrap:9094" \
    --command-config /tmp/client.properties \
    --list)

# Trim whitespace and store in an array
read -r -a topics <<<"$TOPIC_OUTPUT"

if (( ${#topics[@]} != 1 )); then
    fail "Expected exactly ONE topic, got ${#topics[@]}."
    die "Actual output:\n$TOPIC_OUTPUT"
fi

if [[ "${topics[0]}" != "kafka-tenant-a-testtopic" ]]; then
    fail "Expected topic name 'kafka-tenant-a-testtopic', got '${topics[0]}'."
    die "Actual output:\n$TOPIC_OUTPUT"
fi

ok "Topic list check passed - only 'kafka-tenant-a-testtopic' is present."

# ---------- 2️⃣  Verify broker data‑directory layout ----------
log "▶️  Listing directory /var/lib/kafka/data-0/kafka-log0/ inside pods/our-kafka-cluster-dual-role-0 ..."
DIR_LIST=$(kubectl -n "$KAFKA_NS" exec -i pods/our-kafka-cluster-dual-role-0 -- \
    /bin/ls -1p "/var/lib/kafka/data-0/kafka-log0/")

# Helper: check a pattern exists in the listing (exact match or prefix)
has_dir() {
    local pattern=$1
    grep -q "^${pattern}" <<<"$DIR_LIST"
}

# Required directories (full set)
# required_dirs=(
#     "kafka-tenant-a-testtopic-0/"
#     "kafka-tenant-a-testtopic-1/"
#     "kafka-tenant-a-testtopic-2/"
#     "not-owned-by-or-accessible-to-anyone-0/"
#     "not-owned-by-or-accessible-to-anyone-1/"
#     "not-owned-by-or-accessible-to-anyone-2/"
# )

# If you want the looser requirement (only the *‑0/ entries)
required_dirs=(
   "kafka-tenant-a-testtopic-0/"
   "not-owned-by-or-accessible-to-anyone-0/"
)

missing=()
for d in "${required_dirs[@]}"; do
    if ! has_dir "$d"; then
        missing+=("$d")
    fi
done

if ((${#missing[@]})); then
    fail "Missing ${#missing[@]} expected directory(ies):"
    for m in "${missing[@]}"; do echo "   - $m"; done
    die "Full directory list from pod:\n$DIR_LIST"
fi

ok "All required partition directories are present."

# ------------------------------------------------------------
log "🎉 All checks passed!"
# ---------------------------
echo "Adding schema to schema registry"
curl -X POST -H "Content-Type: application/vnd.schemaregistry.v1+json" -d '{"schema":"{\"$schema\":\"http://json-schema.org/draft-07/schema#\",\"type\":\"object\",\"properties\":{\"sid\":{\"type\":\"string\"}},\"required\":[\"sid\"],\"additionalProperties\":false}","schemaType":"JSON"}' http://schema-registry.localho.st:8080/apis/ccompat/v7/subjects/testschema/versions
echo "Retrieving schema from schema registry"
curl http://schema-registry.localho.st:8080/apis/ccompat/v7/subjects/testschema/versions/1
