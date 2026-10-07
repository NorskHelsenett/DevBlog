#!/bin/sh
# Checks manifest file age and regenerates from chart if older than 7 days.

set -e

# ---------- configurable defaults ----------
URL="https://example.com/content.txt"
REPO="example-repo"
CHART="example-chart"
RELEASE="release-name"
NAMESPACE="default"
VALUES=""
DAYS="7"
FILE="UNSET"
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --url*|-u*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      URL="$1"
      ;;
    --repo*|-h*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      REPO="$1"
      ;;
    --chart*|-c*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      CHART="$1"
      ;;
    --release*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      RELEASE="$1"
      ;;
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      NAMESPACE="$1"
      ;;
    --values-file*|-v*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      VALUES="$1"
      ;;
    --days*|-d*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      DAYS="$1"
      ;;
    --manifest-file*|-m*)
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
    stat -c '%Y' "$FILE" 2>/dev/null || return 1
  else
    stat -f '%m' "$FILE" 2>/dev/null || return 1
  fi
}

# ---------- main ----------
a="/$FILE"; a="${a%/*}"; a="${a:-.}"; a="${a##/}/"; CALLER_DIR=$(cd "$a"; pwd)
if [ ! -f "$VALUES" ]; then
  VALUES="${CALLER_DIR}/values.yaml"
  touch $VALUES
fi

if [ ! -f "$FILE" ]; then
  log "[$(date)] Manifest file does not exist - fetching from $URL"
  helm repo add $REPO $URL
  helm repo update 1>/dev/null
  helm template \
    $RELEASE \
    $REPO/$CHART \
    --namespace $NAMESPACE \
    --include-crds \
    --values $VALUES \
    > $FILE

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
  log "[$(date)] File $FILE is $(( age / 86400 )) day(s) old (threshold: $DAYS days) - refreshing from $URL"
  helm repo add $REPO $URL
  helm repo update 1>/dev/null
  helm template \
    $RELEASE \
    $REPO/$CHART \
    --namespace $NAMESPACE \
    --include-crds \
    --values $VALUES \
    > $FILE
else
  log "[$(date)] File $FILE is fresh (age: $(( age / 86400 )) day(s), threshold: $DAYS days) - no action needed" 1>/dev/null
fi
