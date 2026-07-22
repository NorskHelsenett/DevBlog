echo "Pulling test image"
docker pull gcr.io/google-samples/hello-app:1.0
echo "📝 Retagging test image"
docker tag gcr.io/google-samples/hello-app:1.0 localhost:5001/hello-app:1.0
echo "Pushing retagged image to local registry"
docker push localhost:5001/hello-app:1.0
echo "📝 Creating deployment using image from local registry"
kubectl create deployment hello-server --image=localhost:5001/hello-app:1.0
echo "⏳ Waiting for deployment to become ready"
kubectl wait --for=condition=available deployments/hello-server --timeout=10s
echo "Getting logs from deployment"
kubectl logs deployments/hello-server
echo "🗑️ Cleaning up test deployment"
kubectl delete deployments.apps hello-server
