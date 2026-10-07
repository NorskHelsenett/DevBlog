NAMESPACE="placeholder"
SECRET="placeholder"
OUTPUT_DIR="."
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      NAMESPACE="$1"
      ;;
    --secret*|-s*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      SECRET="$1"
      ;;
    --output-dir*|-o*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      OUTPUT_DIR="$1"
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

mkdir -p "${OUTPUT_DIR}"

kubectl --namespace "${NAMESPACE}" get secret "${SECRET}" -o jsonpath='{.data}' |
jq -r 'to_entries[] | "\(.key) \(.value)"' |
while read -r key b64; do
  echo "$b64" | base64 -d > "${OUTPUT_DIR}/${key}"
  log "Secret ${SECRET} → Created ${OUTPUT_DIR}/${key}"
done
