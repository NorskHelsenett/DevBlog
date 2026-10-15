a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)
. "${SCRIPT_DIR}/../container-runtime.sh"
echo "Pulling test image"
${CONTAINER_RUNTIME} pull gcr.io/google-samples/hello-app:1.0
echo "📝 Retagging test image"
${CONTAINER_RUNTIME} tag gcr.io/google-samples/hello-app:1.0 localhost:5001/hello-app:1.0
echo "Pushing retagged image to local registry"
${CONTAINER_RUNTIME} push ${REGISTRY_PUSH_FLAGS} localhost:5001/hello-app:1.0
echo "📝 Creating deployment using image from local registry"
kubectl create deployment hello-server --image=localhost:5001/hello-app:1.0
echo "⏳ Waiting for deployment to become ready"
kubectl wait --for=condition=available deployments/hello-server --timeout=10s
echo "Getting logs from deployment"
kubectl logs deployments/hello-server
echo "🗑️ Cleaning up test deployment"
kubectl delete deployments.apps hello-server
