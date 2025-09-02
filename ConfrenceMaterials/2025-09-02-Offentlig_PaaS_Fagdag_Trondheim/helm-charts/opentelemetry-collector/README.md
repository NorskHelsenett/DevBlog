# OpenTelemetry-Collector

This is a helm chart for deploying an opentelemetry collector in your cluster to ship telemetry data to kafka and the platform teams observability stack. To use this chart in your cluster you need to create a secret with the name `kafka-example-secret-name` containing the kafka certificates, with the keys `BrokerCaCertificate.pem`, `UserCertificate.pem`, and `UserKey.pem`. You also need to fill in the following fields in the `values.yaml` file:

```yaml
config:
  environment: "either test or prod"
  location: "either examplelocationfirst or examplelocationsecond (which data center is your cluster located in?)"
  team: "must be in the form: team-<name>, e.g. team-example. Must match the topic in kafka"
```

The kafka values are templated based on the current kafka-platform in DHP.

## Example with Argocd

This example shows how you can deploy the chart in an app of apps pattern with argocd. Note that the `spec.source.targetRevision` field points to the latest version at the time of writing this. You can see the latest available release [here](https://example.com). If you wish to automatically update this version you can either use ranges [as described in argocds documentation](https://argo-cd.readthedocs.io/en/stable/user-guide/tracking_strategies/#helm) or you can set up something like renovate bot to check the latest published version and create a merge request in you repository. Also note that we recommend setting the resource requests and limits, as well as the GOMEMLIMIT environment variable as shown below.

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  finalizers:
    - resources-finalizer.argocd.argoproj.io
  name: opentelemetry-collector
  namespace: argocd
spec:
  destination:
    namespace: opentelemetry
    server: https://kubernetes.default.svc
  project: default
  source:
    chart: opentelemetry-collector
    helm:
      values: |
        config:
          environment: "test"
          location: "trd"
          team: "team-example"
        opentelemetry-collector:
          extraEnvs:
            - name: GOMEMLIMIT
              valueFrom:
                resourceFieldRef:
                  containerName: opentelemetry-collector
                  resource: requests.memory
          resources:
            requests:
              cpu: 50m
              memory: 256Mi
            limits:
              memory: 512Mi
    repoURL: container-registry.example.com/plattform-helm
    targetRevision: "0.2.0"
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
```

## Development

Note that we set both the field `.Values.opentelemetry-collector.fullnameOverride` and `.Values.opentelemetry-collector.configMap.existingName` in values.yaml and the `name` field in the configmap.yaml files. These values should all match.
