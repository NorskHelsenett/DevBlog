# Sourced, not executed. Picks the container runtime used by kind and the scripts.
# Override with: CONTAINER_RUNTIME=podman ./setup.sh (or docker)
# A `docker` that is really the podman-docker shim counts as podman.
if [ -z "${CONTAINER_RUNTIME}" ]; then
  if command -v docker >/dev/null 2>&1 && ! docker --version 2>/dev/null | grep -qi podman; then
    CONTAINER_RUNTIME=docker
  elif command -v podman >/dev/null 2>&1; then
    CONTAINER_RUNTIME=podman
  else
    echo "Neither docker nor podman found" >&2
    exit 1
  fi
fi
export CONTAINER_RUNTIME

# kind only auto-detects podman when no real docker is present, so be explicit.
# Podman also does not treat localhost registries as plain-http like docker does.
if [ "${CONTAINER_RUNTIME}" = "podman" ]; then
  export KIND_EXPERIMENTAL_PROVIDER=podman
  REGISTRY_PUSH_FLAGS="--tls-verify=false"
else
  REGISTRY_PUSH_FLAGS=""
fi
