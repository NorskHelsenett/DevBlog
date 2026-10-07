a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KEYCLOAK_URL="http://keycloak.localho.st:8080"
# KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
REALM="kafka-ui"
REALM_ADMIN_USERNAME="realm-admin"
REALM_ADMIN_PASSWORD="password"
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --kc-url*|-k*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KEYCLOAK_URL="$1"
      ;;
    --realm*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      REALM="$1"
      ;;
    --admin-username*|-u*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      REALM_ADMIN_USERNAME="$1"
      ;;
    --admin-password*|-p*)
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

# echo "Fetching admin user token"
TOKEN=$(curl -s \
  -d "client_id=admin-cli" \
  -d "username=${REALM_ADMIN_USERNAME}" \
  -d "password=${REALM_ADMIN_PASSWORD}" \
  -d "grant_type=password" \
  "${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token" \
  | jq -r .access_token)
# echo "Keycloak realm admin access token raw: ${TOKEN}"
# echo "Keycloak realm admin access token decoded:"
# echo $TOKEN | jq -R 'split(".") | .[1] | @base64d | fromjson'


echo "👥 KafkaUI: Create global admin and read only groups and users"
"${SCRIPT_DIR}/../../../utilities/keycloak-create-group-with-user.sh" \
  --kc-url "${KEYCLOAK_URL}" --token "${TOKEN}" --realm "${REALM}" \
  --new-group-name "kui-admins" --new-user-username "kui-admin" \
   $VERBOSE_FLAG
"${SCRIPT_DIR}/../../../utilities/keycloak-create-group-with-user.sh" \
  --kc-url "${KEYCLOAK_URL}" --token "${TOKEN}" --realm "${REALM}" \
  --new-group-name "kui-ro" --new-user-username "kui-ro-user" \
   $VERBOSE_FLAG
"${SCRIPT_DIR}/../../../utilities/keycloak-create-group-with-user.sh" \
  --kc-url "${KEYCLOAK_URL}" --token "${TOKEN}" --realm "${REALM}" \
  --new-group-name "kui-no-access" --new-user-username "kui-no-access" \
   $VERBOSE_FLAG

"${SCRIPT_DIR}/../../../utilities/keycloak-create-group-with-user.sh" \
  --kc-url "${KEYCLOAK_URL}" --token "${TOKEN}" --realm "${REALM}" \
  --new-group-name "kui-cool-topics-admin" --new-user-username "kui-cool-admin-user" \
   $VERBOSE_FLAG
"${SCRIPT_DIR}/../../../utilities/keycloak-create-group-with-user.sh" \
  --kc-url "${KEYCLOAK_URL}" --token "${TOKEN}" --realm "${REALM}" \
  --new-group-name "kui-cool-topics-ro" --new-user-username "kui-cool-ro-user" \
   $VERBOSE_FLAG

"${SCRIPT_DIR}/../../../utilities/keycloak-create-group-with-user.sh" \
  --kc-url "${KEYCLOAK_URL}" --token "${TOKEN}" --realm "${REALM}" \
  --new-group-name "kui-awesome-topics-admin" --new-user-username "kui-awesome-admin-user" \
   $VERBOSE_FLAG
"${SCRIPT_DIR}/../../../utilities/keycloak-create-group-with-user.sh" \
  --kc-url "${KEYCLOAK_URL}" --token "${TOKEN}" --realm "${REALM}" \
  --new-group-name "kui-awesome-topics-ro" --new-user-username "kui-awesome-ro-user" \
   $VERBOSE_FLAG
