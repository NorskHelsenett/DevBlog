# Schema registry

## How to figure out apicurio config settings

Exists guide for how to configure apicurio storage, but only using operator:

https://www.apicur.io/registry/docs/apicurio-registry-operator/1.2.0-dev-v2.6.x/assembly-registry-storage.html

Same with getting started guide:
https://www.apicur.io/registry/docs/apicurio-registry/3.3.x/getting-started/assembly-deploying-registry-secured-kafka.html#secured-kafka-oauth_registry

Full config reference no much mentions of valid values or combinations:
https://www.apicur.io/registry/docs/apicurio-registry/3.3.x/getting-started/assembly-config-reference.html

Solution:
Find deploy operator page:
https://www.apicur.io/registry/docs/apicurio-registry/3.3.x/getting-started/assembly-deploying-registry-operator.html
Because no use openshift or operator hub, dig into linked operator repo, which deprecated but redirects to new monorepo:
https://github.com/Apicurio/apicurio-registry/tree/main/operator#quickstart

set up variables:
```sh
export NAMESPACE=apicurio-registry
export VERSION=3.3.0
```

create namespace:

```sh
kubectl create namespace $NAMESPACE
```

run operator installation:

```sh
curl -sSL "https://raw.githubusercontent.com/Apicurio/apicurio-registry/$VERSION/operator/install/install.yaml" | sed "s/PLACEHOLDER_NAMESPACE/$NAMESPACE/g" | kubectl -n $NAMESPACE apply -f -
kubectl apply -f https://raw.githubusercontent.com/Apicurio/apicurio-registry/refs/tags/3.3.0/operator/controller/src/main/deploy/examples/simple.apicurioregistry3.yaml -n $NAMESPACE
```

wait for it to finish
deploy wanted config using operator:

```sh
kubectl apply -f - >/dev/null <<EOF
apiVersion: registry.apicur.io/v1
kind: ApicurioRegistry3
metadata:
  name: example-registry-kafkasql-oauth
  namespace: apicurio-registry
spec:
  app:
    storage:
      type: kafkasql
      kafkasql:
        bootstrapServers: "our-kafka-cluster-kafka-bootstrap.kafka.svc.cluster.local:9094"
        tls:
          truststoreSecretRef:
            name: our-kafka-cluster-cluster-ca-cert
            key: ca.p12
          truststorePasswordSecretRef:
            name: our-kafka-cluster-cluster-ca-cert
            key: ca.password
        auth:
          enabled: true
          mechanism: OAUTHBEARER
          clientIdRef:
            name: kafka-oauth-credentials
            key: clientId
          clientSecretRef:
            name: kafka-oauth-credentials
            key: clientSecret
          tokenEndpoint: http://keycloak.keycloak.svc.cluster.local:8080/realms/kafka/protocol/openid-connect/token
          loginHandlerClass: io.strimzi.kafka.oauth.client.JaasClientOauthLoginCallbackHandler
    ingress:
      host: registry.localhost.nhn.no
  ui:
    ingress:
      host: registry-ui.localhost.nhn.no
EOF
```

get resulting pod config from resulting deployment by running:

```sh
kubectl -n apicurio-registry get deployment/example-registry-kafkasql-oauth-app-deployment -o yaml
```
