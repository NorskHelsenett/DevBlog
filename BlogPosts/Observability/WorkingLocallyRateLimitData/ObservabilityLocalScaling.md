Scaling observability locally

In this piece we'll look at how to deal with larger volumes of telemetry data locally.

Some assumptions and background:
- We are using [OpenTelemetry](https://opentelemetry.io/)
  - because it's the current industry standard
  - and because it's easy to get started with
- We are running it locally because
  - It exposes useful information about the performance and behaviour of our programs
    - It covers most (all?) of the info needed
  - It's good to familiarize ourselves with the interfaces available for working with the programs where they actually run
    - The performance UIs of the various IDEs for instance are nice, but not available in prod, and a pain to work with across different programs working together
  - It's easy to get started with
    - Can simply run the [Grafana](https://grafana.com) OTel LGMT image ([image](https://hub.docker.com/r/grafana/otel-lgtm), [docs](https://github.com/grafana/docker-otel-lgtm/?tab=readme-ov-file#docker-otel-lgtm))
  - It is cheap
    - No spending time negotiating about please getting money for more capacity (if you are lucky enough that your budget is greater than 0)

With that out of the way, we can take a closer look at our problem:
- Want to observe and possibly tweak a collection of processes that generates lots of [traces](https://opentelemetry.io/docs/concepts/signals/traces/), based on what can be divined from the traces
  - The large volume results in [tempo](https://grafana.com/oss/tempo/) getting [oom](https://en.wikipedia.org/wiki/Out_of_memory) killed (and taking the lgtm image with it if run from there), and we se no traces
- Don't want to bake [head sampling](https://opentelemetry.io/docs/concepts/sampling/#head-sampling) into the application
  - Because data volumes are manageable for production, and don't want to limit observability of prod because laptop struggles

What do?
- Learn that [tail sampling](https://opentelemetry.io/docs/concepts/sampling/#tail-sampling) is a thing and sounds useful in this context.
- Abandon lgtm image, because we now want to configure the [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/), and is nice that not everything crashes when tempo runs out of memory, e.g. nice to still see logs in grafana
- ToDo: Add example yaml, or just link to repo? Maybe put it here?
  - I think put it here and link to it, because long. Put full example in bottom of text for copy/paste friendliness

Babys first attempt
- Learn about [Probabilistic Sampling Processor](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/processor/probabilisticsamplerprocessor)
  - Amazing, can reduce data throughput, but preserves information like spikes in amounts of operations at certain points in time!
- Learn that you need to stuff it in a processor in a pipeline, like the [Tail Sampling Processor](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/processor/tailsamplingprocessor) (see examples in the linked tail sampling processor doc)
- Create policy config like
```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: opentelemetry-collector:4317
      http:
        endpoint: opentelemetry-collector:4318

processors:
  batch:
  memory_limiter:
    check_interval: 1s
    limit_mib: 1000
    spike_limit_mib: 100
  tail_sampling:
    decision_wait: 1s
    num_traces: 100_000
    expected_new_traces_per_sec: 10_000
    decision_cache:
      sampled_cache_size: 1_000_000
      non_sampled_cache_size: 1_000_000
    policies: [
        {
          name: percentage,
          type: probabilistic,
          probabilistic: {sampling_percentage: 0.001},
        },
      ]
exporters:
  otlphttp/loki:
    endpoint: http://grafana-loki:3100/otlp
  otlphttp/mimir:
    endpoint: http://grafana-mimir:9009/otlp
  otlp/tempo:
    endpoint: grafana-tempo:4317
    tls:
      insecure: true

extensions:
  health_check:

service:
  extensions: [health_check]
  pipelines:
    logs:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlphttp/loki]
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlphttp/mimir]
    traces:
      receivers: [otlp]
      processors: [memory_limiter, tail_sampling, filter, batch]
      exporters: [otlp/tempo]
  telemetry:
    logs:
      level: debug
```
- Attempt to try it, but get error TODO
- Go over to using [OpenTelemetry Collector Contrib](https://github.com/open-telemetry/opentelemetry-collector-contrib) [image](https://hub.docker.com/r/otel/opentelemetry-collector-contrib)
- Try it out, and observe that you probably sample the [trace spans](https://opentelemetry.io/docs/concepts/signals/traces/#spans) which are constantly spammed, and miss out on the crucial info from the less frequent one off events.

Back to the drawing board
- Figure out that you can try start with only reducing the most spammed spans.
- Need to figure out what they are.
  - Come up with tempo [TraceQL](https://grafana.com/docs/tempo/latest/traceql/) query `{} | count_over_time() by (span:name)` to show how rate of different trace spans over time.
    - ToDo: Picture of resulting histogram?
- Start with the first that comes a lot first.
- Find out that [OpenTelemetry Transformation Language (OTTL)](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/pkg/ottl) is needed to match span names in the tail sampling processor policies.
- Learn that the and policy is needed to combine the name matching condition and probabilistic sampling.
```yaml
    policies: [
      {
        name: probabilistic-by-span-name-1,
        type: and,
        and: {
          and_sub_policy:
            [
              {
                name: name-match,
                type: ottl_condition,
                ottl_condition: { error_mode: ignore, span: [ 'name == "Span.First"' ] }
              },
              {
                name: percentage,
                type: probabilistic,
                probabilistic: {sampling_percentage: 0.001},
              },
            ]
          }
      },
    ]
```
- Discover that everything not picked up by the filter is dropped, so now we only have traces of the less sampled span.
- Try to sample all of everything else by adding additional policy inverting the name match and sampling at a 100% rate:
```yaml
    policies: [
      {
        name: probabilistic-by-span-name-1,
        type: and,
        and: {
          and_sub_policy:
            [
              {
                name: name-match,
                type: ottl_condition,
                ottl_condition: { error_mode: ignore, span: [ 'name == "Span.First"' ] }
              },
              {
                name: percentage,
                type: probabilistic,
                probabilistic: {sampling_percentage: 0.001},
              },
            ]
          }
      },
      {
        name: everything-else-1,
        type: and,
        and: {
          and_sub_policy:
            [
              {
                name: name-match,
                type: ottl_condition,
                ottl_condition: { error_mode: ignore, span: [ 'name != "Span.First"' ] }
              },
              {
                name: percentage,
                type: probabilistic,
                probabilistic: {sampling_percentage: 100},
              },
            ]
          }
      },
    ]
```
- It kinda works!

Now the other spans
- Expand on approach above with and policies and probabilistically sampling less of most spammed spans.
- Discover that this approach does not work for nested spans
  - Bot the parent and children spans are sampled at a 100% rate, drowning out everything else and eventually setting the OOM Reaper on tempo.
- Great sadness, time to explore other alternatives.
- Stumble upon the `rate_limiting` policy in the Tail Sampling Processor:
```yaml
    policies: [
      {
          name: global-rate-limit,
          type: rate_limiting,
          rate_limiting: {spans_per_second: 20}
      },
    ]
```
- Seemingly works, only 20 spans per second come through, tempo no longer struggles with visits from the oom reaper!
- Become uncertain of how the hard cap works
  - Are the rare events guaranteed to be sampled, or could we end up never seeing those spans?
- Despair; Consider using the `drop` policy:
```yaml
    policies: [
      {
          name: drop-policy-1,
          type: drop,
          drop: {
            drop_sub_policy:
            [
              {
                name: name-match,
                type: ottl_condition,
                ottl_condition: { error_mode: ignore, span: [ 'name != "Span.First"' ] }
              }
            ]
          }
      },
    ]
```
- Works! But no good, next developer (me in 4 months) might not realize the dropped spans and events exist because they're not visible
- Consider if we can break something into the `composite` policy, the `max_total_spans_per_second` property combined with the `rate_allocation` section with `percent` keys per entry looks highly promising now that we've figured out the ottl matching language.
```yaml
    policies: [
      {
        name: composite-policy-1,
        type: composite,
        composite:
          {
            max_total_spans_per_second: 100,
            policy_order:
              [
                composite-name-match-1,
                composite-name-match-2,
                composite-pass-rest,
              ],
            composite_sub_policy:
              [
                {
                  name: composite-name-match-1,
                  type: ottl_condition,
                  ottl_condition: { error_mode: ignore, span: ['name == "Span.First"'] },
                },
                {
                  name: composite-name-match-2,
                  type: ottl_condition,
                  ottl_condition: { error_mode: ignore, span: [ 'name == "Span.Second"' ] },
                },
                { name: composite-pass-rest, type: always_sample },
              ],
            rate_allocation:
              [
                { policy: composite-name-match-1, percent: 5 },
                { policy: composite-name-match-2, percent: 5 },
              ],
          },
      },
    ]
```
- Successfully limits the mentioned spans, but where are the rest? Looks like the example in the docs?
- Instead of `always_sample`, try ottl expression with `'name != ""'`?
  - No good, picks up the rest again
- Maybe docs is incomplete, we can give the policy gathering up the rest a rate allocation and seeing if that helps?
- Setting `{ policy: composite-pass-rest, percent: 90 },` works!
- Not fun having to remember changing it and then calculating numbers and stuff if we add more spans to the rate limiting sieve.
  - Does setting the rate to 100 make our config not pass validation, or remove the other spans? Letch check.
    - Setting 100 as the allocation works!
      - Setting 100 on everything also works!
        - Maybe bad idea if many of the spammy things and other things happen at the same time though, so go back to setting reasonable ratios manually for now.

Full example

```yaml
networks:
  apps_network:
    attachable: true
    name: apps_network
    ipam:
      driver: default
      config:
        - subnet: "172.80.80.0/24"
        - subnet: "2001:8080:8080::/64"
services:
  create-observability-stack-configs:
    image: debian:stable-slim
    user: 1000:1001
    container_name: create-observability-stack-configs
    depends_on:
      set-up-container-mount-area:
        required: true
        restart: false
        condition: service_completed_successfully
    volumes:
      - .:/ProjectDir
    environment:
      RECREATE_IF_EXISTS: "false"
    entrypoint:
      - '/bin/bash'
      - '-c'
      - |
        echo '================ Creating directory structure ============================='
        mkdir -p /ProjectDir/ContainerData
        if [ ! -f /ProjectDir/ContainerData/.gitignore ]; then
          echo 'Creating gitignore so that you dont accidentally check in container data'
          echo 'If you want some of the container data checked in, simply add an exception to the gitignore'
          printf '%s\n' '*' '#!.gitignore' > /ProjectDir/ContainerData/.gitignore
        fi
        mkdir -p /ProjectDir/ContainerData/ObservabilityStackConfig
        cd /ProjectDir/ContainerData/ObservabilityStackConfig
        mkdir -p /ProjectDir/ContainerData/ObservabilityStackConfig/grafana
        mkdir -p /ProjectDir/ContainerData/ObservabilityStackConfig/grafana/datasources
        mkdir -p /ProjectDir/ContainerData/ObservabilityStackConfig/grafana-loki
        mkdir -p /ProjectDir/ContainerData/ObservabilityStackConfig/grafana-mimir
        mkdir -p /ProjectDir/ContainerData/ObservabilityStackConfig/grafana-tempo
        mkdir -p /ProjectDir/ContainerData/ObservabilityStackConfig/opentelemetry-collector
        echo '================ Done Creating directory structure ========================'
        # In the section below, note that cat <<'EOF' makes the output literal without bash attempting transforms,
        # but still need to escape $ with $$, so outputted $$ has to be rewritten to $$$$ here.
        echo '================ Creating Grafana Ini Config ============================='
        cat <<'EOF' > /ProjectDir/ContainerData/ObservabilityStackConfig/grafana/grafana.ini
        [auth]:
        disable_login_form: false

        [auth.anonymous]:
        enabled: true
        org_role: Admin

        [log]:
        mode: console
        level: error

        [feature_toggles]:
        enable: traceqlEditor traceQLStreaming metricsSummary tempoApmTable traceToMetrics

        [analytics]:
        enabled: false
        reporting_enabled: false
        check_for_updates: false
        check_for_plugin_updates: false
        EOF
        echo '================ Done Creating Grafana Ini Config ========================'

        echo '================ Creating Grafana Data Sources Config ============================='
        cat <<'EOF' > /ProjectDir/ContainerData/ObservabilityStackConfig/grafana/datasources/datasources.yaml
        apiVersion: 1

        datasources:
          - name: Loki
            type: loki
            uid: Loki
            url: http://grafana-loki:3100
            editable: true
            jsonData:
              derivedFields:
                - datasourceUid: "Tempo"
                  matcherRegex: "trace_id"
                  matcherType: "label"
                  name: "trace_id"
                  url: "$$$${__value.raw}"
          - name: Mimir
            type: prometheus
            uid: Mimir
            url: http://grafana-mimir:9009/prometheus
            editable: true
            jsonData:
              httpMethod: POST
              exemplarTraceIdDestinations:
                - datasourceUid: Tempo
                  name: trace_id
              prometheusType: Mimir
              prometheusVersion: 2.9.1
          - name: Tempo
            type: tempo
            uid: Tempo
            url: http://grafana-tempo:3200
            editable: true
            jsonData:
              httpMethod: GET
              nodeGraph:
                enabled: true
              search:
                filters:
                  - id": service-name
                    operator: =
                    scope: resource
                    tag: service.name
                  - id: span-name
                    operator: =
                    scope: span
                    tag: name
              serviceMap:
                datasourceUid: Mimir
              tracesToLogsV2:
                customQuery: true
                datasourceUid: Loki
                filterByTraceID: false
                query: '{$$$${__tags}} | trace_id="$$$${__trace.traceId}"'
                spanEndTimeShift: 30s
                spanStartTimeShift: -30s
                tags:
                  - key: service.name
                    value: service_name
        EOF
        echo '================ Done Creating Grafana Data Sources Config ========================'

        echo '================ Creating Grafana Loki Config ============================='
        cat <<'EOF' > /ProjectDir/ContainerData/ObservabilityStackConfig/grafana-loki/config.yaml
        auth_enabled: false

        server:
          http_listen_port: 3100
          grpc_listen_port: 9096
          log_level: error

        common:
          instance_addr: 127.0.0.1
          path_prefix: /tmp/loki
          storage:
            filesystem:
              chunks_directory: /tmp/loki/chunks
              rules_directory: /tmp/loki/rules
          replication_factor: 1
          ring:
            kvstore:
              store: inmemory

        query_range:
          results_cache:
            cache:
              embedded_cache:
                enabled: true
                max_size_mb: 100

        schema_config:
          configs:
            - from: 2020-10-24
              store: tsdb
              object_store: filesystem
              schema: v13
              index:
                prefix: index_
                period: 24h

        limits_config:
          allow_structured_metadata: true

        analytics:
          reporting_enabled: false
        EOF
        echo '================ Done Creating Grafana Loki Config ========================'

        echo '================ Creating Grafana Mimir Config ============================='
        cat <<'EOF' > /ProjectDir/ContainerData/ObservabilityStackConfig/grafana-mimir/config.yaml
        # Do not use this configuration in production.
        # It is for demonstration purposes only.
        multitenancy_enabled: false

        memberlist:
          bind_addr:
            - 127.0.0.1
          join_members:
            - grafana-mimir-gossip-ring:7946

        blocks_storage:
          backend: filesystem
          bucket_store:
            sync_dir: /tmp/mimir/tsdb-sync
          filesystem:
            dir: /tmp/mimir/data/tsdb
          tsdb:
            dir: /tmp/mimir/tsdb

        compactor:
          data_dir: /tmp/mimir/compactor
          sharding_ring:
            kvstore:
              store: memberlist

        distributor:
          ring:
            instance_addr: 127.0.0.1
            kvstore:
              store: memberlist

        ingester:
          ring:
            instance_addr: 127.0.0.1
            kvstore:
              store: memberlist
            replication_factor: 1

        ruler_storage:
          backend: filesystem
          filesystem:
            dir: /tmp/mimir/rules

        server:
          http_listen_port: 9009
          log_level: error

        store_gateway:
          sharding_ring:
            replication_factor: 1

        limits:
          max_global_exemplars_per_user: 10000

        usage_stats:
          enabled: false
        EOF
        echo '================ Done Creating Grafana Mimir Config ========================'

        echo '================ Creating Grafana Tempo Config ============================='
        cat <<'EOF' > /ProjectDir/ContainerData/ObservabilityStackConfig/grafana-tempo/config.yaml
        distributor:
          receivers:
            otlp:
              protocols:
                grpc:
                  endpoint: grafana-tempo:4317

        ingester:
          flush_check_period: 1s
          lifecycler:
            address: grafana-tempo
            min_ready_duration: 1s
            ring:
              kvstore:
                store: inmemory
              replication_factor: 1
          max_block_duration: 1s
          trace_idle_period: 1s

        metrics_generator:
          processor:
            local_blocks:
              filter_server_spans: false
            span_metrics:
              dimensions:
                - operation
                - service_name
                - status_code
          storage:
            path: /tmp/tempo/generator/wal
            remote_write:
              - url: http://grafana-mimir:9009/api/v1/push
                send_exemplars: true
          traces_storage:
            path: /tmp/tempo/generator/traces

        stream_over_http_enabled: true

        server:
          grpc_listen_port: 9096
          http_listen_port: 3200
          log_level: error

        querier:
          frontend_worker:
            frontend_address: grafana-tempo:9096

        storage:
          trace:
            backend: local
            local:
              path: /tmp/tempo/blocks
            wal:
              path: /tmp/tempo/wal

        overrides:
          metrics_generator_processors:
            - local-blocks
            - service-graphs
            - span-metrics

        usage_report:
          reporting_enabled: false

        EOF
        echo '================ Done Creating Grafana Tempo Config ========================'
        echo '================ Creating OpenTelemetry Collector Config ============================='
        # Nice guide for filtering: https://last9.io/blog/opentelemetry-configurations-filtering-sampling-enrichment/
        # Updated docs with correct syntax:
        # - Tail sampler: https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/main/processor/tailsamplingprocessor/README.md
        # - Filter: https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/main/processor/filterprocessor/README.md
        # Note you have to use the otel contrib variant of the collector image to get these running
        cat <<'EOF' > /ProjectDir/ContainerData/ObservabilityStackConfig/opentelemetry-collector/config.yaml
        receivers:
          otlp:
            protocols:
              grpc:
                endpoint: opentelemetry-collector:4317
              http:
                endpoint: opentelemetry-collector:4318

        processors:
          batch:
          memory_limiter:
            check_interval: 1s
            limit_mib: 1000
            spike_limit_mib: 100
          tail_sampling:
            decision_wait: 1s
            num_traces: 100_000
            expected_new_traces_per_sec: 10_000
            decision_cache:
              sampled_cache_size: 1_000_000
              non_sampled_cache_size: 1_000_000
            policies:
              [
                  {
                    # Always pass errors further on, don't drop them
                    name: status-code,
                    type: status_code,
                    status_code: { status_codes: [ERROR] },
                  },
                  {
                    name: composite-policy-1,
                    type: composite,
                    composite:
                    {
                      max_total_spans_per_second: 100,
                      policy_order:
                      [
                        composite-name-match-1,
                        composite-name-match-2,
                        composite-name-match-3,
                        composite-name-match-4,
                        composite-name-match-5,
                        composite-pass-rest
                      ],
                      composite_sub_policy:
                        [
                          { name: composite-name-match-1, type: ottl_condition, ottl_condition: { error_mode: ignore, span: [ 'name == "Span.First"' ], spanevent: [] } },
                          { name: composite-name-match-2, type: ottl_condition, ottl_condition: { error_mode: ignore, span: [ 'name == "Span.Second"' ], spanevent: [] } },
                          { name: composite-name-match-3, type: ottl_condition, ottl_condition: { error_mode: ignore, span: [ 'name == "Spand.Second.Subspan"' ], spanevent: [] } },
                          { name: composite-name-match-4, type: ottl_condition, ottl_condition: { error_mode: ignore, span: [ 'name == "Span.Third.Subspan"' ], spanevent: [] } },
                          { name: composite-name-match-5, type: ottl_condition, ottl_condition: { error_mode: ignore, span: [ 'name == "Span.Third"' ], spanevent: [] } },
                          { name: composite-pass-rest, type: always_sample }
                        ],
                      rate_allocation:
                        [
                          { policy: composite-name-match-1, percent: 5 },
                          { policy: composite-name-match-2, percent: 10 },
                          { policy: composite-name-match-3, percent: 10 },
                          { policy: composite-name-match-4, percent: 10 },
                          { policy: composite-name-match-5, percent: 10 },
                          { policy: composite-pass-rest, percent: 100 }
                        ]
                    }
                  }
              ]
          filter:
            # Don't bother with samling health check event data, these are for kubernetes/docker, low value on telemetry end
            error_mode: ignore
            traces:
              span:
                - IsMatch(resource.attributes["http.url"], ".*/health")
            logs:
              log_record:
                - 'IsMatch(body, "healthy")'
                - 'severity_number < SEVERITY_NUMBER_WARN'

        exporters:
          otlphttp/loki:
            endpoint: http://grafana-loki:3100/otlp
          otlphttp/mimir:
            endpoint: http://grafana-mimir:9009/otlp
          otlp/tempo:
            endpoint: grafana-tempo:4317
            tls:
              insecure: true

        extensions:
          health_check:

        service:
          extensions: [health_check]
          pipelines:
            logs:
              receivers: [otlp]
              processors: [memory_limiter, batch]
              exporters: [otlphttp/loki]
            metrics:
              receivers: [otlp]
              processors: [memory_limiter, batch]
              exporters: [otlphttp/mimir]
            traces:
              receivers: [otlp]
              processors: [memory_limiter, tail_sampling, filter, batch]
              exporters: [otlp/tempo]
          telemetry:
            logs:
              level: debug
        EOF
        echo '================ Done Creating OpenTelemetry Collector Config ========================'
  grafana:
    image: grafana/grafana:12.1.0
    depends_on:
      create-observability-stack-configs:
        required: true
        restart: false
        condition: service_completed_successfully
    ports:
      - 3000:3000
    networks:
      - apps_network
    volumes:
      - ./ContainerData/ObservabilityStackConfig/grafana/grafana.ini:/etc/grafana/grafana.ini
      - ./ContainerData/ObservabilityStackConfig/grafana/datasources:/etc/grafana/provisioning/datasources
  grafana-loki:
    image: grafana/loki:3.5
    depends_on:
      create-observability-stack-configs:
        required: true
        restart: false
        condition: service_completed_successfully
    command: ["-config.file=/etc/config.yaml"]
    networks:
      - apps_network
    volumes:
      - ./ContainerData/ObservabilityStackConfig/grafana-loki/config.yaml:/etc/config.yaml
  grafana-mimir:
    image: grafana/mimir:2.16.1
    depends_on:
      create-observability-stack-configs:
        required: true
        restart: false
        condition: service_completed_successfully
    command: ["-config.file=/etc/config.yaml"]
    networks:
      - apps_network
    volumes:
      - ./ContainerData/ObservabilityStackConfig/grafana-mimir/config.yaml:/etc/config.yaml
  grafana-tempo:
    image: grafana/tempo:2.7.2
    depends_on:
      create-observability-stack-configs:
        required: true
        restart: false
        condition: service_completed_successfully
    command: ["-config.file=/etc/config.yaml"]
    networks:
      - apps_network
    volumes:
      - ./ContainerData/ObservabilityStackConfig/grafana-tempo/config.yaml:/etc/config.yaml
  opentelemetry-collector:
    image: otel/opentelemetry-collector-contrib:0.130.1
    ports:
      - 4317:4317
      - 4318:4318
      - 9201:9201
    command: ["--config=/etc/config.yaml"]
    networks:
      - apps_network
    volumes:
      - ./ContainerData/ObservabilityStackConfig/opentelemetry-collector/config.yaml:/etc/config.yaml
    depends_on:
      - grafana-loki
      - grafana-mimir
      - grafana-tempo

  # otel-lgtm-stack:
  #   image: grafana/otel-lgtm
  #   ports:
  #     - 3000:3000
  #     - 4317:4317
  #     - 4318:4318
  #   networks:
  #     - apps_network
  #   environment:
  #     GF_PATHS_DATA: "/data/grafana"
  #   volumes:
  #     - "./ContainerData/OtelGrafana/tempo/data:/data/tempo"
  #     - "./ContainerData/OtelGrafana/grafana/data:/data/grafana"
  #     - "./ContainerData/OtelGrafana/loki/data:/data/loki"
  #     - "./ContainerData/OtelGrafana/loki/storage:/loki"
  #     - "./ContainerData/OtelGrafana/prometheus/data:/data/prometheus"
  #     - "./ContainerData/OtelGrafana/pyroscope/data:/data/pyroscope"
```

Share and enjoy!
