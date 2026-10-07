#!/bin/sh
# -------------------------------------------------
# wait-for-resource.sh – block until a Kubernetes resource appears
# -------------------------------------------------
# Usage:   ./wait-for-resource.sh <namespace> <resource> [timeout] [interval]
#   namespace – Kubernetes namespace (required)
#   type      – Resource type (required)
#   resource  – Resource name to wait for (required)
#   timeout   – Seconds to wait before giving up (default 300 s = 5 min)
#   interval  – Seconds between polls (default 1 s)
# -------------------------------------------------

# Exit on errors and treat unset variables as errors.
set -eu

NAMESPACE="not_set"
RESOURCE_TYPE="not_set"
RESOURCE_NAME="not_set"
TIMEOUT="300"
INTERVAL="1"
VERBOSE_FLAG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --namespace*|-n*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      NAMESPACE="$1"
      ;;
    --type*|-t*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      RESOURCE_TYPE="$1"
      ;;
    --resource-name*|-r*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      RESOURCE_NAME="$1"
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

# ---------- Compute end‑time ----------
# $(date +%s) gives the current epoch seconds.
start=$(date +%s)               # remember when we started
end=$(( start + TIMEOUT ))      # when we should give up

# Escape sequence that clears the whole current line.
# CLEAR_LINE='\r\033[2K'
CLEAR_LINE='\r\e[K'

# ---------- Main waiting loop ----------
while :   # “:” is the POSIX no‑op command, equivalent to “while true”
do
    # Does the resource already exist?
    if kubectl get "${RESOURCE_TYPE}" "${RESOURCE_NAME}" -n "${NAMESPACE}" >/dev/null 2>&1; then
        printf '\r\e[K✅ "%s" resource "%s" exists in namespace "%s".\n' "${RESOURCE_TYPE}" "${RESOURCE_NAME}" "${NAMESPACE}"
        exit 0
    fi

    # Have we run out of time?
    now=$(date +%s)
    if [ "$now" -ge "$end" ]; then
        printf '\r\e[K❌ Timed-out after %s seconds - "%s" resource "%s" not found.\n' "${TIMEOUT}" "${RESOURCE_TYPE}" "${RESOURCE_NAME}"
        exit 1
    fi

    # --------- progress line (single‑line update) ----------
    elapsed=$(( now - start ))          # seconds we have already waited
    remaining=$(( end - now ))          # seconds left before timeout

    # \r moves the cursor to the beginning of the line;
    # no trailing "\n" means the next printf will overwrite it.
    printf '\r\e[K⏳ Waiting for "%s" resource "%s" in namespace "%s"... %ds elapsed, %ds left' \
           "${RESOURCE_TYPE}" "${RESOURCE_NAME}" "${NAMESPACE}" "$elapsed" "$remaining"

    # Give the terminal a chance to actually render the line before we sleep.
    # fflush is implicit for printf; we just need to pause.
    sleep "${INTERVAL}"
done
