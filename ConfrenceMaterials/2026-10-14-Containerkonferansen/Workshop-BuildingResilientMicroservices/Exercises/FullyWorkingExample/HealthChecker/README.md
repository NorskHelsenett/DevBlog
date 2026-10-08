This is a small piece of code to enable doing health checks
from within the container, which is useful in some scenarios
(like running in docker compose or certain ephemeral cloud services).

Based on the discussion in https://github.com/dotnet/dotnet-docker/discussions/4296
and the subsequent https://github.com/dotnet/dotnet-docker/issues/4300
it could at some point become natively bundled with dotnet itself,
but for now here is a tiny manual implementation.

Put one copy in each exercise and the fully working example, even though
that is not a good example of the dryest code possible. The reasoning is
that it becomes easier to branch out work from one of the exercises or
the fully working example, without having to deal witt cutting out pieces
of a more complicated structure. The way it's done now, you can just copy
the exercise/example folder, and you'll have a fully working app and dockerfile.
