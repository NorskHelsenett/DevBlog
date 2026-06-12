# Observability

## Forberedelser

Følgende hemmeligheter må lages i clusteret:

- `grafana`
  - må lages i namespacet `grafana`
  - må inneholde nøklene `admin-user` og `admin-password`
- `grafana-oauth2`
  - må lages i namespacet `grafana`
  - må inneholde nøklene `GF_AUTH_GENERIC_OAUTH_CLIENT_ID` og `GF_AUTH_GENERIC_OAUTH_CLIENT_SECRET`
- `kafka-otel-user-secret`
  - må lages i namespacet `opentelemetry`
  - må inneholde nøklene `BrokerCaCertificate.pem`, `UserCertificate.pem` og `UserKey.pem`
- `loki`
  - må lages i namespacet `loki`
  - må inneholde nøklene `S3_PASSWORD` og `S3_USER`
- `mimir`
  - må lages i namespacet `mimir`
  - må inneholde nøklene `S3_PASSWORD` og `S3_USER`
- `tempo`
  - må lages i namespacet `tempo`
  - må inneholde nøklene `S3_PASSWORD` og `S3_USER`

## Hvordan slette logger i loki

1. Port-forward loki-gateway `kubectl -n loki port-forward svc/loki-gateway 3100:80`.
2. Kjør en delete request med curl. Lag en spørring for hvilke data som skal slettes og velg et starttidspunkt (unix timestamp) for slettingen. Du kan også ha et sluttidspunkt. Eksempel:

```sh
curl -G --noproxy '*' \
  -X POST 'http://localhost:3100/loki/api/v1/delete' \
  -H 'X-Scope-OrgID: team-awesome' \
  --data-urlencode 'query={service_name="CoolesService.BEST"} |~ "target-number-name\":\"[0-9]{11}\"|second-number-target-name\":\"[0-9]{11}\"|anotherNumber\":\"[0-9]{11}\"|or_just_this_string"' \
  --data-urlencode 'start=1577840461'
```

Slettinger har en kanselleringsperiode på 24 timer. For å se hvilke slettinger som er lagt inn kan du kjøre spørringen:

```sh
curl --noproxy '*' -X GET http://localhost:3100/loki/api/v1/delete -H 'X-Scope-OrgID: team-awesome'
```

## Importere rules fra clusterne

