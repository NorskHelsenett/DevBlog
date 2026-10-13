a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

docker build -t localhost:5001/topiccopier:latest "${SCRIPT_DIR}/TopicCopier" && \
docker push localhost:5001/topiccopier:latest

docker build -t localhost:5001/workshop-api:latest "${SCRIPT_DIR}/DistributedKvEngine" && \
docker push localhost:5001/workshop-api:latest

helm template "${SCRIPT_DIR}/helm/WorkshopChart" | kubectl apply -f -

WORKSHOP_NS="workshop"
kubectl -n "${WORKSHOP_NS}" rollout restart deployment -l "app.kubernetes.io/name=topic-copier"
kubectl -n "${WORKSHOP_NS}" rollout restart deployment -l "app.kubernetes.io/name=workshop-api"
