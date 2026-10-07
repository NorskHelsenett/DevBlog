#!/bin/sh
# Portable shell script: checks file age and re-fetches from URL if older than 7 days.

# set -e

URL="https://example.com/content.txt"
DAYS="7"
FILE="not_set"
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --url*|-u*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      URL="$1"
      ;;
    --days*|-d*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      DAYS="$1"
      ;;
    --file*|-f*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      FILE="$1"
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

# ---------- helpers ----------

log() {
  if [ -n "${VERBOSE_FLAG}" ]; then
    # Simple timestamped logger
    _now=$(date +%H:%M:%S)
    printf '[%s] %s\n' "$_now" "$*"
  fi
}

# Return file modification time as epoch seconds (portable across GNU & BSD stat)
get_mtime() {
  # Try GNU stat first, fall back to BSD stat
  if stat --version 2>/dev/null | grep -q GNU; then
    stat -c '%Y' "${FILE}" 2>/dev/null || return 1
  else
    stat -f '%m' "${FILE}" 2>/dev/null || return 1
  fi
}

# ---------- main ----------

if [ ! -f "${FILE}" ]; then
  log "[$(date)] File does not exist - creating from ${URL}"
  curl -sfL "${URL}" -o "${FILE}"
  exit 0
fi

# Current epoch seconds
now=$(date +%s)

# File modification epoch seconds
mtime=$(get_mtime)

# Age in seconds
age=$(( now - mtime ))

# Threshold in seconds
threshold=$(( DAYS * 86400 ))

if [ "$age" -gt "$threshold" ]; then
  log "🚛[$(date)] File ${FILE} is $(( age / 86400 )) day(s) old (threshold: $DAYS days) - refreshing from ${URL}"
  curl -sfL "${URL}" -o "${FILE}"
else
  log "🐣[$(date)] File ${FILE} is fresh (age: $(( age / 86400 )) day(s), threshold: $DAYS days) - no action needed" 1>/dev/null
fi
