#!/bin/sh
# -------------------------------------------------
# wait-for-resource.sh – block until a Kubernetes resource appears
# -------------------------------------------------
# Usage:   ./wait-for-resource.sh <namespace> <resource> [timeout] [interval]
#   namespace – Kubernetes namespace (required)
#   type      – Resource type (required)
#   resource  – Resource name to wait for (required)
#   timeout   – Seconds to wait before giving up (default 300 s = 5 min)
#   interval  – Seconds between polls (default 2 s)
# -------------------------------------------------

# Exit on errors and treat unset variables as errors.
set -eu

# ---------- Argument handling ----------
if [ "$#" -lt 3 ]; then
    printf 'Usage: %s <namespace> <resource type> <resource name> [timeout] [interval]\n' "$0" >&2
    exit 2
fi

ns=$1
type=$2
resource=$3
timeout=${4:-300}   # default 5 minutes
interval=${5:-2}    # default 2 seconds

# ---------- Compute end‑time ----------
# $(date +%s) gives the current epoch seconds.
start=$(date +%s)               # remember when we started
end=$(( start + timeout ))      # when we should give up

# Escape sequence that clears the whole current line.
CLEAR_LINE='\r\033[2K'

# ---------- Main waiting loop ----------
while :   # “:” is the POSIX no‑op command, equivalent to “while true”
do
    # Does the resource already exist?
    if kubectl get $type "$resource" -n "$ns" >/dev/null 2>&1; then
        printf '\r%s✅ "%s" resource "%s" exists in namespace "%s".\n' "$CLEAR_LINE" "$type" "$resource" "$ns"
        exit 0
    fi

    # Have we run out of time?
    now=$(date +%s)
    if [ "$now" -ge "$end" ]; then
        printf '%s❌ Timed-out after %s seconds - "%s" resource "%s" not found.\n' "$CLEAR_LINE" "$timeout" $type "$resource"
        exit 1
    fi

    # --------- progress line (single‑line update) ----------
    elapsed=$(( now - start ))          # seconds we have already waited
    remaining=$(( end - now ))          # seconds left before timeout

    # \r moves the cursor to the beginning of the line;
    # no trailing "\n" means the next printf will overwrite it.
    printf '%s⏳ Waiting for "%s" resource "%s" in namespace "%s"... %ds elapsed, %ds left' \
           "$CLEAR_LINE" "$type" "$resource" "$ns" "$elapsed" "$remaining"

    # Give the terminal a chance to actually render the line before we sleep.
    # fflush is implicit for printf; we just need to pause.
    sleep "$interval"
done
