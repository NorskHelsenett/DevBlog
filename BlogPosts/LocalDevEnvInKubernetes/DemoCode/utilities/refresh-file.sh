#!/bin/sh
# Portable shell script: checks file age and refetches from URL if older than 7 days.
# Works in both bash (Linux) and zsh (macOS).
#
# Usage: ./refresh-file.sh <file_path>
#
# Environment variables (optional overrides):
#   REFRESH_URL  - URL to fetch (default: https://example.com/content.txt)
#   REFRESH_DAYS - Age threshold in days (default: 7)

set -e

# ---------- configurable defaults ----------
URL="${REFRESH_URL:-https://example.com/content.txt}"
DAYS="${REFRESH_DAYS:-7}"
FILE="${1:?Usage: $0 <file_path>}"

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

if [ ! -f "$FILE" ]; then
  echo "[$(date)] File does not exist – creating from $URL"
  curl -sfL "$URL" -o "$FILE"
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
  echo "🚛[$(date)] File $FILE is $(( age / 86400 )) day(s) old (threshold: $DAYS days) – refreshing from $URL"
  curl -sfL "$URL" -o "$FILE"
else
  echo "🐣[$(date)] File $FILE is fresh (age: $(( age / 86400 )) day(s), threshold: $DAYS days) – no action needed" 1>/dev/null
fi
