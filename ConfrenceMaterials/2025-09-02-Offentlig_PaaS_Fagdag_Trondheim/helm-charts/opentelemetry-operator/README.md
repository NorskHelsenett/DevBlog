# opentelemetry-operator

This chart deploys an opentelemetry-operator and two optional opentelemetry-collectors, one for collecting cluster metrics (named cluster-collector) and one for receiving opentelemetry data over otlp (named otlp-collector). The scrape targets for the cluster-collector are configured through custom resources defined by the prometheus-operator (ServiceMonitor e.g.). To deploy this with argocd, you can do something like this:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  finalizers:
    - resources-finalizer.argocd.argoproj.io
  name: opentelemetry-operator
  namespace: argocd
spec:
  destination:
    namespace: opentelemetry
    server: https://kubernetes.default.svc
  project: default
  source:
    chart: opentelemetry-operator
    helm:
      valuesObject:
        clusterCollector:
          additionalMetricLabels:
            source_cluster: "env-produc-cluster-0"
          resources:
            requests:
              cpu: 250m
              memory: 768Mi
            limits:
              memory: 960Mi
          targetAllocator:
            resources:
              requests:
                cpu: 100m
                memory: 512Mi
              limits:
                memory: 640Mi
        kafka:
          bootstrapServer: "bootstrap.kafka.example.com:9094"
          secretName: "kafka-secrets"
          topics:
            logs: "otel-logs"
            metrics: "otel-metrics"
            traces: "otel-traces"
        otlpCollector:
          additionalLabels:
            source_cluster: "env-produc-cluster-0"
          resources:
            requests:
              cpu: 50m
              memory: 256Mi
            limits:
              memory: 512Mi
        opentelemetry-operator:
          manager:
            resources:
              requests:
                cpu: 100m
                memory: 256Mi
              limits:
                memory: 320Mi
    repoURL: container-registry.example.com/plattform-helm
    targetRevision: "*"
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
```

Note that you can add labels to all scraped metrics by adding them to the `clusterCollector.additionalMetricLabels` block. You can also add labels to all telemetry sent to the otlp-collector by adding them to the `otlpCollector.additionalLabels`.

The kafka secret defaults to the name `kafka-secrets`.

## Compression

Compression is enabled by default in the kafka exporters used in this helm chart. If you wish to disable compression you need to set:

```yaml
kafka:
  compression: null
```

If you wish to change the values you need to set them according to the options [described in the exporter README](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/kafkaexporter#configuration-settings).

## Fixing target allocator certificate validation errors

Sometimes the OpentelemetryOperator locks up with an error message like this:
> err: Get "https://cluster-targetallocator:443/jobs/probe%2Fmonitoring%2health-check-probes/targets?collector_id=cluster-collector-0": tls: failed to verify certificate: x509: certificate signed by unknown authority (possibly because of "crypto/rsa: verification error" while trying to verify candidate authority certificate "cluster-ca-cert")

The fix is deleting the secrets `cluster-ca-cert`, `cluster-ta-client-cert`, and `cluster-ta-server-cert`, which are usually found in the `opentelemetry` namespace. After the rotten secrets have been cleared, delete the `OpenTelemetryCollector` named `otlp`, and the `OpenTelemetryCollector` resource named `cluster`. Verify that ArgoCd syncs.

As a one-liner:
```sh
kubectl --namespace opentelemetry delete secrets cluster-ca-cert cluster-ta-client-cert cluster-ta-server-cert && \
kubectl --namespace opentelemetry delete opentelemetrycollectors.opentelemetry.io cluster otlp
```
