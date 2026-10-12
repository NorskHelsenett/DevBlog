This is a small piece of code to enable doing health checks
from within the container, which is useful in some scenarios
(like running in docker compose or certain ephemeral cloud services).

Based on the discussion in https://github.com/dotnet/dotnet-docker/discussions/4296
and the subsequent https://github.com/dotnet/dotnet-docker/issues/4300
it could at some point become natively bundled with dotnet itself,
but for now here is a tiny manual implementation.
