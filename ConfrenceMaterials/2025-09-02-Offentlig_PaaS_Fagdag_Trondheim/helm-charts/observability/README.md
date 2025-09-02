# Observability

## Forberedelser

Følgende hemmeligheter må lages i clusteret:

- grafana-example-secret-name
  - må lages i namespacet `grafana`
  - må inneholde nøklene `admin-user` og `admin-password`
- grafana-oauth2-example-secret-name
  - må lages i namespacet `grafana`
  - må inneholde nøklene `GF_AUTH_GENERIC_OAUTH_CLIENT_ID` og `GF_AUTH_GENERIC_OAUTH_CLIENT_SECRET`
- kafka-example-secret-name
  - må lages i namespacet `opentelemetry`
  - må inneholde nøklene `BrokerCaCertificate.pem`, `UserCertificate.pem` og `UserKey.pem`
- loki-s3-example-secret-name
  - må lages i namespacet `loki`
  - må inneholde nøklene `LOKI_S3_PASSWORD_EXAMPLE_KEY_NAME` og `LOKI_S3_USER_EXAMPLE_KEY_NAME`
- mimir-s3-example-secret-name
  - må lages i namespacet `mimir`
  - må inneholde nøklene `MIMIR_S3_PASSWORD_EXAMPLE_KEY_NAME` og `MIMIR_S3_USER_EXAMPLE_KEY_NAME`
- tempo-s3-example-secret-name
  - må lages i namespacet `tempo`
  - må inneholde nøklene `TEMPO_S3_PASSWORD_EXAMPLE_KEY_NAME` og `TEMPO_S3_USER_EXAMPLE_KEY_NAME`

## Hvordan slette logger i loki

1. Port-forward loki-gateway `kubectl -n loki port-forward svc/loki-gateway 3100:80`.
2. Kjør en delete request med curl. Lag en spørring for hvilke data som skal slettes og velg et starttidspunkt (unix timestamp) for slettingen. Du kan også ha et sluttidspunkt. Eksempel:

```sh
curl -G -X POST 'http://localhost:3100/loki/api/v1/delete' -H 'X-Scope-OrgID: team-eksempelteam' --data-urlencode 'query={service_name="ExampleServiceName"} |~ "jsonKey1\":\"[0-9]{11}\"|OtherJsonKey\":\"[0-9]{11}\"|ExactSubstring"' --data-urlencode 'start=1739188615'
```

Slettinger har en kanselleringsperiode på 24 timer. For å se hvilke slettinger som er lagt inn kan du kjøre spørringen:

```sh
curl -X GET http://localhost:3100/loki/api/v1/delete -H 'X-Scope-OrgID: team-eksempelteam'
```

## Importere recording rules fra clusterne

[kube-prometheus-stack](https://github.com/prometheus-community/helm-charts/tree/main/charts/kube-prometheus-stack) blir installert i alle clusterne og kommer med en del recording og alerting rules. For å få disse inn i mimir kan man gjøre følgende:

```sh
kubectl -n prometheus-operator port-forward svc/prometheus-kube-prometheus-prometheus 9090

curl "http://localhost:9090/api/v1/rules?type=record" | yq -P | yq '.data' >rules.yaml

kubectl -n mimir port-forward svc/mimir-gateway 8080:80

export MIMIR_ADDRESS="http://localhost:8080"

export MIMIR_TENANT="<tenant-id (for eksempel team-eksempelteam)>"

mimirtool rules load rules.yaml
```

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
