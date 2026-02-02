# Pre demo setup:

kubectl create namespace demo-eso-secret-storage
kubectl create namespace demo-external-secrets-operator
kubectl create namespace demo-where-app-lives

# apply it:

kubectl apply -f ./demo-manifest.yaml

# explore created:
kubectl --namespace demo-eso-secret-storage get secret
kubectl --namespace demo-eso-secret-storage get secret app-password-secret -o yaml
kubectl --namespace demo-where-app-lives get secret
kubectl --namespace demo-where-app-lives get secret demo-app-credentials -o yaml
kubectl --namespace demo-where-app-lives get secret demo-alternative-app-credentials -o yaml

kubectl --namespace demo-where-app-lives get externalsecrets.external-secrets.io

# clean it up:

kubectl --namespace demo-where-app-lives delete externalsecrets.external-secrets.io demo-eso-create-alternative-secret-for-use-by-app
# Check deleted: kubectl --namespace demo-where-app-lives get externalsecrets.external-secrets.io
# Verify secret deleted: kubectl --namespace demo-where-app-lives get secret
kubectl --namespace demo-where-app-lives delete externalsecrets.external-secrets.io demo-eso-create-secret-for-use-by-app
kubectl delete namespace demo-where-app-lives

# kubectl --namespace demo-eso-secret-storage get pushsecrets.external-secrets.io
# kubectl --namespace demo-eso-secret-storage get secret
kubectl --namespace demo-eso-secret-storage delete pushsecrets.external-secrets.io demo-pushsecret-app-password
# Deletes push secret, but not secret now managed by cluster store
# kubectl --namespace demo-eso-secret-storage get passwords.generators.external-secrets.io
kubectl --namespace demo-eso-secret-storage delete passwords.generators.external-secrets.io demo-eso-app-restriction-specific-password-generator

# Note unnamespaced resource
# kubectl get clustersecretstores.external-secrets.io
kubectl delete clustersecretstores.external-secrets.io demo-eso-cluster-secret-store
# Secret still not deleted: kubectl --namespace demo-eso-secret-storage get secret
kubectl --namespace demo-eso-secret-storage delete secret app-password-secret
kubectl delete namespace demo-eso-secret-storage

# kubectl get clusterrolebindings.rbac.authorization.k8s.io demo-bind-eso-store-role-to-eso-service-account
kubectl delete clusterrolebindings.rbac.authorization.k8s.io demo-bind-eso-store-role-to-eso-service-account

# kubectl --namespace demo-external-secrets-operator get serviceaccounts
kubectl --namespace demo-external-secrets-operator delete serviceaccount demo-eso-service-account

# kubectl get clusterroles.rbac.authorization.k8s.io demo-eso-store-role
kubectl delete clusterroles.rbac.authorization.k8s.io demo-eso-store-role

kubectl delete namespace demo-external-secrets-operator