[kube-prometheus-stack](https://github.com/prometheus-community/helm-charts/tree/main/charts/kube-prometheus-stack) blir installert i alle clusterne og kommer med en del recording og alerting rules.

OBS! Clusterene med observability stacken følger med en del Grafana spesifikke rules, så det kan være greit å hente dem ut av et annet rent cluster som env-produc-cluster-0, eller rett fra prometheus prosjektet på github.

Recording rules fra kube-prometheus-stack blir installert i observability-stacken via jobben `mimirtools-load-prometheus-cluster-rules` som er definert i helm chartet her.

For å manuelt legge inn recording og alerting rule-ene inn i mimir kan man gjøre følgende:

`Recording rules:`

```sh
kubectl -n prometheus-operator port-forward svc/prometheus-kube-prometheus-prometheus 9090

curl "http://localhost:9090/api/v1/rules?type=record" | yq -P | yq '.data' |
  yq 'del(.. | select(has("evaluationTime")).evaluationTime)' |
  yq 'del(.. | select(has("lastEvaluation")).lastEvaluation)' |
  yq 'del(.groups.[].file)' |
  yq 'del(.groups.[].rules.[].health)' |
  yq 'del(.groups.[].rules.[].type)' |
  yq 'del(.groups.[].interval)' |
  yq 'del(.groups.[].limit)' |
  yq '.groups[].rules[] |= with_entries( (.key | select(. == "query")) = "expr" )' |
  yq '.groups[].rules[] |= with_entries( (.key | select(. == "name")) = "record" )' |
  >rules.yaml

sed -E 's/cluster([^:_])/source_cluster\1/g' rules.yaml > prometheus_aggregated_cluster_metrics.yaml

kubectl -n mimir port-forward svc/mimir-gateway 8080:80

export MIMIR_ADDRESS="http://localhost:8080"

export MIMIR_TENANT_ID="<tenant-id (for eksempel team-best)>"

mimirtool rules load prometheus_aggregated_cluster_metrics.yaml
```

`Alerting rules:`

```sh
kubectl -n prometheus-operator port-forward svc/prometheus-kube-prometheus-prometheus 9090

curl "http://localhost:9090/api/v1/rules?type=alert" | yq -P | yq '.data' |
  yq 'del(.. | select(has("evaluationTime")).evaluationTime)' |
  yq 'del(.. | select(has("lastEvaluation")).lastEvaluation)' |
  yq 'del(.groups.[].file)' |
  yq 'del(.groups.[].interval)' |
  yq 'del(.groups.[].limit)' |
  yq 'del(.groups.[].rules.[].health)' |
  yq 'del(.groups.[].rules.[].type)' |
  yq 'del(.groups.[].rules.[].alerts)' |
  yq 'del(.groups.[].rules.[].keepFiringFor)' |
  yq 'del(.groups.[].rules.[].state)' |
  yq '.groups[].rules[] |= with_entries( (.key | select(. == "duration")) = "for" )' |
  yq '.groups[].rules[] |= .for += "s"' |
  yq '.groups[].rules[] |= with_entries( (.key | select(. == "query")) = "expr" )' |
  yq '.groups[].rules[] |= with_entries( (.key | select(. == "name")) = "alert" )' |
  >alerts.yaml

sed -E 's/cluster([^:_])/source_cluster\1/g' rules.yaml > prometheus_aggregated_cluster_metrics.yaml

kubectl -n mimir port-forward svc/mimir-gateway 8080:80

export MIMIR_ADDRESS="http://localhost:8080"

export MIMIR_TENANT_ID="<tenant-id (for eksempel team-morsomst)>"

mimirtool rules load alerts.yaml
```

## Øke størrelsen på PVC

For å øke størrelsen på PVCer må man gjøre følgende:

- Øke størrelsen i manifestene
- Pushe ny versjon
- Sync i argo
- Redigere alle pvcer som er endret manuelt med kubectl
- Slett statefulset og sync på nytt. OBS! Pass på at du bare sletter et statefulset av gangen slik at minst to soner er tilgjengelig.
- Gjenta til alt er i sync i argo

## Hvordan onboarde et nytt team

1. Opprett topics og applikasjon i Kafka (gjøres av teamet)
2. Be om tilgang til å konsumere topics fra observability-stack applikasjon i Kafka (gjøres av plattformteamet og godkjennes av teamet)
3. Legg til teamet i test og/eller prod values-fil i [observability helm chartet](./) (gjøres av plattformteamet)
4. Lag ny rolle i [Azure entra id i app-registreringen EksempelAppRegistrationNavn](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/AppRoles/appId/000-000-000/isMSAApp~/false) (gjøres av plattformteamet)
5. Legg til personer eller grupper med riktig rolle i [enterprise applicationen EksempelEnterpriseAppNavn i Azure](https://portal.azure.com/#view/Microsoft_AAD_IAM/ManagedAppMenuBlade/~/Users/objectId/111-111-111/appId/000-000-000) (gjøres av plattformteamet)

Det kan hende at brukerne må logge ut og inn igjen i grafana for at det skal fungere.

## Feilsituasjoner

### Grafana-provisioner får ikke provisjonert en ny organisasjon

Når man onboarder et nytt team kan det hende at grafana-provisioner feiler i provisjoneringen av ressurser i grafana. Dette kan skje fordi jobben kjører før grafana er klar til å ta i mot trafikk. Løsningen er stort sett bare å kjøre grafana-provisioner jobben på nytt, som kan gjøres ved å synce applikasjonen på nytt i argocd.

## Hvordan legge til nye dashboards i fellesgrafana (grafana.example.com)

I grafana-instansene som ligger i denne stacken så blir det provisjonert en del ressurser ved onboarding av nye team. Per i dag (10.11.2025) blir dette gjort av en egenutviklet applikasjon som blir deployet i denne stacken kalt grafana-provisioner (kan også gjøres med for eksempel terraform). Kildekoden til dette prosjektet [ligger her](../../grafana-provisioner/). Du kan lese hvordan man legger til eller endrer ressurser som blir provisjonert i prosjektets README.

## Litt om tenants

Vi har satt opp grafana-stacken slik at hvert team sender inn data som en egen "tenant" i Loki/Mimir/Tempo ved å sette headeren `X-Scope-OrgId` når vi sender dataen inn fra opentelemetry-collectorene. For å skille dataen i grafana har hvert team deretter fått sin egen organisasjon som er koblet til datakildene med riktig tenant-header. Tenant-headerene er ikke hemmelig i vårt oppsett, dvs de er på formen `team-abc`, som vil si at hvis et team får tilgang til å være administrator i sin organisasjon så vil de kunne legge til datakilder som leser data fra andre team. Vi har derfor ikke gitt noen team tilgang til administrator-rollen på sin organisasjon. Dette gjør at ingen team kan markere sine egne dashboards som [externally shared](https://grafana.com/docs/grafana/latest/visualizations/dashboards/share-dashboards-panels/shared-dashboards/) siden man må være administrator for å gjøre det. Det betyr at plattformteamet må gjøre det for alle team som ønsker å dele sine dashboard eksternt, for eksempel hvis man ønsker å ha et dashboard på en skjerm i kontorlokalene.

Et annet alternativ til dette oppsettet er at man lager tilfeldige hemmelige tenant-id for hvert team automatisk. Da vil man kunne åpne for at et team også kan være administrator i sin organisasjon. Dette er også en mer "anbefalt" måte å gjøre det på i den forstand at grafana anbefaler at verdien til `X-Scope-OrgId` skal være hemmelig. For at dette skal fungere så må det lages kubernetes-secrets som kobler et team med en tenant-id som distribueres i de forskjellige namespacene som trenger det, dvs i namespacene `grafana`, `grafana-provisioner`, `loki`, `mimir`, `opentelemetry` og `tempo`.

Hvis oppsettet skal endres på så må dataen migreres fra gammel til ny tenant for teamene.
