KEYCLOAK_URL="PLACEHOLDER"
TOKEN="PLACEHOLDER"
REALM="PLACEHOLDER"
REALM_ADMIN_USERNAME="PLACEHOLDER"
REALM_ADMIN_PASSWORD="password"
VERBOSE_FLAG="PLACEHOLDER"
while [ $# -gt 0 ]; do
  case "$1" in
    --kc-url*|-k*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KEYCLOAK_URL="$1"
      ;;
    --token*|-t*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      TOKEN="$1"
      ;;
    --realm*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      REALM="$1"
      ;;
    --new-admin-username*|-u*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      REALM_ADMIN_USERNAME="$1"
      ;;
    --new-admin-password*|-p*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      REALM_ADMIN_PASSWORD="$1"
      ;;
    --verbose*|-v*)
      # This is flag, don't consume next value # if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      VERBOSE_FLAG="--verbose"
      ;;
    *)
      >&2 printf "Error: Invalid parameter\n"
      exit 1
      ;;
  esac
  shift
done

log() {
  if [ -n "${VERBOSE_FLAG}" ]; then
    # Simple timestamped logger
    _now=$(date +%H:%M:%S)
    printf '[%s] %s\n' "$_now" "$*"
  fi
}

# log "Fetching admin user token"
# KC_ADMIN_USERNAME=admin
# KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
# TOKEN=$(curl -s \
#   -d "client_id=admin-cli" \
#   -d "username=${KC_ADMIN_USERNAME}" \
#   -d "password=${KC_ADMIN_PASSWORD}" \
#   -d "grant_type=password" \
#   "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
#   | jq -r .access_token)
# # log "Keycloak admin access token raw: ${TOKEN}"
# # log "Keycloak admin access token decoded:"
# # log $TOKEN | jq -R 'split(".") | .[1] | @base64d | fromjson'

log "🌍  Creating realm \"${REALM}\" …"

payload=$(jq -n \
  --arg realm "${REALM}" \
  --arg displayName "${REALM}" \
  '{
    realm: $realm,
    displayName: $displayName,
    enabled: true,
    sslRequired: "NONE"
  }')

curl -sS -X POST "${KEYCLOAK_URL}/admin/realms" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "${payload}" \
  -w "\nHTTP %{http_code}\n" 1>/dev/null

log "Update account-console client so users can log in and view their profile (to test their credentials)."

CLIENT_ID="account-console"
CLIENT_UUID=$(curl -s -G "${KEYCLOAK_URL}/admin/realms/${REALM}/clients" \
  -H "Authorization: Bearer ${TOKEN}" \
  --data-urlencode "clientId=$CLIENT_ID" | jq -r '.[0].id')

curl -sS -X PUT "${KEYCLOAK_URL}/admin/realms/${REALM}/clients/${CLIENT_UUID}" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"webOrigins":["*"]}' \
  -w "\nHTTP %{http_code}\n" 1>/dev/null

log "Set up group for realm admins"
GROUP_NAME="realm-admins"

EXISTING_GROUP_ID=$(curl -s "${KEYCLOAK_URL}/admin/realms/${REALM}/groups" \
        -H "Authorization: Bearer ${TOKEN}" |
        jq -r ".[] | select(.name==\"${GROUP_NAME}\") | .id")

if [[ -n "$EXISTING_GROUP_ID" && "$EXISTING_GROUP_ID" != "null" ]]; then
  GROUP_ID=$EXISTING_GROUP_ID
  log "ℹ️  Group \"${GROUP_NAME}\" already exists (id=$GROUP_ID)."
else
  log "🛠️  Creating group \"${GROUP_NAME}\" …"
  create_group_resp=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
          "${KEYCLOAK_URL}/admin/realms/${REALM}/groups" \
          -H "Authorization: Bearer ${TOKEN}" \
          -H "Content-Type: application/json" \
          -d "{\"name\":\"${GROUP_NAME}\"}")

  if [[ "$create_group_resp" != "201" && "$create_group_resp" != "204" ]]; then
    log "❌ Failed to create group – HTTP $create_group_resp"
    exit 1
  fi

  GROUP_ID=$(curl -s "${KEYCLOAK_URL}/admin/realms/${REALM}/groups" \
          -H "Authorization: Bearer ${TOKEN}" |
          jq -r ".[] | select(.name==\"${GROUP_NAME}\") | .id")

  log "✅ Created group \"${GROUP_NAME}\" with id $GROUP_ID"
fi

log "Extracting internal ID of the realm-management client"
RM_CLIENT_ID="realm-management"
RM_CLIENT_UUID=$(curl -s -G "${KEYCLOAK_URL}/admin/realms/${REALM}/clients" \
        -H "Authorization: Bearer ${TOKEN}" \
        --data-urlencode "clientId=${RM_CLIENT_ID}" |
        jq -r '.[0].id')

