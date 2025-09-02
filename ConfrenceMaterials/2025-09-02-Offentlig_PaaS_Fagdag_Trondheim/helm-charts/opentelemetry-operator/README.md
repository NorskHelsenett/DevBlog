# opentelemetry-operator

This chart deploys an opentelemetry-operator and two optional opentelemetry-collectors, one for collecting cluster metrics (named cluster-collector) and one for receiving opentelemetry data over otlp (named otlp-collector). The scrape targets for the cluster-collector are configured through custom resources defined by the prometheus-operator (ServiceMonitor e.g.). To deploy this with argocd, you can do something like this:

```yaml
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
      values: |
        clusterCollector:
          additionalMetricLabels:
            source_cluster: "example-cluster"
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
          secretName: "kafka-example-secret-name"
          topics:
            logs: "otel-team-example-logs"
            metrics: "otel-team-example-metrics"
            traces: "otel-team-example-traces"
        otlpCollector:
          enabled: true
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
          kubeRBACProxy:
            resources:
              requests:
                cpu: 50m
                memory: 128Mi
              limits:
                memory: 160Mi
    repoURL: container-registry.example.com/plattform-helm
    targetRevision: "*"
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
```

Note that you can add labels to all scraped metrics by adding them to the `clusterCollector.additionalMetricLabels` block. The kafka secret defaults to the name `kafka-example-secret-name`.

## Compression

Compression is enabled by default in the kafka exporters used in this helm chart. If you wish to disable compression you need to set:

```yaml
kafka:
  compression: null
```

If you wish to change the values you need to set them according to the options [described in the exporter README](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/kafkaexporter#configuration-settings).
