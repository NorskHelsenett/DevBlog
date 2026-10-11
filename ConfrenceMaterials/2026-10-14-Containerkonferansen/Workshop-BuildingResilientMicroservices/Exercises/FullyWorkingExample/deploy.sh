a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

docker build -t localhost:5001/topiccopier:latest "${SCRIPT_DIR}/TopicCopier" && \
docker push localhost:5001/topiccopier:latest

docker build -t localhost:5001/resilientservice:latest "${SCRIPT_DIR}/ResilientMicroservice" && \
docker push localhost:5001/resilientservice:latest

helm template "${SCRIPT_DIR}/helm/WorkshopChart" | kubectl apply -f -
