ToDo: Fluffify the text

# Scaling when working with observability locally

In this piece we'll look at how to deal with larger volumes of telemetry data locally, using the OpenTelemetry Collector to tackle the challenge.

# Background

Using [OpenTelemetry](https://opentelemetry.io/) has made making observable applications fairly easy to do. And because it's a widely adopted standard, there is excellent tooling surrounding it, enabling us to easily make sense and use of the telemetry data. What's really good is that we get a common and cohesive way to gain insights into how our applications interact and behave, even across environments. While there are excellent performance profiling and debugging tools, these are often hard or impossible to use in production. And if you are investigating performance related issues, enabling the tools can subtly change what you're trying to observe, messing with your work.

Thankfully [Grafana](https://grafana.com) has made a lightweight image that is simple to use, and makes getting started a breeze. Their OTel LGMT image ([image](https://hub.docker.com/r/grafana/otel-lgtm), [docs](https://github.com/grafana/docker-otel-lgtm/?tab=readme-ov-file#docker-otel-lgtm)) lets you spin up a whole observability stack in one go, that covers everything from the ingestion process, to the logs/metrics/traces storage and querying engines, and the Grafana web UI.

# Problem of the day

The problem has it's roots in trying to match up two datasets of about 2 million entries. All in all not a huge amount of data, 3 to 4 gigabytes each. However, occasionally having to reprocess it, makes the points in the process taking 1 to 10 milliseconds a painful bottleneck when you can observe most things taking tens to hundreds of microseconds. Initially the telemetry has already proved invaluable in speeding up some of the most egregious offense, especially the tracing.

The main part of our beings now with the pipeline growing longer, and [Tempo](https://grafana.com/oss/tempo/) basically [oom](https://en.wikipedia.org/wiki/Out_of_memory) killing the LGTM image observability stack. Which is highly annoying, because the traces are very useful at this stage. And it doesn't help that it takes Grafana down with it, along with the other creature comforts it provides.

At this point we could have ended the story early, by implementing [head sampling](https://opentelemetry.io/docs/concepts/sampling/#head-sampling) in the application(s). This is however not desirable, as the telemetry amounts are manageable for production, and it would be dumb to limit our insights into production because my laptop struggles to keep up.

# Tail Sampling

So what do we do? At the same place we learned about the existence of head sampling, we discover [tail sampling](https://opentelemetry.io/docs/concepts/sampling/#tail-sampling). Which should allow us to configure things locally so that while we generate the same amount of telemetry, thus not changing the codes behaviour and performance significantly, we don't neccessarily process all of the gathered data. Perfect!

This however means that we have to start tinkering with the [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/) piece of the telemetry pipeline. And because the chill simplicity of the just spinning up the LGTM image goes bye-bye, we might as well set up things a bit more properly, with running separate images for the different components. This also has the added bonus of that if one of them dies, like say Tempo receives too many traces for the RAM allocated by Docker, the rest should still work!

So, in practice this means that we'll have to spin up 5 images, and create the configs for them:

- The OpenTelemetry Collector
- Loki (Grafanas log engine)
- Mimir (Grafans metrics engine)
- Tempo (Grafanas engine for traces)
- Grafana

There is a fully working example (at the time of writing at least) at the bottom of this article, as at this address: [TODO](./)

# First contact

Poking at the problem a bit, I stumbled upon the [Probabilistic Sampling Processor](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/processor/probabilisticsamplerprocessor). This looked really promising, because it can reduce the amount of data passed through, but retain interesting information like if any traces occur in bursts, how large they are. Sounds awesome! Looking into it a bit further, I discovered that to use it, I needed to stuff it into a policy as a member of a processing pipeline like the [Tail Sampling Processor](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/processor/tailsamplingprocessor) (the linked documentation has some more examples that what I'll cover here).

After some trial and error, I tried my luck with a configuration file for the open telemetry collector looking more or less like this:

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

Of course, on the first attempt of using it, it failed spectacularly. As it turns out, the lightweight OTel Collector image recommended for uses like running locally, didn't have the `probabilistic` sampling processor installed. After some wtf-ing and searching, I discovered that the reasonable approach to my voes would be going over to use the [OpenTelemetry Collector Contrib](https://github.com/open-telemetry/opentelemetry-collector-contrib) [image](https://hub.docker.com/r/otel/opentelemetry-collector-contrib) instead, as it comes with more or less all the commonly used processors.

So then, armed with the new image I set out. And it worked!

But, I soon after observed that the most frequently occurring [trace spans](https://opentelemetry.io/docs/concepts/signals/traces/#spans) in the application had such a large volume, that they simply drowned out the chances of sampling the ones more rarely emitted. With regards to Tempo dying because of memory issues it was certainly an improvement, but lots of observability of only one trace through a small part of the code was still not quite what I was looking for.

# Back to the drawing board