if [[ -z "$RM_CLIENT_UUID" || "$RM_CLIENT_UUID" == "null" ]]; then
  log "❌ Could not locate client $RM_CLIENT_ID"
  exit 1
fi
log "✅ realm-management client UUID = $RM_CLIENT_UUID"

log "Pull the full representation of the realm-admin role"
ROLE_NAME="realm-admin"
ROLE_REP=$(curl -s "${KEYCLOAK_URL}/admin/realms/${REALM}/clients/${RM_CLIENT_UUID}/roles/${ROLE_NAME}" \
        -H "Authorization: Bearer ${TOKEN}" |
        jq '.')

if [[ -z "$ROLE_REP" || "$ROLE_REP" == "null" ]]; then
  log "❌ Could not fetch role $ROLE_NAME"
  exit 1
fi
log "✅ Got representation of role \"$ROLE_NAME\""

log "Assign the ${ROLE_NAME} role to the group (client-role-mapping)"
payload=$(jq -n --argjson role "$ROLE_REP" '[ $role ]')

log "🔗 Assigning role \"$ROLE_NAME\" to group \"${GROUP_NAME}\" …"
assign_resp=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
        "${KEYCLOAK_URL}/admin/realms/${REALM}/groups/${GROUP_ID}/role-mappings/clients/${RM_CLIENT_UUID}" \
        -H "Authorization: Bearer ${TOKEN}" \
        -H "Content-Type: application/json" \
        -d "$payload")

log "👤  Creating user \"${REALM_ADMIN_USERNAME}\" in realm \"${REALM}\" …"
user_payload=$(jq -n \
  --arg username "${REALM_ADMIN_USERNAME}" \
  --arg firstName "Realm Admin" \
  --arg lastName "${REALM_ADMIN_USERNAME}" \
  --arg email "${REALM_ADMIN_USERNAME}@example.com" \
  '{
    username: $username,
    firstName: $firstName,
    lastName: $lastName,
    email: $email,
    enabled: true,
    emailVerified: false
  }')

create_user_response=$(curl -s -i -X POST "${KEYCLOAK_URL}/admin/realms/${REALM}/users" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "${user_payload}")

USER_ID=$(curl -s -G "${KEYCLOAK_URL}/admin/realms/${REALM}/users" \
        -H "Authorization: Bearer ${TOKEN}" \
        --data-urlencode "username=${REALM_ADMIN_USERNAME}" \
        --data-urlencode "exact=true" \
        --data-urlencode "max=1" \
        --data-urlencode "briefRepresentation=true" |
        jq -r 'if length == 0 then "" else .[0].id end')

# log "Users ID is: ${USER_ID}"

log "🔐  Setting password for user \"${REALM_ADMIN_USERNAME}\" …"
pw_payload=$(jq -n \
  --arg pwd "$REALM_ADMIN_PASSWORD" \
  '{
    type: "password",
    temporary: false,
    value: $pwd
  }')

curl -sS -X PUT "${KEYCLOAK_URL}/admin/realms/${REALM}/users/${USER_ID}/reset-password" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "${pw_payload}" \
  -w "\nHTTP %{http_code}\n" 1>/dev/null

log "The realm admin ${REALM_ADMIN_USERNAME} can now use the password ${REALM_ADMIN_PASSWORD} to log in and view their credentials at ${KEYCLOAK_URL}/realms/${REALM}/account"

log "👥 Adding user $REALM_ADMIN_USERNAME to group ${GROUP_NAME} …"
add_user_resp=$(curl -s -o /dev/null -w "%{http_code}" -X PUT \
        "${KEYCLOAK_URL}/admin/realms/${REALM}/users/${USER_ID}/groups/${GROUP_ID}" \
        -H "Authorization: Bearer ${TOKEN}")

if [[ "$add_user_resp" != "204" && "$add_user_resp" != "201" ]]; then
  log "❌ Failed to add user to group – HTTP $add_user_resp"
  exit 1
fi
log "✅ User $REALM_ADMIN_USERNAME is now a member of group \"${GROUP_NAME}\""

# echo "🔎 Verifying role mapping for group \"${GROUP_NAME}\" …"
# curl -sS "${KEYCLOAK_URL}/admin/realms/${REALM}/groups/${GROUP_ID}/role-mappings/clients/${RM_CLIENT_UUID}" \
#       -H "Authorization: Bearer ${TOKEN}" |
#       jq -r '.[] | "\(.name)   (\(.composite|tostring))"'

log "🎉 $REALM_ADMIN_USERNAME now has full realm-admin rights via group \"$GROUP_NAME\" and can log in and manage the realm at ${KEYCLOAK_URL}/admin/${REALM}/console"
