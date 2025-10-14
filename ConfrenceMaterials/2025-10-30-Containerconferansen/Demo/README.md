This is a demo that sets up a Kafka cluster, an LGTM observability stack (Grafana Loki Tempo Mimir), and a fairly simple dotnet project that we can observe how behaves.

# How to run it

```sh
$ docker compose up -d
$ cd ./source
$ docker compose up -d
```

## How to stop the madness

```sh
$ cd ./source
$ docker compose down
$ cd ..
$ docker compose down
```

# How it all hangs together

ToDo: Drawing or something for the textually impaired

# Related reading material

ToDo:
- Consider moving this to the top level readme?
- Link kafka mtls repo
- Link otel rate limiting blogpost
- Link sibling repo entry offentlig paas fagdag otel presentation
  - Mention the additional helm charts setup not found in this demo
-
