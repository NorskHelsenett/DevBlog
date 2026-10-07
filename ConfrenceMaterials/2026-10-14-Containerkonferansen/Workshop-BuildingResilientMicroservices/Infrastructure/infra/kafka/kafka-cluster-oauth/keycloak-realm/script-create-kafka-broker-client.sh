kcUrl="not_set"
kcRealm="not_set"
kcToken="not_set"
desiredClientName="not_set"
desiredClientSecret="not_set"
LOG_ENABLED="logdisabled"
while [ $# -gt 0 ]; do
  case "$1" in
    --kc-url*|-k*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      kcUrl="$1"
      ;;
    --token*|-t*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      kcToken="$1"
      ;;
    --realm*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      kcRealm="$1"
      ;;
    --broker-client-name*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      desiredClientName="$1"
      ;;
    --broker-client-secret*|-s*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      desiredClientSecret="$1"
      ;;
    --verbose*|-v*)
      # This is flag, don't consume next value # if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      LOG_ENABLED="logenabled"
      ;;
    *)
      >&2 printf "Error: Invalid parameter\n"
      exit 1
      ;;
  esac
  shift
done

log() {
  if [ "$LOG_ENABLED" = "logenabled" ]; then
    # Simple timestamped logger
    _now=$(date +%H:%M:%S)
    printf '[%s] %s\n' "$_now" "$*"
  fi
}

CLIENT_KCID="${desiredClientName}-db-id"

log "\n==> Creating Kafka oauth client registration for ${desiredClientName} in realm ${kcRealm}"
curl -s -X POST "${kcUrl}/admin/realms/${kcRealm}/clients" \
  -H "Authorization: Bearer ${kcToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "id": "'${CLIENT_KCID}'",
    "clientId": "'${desiredClientName}'",
    "name": "'${desiredClientName}'",
    "description": "kafka oauth client for tenant '${desiredClientName}'",
    "protocol": "openid-connect",
    "publicClient": false,
    "bearerOnly": false,
    "directAccessGrantsEnabled": false,
    "serviceAccountsEnabled": true,
    "standardFlowEnabled": false,
    "authorizationServicesEnabled": true,
    "clientAuthenticatorType": "client-secret",
    "secret": "'${desiredClientSecret}'",
    "attributes": {
      "access.token.lifespan": "600"
    },
    "defaultClientScopes": ["openid", "profile"],
    "optionalClientScopes": [],
    "protocolMappers": []
  }' 1>/dev/null

# CLIENTS=$(curl -s -H "Authorization: Bearer ${TOKEN}" "${KEYCLOAK_URL}/admin/realms/${REALM}/clients")
# CLIENT_KCID=$(echo ${CLIENTS} | jq -r ".[] | select(.clientId==\"${desiredClientName}\") | .id")

log "\n==> Creating audience mappers for kafka clients so that token audience becomes expected kafka-broker"
curl -s -X POST "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/protocol-mappers/models" \
  -H "Authorization: Bearer ${kcToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "kafka-audience",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-audience-mapper",
    "consentRequired": false,
    "config": {
      "included.client.audience": "kafka-broker",
      "id.token.claim": "true",
      "access.token.claim": "true"
    }
  }' 1>/dev/null

log "\n==> Configuring broker client to strictly enforce autz"
curl -s -X PUT "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $kcToken" \
  -d '{
    "allowRemoteResourceManagement": true,
    "policyEnforcementMode": "ENFORCING",
    "decisionStrategy": "AFFIRMATIVE"
  }' 1>/dev/null

# log "\n===> Delete Default Resource"
# # RESOURCE_ID=$(curl -s -H "Authorization: Bearer $kcToken" \
# #   "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/resource" \
# #   | jq -r '.[] | select(.name=="Default Resource") | ._id')

# # curl -s -X DELETE \
# #   "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/resource/${RESOURCE_ID}" \
# #   -H "Authorization: Bearer $kcToken"
# RESOURCE_ID=$(curl -s -H "Authorization: Bearer ${kcToken}" \
#   "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/resource" |
#   jq -r '.[] | select(.name=="Default Resource") | .id')

# if [[ -n "${RESOURCE_ID}" && "${RESOURCE_ID}" != "null" ]]; then
#   curl -s -X DELETE \
#     "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/resource/${RESOURCE_ID}" \
#     -H "Authorization: Bearer ${kcToken}" > /dev/null
#   log "    → Deleted resource ${RESOURCE_ID}"
# else
#   log "    → No Default Resource found - skipping"
# fi

# log "\n===> Delete Default Policy"
# # POLICY_ID=$(curl -s -H "Authorization: Bearer $kcToken" \
# #   "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/policy" \
# #   | jq -r '.[] | select(.name=="Default Policy") | .id')

# # curl -s -X DELETE \
# #   "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/policy/${POLICY_ID}" \
# #   -H "Authorization: Bearer $kcToken"
# POLICY_ID=$(curl -s -H "Authorization: Bearer ${kcToken}" \
#   "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/policies" |
#   jq -r '.[] | select(.name=="Default Policy") | .id')

# if [[ -n "${POLICY_ID}" && "${POLICY_ID}" != "null" ]]; then
#   curl -s -X DELETE \
#     "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/policies/${POLICY_ID}" \
#     -H "Authorization: Bearer ${kcToken}" > /dev/null
#   log "    → Deleted policy ${POLICY_ID}"
# else
#   log "    → No Default Policy found - skipping"
# fi

log "\n===>Creating broker client scopes"
for scope in Create Write Read Delete Describe Alter \
             DescribeConfigs AlterConfigs ClusterAction \
             IdempotentWrite All; do
  log "\n==> Creating scope ${scope} in client ${CLIENT_KCID} in realm ${kcRealm}"
  curl -s -X POST \
    "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}/authz/resource-server/scope" \
    -H "Authorization: Bearer ${kcToken}" \
    -H "Content-Type: application/json" \
    -d '{"name":"'${scope}'","displayName":"'${scope}' kafka operation"}' 1>/dev/null
done

log "\n===> Broker enabling client auth"
curl -s -X PUT "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}" \
  -H "Authorization: Bearer ${kcToken}" \
  -H "Content-Type: application/json" \
  -d "$(curl -s -H "Authorization: Bearer ${kcToken}" \
        "${kcUrl}/admin/realms/${kcRealm}/clients/${CLIENT_KCID}" \
        | jq '.attributes."client.authz.enabled" = "true"')" 1>/dev/null
