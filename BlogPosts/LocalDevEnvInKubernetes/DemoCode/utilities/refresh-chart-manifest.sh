#!/bin/sh
# Portable shell script: checks manifest file age and regenerates from chart if older than 7 days.
# Works in both bash (Linux) and zsh (macOS).
#
# Usage: ./refresh-chart-manifest.sh <manifest_file_path>
#
# Environment variables (optional overrides):
#   REFRESH_REPO_URL   - URL of chart repo to fetch from (default: https://example.com/content.txt)
#   REFRESH_REPO_NAME  - Name of repo and chart to fetch from (default: example-repo)
#   REFRESH_CHART_NAME - Name of repo and chart to fetch from (default: example-chart)
#   REFRESH_NAMESPACE  - Namespace (default: default)
#   REFRESH_DAYS       - Age threshold in days (default: 7)

set -e

# ---------- configurable defaults ----------
URL="${REFRESH_REPO_URL:-https://example.com/content.txt}"
REPO="${REFRESH_REPO_NAME:-example-repo}"
CHART="${REFRESH_CHART_NAME:-example-chart}"
RELEASE="${REFRESH_RELEASE_NAME:-release-name}"
NAMESPACE="${REFRESH_NAMESPACE:-default}"
VALUES="${REFRESH_VALUES_FILE:-values.yaml}"
DAYS="${REFRESH_DAYS:-7}"
FILE="${1:?Usage: $0 <manifest_file_path>}"

# ---------- helpers ----------

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
  echo "[$(date)] Manifest file does not exist - fetching from $URL"
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
  echo "[$(date)] File $FILE is $(( age / 86400 )) day(s) old (threshold: $DAYS days) – refreshing from $URL"
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
  echo "[$(date)] File $FILE is fresh (age: $(( age / 86400 )) day(s), threshold: $DAYS days) – no action needed" 1>/dev/null
fi
