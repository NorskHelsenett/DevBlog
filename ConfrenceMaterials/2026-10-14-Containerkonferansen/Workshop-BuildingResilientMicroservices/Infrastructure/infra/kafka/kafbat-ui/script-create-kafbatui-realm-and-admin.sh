a="/$0"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; SCRIPT_DIR=$(cd "$a"; pwd)

KEYCLOAK_URL="http://keycloak.localho.st:8080"
KC_ADMIN_USERNAME=admin
KC_ADMIN_PASSWORD=$(kubectl -n keycloak get secret kc-bootstrap-admin -o jsonpath="{.data.password}" | base64 -d)
NEW_REALM_NAME=kafka-ui
NEW_REALM_ADMIN_USERNAME=realm-admin
NEW_REALM_ADMIN_PASSWORD=password
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --kc-url*|-k*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KEYCLOAK_URL="$1"
      ;;
    --realm*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      NEW_REALM_NAME="$1"
      ;;
    --instance-admin-username*|-a*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KC_ADMIN_USERNAME="$1"
      ;;
    --instance-admin-password*|-s*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      KC_ADMIN_PASSWORD="$1"
      ;;
    --realm-admin-username*|-u*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      NEW_REALM_ADMIN_USERNAME="$1"
      ;;
    --realm-admin-password*|-p*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      NEW_REALM_ADMIN_PASSWORD="$1"
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
  -d "username=${KC_ADMIN_USERNAME}" \
  -d "password=${KC_ADMIN_PASSWORD}" \
  -d "grant_type=password" \
  "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
  | jq -r .access_token)
# echo "Keycloak admin access token raw: ${TOKEN}"
# echo "Keycloak admin access token decoded:"
# echo $TOKEN | jq -R 'split(".") | .[1] | @base64d | fromjson'

"${SCRIPT_DIR}/../../../utilities/keycloak-create-realm-with-admin.sh" \
  --kc-url "${KEYCLOAK_URL}"\
  --token "${TOKEN}" \
  --realm "${NEW_REALM_NAME}" \
  --new-admin-username "${NEW_REALM_ADMIN_USERNAME}" \
  --new-admin-password "${NEW_REALM_ADMIN_PASSWORD}" \
  $VERBOSE_FLAG
