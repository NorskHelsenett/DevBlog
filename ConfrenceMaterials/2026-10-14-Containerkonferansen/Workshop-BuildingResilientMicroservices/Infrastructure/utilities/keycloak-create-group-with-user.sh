kcUrl="PLACEHOLDER"
kcRealm="PLACEHOLDER"
kcToken="PLACEHOLDER"
desiredGroup="PLACEHOLDER"
desiredUserName="PLACEHOLDER"
desiredPassword="password"
VERBOSE_FLAG=""
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
    --new-group-name*|-g*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      desiredGroup="$1"
      ;;
    --new-user-username*|-u*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      desiredUserName="$1"
      ;;
    --new-user-password*|-p*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      desiredPassword="$1"
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

log "Set up group ${desiredGroup} in realm ${kcRealm}"

# First, check whether the group already exists
EXISTING_GROUP_ID=$(curl -s "${kcUrl}/admin/realms/${kcRealm}/groups" \
        -H "Authorization: Bearer ${kcToken}" |
        jq -r ".[] | select(.name==\"${desiredGroup}\") | .id")

if [[ -n "$EXISTING_GROUP_ID" && "$EXISTING_GROUP_ID" != "null" ]]; then
  GROUP_ID=$EXISTING_GROUP_ID
  log "ℹ️  Group \"${desiredGroup}\" already exists (id=$GROUP_ID)."
else
  # Create the group
  log "🛠️  Creating group \"${desiredGroup}\" …"
  create_group_resp=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
          "${kcUrl}/admin/realms/${kcRealm}/groups" \
          -H "Authorization: Bearer ${kcToken}" \
          -H "Content-Type: application/json" \
          -d "{\"name\":\"${desiredGroup}\"}")

  if [[ "$create_group_resp" != "201" && "$create_group_resp" != "204" ]]; then
    log "❌ Failed to create group – HTTP $create_group_resp"
    exit 1
  fi

  # The API does **not** return the new ID, so we list groups again to fetch it
  GROUP_ID=$(curl -s "${kcUrl}/admin/realms/${kcRealm}/groups" \
          -H "Authorization: Bearer ${kcToken}" |
          jq -r ".[] | select(.name==\"${desiredGroup}\") | .id")

  log "✅ Created group \"${desiredGroup}\" with id $GROUP_ID"
fi

log "👤  Creating user \"${desiredUserName}\" in realm \"${kcRealm}\" …"
user_payload=$(jq -n \
  --arg username "$desiredUserName" \
  --arg firstName "UsersGiven" \
  --arg lastName "UsersFamily" \
  --arg email "$desiredUserName@example.com" \
  '{
    username: $username,
    firstName: $firstName,
    lastName: $lastName,
    email: $email,
    enabled: true,
    emailVerified: false
  }')

# POST → returns empty body + 201 + Location header with the new user’s URL
create_user_response=$(curl -s -i -X POST "${kcUrl}/admin/realms/${kcRealm}/users" \
  -H "Authorization: Bearer ${kcToken}" \
  -H "Content-Type: application/json" \
  -d "${user_payload}")

# log "${create_user_response}"

USER_ID=$(curl -s -G "${kcUrl}/admin/realms/${kcRealm}/users" \
        -H "Authorization: Bearer ${kcToken}" \
        --data-urlencode "username=${desiredUserName}" \
        --data-urlencode "exact=true" \
        --data-urlencode "max=1" \
        --data-urlencode "briefRepresentation=true" |
        jq -r 'if length == 0 then "" else .[0].id end')

# log "Users ID is: ${USER_ID}"

log "🔐  Setting password for user \"${desiredUserName}\" …"
pw_payload=$(jq -n \
  --arg pwd "$desiredPassword" \
  '{
    type: "password",
    temporary: false,
    value: $pwd
  }')

curl -s -X PUT "${kcUrl}/admin/realms/${kcRealm}/users/${USER_ID}/reset-password" \
  -H "Authorization: Bearer ${kcToken}" \
  -H "Content-Type: application/json" \
  -d "${pw_payload}" \
  -w "\nHTTP %{http_code}\n" 1>/dev/null

# log "The user ${desiredUserName} can now use the password ${desiredPassword} to log in and view their credentials at ${kcUrl}/realms/${kcRealm}/account"

log "👥 Adding user $desiredUserName to group ${desiredGroup} …"
add_user_resp=$(curl -s -o /dev/null -w "%{http_code}" -X PUT \
        "${kcUrl}/admin/realms/${kcRealm}/users/${USER_ID}/groups/${GROUP_ID}" \
        -H "Authorization: Bearer ${kcToken}")

if [[ "$add_user_resp" != "204" && "$add_user_resp" != "201" ]]; then
  log "❌ Failed to add user to group – HTTP $add_user_resp"
  exit 1
fi
log "✅ User $desiredUserName is now a member of group \"${desiredGroup}\""
